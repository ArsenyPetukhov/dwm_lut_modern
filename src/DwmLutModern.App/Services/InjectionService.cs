using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using DwmLutModern.Models;
using static DwmLutModern.Services.NativeMethods;

namespace DwmLutModern.Services
{
    /// <summary>Whether the LUT DLL is currently loaded in dwm.exe.</summary>
    public enum InjectionStatus
    {
        Inactive,
        Active,
        Unknown
    }

    /// <summary>Structured outcome of an inject/uninject, so the UI can show an inline info bar
    /// with details and a Retry instead of a blocking MessageBox.</summary>
    public sealed class InjectionResult
    {
        public bool Success { get; init; }

        /// <summary>False when the operation was a successful no-op (nothing was injected/ejected),
        /// so the UI can skip the screen-redraw flash and success toast.</summary>
        public bool Changed { get; init; } = true;

        public string Message { get; init; } = string.Empty;
        public string Diagnostics { get; init; } = string.Empty;

        public static InjectionResult Ok(string message = "") => new InjectionResult { Success = true, Message = message };
        public static InjectionResult Skipped(string message = "") => new InjectionResult { Success = true, Changed = false, Message = message };
        public static InjectionResult Fail(string message, string diagnostics = "") =>
            new InjectionResult { Success = false, Message = message, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Injects/ejects the native LUT DLL into dwm.exe. The Win32 mechanics (privilege dance,
    /// architecture validation, staging, CreateRemoteThread+LoadLibraryA/FreeLibrary) are the
    /// proven legacy path; this wrapper adds async-friendly, structured results and routes the
    /// GUI log under %AppData%. The staged DLL and LUT files still live in %SystemRoot%\Temp
    /// because that is where the sandboxed DWM process can read them.
    /// </summary>
	public sealed class InjectionService : IInjectionService
    {
        private const string DllName = "dwm_lut.dll";
        private const uint InjectionTimeoutMs = 30000;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitTimeout = 0x00000102;
        private const uint WaitFailed = 0xFFFFFFFF;

        private readonly string _stagedDllPath;
        private readonly string _lutsPath;
        private readonly IntPtr _loadLibraryA;
        private readonly IntPtr _freeLibrary;

        /// <summary>True when Process.EnterDebugMode failed; Apply/Disable cannot work.</summary>
        public bool NoDebug { get; }

        public InjectionService()
        {
            var systemTemp = Environment.ExpandEnvironmentVariables("%SYSTEMROOT%\\Temp\\");
            _stagedDllPath = systemTemp + DllName;
            _lutsPath = systemTemp + "luts\\";

            var kernel32 = GetModuleHandle("kernel32.dll");
            _loadLibraryA = GetProcAddress(kernel32, "LoadLibraryA");
            _freeLibrary = GetProcAddress(kernel32, "FreeLibrary");

            try
            {
                Process.EnterDebugMode();
            }
            catch (Exception ex)
            {
                Log("Process.EnterDebugMode failed: " + ex);
                NoDebug = true;
            }
        }

        public InjectionStatus GetStatus()
        {
            if (NoDebug) return InjectionStatus.Unknown;

            var dwmInstances = Process.GetProcessesByName("dwm");
            if (dwmInstances.Length == 0) return InjectionStatus.Unknown;

            var status = InjectionStatus.Inactive;
            foreach (var dwm in dwmInstances)
            {
                try
                {
                    foreach (ProcessModule module in dwm.Modules)
                    {
                        if (string.Equals(module.ModuleName, DllName, StringComparison.OrdinalIgnoreCase))
                        {
                            status = InjectionStatus.Active;
                        }

                        module.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log("GetStatus could not enumerate dwm.exe pid " + dwm.Id + " modules: " + ex.Message);
                    status = InjectionStatus.Unknown;
                }

                dwm.Dispose();
            }

            return status;
        }

        /// <summary>Uninject then (if any monitor has a LUT) inject with the given resolver mode.</summary>
        public InjectionResult Apply(IEnumerable<MonitorConfig> monitors, string resolverMode)
        {
            var support = DwmProfileService.Detect();
            if (!support.CanApply)
            {
                return InjectionResult.Fail(support.Summary + ". " + support.Detail);
            }

            var monitorList = monitors.ToList();

            var uninject = Uninject();
            if (!uninject.Success) return uninject;

            if (monitorList.All(m => !m.HasEffectiveLut))
            {
                return InjectionResult.Ok("No LUTs assigned; nothing to apply.");
            }

            return Inject(monitorList, resolverMode);
        }

        private InjectionResult Inject(List<MonitorConfig> monitors, string resolverMode)
        {
            if (NoDebug)
            {
                return InjectionResult.Fail(
                    "Cannot apply: failed to enter debug mode. Run DWM LUT Modern as Administrator.");
            }

            var sourceDllPath = AppPaths.NativeDllSource;
            if (!File.Exists(sourceDllPath))
            {
                return InjectionResult.Fail(
                    "The native shader library dwm_lut.dll was not found next to the app at " + sourceDllPath + ".");
            }

            var normalizedResolverMode = string.IsNullOrWhiteSpace(resolverMode) ? "auto" : resolverMode.Trim();
            var impersonating = false;
            var dwmInstances = Process.GetProcessesByName("dwm");

            try
            {
                if (!EnsureInjectionPrivileges(out var privilegeDiagnostics, out impersonating))
                {
                    return InjectionResult.Fail(
                        "DWM LUT must be run as Administrator. Could not obtain injection privileges.",
                        privilegeDiagnostics);
                }
                Log("Inject privileges:" + Environment.NewLine + privilegeDiagnostics.TrimEnd());

                ValidateDwmArchitecture(dwmInstances, sourceDllPath);

                File.Copy(sourceDllPath, _stagedDllPath, true);
                ClearPermissions(_stagedDllPath);

                if (Directory.Exists(_lutsPath)) Directory.Delete(_lutsPath, true);
                Directory.CreateDirectory(_lutsPath);
                ClearPermissions(_lutsPath);
                WriteMonitorManifest(monitors);
                WriteResolverConfig(normalizedResolverMode);

                foreach (var monitor in monitors)
                {
                    var positionKey = (monitor.Position ?? string.Empty).Replace(',', '_');

                    // Stage only the domains the per-monitor mode allows. Combined with the
                    // native DLL's domain-strict selection, this guarantees a calibration LUT is
                    // never applied outside its signal domain.
                    var sdr = monitor.EffectiveSdrLut;
                    if (!string.IsNullOrEmpty(sdr))
                    {
                        var dest = _lutsPath + positionKey + ".cube";
                        LutConverter.CopyOrConvert(sdr, dest);
                        ClearPermissions(dest);
                    }

                    var hdr = monitor.EffectiveHdrLut;
                    if (!string.IsNullOrEmpty(hdr))
                    {
                        var dest = _lutsPath + positionKey + "_hdr.cube";
                        LutConverter.CopyOrConvert(hdr, dest);
                        ClearPermissions(dest);
                    }
                }

                if (dwmInstances.Length == 0)
                {
                    return InjectionResult.Fail("No dwm.exe process was found.");
                }

                var diagnostics = new StringBuilder();
                var failed = InjectAll(dwmInstances, diagnostics);

                if (Directory.Exists(_lutsPath)) Directory.Delete(_lutsPath, true);

                if (!failed)
                {
                    return InjectionResult.Ok("LUT applied.");
                }

				// A mixed result is never healthy: roll back every module that did load. If rollback
				// cannot be verified, retain the staged DLL so Disable can retry safely.
				var rollback = Uninject();
				diagnostics.AppendLine(rollback.Success
					? "Partial apply rollback completed and verified."
					: "Partial apply rollback failed: " + rollback.Message);
				if (!string.IsNullOrWhiteSpace(rollback.Diagnostics))
				{
					diagnostics.AppendLine(rollback.Diagnostics);
				}
                Log("Inject failed:" + Environment.NewLine + diagnostics.ToString().TrimEnd());

                var support = DwmProfileService.Detect();
                var hint = support.IsVerified
                    ? "A LUT file may be malformed, or this token cannot inject into DWM."
                    : "This Windows build (" + support.Summary + ") has no verified DWM profile. " +
                      "Open Settings → Advanced and try a Compatibility mode that matches your build.";
                return InjectionResult.Fail(
					"Couldn't apply the LUT consistently across DWM processes. " + hint,
                    diagnostics + Environment.NewLine +
                    "GUI log: " + AppPaths.GuiLogFile + Environment.NewLine +
                    "DWM log: %SystemRoot%\\Temp\\dwm_lut.log");
            }
            catch (Exception ex)
            {
                Log("Inject exception: " + ex);
                return InjectionResult.Fail(ex.Message, ex.ToString());
            }
            finally
            {
                foreach (var dwm in dwmInstances) dwm.Dispose();
                if (impersonating) RevertToSelf();
            }
        }

        private bool InjectAll(Process[] dwmInstances, StringBuilder diagnostics)
        {
            var failed = false;
            var bytes = Encoding.ASCII.GetBytes(_stagedDllPath + "\0");
            Log("Inject start: dwm_count=" + dwmInstances.Length);

            foreach (var dwm in dwmInstances)
            {
                var processHandle = IntPtr.Zero;
                var address = IntPtr.Zero;
                var thread = IntPtr.Zero;
                try
                {
                    processHandle = OpenProcess(
                        DesiredAccess.ProcessCreateThread | DesiredAccess.ProcessQueryInformation |
                        DesiredAccess.ProcessVmOperation | DesiredAccess.ProcessVmWrite | DesiredAccess.ProcessVmRead,
                        false, (uint)dwm.Id);
                    if (processHandle == IntPtr.Zero)
                    {
                        failed = true;
                        diagnostics.AppendLine("OpenProcess(dwm:" + dwm.Id + ") failed: " + LastWin32Error());
                        continue;
                    }

                    address = VirtualAllocEx(processHandle, IntPtr.Zero, (UIntPtr)bytes.Length,
                        AllocationType.Reserve | AllocationType.Commit, MemoryProtection.ReadWrite);
                    if (address == IntPtr.Zero)
                    {
                        failed = true;
                        diagnostics.AppendLine("VirtualAllocEx(dwm:" + dwm.Id + ") failed: " + LastWin32Error());
                        continue;
                    }

                    if (!WriteProcessMemory(processHandle, address, bytes, (UIntPtr)bytes.Length, out var written) ||
                        written.ToUInt64() != (ulong)bytes.Length)
                    {
                        failed = true;
                        diagnostics.AppendLine("WriteProcessMemory(dwm:" + dwm.Id + ") failed or wrote " +
                                               written + "/" + bytes.Length + " bytes: " + LastWin32Error());
                        continue;
                    }

                    thread = CreateRemoteThread(processHandle, IntPtr.Zero, 0, _loadLibraryA, address, 0, out var tid);
                    if (thread == IntPtr.Zero)
                    {
                        failed = true;
                        diagnostics.AppendLine("CreateRemoteThread(LoadLibraryA,dwm:" + dwm.Id + ") failed: " + LastWin32Error());
                        continue;
                    }

                    var wait = WaitForSingleObject(thread, InjectionTimeoutMs);
                    if (wait != WaitObject0)
                    {
                        failed = true;
                        diagnostics.AppendLine("WaitForSingleObject(LoadLibraryA,dwm:" + dwm.Id + ",tid:" + tid + ") returned 0x" +
                            wait.ToString("X") + (wait == WaitTimeout ? " (timeout)" : wait == WaitFailed ? " (" + LastWin32Error() + ")" : ""));
                        continue;
                    }

                    if (!GetExitCodeThread(thread, out var exitCode))
                    {
                        failed = true;
                        diagnostics.AppendLine("GetExitCodeThread(dwm:" + dwm.Id + ",tid:" + tid + ") failed: " + LastWin32Error());
                        continue;
                    }

                    if (exitCode == 0)
                    {
                        failed = true;
                        diagnostics.AppendLine("LoadLibraryA(dwm:" + dwm.Id + ",tid:" + tid +
                            ") returned NULL. See %SystemRoot%\\Temp\\dwm_lut.log for DLL-side diagnostics.");
                    }
                    else
                    {
                        diagnostics.AppendLine("LoadLibraryA(dwm:" + dwm.Id + ",tid:" + tid +
                            ") returned module handle 0x" + exitCode.ToString("X") + ".");
                    }
                }
                finally
                {
                    if (thread != IntPtr.Zero) CloseHandle(thread);
                    if (address != IntPtr.Zero) VirtualFreeEx(processHandle, address, 0, FreeType.Release);
                    if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
                }
            }

            return failed;
        }

        public InjectionResult Uninject()
        {
            if (NoDebug)
            {
                return InjectionResult.Fail(
                    "Cannot disable: failed to enter debug mode. Run DWM LUT Modern as Administrator.");
            }

            var impersonating = false;
            var dwmInstances = Process.GetProcessesByName("dwm");
            var diagnostics = new StringBuilder();

            try
            {
                if (!EnsureInjectionPrivileges(out var privilegeDiagnostics, out impersonating))
                {
                    return InjectionResult.Fail(
                        "DWM LUT must be run as Administrator. Could not obtain injection privileges.",
                        privilegeDiagnostics);
                }
                Log("Uninject privileges:" + Environment.NewLine + privilegeDiagnostics.TrimEnd());

                foreach (var dwm in dwmInstances)
                {
                    var processHandle = OpenProcess(
                        DesiredAccess.ProcessCreateThread | DesiredAccess.ProcessQueryInformation |
                        DesiredAccess.ProcessVmOperation | DesiredAccess.ProcessVmWrite | DesiredAccess.ProcessVmRead,
                        false, (uint)dwm.Id);
                    if (processHandle == IntPtr.Zero)
                    {
                        diagnostics.AppendLine("OpenProcess(dwm:" + dwm.Id + ") for uninject failed: " + LastWin32Error());
                        continue;
                    }

                    try
                    {
                        foreach (ProcessModule module in dwm.Modules)
                        {
                            if (string.Equals(module.ModuleName, DllName, StringComparison.OrdinalIgnoreCase))
                            {
                                var thread = CreateRemoteThread(processHandle, IntPtr.Zero, 0, _freeLibrary, module.BaseAddress, 0, out var tid);
                                if (thread == IntPtr.Zero)
                                {
                                    diagnostics.AppendLine("CreateRemoteThread(FreeLibrary,dwm:" + dwm.Id + ") failed: " + LastWin32Error());
                                }
                                else
                                {
                                    var wait = WaitForSingleObject(thread, InjectionTimeoutMs);
                                    if (wait != WaitObject0)
                                    {
                                        diagnostics.AppendLine("WaitForSingleObject(FreeLibrary,dwm:" + dwm.Id + ",tid:" + tid + ") returned 0x" + wait.ToString("X") + ".");
                                    }
                                    CloseHandle(thread);
                                }
                            }

                            module.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostics.AppendLine("Uninject module enumeration for dwm:" + dwm.Id + " failed: " + ex.Message);
                    }

                    CloseHandle(processHandle);
                }

				if (diagnostics.Length != 0)
				{
					Log("Uninject diagnostics:" + Environment.NewLine + diagnostics.ToString().TrimEnd());
				}

				var verifiedStatus = GetStatus();
				if (verifiedStatus != InjectionStatus.Inactive)
				{
					var verification = verifiedStatus == InjectionStatus.Active
						? "dwm_lut.dll is still loaded in at least one DWM process."
						: "DWM module state could not be verified.";
					diagnostics.AppendLine(verification);
					Log("Uninject verification failed:" + Environment.NewLine + diagnostics.ToString().TrimEnd());
					return InjectionResult.Fail(
						"Couldn't fully disable the LUT. " + verification + " Retry Disable or sign out.",
						diagnostics.ToString());
				}

				SafeDelete(_stagedDllPath);
				return InjectionResult.Ok("LUT disabled and verified unloaded.");
            }
            catch (Exception ex)
            {
                Log("Uninject exception: " + ex);
                return InjectionResult.Fail(ex.Message, ex.ToString());
            }
            finally
            {
                foreach (var dwm in dwmInstances) dwm.Dispose();
                if (impersonating) RevertToSelf();
            }
        }

        // ---- privilege / impersonation ----------------------------------------------------

        private bool EnsureInjectionPrivileges(out string diagnosticsText, out bool impersonating)
        {
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("Starting identity: " + CurrentIdentityName() + ".");

            TryEnablePrivilege("SeDebugPrivilege", diagnostics);

            if (TryImpersonateSystem(diagnostics))
            {
                impersonating = true;
                diagnosticsText = diagnostics.ToString();
                return true;
            }

            impersonating = false;
            if (CurrentProcessIsElevated())
            {
                diagnostics.AppendLine("SYSTEM impersonation failed; continuing as elevated administrator.");
                diagnosticsText = diagnostics.ToString();
                return true;
            }

            diagnostics.AppendLine("Not elevated and could not impersonate SYSTEM.");
            diagnosticsText = diagnostics.ToString();
            return false;
        }

        private static bool TryImpersonateSystem(StringBuilder diagnostics)
        {
            foreach (var processName in new[] { "winlogon", "services", "lsass", "svchost" })
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        if (TryImpersonateSystemFromProcess(process, diagnostics)) return true;
                    }
                }
            }

            return false;
        }

        private static bool TryImpersonateSystemFromProcess(Process process, StringBuilder diagnostics)
        {
            var processHandle = IntPtr.Zero;
            var tokenHandle = IntPtr.Zero;
            var duplicateTokenHandle = IntPtr.Zero;

            try
            {
                processHandle = OpenProcess(DesiredAccess.ProcessQueryLimitedInformation, false, (uint)process.Id);
                if (processHandle == IntPtr.Zero)
                {
                    diagnostics.AppendLine("OpenProcess(" + process.ProcessName + ":" + process.Id + ") failed: " + LastWin32Error());
                    return false;
                }

                if (!OpenProcessToken(processHandle, TokenAccess.Duplicate | TokenAccess.Impersonate | TokenAccess.Query, out tokenHandle))
                {
                    diagnostics.AppendLine("OpenProcessToken(" + process.ProcessName + ":" + process.Id + ") failed: " + LastWin32Error());
                    return false;
                }

                if (!DuplicateTokenEx(tokenHandle, TokenAccess.Impersonate | TokenAccess.Query, IntPtr.Zero,
                        SecurityImpersonationLevel.SecurityImpersonation, TokenType.TokenImpersonation, out duplicateTokenHandle))
                {
                    diagnostics.AppendLine("DuplicateTokenEx(" + process.ProcessName + ":" + process.Id + ") failed: " + LastWin32Error());
                    return false;
                }

                if (!ImpersonateLoggedOnUser(duplicateTokenHandle))
                {
                    diagnostics.AppendLine("ImpersonateLoggedOnUser(" + process.ProcessName + ":" + process.Id + ") failed: " + LastWin32Error());
                    return false;
                }

                if (CurrentIdentityIsSystem())
                {
                    diagnostics.AppendLine("Impersonating SYSTEM via " + process.ProcessName + ":" + process.Id + ".");
                    return true;
                }

                diagnostics.AppendLine("Impersonation via " + process.ProcessName + ":" + process.Id + " produced " + CurrentIdentityName() + ", not SYSTEM.");
                RevertToSelf();
                return false;
            }
            finally
            {
                if (duplicateTokenHandle != IntPtr.Zero) CloseHandle(duplicateTokenHandle);
                if (tokenHandle != IntPtr.Zero) CloseHandle(tokenHandle);
                if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
            }
        }

        private static bool TryEnablePrivilege(string privilege, StringBuilder diagnostics)
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenAccess.AdjustPrivileges | TokenAccess.Query, out var tokenHandle))
            {
                diagnostics.AppendLine("OpenProcessToken(current) failed while enabling " + privilege + ": " + LastWin32Error());
                return false;
            }

