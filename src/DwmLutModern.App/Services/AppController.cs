using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
    /// <summary>Named operations, so listeners can tailor toasts/info bars.</summary>
    public enum AppOperation
    {
        Apply,
        Disable,
        Reapply,
        Startup
    }

    public sealed class OperationEventArgs : EventArgs
    {
        public AppOperation Operation { get; init; }
        public InjectionResult Result { get; init; } = InjectionResult.Ok();
        public bool Silent { get; init; }
    }

    /// <summary>
    /// The application's domain heart. Every path that can turn the LUT on or off — the UI,
    /// the tray, hotkeys, the CLI, and the debounced power/session watcher — goes through here,
    /// so the persisted intent (<see cref="AppState.IsEnabled"/>) is enforced in exactly one
    /// place. That single choke point is what makes "disabled stays disabled across sleep and
    /// reboot" true no matter how the event arrives.
    /// </summary>
    public sealed class AppController
    {
		private readonly IAppStateStore _store;
		private readonly IInjectionService _injection;
		private readonly IMonitorService _monitors;
		private readonly IAutostartService _autostart;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public AppController(
			IAppStateStore store,
			IInjectionService injection,
			IMonitorService monitors,
			IAutostartService autostart)
        {
            _store = store;
            _injection = injection;
            _monitors = monitors;
            _autostart = autostart;
        }

        private string _monitorSignature = string.Empty;

		// Watchdog / self-heal state: recover desired Enabled state after a cold app start or
		// DWM restart, without looping forever on a build where injection never works.
		private int _healAttempts;
        private int _ticksSinceHeal;
        private const int MaxHealAttempts = 5;
        private const int HealCooldownTicks = 3;

		public AppState State => _store.State;
		public bool NoDebug => _injection.NoDebug;
		public AppOperation? LastOperation { get; private set; }
		public InjectionResult? LastOperationResult { get; private set; }

        /// <summary>Currently-connected monitors — references into <c>State.Monitors</c>.</summary>
        public IReadOnlyList<MonitorConfig> Monitors { get; private set; } = new List<MonitorConfig>();

        /// <summary>Raised after any state change so views can refresh. May fire on a background thread.</summary>
        public event EventHandler? StateChanged;

        /// <summary>Raised when the set of connected monitors changes (so the UI rebuilds its cards).</summary>
        public event EventHandler? MonitorsChanged;

        /// <summary>Raised after an apply/disable/reapply completes, for toasts and info bars.</summary>
        public event EventHandler<OperationEventArgs>? OperationCompleted;

		public InjectionStatus Status => _injection.GetStatus();

		/// <summary>
		/// Disable remains available whenever the user still desires Enabled, the DLL may still be
		/// loaded, or the previous disable could not prove removal.
		/// </summary>
		public bool CanRequestDisable =>
			State.IsEnabled ||
			Status != InjectionStatus.Inactive ||
			(LastOperation == AppOperation.Disable && LastOperationResult is { Success: false });

        /// <summary>
        /// Reconcile attached displays into canonical state (preserving LUT assignments) and
        /// return the connected ones. Raises <see cref="MonitorsChanged"/> only when the connected
        /// set actually changed, so editing an assignment never rebuilds the cards underneath you.
        /// </summary>
        public IReadOnlyList<MonitorConfig> RefreshMonitors()
        {
            Monitors = _monitors.Reconcile(State);

		var signature = string.Join("|", Monitors.Select(m => m.Identity + "@" + m.Position + ":" + m.ColorMode));
            if (signature != _monitorSignature)
            {
                _monitorSignature = signature;
                MonitorsChanged?.Invoke(this, EventArgs.Empty);
            }

            RaiseStateChanged();
            return Monitors;
        }

        public void Save()
        {
            _store.Save();
            RaiseStateChanged();
        }

        /// <summary>
        /// Assign a LUT to a monitor's SDR or HDR slot from anywhere (e.g. the LUT library),
        /// persist it, refresh the monitor cards, and re-apply if the LUT is currently on.
        /// </summary>
        public async Task AssignLutAsync(MonitorConfig monitor, string path, bool hdr)
        {
            if (hdr) monitor.SetHdrLut(path);
            else monitor.SetSdrLut(path);

            _store.Save();
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
            await ReapplyIfActiveAsync();
        }

        /// <summary>Turn the LUT on and remember that intent.</summary>
        public Task<InjectionResult> EnableAsync() => SetEnabledAsync(true, AppOperation.Apply, silent: false);

        /// <summary>Turn the LUT off and remember that intent.</summary>
        public Task<InjectionResult> DisableAsync() => SetEnabledAsync(false, AppOperation.Disable, silent: false);

        public Task<InjectionResult> ToggleAsync() =>
            State.IsEnabled ? DisableAsync() : EnableAsync();

        /// <summary>
        /// Re-apply the LUT only if intent is Enabled; otherwise ensure it is off. Used by the
        /// power/session watcher and by <c>--startup</c>. This is the method that makes a
        /// disabled LUT stay disabled after sleep or logon.
        /// </summary>
        public async Task<InjectionResult> ReapplyIfEnabledAsync(AppOperation operation, bool silent)
        {
            if (!State.IsEnabled)
            {
                // Nothing to do: the user left it off. Never re-inject.
                return InjectionResult.Skipped("Disabled; not applying.");
            }

            return await RunAsync(operation, silent, () =>
            {
                // Skip a redundant re-inject when a display event didn't actually change anything
                // (avoids flicker on spurious DisplaySettingsChanged), but always heal if dropped.
                var before = _monitorSignature;
                RefreshMonitors();
                var topology = TopologyForApply().ToList();
				if (topology.Count == 0)
				{
					return InjectionResult.Fail("No connected display topology could be verified; applying nothing.");
				}
                if (topology.All(m => !m.HasEffectiveLut))
                {
                    return DisableWhenConfigurationIsEmpty();
                }
				if (_monitorSignature == before && _injection.GetStatus() == InjectionStatus.Active &&
					LastOperationResult is not { Success: false })
                {
                    return InjectionResult.Skipped("No display change; LUT still active.");
                }
                return _injection.Apply(topology, State.ResolverMode);
            });
        }

        /// <summary>Re-apply the current LUTs if enabled (e.g. after the user changed an assignment).</summary>
        public async Task<InjectionResult> ReapplyIfActiveAsync()
        {
            if (!State.IsEnabled) return InjectionResult.Skipped();
            return await RunAsync(AppOperation.Reapply, silent: true, () =>
            {
                var topology = TopologyForApply().ToList();
				if (topology.Count == 0)
				{
					return InjectionResult.Fail("No connected display topology could be verified; applying nothing.");
				}
                return topology.All(m => !m.HasEffectiveLut)
                    ? DisableWhenConfigurationIsEmpty()
                    : _injection.Apply(topology, State.ResolverMode);
            });
        }

        private async Task<InjectionResult> SetEnabledAsync(bool enabled, AppOperation operation, bool silent)
        {
            // Reset self-heal bookkeeping on an explicit user action.
            _healAttempts = 0;
            _ticksSinceHeal = 0;

            return await RunAsync(operation, silent, () =>
            {
                if (enabled)
                {
                    RefreshMonitors();
                    var topology = TopologyForApply().ToList();
					if (topology.Count == 0)
					{
						return InjectionResult.Fail("No connected display topology could be verified; applying nothing.");
					}
                    if (topology.All(m => !m.HasEffectiveLut))
                    {
                        // A configuration error is not a recoverable injection failure. Do not
                        // persist Enabled and make the watchdog retry forever with nothing to stage.
                        State.IsEnabled = false;
                        _store.Save();
                        return InjectionResult.Fail("Assign an SDR or HDR LUT before applying.");
                    }

                    State.IsEnabled = true;
                    _store.Save();
                    return _injection.Apply(topology, State.ResolverMode);
                }

                State.IsEnabled = false;
                _store.Save();
                return _injection.Uninject();
            });
        }

        /// <summary>
        /// Called on a periodic timer. Refreshes the live status for the UI/tray, and — if the LUT
        /// was working while enabled but has since dropped (e.g. DWM restarted) — re-applies it a
		/// bounded number of times. It also treats an Active module after a failed/partial Apply as
		/// unhealthy, forcing cleanup/retry rather than accepting a partially injected session.
        /// </summary>
        public async Task RunWatchdogAsync()
        {
            if (NoDebug) return;

            // Keep monitor metadata (HDR on/off, position) live so the per-display indicator and
            // badge update within a couple seconds of toggling HDR — even when the LUT is off.
            // RefreshMonitors only rebuilds the cards when the display set actually changed.
            RefreshMonitors();

            var status = _injection.GetStatus();
			var previousApplyFailed = State.IsEnabled && LastOperationResult is { Success: false } &&
				LastOperation is AppOperation.Apply or AppOperation.Reapply or AppOperation.Startup;

			if (status == InjectionStatus.Active && State.IsEnabled && !previousApplyFailed)
			{
				_healAttempts = 0;
			}

            RaiseStateChanged();

            _ticksSinceHeal++;
			var dropped = State.IsEnabled &&
				(status == InjectionStatus.Inactive || previousApplyFailed);
			var readyToHeal = _healAttempts == 0 || _ticksSinceHeal >= HealCooldownTicks;
			if (dropped && _healAttempts < MaxHealAttempts && readyToHeal)
            {
                _ticksSinceHeal = 0;
                _healAttempts++;
                await ReapplyIfEnabledAsync(AppOperation.Reapply, silent: true);
            }
        }

        private async Task<InjectionResult> RunAsync(AppOperation operation, bool silent, Func<InjectionResult> work)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            InjectionResult result;
            try
            {
                result = await Task.Run(work).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = InjectionResult.Fail(ex.Message, ex.ToString());
            }
            finally
            {
                _gate.Release();
            }

			LastOperation = operation;
			LastOperationResult = result;
			RaiseStateChanged();
			OperationCompleted?.Invoke(this, new OperationEventArgs { Operation = operation, Result = result, Silent = silent });
            return result;
        }

        /// <summary>
        /// Full connected topology. Injection stages LUTs only for configured domains, but the
        /// native side needs every connected display in the manifest to distinguish a true
        /// single-monitor desktop from a multi-monitor desktop with only one assigned LUT.
        /// </summary>
        private IEnumerable<MonitorConfig> TopologyForApply()
        {
			// Never stage stale saved coordinates when live enumeration returned no displays.
			return State.Monitors.Where(m => m.IsConnected).ToList();
        }

        private InjectionResult DisableWhenConfigurationIsEmpty()
        {
            State.IsEnabled = false;
            _store.Save();

            if (_injection.GetStatus() == InjectionStatus.Inactive)
            {
                return InjectionResult.Skipped("No LUTs remain assigned; LUT disabled.");
            }

            var result = _injection.Uninject();
            return result.Success
                ? InjectionResult.Ok("No LUTs remain assigned; LUT disabled.")
                : result;
        }

        // ---- autostart -------------------------------------------------------------------

		public bool SyncAutostart()
		{
			var actuallyOn = _autostart.IsEnabled();
			// Recreate enabled tasks on every owner launch so a moved portable folder repairs the
			// registered executable path. Disabled state only mutates the OS when a task exists.
			var ok = State.StartWithWindows
				? _autostart.Enable() && _autostart.IsEnabled()
				: !actuallyOn || (_autostart.Disable() && !_autostart.IsEnabled());
			RaiseStateChanged();
			return ok;
		}

		public bool SetAutostart(bool enabled)
		{
			// Disabling an already-absent task is success without depending on localized schtasks
			// error text. Enabling always recreates the task so a moved portable path is repaired.
			var changed = enabled
				? _autostart.Enable()
				: !_autostart.IsEnabled() || _autostart.Disable();
			var ok = changed && _autostart.IsEnabled() == enabled;
			if (ok)
			{
				State.StartWithWindows = enabled;
				if (!enabled) State.StartMinimized = false;
				_store.Save();
			}
			RaiseStateChanged();
			return ok;
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
