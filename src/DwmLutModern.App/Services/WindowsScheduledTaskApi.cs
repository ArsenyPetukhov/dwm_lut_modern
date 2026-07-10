using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DwmLutModern.Services
{
	/// <summary>Small schtasks.exe adapter; localized text never decides task presence/deletion.</summary>
	public sealed class WindowsScheduledTaskApi : IScheduledTaskApi
	{
		public ScheduledTaskPresence GetPresence(string taskName)
		{
			var result = Run("/query /tn \"" + taskName + "\"");
			if (result.ExitCode == 0) return ScheduledTaskPresence.Present;
			// schtasks uses ERROR_FILE_NOT_FOUND (1) for a missing named task. Any launch/timeout
			// failure is Unknown so Disable never claims a verified end state from translated text.
			return result.Started && !result.TimedOut && result.ExitCode == 1
				? ScheduledTaskPresence.Missing
				: ScheduledTaskPresence.Unknown;
		}

		public bool CreateLogonTask(string taskName, string executablePath, string arguments)
		{
			var command = "\\\"" + executablePath + "\\\" " + arguments;
			var create = "/create /tn \"" + taskName + "\" /tr \"" + command + "\" /sc onlogon /rl highest /f";
			return Run(create).ExitCode == 0;
		}

		public ScheduledTaskDeleteResult Delete(string taskName)
		{
			var presence = GetPresence(taskName);
			if (presence == ScheduledTaskPresence.Missing) return ScheduledTaskDeleteResult.NotFound;
			if (presence == ScheduledTaskPresence.Unknown) return ScheduledTaskDeleteResult.Failed;
			return Run("/delete /tn \"" + taskName + "\" /f").ExitCode == 0
				? ScheduledTaskDeleteResult.Deleted
				: ScheduledTaskDeleteResult.Failed;
		}

		private static ProcessResult Run(string arguments)
		{
			try
			{
				var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					WindowStyle = ProcessWindowStyle.Hidden
				};

				using var process = Process.Start(startInfo);
				if (process == null) return ProcessResult.NotStarted;

				var stdout = process.StandardOutput.ReadToEndAsync();
				var stderr = process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(15000))
				{
					try { process.Kill(entireProcessTree: true); process.WaitForExit(5000); } catch { }
					Task.WaitAll(new Task[] { stdout, stderr }, 5000);
					return ProcessResult.Timeout;
				}

				Task.WaitAll(stdout, stderr);
				return new ProcessResult(true, false, process.ExitCode);
			}
			catch
			{
				return ProcessResult.NotStarted;
			}
		}

		private readonly record struct ProcessResult(bool Started, bool TimedOut, int ExitCode)
		{
			public static ProcessResult NotStarted => new ProcessResult(false, false, -1);
			public static ProcessResult Timeout => new ProcessResult(true, true, -1);
		}
	}
}