            try
            {
                if (!LookupPrivilegeValue(null, privilege, out var luid))
                {
                    diagnostics.AppendLine("LookupPrivilegeValue(" + privilege + ") failed: " + LastWin32Error());
                    return false;
                }

                var privileges = new TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = SePrivilegeEnabled };
                if (!AdjustTokenPrivileges(tokenHandle, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    diagnostics.AppendLine("AdjustTokenPrivileges(" + privilege + ") failed: " + LastWin32Error());
                    return false;
                }

                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    diagnostics.AppendLine("AdjustTokenPrivileges(" + privilege + ") warning: " + new Win32Exception(error).Message + " (0x" + error.ToString("X") + ")");
                    return false;
                }

                diagnostics.AppendLine("Enabled " + privilege + ".");
                return true;
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }

        // ---- architecture validation & staging helpers ------------------------------------

        private static void ValidateDwmArchitecture(IEnumerable<Process> dwmInstances, string dllPath)
        {
            var dllMachine = ReadPortableExecutableMachine(dllPath);
            foreach (var dwm in dwmInstances)
            {
                var dwmMachine = GetProcessMachine(dwm);
                if (dwmMachine != 0 && dwmMachine != dllMachine)
                {
                    throw new InvalidOperationException(
                        "Architecture mismatch: dwm_lut.dll is " + MachineName(dllMachine) +
                        " but dwm.exe pid " + dwm.Id + " is " + MachineName(dwmMachine) + ". Use a matching package.");
                }
            }
        }

