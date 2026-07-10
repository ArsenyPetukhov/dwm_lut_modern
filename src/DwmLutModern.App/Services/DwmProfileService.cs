using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
	public enum DwmSupportLevel
	{
		LiveValidated,
		Experimental,
		Unsupported
	}

    /// <summary>Human-readable support status for the current dwmcore.dll.</summary>
    public sealed class DwmSupport
    {
        public string Summary { get; set; } = "DWM support unknown";
        public string Detail { get; set; } = string.Empty;
        public string Machine { get; set; } = "unknown";
        public string PdbGuid { get; set; } = string.Empty;
        public uint PdbAge { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public int WindowsBuild { get; set; }
		public bool IsVerified { get; set; }
		public bool IsExperimental { get; set; }
		public bool HasExactProfile { get; set; }
		public DwmSupportLevel Level { get; set; } = DwmSupportLevel.Unsupported;
		public bool CanApply => Level != DwmSupportLevel.Unsupported;
		public bool ShouldShowCommunityNotice => !IsVerified;
    }

    /// <summary>
    /// Reads the live dwmcore.dll PDB identity and reports whether the compiled-in profile table
    /// has a verified match — surfacing "will this work on my Windows build?" in the UI. Ported
    /// from the legacy DwmSupportStatus.
    /// </summary>
    public static class DwmProfileService
    {
		public const string CommunityNotice =
			"If correction does not apply or behaves incorrectly on this Windows build, disable it and report the issue to the developer on GitHub.";

        public static DwmSupport Detect()
        {
            try
            {
                var path = Path.Combine(Environment.SystemDirectory, "dwmcore.dll");
                var pe = PeIdentity.Read(path);
                var build = GetWindowsBuild();
                var profile = DwmKnownProfiles.All.FirstOrDefault(x =>
                    string.Equals(x.Machine, pe.MachineName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.PdbGuid, pe.PdbGuid, StringComparison.OrdinalIgnoreCase) &&
                    x.PdbAge == pe.PdbAge);

			if (profile != null)
			{
				var experimental = string.Equals(profile.ValidationState, "experimental", StringComparison.OrdinalIgnoreCase);
				var verified = IsVerifiedValidationState(profile.ValidationState);
				return new DwmSupport
				{
					Summary = verified
						? "Live-validated support: " + profile.Id
						: "Exact profile available; validation pending: " + profile.Id,
                        Detail = profile.EngineFamily + " / " + profile.BackbufferStrategy + " / " + profile.ValidationState,
                        Machine = pe.MachineName,
                        PdbGuid = pe.PdbGuid,
                        PdbAge = pe.PdbAge,
                        Sha256 = pe.Sha256,
                        WindowsBuild = build,
					IsVerified = verified,
					HasExactProfile = true,
					IsExperimental = experimental,
					Level = verified ? DwmSupportLevel.LiveValidated : DwmSupportLevel.Experimental
                    };
		}

				var fallbackSupported = string.Equals(pe.MachineName, "x64", StringComparison.OrdinalIgnoreCase) && build > 0;
                return new DwmSupport
                {
					Summary = fallbackSupported ? GetFallbackSummary(build) : "Unsupported DWM architecture or build",
                    Detail = "No verified DWM profile for " + pe.PdbGuid + ":" + pe.PdbAge +
						(fallbackSupported
							? "; Auto will use an experimental Windows-family fallback unless Compatibility is set manually."
							: "; no safe native resolver is available."),
                    Machine = pe.MachineName,
                    PdbGuid = pe.PdbGuid,
                    PdbAge = pe.PdbAge,
                    Sha256 = pe.Sha256,
                    WindowsBuild = build,
					IsVerified = false,
					IsExperimental = fallbackSupported,
					Level = fallbackSupported ? DwmSupportLevel.Experimental : DwmSupportLevel.Unsupported
                };
            }
            catch (Exception ex)
            {
				return new DwmSupport
				{
					Summary = "DWM support unavailable; Apply is disabled",
					Detail = ex.Message,
					Machine = "unknown",
					Level = DwmSupportLevel.Unsupported
				};
            }
        }

		public static bool IsVerifiedValidationState(string? validationState)
		{
			return string.Equals(validationState, "verified-stable", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(validationState, "live-validated", StringComparison.OrdinalIgnoreCase);
		}

        private static string GetFallbackSummary(int build)
        {
            if (build > 0 && build < 22000) return "Fallback: Windows 10 signature engine";
            if (build >= 22000 && build < 26100) return "Fallback: Windows 11 legacy signature engine";
            if (build >= 26100 && build < 26200) return "Fallback: Windows 11 24H2 signature engine";
            if (build >= 26200) return "Fallback: Windows 11 25H2+ signature engine";
            return "Fallback: unknown Windows build";
        }

        private static int GetWindowsBuild()
        {
            var version = new RTL_OSVERSIONINFOEX { dwOSVersionInfoSize = Marshal.SizeOf(typeof(RTL_OSVERSIONINFOEX)) };
            return RtlGetVersion(ref version) == 0 ? version.dwBuildNumber : Environment.OSVersion.Version.Build;
        }

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX version);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RTL_OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        private sealed class PeIdentity
        {
            public string MachineName { get; private set; } = string.Empty;
            public string PdbGuid { get; private set; } = string.Empty;
            public uint PdbAge { get; private set; }
            public string Sha256 { get; private set; } = string.Empty;

            public static PeIdentity Read(string path)
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Invalid MZ header in " + path);
                stream.Position = 0x3C;
                var ntOffset = reader.ReadInt32();
                stream.Position = ntOffset;
                if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("Invalid PE header in " + path);

                var machine = reader.ReadUInt16();
                var numberOfSections = reader.ReadUInt16();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                var optionalHeaderSize = reader.ReadUInt16();
                reader.ReadUInt16();

                var optionalHeaderOffset = stream.Position;
                var magic = reader.ReadUInt16();
                var dataDirectoryOffset = optionalHeaderOffset + (magic == 0x20B ? 112 : 96);
                stream.Position = dataDirectoryOffset + (6 * 8);
                var debugRva = reader.ReadUInt32();
                var debugSize = reader.ReadUInt32();

                var sectionOffset = optionalHeaderOffset + optionalHeaderSize;
                stream.Position = sectionOffset;
                var sections = new Section[numberOfSections];
                for (var i = 0; i < numberOfSections; i++)
                {
                    stream.Position += 8;
                    var virtualSize = reader.ReadUInt32();
                    var virtualAddress = reader.ReadUInt32();
                    var rawSize = reader.ReadUInt32();
                    var rawPointer = reader.ReadUInt32();
                    stream.Position += 16;
                    sections[i] = new Section(virtualAddress, virtualSize, rawPointer, rawSize);
                }

                var debugOffset = RvaToOffset(debugRva, sections);
                if (debugOffset < 0 || debugSize < 28) throw new InvalidDataException("No usable debug directory in " + path);
                stream.Position = debugOffset;
                var entries = debugSize / 28;
                for (var i = 0; i < entries; i++)
                {
                    stream.Position = debugOffset + i * 28 + 12;
                    var type = reader.ReadUInt32();
                    var sizeOfData = reader.ReadUInt32();
                    reader.ReadUInt32();
                    var pointerToRawData = reader.ReadUInt32();
                    if (type != 2 || sizeOfData < 24) continue;

                    stream.Position = pointerToRawData;
                    if (reader.ReadUInt32() != 0x53445352) continue;
                    var guidBytes = reader.ReadBytes(16);
                    var guid = new Guid(guidBytes).ToString("D").ToUpperInvariant();
                    var age = reader.ReadUInt32();
                    return new PeIdentity
                    {
                        MachineName = FormatMachineName(machine),
                        PdbGuid = guid,
                        PdbAge = age,
                        Sha256 = FileHash(path)
                    };
                }

                throw new InvalidDataException("No CodeView RSDS record found in " + path);
            }

            private static int RvaToOffset(uint rva, Section[] sections)
            {
                foreach (var section in sections)
                {
                    var size = Math.Max(section.VirtualSize, section.RawSize);
                    if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
                    {
                        return (int)(section.RawPointer + (rva - section.VirtualAddress));
                    }
                }

                return -1;
            }

            private static string FormatMachineName(ushort machine)
            {
                if (machine == 0x8664) return "x64";
                if (machine == 0xAA64) return "arm64";
                return "0x" + machine.ToString("X4");
            }

            private static string FileHash(string path)
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }

            private readonly struct Section
            {
                public readonly uint VirtualAddress;
                public readonly uint VirtualSize;
                public readonly uint RawPointer;
                public readonly uint RawSize;

                public Section(uint virtualAddress, uint virtualSize, uint rawPointer, uint rawSize)
                {
                    VirtualAddress = virtualAddress;
                    VirtualSize = virtualSize;
                    RawPointer = rawPointer;
                    RawSize = rawSize;
                }
            }
        }
    }
}
