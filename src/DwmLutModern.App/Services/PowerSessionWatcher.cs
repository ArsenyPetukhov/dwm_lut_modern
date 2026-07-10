using System;
using System.Threading;
using Microsoft.Win32;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Watches the events that historically knocked the LUT out or silently re-applied it —
    /// resume-from-sleep, session unlock, and display-settings changes (HDR toggle, resolution
    /// change, monitor power-cycle) — and coalesces them into a single debounced
    /// <see cref="ReapplyRequested"/> signal.
    ///
    /// <para>Crucially, this class does <b>not</b> decide whether to apply. It only says "the
    /// display environment changed, re-evaluate." The controller consults the persisted intent
    /// (<c>AppState.IsEnabled</c>) and re-applies only when the user left the LUT on — so a
    /// disabled LUT never comes back after sleep or a resolution change, and an enabled one is
    /// reliably restored.</para>
    /// </summary>
    public sealed class PowerSessionWatcher : IDisposable
    {
        private readonly Timer _debounce;
        private readonly TimeSpan _delay = TimeSpan.FromMilliseconds(1500);
        private bool _started;

        /// <summary>Raised (debounced, on a thread-pool thread) when the environment changed.</summary>
        public event Action? ReapplyRequested;

        public PowerSessionWatcher()
        {
            _debounce = new Timer(_ => ReapplyRequested?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) Schedule();
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock ||
                e.Reason == SessionSwitchReason.ConsoleConnect ||
                e.Reason == SessionSwitchReason.SessionLogon)
            {
                Schedule();
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Schedule();

        private void Schedule() => _debounce.Change(_delay, Timeout.InfiniteTimeSpan);

        public void Dispose()
        {
            if (_started)
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                _started = false;
            }
            _debounce.Dispose();
        }
    }
}
