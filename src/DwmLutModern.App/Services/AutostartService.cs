using System;
using System.Diagnostics;
using System.IO;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Manages the intent-aware logon task through a testable Task Scheduler boundary. An enabled
    /// task is recreated on owner startup, so moving the portable folder repairs its executable
    /// path instead of leaving a stale task behind.
    /// </summary>
	public sealed class AutostartService : IAutostartService
    {
        public const string TaskName = "DwmLutModern_Autostart";
        public const string LegacyTaskName = "DwmLutGUI_Autostart";
        private const string LegacyRunValue = "DwmLutGUI";

		private readonly IScheduledTaskApi _tasks;
		private readonly string _exePath;
		private readonly Action _cleanLegacyRunValue;

		public AutostartService()
			: this(new WindowsScheduledTaskApi(), GetExePath(), CleanLegacyRunValue)
		{
		}

		public AutostartService(IScheduledTaskApi tasks, string exePath, Action cleanLegacyRunValue)
		{
			_tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
			_exePath = string.IsNullOrWhiteSpace(exePath)
				? throw new ArgumentException("An executable path is required.", nameof(exePath))
				: Path.GetFullPath(exePath);
			_cleanLegacyRunValue = cleanLegacyRunValue ?? throw new ArgumentNullException(nameof(cleanLegacyRunValue));
		}

		public bool IsEnabled() => _tasks.GetPresence(TaskName) == ScheduledTaskPresence.Present;

		public bool Enable()
		{
			CleanLegacyAutostart();
			return _tasks.CreateLogonTask(TaskName, _exePath, "--startup");
		}

		public bool Disable()
		{
			return _tasks.Delete(TaskName) is
				ScheduledTaskDeleteResult.Deleted or ScheduledTaskDeleteResult.NotFound;
		}

		/// <summary>Remove the legacy Run-key value and old scheduled task if present.</summary>
		public void CleanLegacyAutostart()
		{
			try { _cleanLegacyRunValue(); } catch { /* Best-effort legacy cleanup. */ }
			_tasks.Delete(LegacyTaskName);
		}

		private static string GetExePath()
		{
			var path = Process.GetCurrentProcess().MainModule?.FileName;
			return string.IsNullOrEmpty(path)
				? Path.Combine(AppPaths.AppDirectory, "DwmLutModern.exe")
				: path;
		}

		private static void CleanLegacyRunValue()
		{
			using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
				@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
			key?.DeleteValue(LegacyRunValue, false);
		}
    }
}