        private static ushort GetProcessMachine(Process process)
        {
            if (IsWow64Process2(process.Handle, out var processMachine, out var nativeMachine))
            {
                return processMachine != 0 ? processMachine : nativeMachine;
            }

            return IntPtr.Size == 8 ? ImageFileMachineAmd64 : (ushort)0;
        }

        private static ushort ReadPortableExecutableMachine(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Not a PE file: " + path);
            stream.Position = 0x3C;
            var ntHeaderOffset = reader.ReadInt32();
            stream.Position = ntHeaderOffset;
            if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("Invalid PE header: " + path);
            return reader.ReadUInt16();
        }

        private static string MachineName(ushort machine) => machine switch
        {
            ImageFileMachineAmd64 => "x64",
            ImageFileMachineArm64 => "arm64",
            0 => "unknown",
            _ => "0x" + machine.ToString("X4")
        };

        private void WriteMonitorManifest(IEnumerable<MonitorConfig> monitors)
        {
            var manifestPath = Path.Combine(_lutsPath, "manifest.tsv");
            using (var writer = new StreamWriter(manifestPath, false, Encoding.UTF8))
            {
			writer.WriteLine("position\tsource_id\tadapter_luid\ttarget_id\tdevice_path\tname\tconnector\tsdr_lut\thdr_lut");
                foreach (var m in monitors)
                {
                    writer.WriteLine(
				ManifestField(m.Position) + "\t" + m.SourceId + "\t" +
				m.AdapterLuidHigh.ToString("X8") + m.AdapterLuidLow.ToString("X8") + "\t" + m.TargetId + "\t" +
                        ManifestField(m.DevicePath) + "\t" + ManifestField(m.Name) + "\t" +
                        ManifestField(m.Connector) + "\t" + ManifestField(m.SdrLutPath) + "\t" + ManifestField(m.HdrLutPath));
                }
            }

            ClearPermissions(manifestPath);
        }

