using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class AppControllerTests
{
	[Fact]
	public async Task Watchdog_restores_enabled_intent_after_a_cold_process_start()
	{
		var state = StateWithAssignedLut(isEnabled: true);
		var injection = new FakeInjectionService
		{
			Status = InjectionStatus.Inactive,
			ApplyResult = InjectionResult.Ok("restored")
		};
		var controller = CreateController(state, injection);

		await controller.RunWatchdogAsync();

		Assert.Equal(1, injection.ApplyCount);
	}

	[Fact]
	public void Autostart_preference_is_not_persisted_when_task_creation_fails()
	{
		var state = StateWithAssignedLut(isEnabled: false);
		var store = new FakeStateStore(state);
		var autostart = new FakeAutostartService { EnableResult = false };
		var controller = CreateController(store, new FakeInjectionService(), autostart);

		var changed = controller.SetAutostart(true);

		Assert.False(changed);
		Assert.False(state.StartWithWindows);
	}

	[Fact]
	public void Disabling_an_already_absent_autostart_task_is_idempotent()
	{
		var state = StateWithAssignedLut(isEnabled: false);
		state.StartWithWindows = true;
		state.StartMinimized = true;
		var store = new FakeStateStore(state);
		var autostart = new FakeAutostartService { Enabled = false, DisableResult = false };
		var controller = CreateController(store, new FakeInjectionService(), autostart);

		var changed = controller.SetAutostart(false);

		Assert.True(changed);
		Assert.False(state.StartWithWindows);
		Assert.False(state.StartMinimized);
		Assert.Equal(1, store.SaveCount);
	}

	[Fact]
	public async Task Disabled_intent_never_calls_the_apply_boundary()
	{
		var state = StateWithAssignedLut(isEnabled: false);
		var injection = new FakeInjectionService { Status = InjectionStatus.Inactive };
		var controller = CreateController(state, injection);

		var result = await controller.ReapplyIfEnabledAsync(AppOperation.Reapply, silent: true);

		Assert.True(result.Success);
		Assert.Equal(0, injection.ApplyCount);
	}

	[Fact]
	public async Task Failed_disable_remains_visible_and_retryable()
	{
		var state = StateWithAssignedLut(isEnabled: true);
		var injection = new FakeInjectionService
		{
			Status = InjectionStatus.Active,
			UninjectResult = InjectionResult.Fail("dwm_lut.dll is still loaded")
		};
		var controller = CreateController(state, injection);

		var result = await controller.DisableAsync();

		Assert.False(result.Success);
		Assert.False(state.IsEnabled);
		Assert.True(controller.CanRequestDisable);
		Assert.Same(result, controller.LastOperationResult);
	}

	[Fact]
	public async Task Apply_passes_the_full_connected_topology_even_when_only_one_display_has_a_lut()
	{
		var state = StateWithAssignedLut(isEnabled: false);
		state.Monitors.Add(new MonitorConfig
		{
			DevicePath = "DISPLAY-B",
			Position = "1920,0",
			IsConnected = true
		});
		var injection = new FakeInjectionService();
		var controller = CreateController(state, injection);

		await controller.EnableAsync();

		Assert.Equal(2, injection.AppliedMonitorCount);
	}

	[Fact]
	public async Task Apply_without_any_assigned_lut_is_rejected_without_arming_recovery()
	{
		var state = new AppState
		{
			Monitors =
			{
				new MonitorConfig { DevicePath = "DISPLAY-A", Position = "0,0", IsConnected = true }
			}
		};
		var injection = new FakeInjectionService();
		var controller = CreateController(state, injection);

		var result = await controller.EnableAsync();

		Assert.False(result.Success);
		Assert.False(state.IsEnabled);
		Assert.Equal(0, injection.ApplyCount);
	}

	[Fact]
	public async Task Clearing_the_last_lut_while_enabled_disables_instead_of_arming_recovery()
	{
		var state = StateWithAssignedLut(isEnabled: true);
		state.Monitors[0].SdrLutPath = null;
		var injection = new FakeInjectionService { Status = InjectionStatus.Active };
		var controller = CreateController(state, injection);

		var result = await controller.ReapplyIfActiveAsync();

		Assert.True(result.Success);
		Assert.False(state.IsEnabled);
		Assert.Equal(0, injection.ApplyCount);
		Assert.Equal(1, injection.UninjectCount);
	}

	[Fact]
	public async Task Reapply_refuses_stale_saved_topology_when_no_display_is_connected()
	{
		var state = StateWithAssignedLut(isEnabled: true);
		state.Monitors[0].IsConnected = false;
		var injection = new FakeInjectionService();
		var controller = CreateController(state, injection);

		var result = await controller.ReapplyIfEnabledAsync(AppOperation.Startup, silent: true);

		Assert.False(result.Success);
		Assert.True(state.IsEnabled);
		Assert.Equal(0, injection.ApplyCount);
	}

	[Fact]
	public async Task Watchdog_repairs_active_but_failed_partial_apply_state()
	{
		var state = StateWithAssignedLut(isEnabled: false);
		var injection = new FakeInjectionService
		{
			ApplyResult = InjectionResult.Fail("partial apply")
		};
		var controller = CreateController(state, injection);
		await controller.EnableAsync();
		injection.Status = InjectionStatus.Active;
		injection.ApplyResult = InjectionResult.Ok("recovered");

		await controller.RunWatchdogAsync();

		Assert.Equal(2, injection.ApplyCount);
		Assert.True(controller.LastOperationResult?.Success);
	}

	private static AppState StateWithAssignedLut(bool isEnabled)
	{
		return new AppState
		{
			IsEnabled = isEnabled,
			Monitors =
			{
				new MonitorConfig
				{
					DevicePath = "DISPLAY-A",
					Position = "0,0",
					SdrLutPath = "calibration.cube",
					IsConnected = true
				}
			}
		};
	}

	private static AppController CreateController(AppState state, FakeInjectionService injection)
	{
		return CreateController(new FakeStateStore(state), injection, new FakeAutostartService());
	}

	private static AppController CreateController(
		FakeStateStore store,
		FakeInjectionService injection,
		FakeAutostartService autostart)
	{
		return new AppController(store, injection, new FakeMonitorService(), autostart);
	}

	private sealed class FakeStateStore : IAppStateStore
	{
		public FakeStateStore(AppState state) => State = state;
		public AppState State { get; }
		public int SaveCount { get; private set; }
		public AppState Load() => State;
		public void Save() => SaveCount++;
	}

	private sealed class FakeInjectionService : IInjectionService
	{
		public bool NoDebug { get; set; }
		public InjectionStatus Status { get; set; } = InjectionStatus.Inactive;
		public InjectionResult ApplyResult { get; set; } = InjectionResult.Ok();
		public InjectionResult UninjectResult { get; set; } = InjectionResult.Ok();
		public int ApplyCount { get; private set; }
		public int AppliedMonitorCount { get; private set; }
		public int UninjectCount { get; private set; }

		public InjectionStatus GetStatus() => Status;

		public InjectionResult Apply(IEnumerable<MonitorConfig> monitors, string resolverMode)
		{
			ApplyCount++;
			AppliedMonitorCount = monitors.Count();
			if (ApplyResult.Success) Status = InjectionStatus.Active;
			return ApplyResult;
		}

		public InjectionResult Uninject()
		{
			UninjectCount++;
			if (UninjectResult.Success) Status = InjectionStatus.Inactive;
			return UninjectResult;
		}
	}

	private sealed class FakeMonitorService : IMonitorService
	{
		public IReadOnlyList<MonitorConfig> Reconcile(AppState state)
		{
			return state.Monitors.Where(m => m.IsConnected).ToList();
		}
	}

	private sealed class FakeAutostartService : IAutostartService
	{
		public bool Enabled { get; set; }
		public bool EnableResult { get; set; } = true;
		public bool DisableResult { get; set; } = true;
		public bool IsEnabled() => Enabled;

		public bool Enable()
		{
			if (EnableResult) Enabled = true;
			return EnableResult;
		}

		public bool Disable()
		{
			if (DisableResult) Enabled = false;
			return DisableResult;
		}
	}
}
