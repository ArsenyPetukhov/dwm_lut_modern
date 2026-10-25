#!/usr/bin/env python3
"""Bundle a compiled native repair into the checksum-verified official v5 portable.

This packaging command does not execute the Windows programs or run tests.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import zipfile


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def package(base, checksum, dll, output, version):
    expected = checksum.read_text(encoding="utf-8-sig").strip().split()[0].lower()
    if not re.fullmatch(r"[0-9a-f]{64}", expected):
        raise ValueError("Invalid official portable checksum file")
    base_bytes = base.read_bytes()
    if sha256(base_bytes) != expected:
        raise ValueError("Official v5 portable checksum mismatch")
    native = dll.read_bytes()
    debug_path = dll.with_suffix(".pdb")
    debug = debug_path.read_bytes()
    if native[:2] != b"MZ":
        raise ValueError("Native output is not a PE file")
    nt = struct.unpack_from("<I", native, 0x3c)[0]
    if native[nt:nt + 4] != b"PE\0\0" or struct.unpack_from("<H", native, nt + 4)[0] != 0x8664:
        raise ValueError("Native output is not Windows x64")
    if not struct.unpack_from("<H", native, nt + 22)[0] & 0x2000:
        raise ValueError("Native output is not a DLL")

    repo = Path(__file__).resolve().parents[1]
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    base_revision = subprocess.check_output(["git", "rev-parse", "v5.0.0^{commit}"], cwd=repo, text=True).strip()
    branch = subprocess.check_output(["git", "branch", "--show-current"], cwd=repo, text=True).strip()
    diff = subprocess.check_output(["git", "diff", "HEAD", "--"], cwd=repo)
    source_names = ("dllmain.cpp", "pch.h", "framework.h", "noise.h", "DwmProfiles.generated.h",
                    "BackBufferAccess.h", "LutRouting.h", "MonitorManifest.h")
    receipt = {
        "package": version,
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "base_release": "v5.0.0",
        "base_zip_sha256": expected,
        "native_dll_sha256": sha256(native),
        "native_pdb_sha256": sha256(debug),
        "source_revision": revision,
        "source_base_revision": base_revision,
        "source_branch": branch,
        "source_has_tracked_changes": bool(diff),
        "tracked_source_diff_sha256": sha256(diff),
        "native_source_sha256": {
            name: sha256((repo / "src/lutdwm" / name).read_bytes())
            for name in source_names
        },
        "native_source_normalized_lf_sha256": {
            name: sha256((repo / "src/lutdwm" / name).read_bytes().replace(b"\r\n", b"\n"))
            for name in source_names
        },
        "compiler": subprocess.check_output(["clang++", "--version"], text=True).splitlines()[0],
        "target_libraries": "LLVM-MinGW 20261020 UCRT x86_64",
        "note": "Retain the source checkout and native source hashes with this receipt.",
        "native_build": "Windows x64 cross compilation on macOS; static C++ runtime",
        "gui": "Original official v5 portable GUI and self-contained managed runtime",
        "windows_execution": "not performed on this Mac",
        "physical_amd_acceptance": "v5.0.1 normal windowed use confirmed by user on 2026-10-26; fullscreen change pending",
    }
    readme = f"""DWM LUT Modern — {version}

This portable package combines the official v5.0.0 application with a freshly
compiled x64 native DLL containing the dedicated 26100.4946 repair.
This build also targets fullscreen windowed video by keeping it in desktop
composition. It covers the common presentation path used by EVR, madVR, and
MPC Video Renderer rather than depending on a particular player setting.

1. Disable LUTs and quit any running copy before switching packages.
2. Extract this entire ZIP into a NEW folder. Keep its files together.
3. Run DwmLutModern.exe as Administrator and keep Compatibility on Auto.
4. Apply the intended SDR/HDR LUTs.

Native log: %SystemRoot%\\Temp\\dwm_lut.log
Expected profile: 26100.4946_24h2
Expected progress: verified physical-buffer/resource vtables, then exact display
adapter LUID and target matching. Unknown/protected buffers apply no LUT.
Fullscreen windowed policy: Forced DisableIndependentFlip to 1.
If the renderer switches Windows into HDR, an appropriate HDR LUT is required;
the app will not apply an SDR LUT to HDR output. Exclusive fullscreen remains
outside desktop composition. Composed playback can reduce VRR/power-saving
benefits while correction is enabled.

The GUI is the original v5 binary. Its embedded support table can still describe
this payload as a family fallback; the repaired native DLL's log reports the
actual exact profile selection.

Build receipt: AMD-HOTFIX-BUILD.json. The Windows x64 DLL was cross-compiled on
macOS. It has not been executed inside DWM on this Mac. The earlier v5.0.1 repair
was confirmed working in ordinary windows; the new fullscreen change still needs
confirmation on the affected laptop. Outputs on a different active D3D11 device
are skipped.

Source commit: {revision}.
"""
    output.parent.mkdir(parents=True, exist_ok=True)
    replaced = []
    pdb_replaced = False
    with zipfile.ZipFile(base) as original, zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as result:
        for info in original.infolist():
            if Path(info.filename.replace("\\", "/")).name.lower() == "dwm_lut.dll":
                result.writestr(info, native)
                replaced.append(info.filename)
            elif Path(info.filename.replace("\\", "/")).name.lower() == "dwm_lut.pdb":
                result.writestr(info, debug)
                pdb_replaced = True
            else:
                result.writestr(info, original.read(info))
        if not replaced:
            raise ValueError("Official portable archive contains no native DLL to replace")
        if not pdb_replaced:
            result.writestr(str(Path(replaced[0]).with_suffix(".pdb")), debug)
        result.writestr("README_AMD_HOTFIX.txt", readme)
        result.writestr("AMD-HOTFIX-BUILD.json", json.dumps(receipt, indent=2) + "\n")
    digest = sha256(output.read_bytes())
    output.with_suffix(output.suffix + ".sha256").write_text(digest + "  " + output.name + "\n")
    receipt["output_zip"] = str(output.resolve())
    receipt["output_zip_sha256"] = digest
    receipt["replaced_entries"] = replaced
    output.with_suffix(".build.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-zip", type=Path, required=True)
    parser.add_argument("--base-checksum", type=Path, required=True)
    parser.add_argument("--dll", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--version", default="v5.0.2-fullscreen-preview")
    args = parser.parse_args()
    package(args.base_zip, args.base_checksum, args.dll, args.output, args.version)
