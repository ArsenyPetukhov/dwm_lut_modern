using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace DwmLutGUI
{
    internal static class Injector
    {
        public static readonly bool NoDebug;

        private static readonly string DllName;
        private static readonly string DllPath;
        private static readonly string LutsPath;
        private static readonly string GuiLogPath;
        private static readonly IntPtr LoadlibraryA;
        private static readonly IntPtr FreeLibrary;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private const ushort ImageFileMachineAmd64 = 0x8664;
        private const ushort ImageFileMachineArm64 = 0xAA64;
        private const uint InjectionTimeoutMs = 30000;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitTimeout = 0x00000102;
        private const uint WaitFailed = 0xFFFFFFFF;

        static Injector()
        {
            var basePath = Environment.ExpandEnvironmentVariables("%SYSTEMROOT%\\Temp\\");
            DllName = "dwm_lut.dll";
            DllPath = basePath + DllName;
            LutsPath = basePath + "luts\\";
            GuiLogPath = basePath + "dwm_lut_gui.log";

            var kernel32 = GetModuleHandle("kernel32.dll");
            LoadlibraryA = GetProcAddress(kernel32, "LoadLibraryA");
            FreeLibrary = GetProcAddress(kernel32, "FreeLibrary");

            try
            {
                Process.EnterDebugMode();
            }
            catch (Exception ex)
            {
                LogGui("Process.EnterDebugMode failed: " + ex);
#if !DEBUG
                MessageBox.Show("Failed to enter debug mode – will not be able to apply LUTs.");
#endif
                NoDebug = true;
            }
        }

        private static void LogGui(string message)
        {
            try
            {
                File.AppendAllText(
                    GuiLogPath,
                    DateTimeOffset.Now.ToString("O") + " pid=" + Process.GetCurrentProcess().Id + " " + message + Environment.NewLine);
            }
            catch
            {
                // Logging must never be the reason injection fails.
            }
        }

        public static bool? GetStatus()
        {
            if (NoDebug) return null;

            var dwmInstances = Process.GetProcessesByName("dwm");
            if (dwmInstances.Length == 0) return null;

            bool? result = false;
            foreach (var dwm in dwmInstances)
            {
                try
                {
                    var modules = dwm.Modules;
                    foreach (ProcessModule module in modules)
                    {
                        if (module.ModuleName == DllName)
                        {
                            result = true;
                        }

                        module.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LogGui("GetStatus could not enumerate dwm.exe pid " + dwm.Id + " modules: " + ex.Message);
                    result = null;
                }

                dwm.Dispose();
            }

            return result;
        }

        private static void CopyOrConvertLut(string source, string dest)
        {
            var extension = Path.GetExtension(source).TrimStart('.').ToLowerInvariant();
            switch (extension)
            {
                case "cube":
                    File.Copy(source, dest);
                    ClearPermissions(dest);
                    break;
                case "txt":
                {
                    var lines = File.ReadAllLines(source);
                    const int lutSize = 65;
                    var expectedLines = lutSize * lutSize * lutSize;
                    if (lines.Length < expectedLines)
                    {
                        throw new Exception("Unsupported DisplayCAL TXT LUT: expected at least " + expectedLines + " rows, found " + lines.Length + ".");
                    }

                    using (var file = new StreamWriter(dest))
                    {
                        file.WriteLine("LUT_3D_SIZE " + lutSize);

                        for (var b = 0; b < lutSize; b++)
                        {
                            for (var g = 0; g < lutSize; g++)
                            {
                                for (var r = 0; r < lutSize; r++)
                                {
                                    var line = lines[g + lutSize * (r + lutSize * b)];
                                    var start = 1;
                                    var found = 0;

                                    while (found != 3 && start < line.Length)
                                    {
                                        if (line[start++] == ' ') found++;
                                    }
                                    if (found != 3 || start >= line.Length)
                                    {
                                        throw new Exception("Unsupported DisplayCAL TXT LUT row at index " + (g + lutSize * (r + lutSize * b)) + ".");
                                    }

                                    file.WriteLine(line.Substring(start));
                                }
                            }
                        }
                    }

                    ClearPermissions(dest);
                    break;
                }
                default:
                    throw new Exception("Unsupported LUT format: " + extension);
            }
        }

        private static string ManifestField(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static void WriteMonitorManifest(IEnumerable<MonitorData> monitors)
        {
            // The injected DLL cannot safely query the full GUI display model
            // inside DWM. This manifest preserves the GUI's monitor topology
            // for diagnostics and future source-id based LUT matching.
            var manifestPath = Path.Combine(LutsPath, "manifest.tsv");
            using (var writer = new StreamWriter(manifestPath, false, Encoding.UTF8))
            {
                writer.WriteLine("position\tsource_id\ttarget_id\tdevice_path\tname\tconnector\tsdr_lut\thdr_lut");
                foreach (var monitor in monitors)
                {
                    writer.WriteLine(
                        ManifestField(monitor.Position) + "\t" +
                        monitor.SourceId + "\t" +
                        monitor.TargetId + "\t" +
                        ManifestField(monitor.DevicePath) + "\t" +
                        ManifestField(monitor.Name) + "\t" +
                        ManifestField(monitor.Connector) + "\t" +
                        ManifestField(monitor.SdrLutPath) + "\t" +
                        ManifestField(monitor.HdrLutPath));
                }
            }

            ClearPermissions(manifestPath);
        }

        private static void WriteResolverConfig(string resolverMode)
        {
            var normalized = string.IsNullOrWhiteSpace(resolverMode) ? "auto" : resolverMode.Trim();
            var resolverPath = Path.Combine(LutsPath, "resolver.cfg");
            File.WriteAllText(resolverPath, "mode=" + normalized + Environment.NewLine, Encoding.ASCII);
            ClearPermissions(resolverPath);
        }

        private static string MachineName(ushort machine)
        {
            switch (machine)
            {
                case ImageFileMachineAmd64:
                    return "x64";
                case ImageFileMachineArm64:
                    return "arm64";
                case 0:
                    return "unknown";
                default:
                    return "0x" + machine.ToString("X4");
            }
        }

        private static ushort ReadPortableExecutableMachine(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt16() != 0x5A4D)
                {
                    throw new Exception("Not a PE file: " + path);
                }

                stream.Position = 0x3C;
                var ntHeaderOffset = reader.ReadInt32();
                stream.Position = ntHeaderOffset;
                if (reader.ReadUInt32() != 0x00004550)
                {
                    throw new Exception("Invalid PE header: " + path);
                }

                return reader.ReadUInt16();
            }
        }

        private static ushort GetProcessMachine(Process process)
        {
            ushort processMachine;
            ushort nativeMachine;
            if (IsWow64Process2(process.Handle, out processMachine, out nativeMachine))
            {
                return processMachine != 0 ? processMachine : nativeMachine;
            }

            return IntPtr.Size == 8 ? ImageFileMachineAmd64 : (ushort)0;
        }

        private static void ValidateDwmArchitecture(IEnumerable<Process> dwmInstances, string dllPath)
        {
            var dllMachine = ReadPortableExecutableMachine(dllPath);
            foreach (var dwm in dwmInstances)
            {
                var dwmMachine = GetProcessMachine(dwm);
                if (dwmMachine != 0 && dwmMachine != dllMachine)
                {
                    throw new Exception(
                        "Architecture mismatch: dwm_lut.dll is " + MachineName(dllMachine) +
                        " but dwm.exe pid " + dwm.Id + " is " + MachineName(dwmMachine) +
                        ". Use a matching package.");
                }
            }
        }

        private static string LastWin32Error()
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 0) return "no Win32 error";
            return new Win32Exception(error).Message + " (0x" + error.ToString("X") + ")";
        }

        private static string CurrentIdentityName()
        {
            var identity = WindowsIdentity.GetCurrent(true) ?? WindowsIdentity.GetCurrent();
            return identity.Name + " [" + identity.User.Value + "]";
        }

        private static bool CurrentIdentityIsSystem()
        {
            var identity = WindowsIdentity.GetCurrent(true) ?? WindowsIdentity.GetCurrent();
            return identity.User != null && identity.User.IsWellKnown(WellKnownSidType.LocalSystemSid);
        }

        private static bool CurrentProcessIsElevated()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool TryEnablePrivilege(string privilege, StringBuilder diagnostics)
        {
            IntPtr tokenHandle;
            if (!OpenProcessToken(GetCurrentProcess(), TokenAccess.AdjustPrivileges | TokenAccess.Query, out tokenHandle))
            {
                diagnostics.AppendLine("OpenProcessToken(current) failed while enabling " + privilege + ": " + LastWin32Error());
                return false;
            }

            try
            {
                LUID luid;
                if (!LookupPrivilegeValue(null, privilege, out luid))
                {
                    diagnostics.AppendLine("LookupPrivilegeValue(" + privilege + ") failed: " + LastWin32Error());
                    return false;
                }

                var privileges = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                if (!AdjustTokenPrivileges(tokenHandle, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    diagnostics.AppendLine("AdjustTokenPrivileges(" + privilege + ") failed: " + LastWin32Error());
                    return false;
                }

                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    diagnostics.AppendLine("AdjustTokenPrivileges(" + privilege + ") returned warning: " + new Win32Exception(error).Message + " (0x" + error.ToString("X") + ")");
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

        private static bool TryImpersonateSystemFromProcess(Process process, StringBuilder diagnostics)
        {
            IntPtr processHandle = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr duplicateTokenHandle = IntPtr.Zero;

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

                if (!DuplicateTokenEx(
                        tokenHandle,
                        TokenAccess.Impersonate | TokenAccess.Query,
                        IntPtr.Zero,
                        SecurityImpersonationLevel.SecurityImpersonation,
                        TokenType.TokenImpersonation,
                        out duplicateTokenHandle))
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

        private static bool EnsureInjectionPrivileges(out string diagnosticsText)
        {
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("Starting identity: " + CurrentIdentityName() + ".");

            TryEnablePrivilege("SeDebugPrivilege", diagnostics);

            if (TryImpersonateSystem(diagnostics))
            {
                diagnosticsText = diagnostics.ToString();
                return true;
            }

            if (CurrentProcessIsElevated())
            {
                diagnostics.AppendLine("SYSTEM impersonation failed; continuing as elevated administrator.");
                diagnosticsText = diagnostics.ToString();
                return false;
            }

            diagnosticsText = diagnostics.ToString();
            throw new Exception(
                "DWM LUT must be run as Administrator. Could not obtain injection privileges." +
                Environment.NewLine + diagnostics);
        }

        public static void Inject(IEnumerable<MonitorData> monitors, string resolverMode)
        {
            var impersonating = EnsureInjectionPrivileges(out var privilegeDiagnostics);
            LogGui("Inject privilege diagnostics:" + Environment.NewLine + privilegeDiagnostics.TrimEnd());
            var monitorList = monitors.ToList();
            var normalizedResolverMode = string.IsNullOrWhiteSpace(resolverMode) ? "auto" : resolverMode.Trim();
            var sourceDllPath = AppDomain.CurrentDomain.BaseDirectory + DllName;
            var dwmInstances = Process.GetProcessesByName("dwm");

            try
            {
                ValidateDwmArchitecture(dwmInstances, sourceDllPath);

                File.Copy(sourceDllPath, DllPath, true);
                ClearPermissions(DllPath);

                if (Directory.Exists(LutsPath))
                {
                    Directory.Delete(LutsPath, true);
                }

                Directory.CreateDirectory(LutsPath);
                ClearPermissions(LutsPath);
                WriteMonitorManifest(monitorList);
                WriteResolverConfig(normalizedResolverMode);

                foreach (var monitor in monitorList)
                {
                    if (!string.IsNullOrEmpty(monitor.SdrLutPath))
                    {
                        var dest = LutsPath + monitor.Position.Replace(',', '_') + ".cube";
                        CopyOrConvertLut(monitor.SdrLutPath, dest);
                    }

                    if (string.IsNullOrEmpty(monitor.HdrLutPath)) continue;
                    {
                        var dest = LutsPath + monitor.Position.Replace(',', '_') + "_hdr.cube";
                        CopyOrConvertLut(monitor.HdrLutPath, dest);
                    }
                }

                if (dwmInstances.Length == 0)
                {
                    throw new Exception("No dwm.exe process was found.");
                }

                var failed = false;
                var diagnostics = new StringBuilder();
                var bytes = Encoding.ASCII.GetBytes(DllPath + "\0");
                LogGui("Inject start: dwm_count=" + dwmInstances.Length + " monitors=" + monitorList.Count + " resolver=" + normalizedResolverMode + " dll=" + sourceDllPath);
                foreach (var dwm in dwmInstances)
                {
                    IntPtr processHandle = IntPtr.Zero;
                    IntPtr address = IntPtr.Zero;
                    IntPtr thread = IntPtr.Zero;
                    try
                    {
                        processHandle = OpenProcess(
                            DesiredAccess.ProcessCreateThread |
                            DesiredAccess.ProcessQueryInformation |
                            DesiredAccess.ProcessVmOperation |
                            DesiredAccess.ProcessVmWrite |
                            DesiredAccess.ProcessVmRead,
                            false,
                            (uint)dwm.Id);
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

                        if (!WriteProcessMemory(processHandle, address, bytes, (UIntPtr)bytes.Length, out var bytesWritten) ||
                            bytesWritten.ToUInt64() != (ulong)bytes.Length)
                        {
                            failed = true;
                            diagnostics.AppendLine("WriteProcessMemory(dwm:" + dwm.Id + ") failed or wrote " + bytesWritten + "/" + bytes.Length + " bytes: " + LastWin32Error());
                            continue;
                        }

                        thread = CreateRemoteThread(processHandle, IntPtr.Zero, 0, LoadlibraryA, address, 0, out var threadId);
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
                            diagnostics.AppendLine("WaitForSingleObject(LoadLibraryA,dwm:" + dwm.Id + ",tid:" + threadId + ") returned 0x" + wait.ToString("X") +
                                                   (wait == WaitTimeout ? " (timeout)" : wait == WaitFailed ? " (" + LastWin32Error() + ")" : ""));
                            continue;
                        }

                        if (!GetExitCodeThread(thread, out var exitCode))
                        {
                            failed = true;
                            diagnostics.AppendLine("GetExitCodeThread(LoadLibraryA,dwm:" + dwm.Id + ",tid:" + threadId + ") failed: " + LastWin32Error());
                            continue;
                        }
                        if (exitCode == 0)
                        {
                            failed = true;
                            diagnostics.AppendLine("LoadLibraryA(dwm:" + dwm.Id + ",tid:" + threadId + ") returned NULL. See %SystemRoot%\\Temp\\dwm_lut.log for DLL-side startup diagnostics.");
                        }
                        else
                        {
                            diagnostics.AppendLine("LoadLibraryA(dwm:" + dwm.Id + ",tid:" + threadId + ") returned module handle 0x" + exitCode.ToString("X") + ".");
                        }
                    }
                    finally
                    {
                        if (thread != IntPtr.Zero) CloseHandle(thread);
                        if (address != IntPtr.Zero) VirtualFreeEx(processHandle, address, 0, FreeType.Release);
                        if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
                    }
                }

                Directory.Delete(LutsPath, true);

                if (!failed) return;

                File.Delete(DllPath);
                LogGui("Inject failed:" + Environment.NewLine + diagnostics.ToString().TrimEnd());

                throw new Exception(
                    "Failed to load or initialize DLL. This probably means that a LUT file is malformed, DWM got updated, or this token still cannot inject into DWM." +
                    Environment.NewLine + Environment.NewLine + diagnostics +
                    Environment.NewLine + "GUI log: " + GuiLogPath +
                    Environment.NewLine + "DWM log: %SystemRoot%\\Temp\\dwm_lut.log");
            }
            finally
            {
                foreach (var dwm in dwmInstances) dwm.Dispose();

                if (impersonating)
                {
                    RevertToSelf();
                }
            }
        }

        public static void Uninject()
        {
            var impersonating = EnsureInjectionPrivileges(out var privilegeDiagnostics);
            LogGui("Uninject privilege diagnostics:" + Environment.NewLine + privilegeDiagnostics.TrimEnd());
            var dwmInstances = Process.GetProcessesByName("dwm");
            var diagnostics = new StringBuilder();
            try
            {
                foreach (var dwm in dwmInstances)
                {
                    var processHandle = OpenProcess(
                        DesiredAccess.ProcessCreateThread |
                        DesiredAccess.ProcessQueryInformation |
                        DesiredAccess.ProcessVmOperation |
                        DesiredAccess.ProcessVmWrite |
                        DesiredAccess.ProcessVmRead,
                        false,
                        (uint)dwm.Id);
                    if (processHandle == IntPtr.Zero)
                    {
                        diagnostics.AppendLine("OpenProcess(dwm:" + dwm.Id + ") for uninject failed: " + LastWin32Error());
                        continue;
                    }

                    try
                    {
                        var modules = dwm.Modules;
                        foreach (ProcessModule module in modules)
                        {
                            if (module.ModuleName == DllName)
                            {
                                var thread = CreateRemoteThread(processHandle, IntPtr.Zero, 0, FreeLibrary, module.BaseAddress,
                                    0, out var threadId);
                                if (thread == IntPtr.Zero)
                                {
                                    diagnostics.AppendLine("CreateRemoteThread(FreeLibrary,dwm:" + dwm.Id + ") failed: " + LastWin32Error());
                                }
                                else
                                {
                                    var wait = WaitForSingleObject(thread, InjectionTimeoutMs);
                                    if (wait != WaitObject0)
                                    {
                                        diagnostics.AppendLine("WaitForSingleObject(FreeLibrary,dwm:" + dwm.Id + ",tid:" + threadId + ") returned 0x" + wait.ToString("X") + ".");
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
                    LogGui("Uninject diagnostics:" + Environment.NewLine + diagnostics.ToString().TrimEnd());
                }

                File.Delete(DllPath);
            }
            finally
            {
                foreach (var dwm in dwmInstances) dwm.Dispose();

                if (impersonating)
                {
                    RevertToSelf();
                }
            }
        }

        private static void ClearPermissions(string path)
        {
            var hFile = CreateFile(path, DesiredAccess.ReadControl | DesiredAccess.WriteDac, 0, IntPtr.Zero,
                CreationDisposition.OpenExisting,
                FlagsAndAttributes.FileAttributeNormal | FlagsAndAttributes.FileFlagBackupSemantics,
                IntPtr.Zero);
            if (hFile == InvalidHandleValue)
            {
                throw new Exception("CreateFile(" + path + ") for ACL reset failed: " + LastWin32Error());
            }
            try
            {
                var result = SetSecurityInfo(hFile, SeObjectType.SeFileObject, SecurityInformation.DaclSecurityInformation, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (result != 0)
                {
                    throw new Exception("SetSecurityInfo(" + path + ") failed: " + new Win32Exception((int)result).Message + " (0x" + result.ToString("X") + ")");
                }
            }
            finally
            {
                CloseHandle(hFile);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpFileName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize,
            AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            UIntPtr nSize,
            out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, FreeType dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(DesiredAccess dwDesiredAccess, bool bInheritHandle,
                       uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, TokenAccess desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, TokenAccess dwDesiredAccess,
            IntPtr lpTokenAttributes, SecurityImpersonationLevel impersonationLevel, TokenType tokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
            ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool RevertToSelf();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, DesiredAccess dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition,
            FlagsAndAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("advapi32.dll")]
        private static extern uint SetSecurityInfo(IntPtr handle, SeObjectType ObjectType,
            SecurityInformation SecurityInfo, IntPtr psidOwner,
            IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

        [Flags]
        private enum FreeType
        {
            Release = 0x8000,
        }

        [Flags]
        private enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000
        }

        [Flags]
        private enum MemoryProtection
        {
            ReadWrite = 0x04
        }

        [Flags]
        private enum DesiredAccess
        {
            ProcessCreateThread = 0x0002,
            ProcessVmOperation = 0x0008,
            ProcessVmRead = 0x0010,
            ProcessVmWrite = 0x0020,
            ProcessQueryInformation = 0x0400,
            ReadControl = 0x20000,
            WriteDac = 0x40000,
            ProcessQueryLimitedInformation = 0x1000
        }

        private enum CreationDisposition
        {
            OpenExisting = 3
        }

        [Flags]
        private enum FlagsAndAttributes
        {
            FileAttributeNormal = 0x80,
            FileFlagBackupSemantics = 0x2000000
        }

        private enum SeObjectType
        {
            SeFileObject = 1
        }

        private enum SecurityInformation
        {
            DaclSecurityInformation = 0x4
        }

        [Flags]
        private enum TokenAccess
        {
            Duplicate = 0x0002,
            Impersonate = 0x0004,
            Query = 0x0008,
            AdjustPrivileges = 0x0020
        }

        private enum SecurityImpersonationLevel
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TokenType
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    }
}

