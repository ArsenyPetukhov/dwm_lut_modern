using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class AutostartServiceTests
{
	[Fact]
	public void Enable_recreates_a_highest_logon_task_for_the_current_executable()
	{
		var tasks = new FakeScheduledTaskApi();
		var legacyCleanupCalled = false;
		var service = new AutostartService(tasks, @"D:\Portable App\DwmLutModern.exe", () => legacyCleanupCalled = true);

		var result = service.Enable();

		Assert.True(result);
		Assert.True(legacyCleanupCalled);
		Assert.Equal("DwmLutGUI_Autostart", tasks.LastDeletedTask);
		Assert.Equal(AutostartService.TaskName, tasks.LastCreatedTask);
		Assert.Equal(@"D:\Portable App\DwmLutModern.exe", tasks.LastExecutable);
		Assert.Equal("--startup", tasks.LastArguments);
	}

	[Theory]
	[InlineData(ScheduledTaskDeleteResult.Deleted, true)]
	[InlineData(ScheduledTaskDeleteResult.NotFound, true)]
	[InlineData(ScheduledTaskDeleteResult.Failed, false)]
	public void Disable_reports_the_verified_scheduler_result(ScheduledTaskDeleteResult deleteResult, bool expected)
	{
		var tasks = new FakeScheduledTaskApi { DeleteResult = deleteResult };
		var service = new AutostartService(tasks, "app.exe", () => { });

		Assert.Equal(expected, service.Disable());
	}

	[Theory]
	[InlineData(ScheduledTaskPresence.Present, true)]
	[InlineData(ScheduledTaskPresence.Missing, false)]
	[InlineData(ScheduledTaskPresence.Unknown, false)]
	public void IsEnabled_requires_a_confirmed_present_task(ScheduledTaskPresence presence, bool expected)
	{
		var tasks = new FakeScheduledTaskApi { Presence = presence };
		var service = new AutostartService(tasks, "app.exe", () => { });

		Assert.Equal(expected, service.IsEnabled());
	}

	private sealed class FakeScheduledTaskApi : IScheduledTaskApi
	{
		public ScheduledTaskPresence Presence { get; set; } = ScheduledTaskPresence.Missing;
		public ScheduledTaskDeleteResult DeleteResult { get; set; } = ScheduledTaskDeleteResult.Deleted;
		public string? LastDeletedTask { get; private set; }
		public string? LastCreatedTask { get; private set; }
		public string? LastExecutable { get; private set; }
		public string? LastArguments { get; private set; }

		public ScheduledTaskPresence GetPresence(string taskName) => Presence;

		public bool CreateLogonTask(string taskName, string executablePath, string arguments)
		{
			LastCreatedTask = taskName;
			LastExecutable = executablePath;
			LastArguments = arguments;
			return true;
		}

		public ScheduledTaskDeleteResult Delete(string taskName)
		{
			LastDeletedTask = taskName;
			return DeleteResult;
		}
	}
}
