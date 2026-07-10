using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DwmLutModern.Services
{
	public static class ScheduledTaskErrorClassifier
	{
		private const int FileNotFound = unchecked((int)0x80070002);
		private const int TaskNotFound = unchecked((int)0x8004130F);

		public static bool IsTaskNotFound(int hresult) =>
			hresult == FileNotFound || hresult == TaskNotFound;
	}

	/// <summary>Small schtasks.exe adapter; localized text never decides task presence/deletion.</summary>
	public sealed class WindowsScheduledTaskApi : IScheduledTaskApi
	{
		public ScheduledTaskPresence GetPresence(string taskName)
		{
			object? serviceObject = null;
			object? folderObject = null;
			object? taskObject = null;
			try
			{
				var serviceType = Type.GetTypeFromProgID("Schedule.Service");
				if (serviceType == null) return ScheduledTaskPresence.Unknown;
				serviceObject = Activator.CreateInstance(serviceType);
				if (serviceObject == null) return ScheduledTaskPresence.Unknown;

				dynamic service = serviceObject;
				service.Connect();
				folderObject = service.GetFolder("\\");
				dynamic folder = folderObject;
				try
				{
					taskObject = folder.GetTask(taskName);
					return ScheduledTaskPresence.Present;
				}
				catch (COMException ex) when (ScheduledTaskErrorClassifier.IsTaskNotFound(ex.HResult))
				{
					return ScheduledTaskPresence.Missing;
				}
			}
			catch
			{
				return ScheduledTaskPresence.Unknown;
			}
			finally
			{
				ReleaseCom(taskObject);
				ReleaseCom(folderObject);
				ReleaseCom(serviceObject);
			}
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

		private static void ReleaseCom(object? value)
		{
			try
			{
				if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
			}
			catch
			{
				// Releasing a disconnected scheduler proxy is best-effort during shutdown/failure.
			}
		}

		private readonly record struct ProcessResult(bool Started, bool TimedOut, int ExitCode)
		{
			public static ProcessResult NotStarted => new ProcessResult(false, false, -1);
			public static ProcessResult Timeout => new ProcessResult(true, true, -1);
		}
	}
}
