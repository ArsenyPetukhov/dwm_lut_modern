using System;
using System.Runtime.InteropServices;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Win32 P/Invoke surface for DLL injection, token privilege/impersonation, and DACL reset.
    /// Ported verbatim in behavior from the legacy Injector so the proven injection path is
    /// unchanged; only the calling code around it is new.
    /// </summary>
    internal static class NativeMethods
    {
        public const ushort ImageFileMachineAmd64 = 0x8664;
        public const ushort ImageFileMachineArm64 = 0xAA64;
        public const uint SePrivilegeEnabled = 0x00000002;
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpFileName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize,
            AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            UIntPtr nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, FreeType dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(DesiredAccess dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr processHandle, TokenAccess desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(IntPtr hExistingToken, TokenAccess dwDesiredAccess,
            IntPtr lpTokenAttributes, SecurityImpersonationLevel impersonationLevel, TokenType tokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out Luid lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
            ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool RevertToSelf();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFile(string lpFileName, DesiredAccess dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition,
            FlagsAndAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("advapi32.dll")]
        public static extern uint SetSecurityInfo(IntPtr handle, SeObjectType objectType,
            SecurityInformation securityInfo, IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

        [Flags]
        public enum FreeType { Release = 0x8000 }

        [Flags]
        public enum AllocationType { Commit = 0x1000, Reserve = 0x2000 }

        [Flags]
        public enum MemoryProtection { ReadWrite = 0x04 }

        [Flags]
        public enum DesiredAccess
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

        public enum CreationDisposition { OpenExisting = 3 }

        [Flags]
        public enum FlagsAndAttributes { FileAttributeNormal = 0x80, FileFlagBackupSemantics = 0x2000000 }

        public enum SeObjectType { SeFileObject = 1 }

        public enum SecurityInformation { DaclSecurityInformation = 0x4 }

        [Flags]
        public enum TokenAccess { Duplicate = 0x0002, Impersonate = 0x0004, Query = 0x0008, AdjustPrivileges = 0x0020 }

        public enum SecurityImpersonationLevel
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        public enum TokenType { TokenPrimary = 1, TokenImpersonation = 2 }

        [StructLayout(LayoutKind.Sequential)]
        public struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public Luid Luid;
            public uint Attributes;
        }
    }
}
