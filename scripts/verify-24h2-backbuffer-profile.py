#!/usr/bin/env python3
"""Verify the issue #2 profile against Microsoft's exact DLL and public PDB.

uv run --python 3.11 --with pefile --with pdbparse python \
    scripts/verify-24h2-backbuffer-profile.py --dll /path/dwmcore.dll --pdb /path/dwmcore.pdb

This is offline ABI evidence, not a native build or physical GPU acceptance test.
"""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import uuid

import pefile
import pdbparse


def verify(dll_path, pdb_path, profiles_path):
    profile = next(p for p in json.loads(profiles_path.read_text(encoding="utf-8-sig"))
                   if p["id"] == "26100.4946_24h2")
    raw = dll_path.read_bytes()
    pe = pefile.PE(data=raw)
    assert hashlib.sha256(raw).hexdigest().upper() == profile["sha256"], "DLL SHA-256 mismatch"
    assert pe.FILE_HEADER.Machine == 0x8664, "Expected x64 payload"
    identities = []
    for entry in pe.DIRECTORY_ENTRY_DEBUG:
        if entry.struct.Type == 2:
            cv = raw[entry.struct.PointerToRawData:entry.struct.PointerToRawData + entry.struct.SizeOfData]
            if cv[:4] == b"RSDS":
                identities.append((str(uuid.UUID(bytes_le=cv[4:20])).upper(), struct.unpack_from("<I", cv, 20)[0]))
    assert (profile["pdbGuid"], profile["pdbAge"]) in identities, "PE CodeView identity mismatch"

    pdb = pdbparse.parse(str(pdb_path), fast_load=True)
    pdb.STREAM_PDB.load()
    guid = pdb.STREAM_PDB.GUID
    pdb_guid = uuid.UUID(bytes_le=struct.pack("<IHH", guid.Data1, guid.Data2, guid.Data3) + guid.Data4)
    assert str(pdb_guid).upper() == profile["pdbGuid"], "Public PDB GUID mismatch"
    pdb.STREAM_DBI.load()
    # The public file's information-stream age is 3; DBI age is 1, matching the
    # PE CodeView/server-key age. Report both rather than conflating the fields.
    assert pdb.STREAM_DBI.DBIHeader.age == profile["pdbAge"], "PDB DBI age mismatch"
    pdb._update_names()
    symbols = pdb.STREAM_GSYM.reload()
    symbols.load()
    names = {}
    for symbol in symbols.globals:
        segment = getattr(symbol, "segment", 0)
        if 0 < segment <= len(pe.sections):
            rva = pe.sections[segment - 1].VirtualAddress + getattr(symbol, "offset", 0)
            names.setdefault(rva, []).append(getattr(symbol, "name", ""))

    def has_symbol(rva, fragment):
        assert any(fragment in n for n in names.get(rva, [])), f"Missing {fragment} at {rva:#x}"

    for field, name in [("presentRva", "?Present@COverlayContext"),
                        ("directFlipCompatibleRva", "?IsCandidateDirectFlipCompatible@COverlayContext"),
                        ("overlaysEnabledRva", "?OverlaysEnabled@COverlayContext"),
                        ("overlayTestModeRva", "?m_dwOverlayTestMode@CCommonRegistryData")]:
        has_symbol(profile[field], name)
    independent_flip = profile.get("disableIndependentFlipRva", 0)
    if independent_flip:
        has_symbol(independent_flip, "?m_fDisableIndependentFlip@CCommonRegistryData")
        section = next(s for s in pe.sections if s.VirtualAddress <= independent_flip <
                       s.VirtualAddress + s.Misc_VirtualSize)
        assert section.Characteristics & 0x80000000, "IndependentFlip control must be writable data"
        # Both runtime decisions compare this DWORD with zero. The global lives
        # in the zero-filled virtual tail of .data; it has no raw-file bytes.
        for reader in (0x8EB51, 0x29453E):
            instruction = pe.get_data(reader, 7)
            assert instruction[:2] == b"\x83\x3d" and instruction[6] == 0
            target = reader + 7 + struct.unpack_from("<i", instruction, 2)[0]
            assert target == independent_flip, "IndependentFlip runtime reader does not match profile"

    layout = profile["backBufferLayout"]
    assert profile["presentAbi"] == "overlay-present-24h2-7"
    assert profile["backbufferStrategy"] == "profiled-physical-backbuffer"
    assert profile["monitorIdentityStrategy"] == "display-adapter-luid-and-target"
    assert profile["displayIdentityOffset"] == 0x30
    assert layout["backBufferSlot"] == 24 and layout["resourceSlot"] == 17
    # Named getter bodies establish the packed identity fields and protection
    # byte; checking only method names would not prove these raw-read offsets.
    getters = [
        (0x2094F0, "?GetDisplayAdapterLuid@COverlaySwapChain", "488b4134488902488bc2c3"),
        (0x226BD0, "?GetVidPnSourceId@COverlaySwapChain", "8b4130c3"),
        (0x2D41D0, "?GetVidPnTargetId@COverlaySwapChain", "8b413cc3"),
        (0x1FD060, "?IsHardwareProtected@COverlaySwapChain", "8a4164c3"),
    ]
    for rva, name, expected_hex in getters:
        has_symbol(rva, name)
        expected = bytes.fromhex(expected_hex)
        assert pe.get_data(rva, len(expected)) == expected, f"Getter body mismatch: {name}"
    checks = []
    for implementation in ("display", "legacy"):
        chain = layout[implementation + "SwapChainVtableRva"]
        buffer = layout[implementation + "BufferVtableRva"]
        for table, slot, fragment in [(chain, 24, "?GetPhysicalBackBuffer@"),
                                      (buffer, 17, "?GetD3D11Resource@"),
                                      (buffer, 19, "?GetTransientRegion@"),
                                      (chain, 27, "?GetDisplayAdapterLuid@"),
                                      (chain, 29, "?GetVidPnTargetId@"),
                                      (chain, 7, "?IsHardwareProtected@")]:
            address = struct.unpack("<Q", pe.get_data(table + slot * 8, 8))[0]
            rva = address - pe.OPTIONAL_HEADER.ImageBase
            has_symbol(rva, fragment)
            checks.append({"implementation": implementation, "vtable_rva": hex(table),
                           "slot": slot, "target_rva": hex(rva), "method": fragment})
    print(json.dumps({"status": "PASS", "profile": profile["id"],
                      "sha256": profile["sha256"], "pdb_guid": profile["pdbGuid"],
                      "pe_and_dbi_age": profile["pdbAge"],
                      "pdb_information_stream_age": pdb.STREAM_PDB.Age,
                      "getter_body_checks": len(getters),
                      "vtable_checks": checks,
                      "boundary": "Offline binary/symbol verification; Windows build and GPU validation not exercised"}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", type=Path, required=True)
    parser.add_argument("--pdb", type=Path, required=True)
    parser.add_argument("--profiles", type=Path, default=Path(__file__).resolve().parents[1] /
                        "artifacts/profiles/compiled_dwm_profiles.json")
    args = parser.parse_args()
    verify(args.dll, args.pdb, args.profiles)