        private void WriteResolverConfig(string resolverMode)
        {
            var normalized = string.IsNullOrWhiteSpace(resolverMode) ? "auto" : resolverMode.Trim();
            var resolverPath = Path.Combine(_lutsPath, "resolver.cfg");
            File.WriteAllText(resolverPath, "mode=" + normalized + Environment.NewLine, Encoding.ASCII);
            ClearPermissions(resolverPath);
        }

        private static string ManifestField(string? value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

        private static void ClearPermissions(string path)
        {
            var handle = CreateFile(path, DesiredAccess.ReadControl | DesiredAccess.WriteDac, 0, IntPtr.Zero,
                CreationDisposition.OpenExisting,
                FlagsAndAttributes.FileAttributeNormal | FlagsAndAttributes.FileFlagBackupSemantics, IntPtr.Zero);
            if (handle == InvalidHandleValue)
            {
                throw new InvalidOperationException("CreateFile(" + path + ") for ACL reset failed: " + LastWin32Error());
            }

            try
            {
                var result = SetSecurityInfo(handle, SeObjectType.SeFileObject, SecurityInformation.DaclSecurityInformation,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (result != 0)
                {
                    throw new InvalidOperationException("SetSecurityInfo(" + path + ") failed: " +
                        new Win32Exception((int)result).Message + " (0x" + result.ToString("X") + ")");
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        // ---- identity helpers -------------------------------------------------------------

        private static string CurrentIdentityName()
        {
            var identity = WindowsIdentity.GetCurrent(true) ?? WindowsIdentity.GetCurrent();
            return identity.Name + " [" + identity.User?.Value + "]";
        }

        private static bool CurrentIdentityIsSystem()
        {
            var identity = WindowsIdentity.GetCurrent(true) ?? WindowsIdentity.GetCurrent();
            return identity.User != null && identity.User.IsWellKnown(WellKnownSidType.LocalSystemSid);
        }

        public static bool CurrentProcessIsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static string LastWin32Error()
        {
            var error = Marshal.GetLastWin32Error();
            return error == 0 ? "no Win32 error" : new Win32Exception(error).Message + " (0x" + error.ToString("X") + ")";
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Deleting the staged DLL is best-effort.
            }
        }

        private static void Log(string message)
        {
            try
            {
                AppPaths.EnsureRoot();
                File.AppendAllText(AppPaths.GuiLogFile,
                    DateTimeOffset.Now.ToString("O") + " pid=" + Process.GetCurrentProcess().Id + " " + message + Environment.NewLine);
            }
            catch
            {
                // Logging must never be the reason injection fails.
            }
        }
    }
}
