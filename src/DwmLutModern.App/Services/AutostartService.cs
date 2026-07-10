using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Manages the "start with Windows" logon scheduled task. Unlike the legacy app — which
    /// created a task that blindly ran <c>-apply</c> every logon and had no working off switch —
    /// this task launches the app with <c>--startup</c>, which re-applies the LUT only if the
    /// user's saved intent is Enabled. Enable/Disable/IsEnabled are live and idempotent, so a
    /// Settings toggle or tray checkbox always reflects and controls the real task.
    /// </summary>
	public sealed class AutostartService : IAutostartService
    {
        public const string TaskName = "DwmLutModern_Autostart";
        private const string LegacyTaskName = "DwmLutGUI_Autostart";
        private const string LegacyRunValue = "DwmLutGUI";

        /// <summary>The path used to launch the app; the exe next to the running assembly.</summary>
        private static string ExePath
        {
            get
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(path))
                {
                    path = Path.Combine(AppPaths.AppDirectory, "DwmLutModern.exe");
                }
                return path!;
            }
        }

        public bool IsEnabled()
        {
            return RunSchtasks("/query /tn \"" + TaskName + "\"", out _);
        }

        public bool Enable()
        {
            CleanLegacyAutostart();

            var command = "\\\"" + ExePath + "\\\" --startup";
            var args = "/create /tn \"" + TaskName + "\" /tr \"" + command + "\" /sc onlogon /rl highest /f";
            return RunSchtasks(args, out _);
        }

        public bool Disable()
        {
            var ok = RunSchtasks("/delete /tn \"" + TaskName + "\" /f", out var output);
            // Treat "task does not exist" as success — the desired end state is "no task".
            if (!ok && output.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return ok;
        }

        /// <summary>Remove the legacy Run-key value and old scheduled task if present.</summary>
        public void CleanLegacyAutostart()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue(LegacyRunValue, false);
            }
            catch
            {
                // Best-effort cleanup.
            }

            RunSchtasks("/delete /tn \"" + LegacyTaskName + "\" /f", out _);
        }

        private static bool RunSchtasks(string arguments, out string output)
        {
            output = string.Empty;
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                // Drain both redirected streams concurrently; sequential ReadToEnd calls can
                // deadlock when one pipe fills. Enforce the timeout before reporting a truthful
                // transactional result, and stop a timed-out helper from mutating the task later.
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                    catch { }
                    var drained = Task.WaitAll(new Task[] { stdout, stderr }, 5000);
                    output = drained ? stdout.Result + stderr.Result : "schtasks.exe timed out.";
                    return false;
                }

                Task.WaitAll(stdout, stderr);
                output = stdout.Result + stderr.Result;
                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
