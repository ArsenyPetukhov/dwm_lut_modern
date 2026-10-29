using System;
using System.Collections.Generic;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
	/// <summary>Durable application state boundary.</summary>
	public interface IAppStateStore
	{
		AppState State { get; }
		AppState Load();
		void Save();
	}

	/// <summary>Privileged DWM injection boundary.</summary>
	public interface IInjectionService
	{
		bool NoDebug { get; }
		InjectionStatus GetStatus();
		InjectionResult Apply(IEnumerable<MonitorConfig> monitors, string resolverMode,
			string sdrDitherMode = SdrDitherModeOption.BlueNoise);
		InjectionResult Uninject();
	}

	/// <summary>Windows display topology boundary.</summary>
	public interface IMonitorService
	{
		IReadOnlyList<MonitorConfig> Reconcile(AppState state);
	}

	/// <summary>Retrying Windows CCD topology provider.</summary>
	public interface IDisplayPathProvider
	{
		WindowsDisplayAPI.DisplayConfig.PathInfo[] GetActivePaths();
	}

	/// <summary>Windows logon task boundary.</summary>
	public interface IAutostartService
	{
		bool IsEnabled();
		bool Enable();
		bool Disable();
	}

	public enum ScheduledTaskPresence
	{
		Missing,
		Present,
		Unknown
	}

	public enum ScheduledTaskDeleteResult
	{
		Deleted,
		NotFound,
		Failed
	}

	/// <summary>Task Scheduler boundary used by the autostart transaction.</summary>
	public interface IScheduledTaskApi
	{
		ScheduledTaskPresence GetPresence(string taskName);
		bool CreateLogonTask(string taskName, string executablePath, string arguments);
		ScheduledTaskDeleteResult Delete(string taskName);
	}

	/// <summary>Win32 global-hotkey registration boundary.</summary>
	public interface IHotkeyRegistrationApi
	{
		bool Register(IntPtr window, int id, uint modifiers, uint virtualKey, out int errorCode);
		bool Unregister(IntPtr window, int id);
	}
}
