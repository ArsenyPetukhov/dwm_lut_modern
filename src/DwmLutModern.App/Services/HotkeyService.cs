using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
    public sealed class HotkeyRegistrationResult
    {
        /// <summary>Whether each requested assignment matches the registration left active.</summary>
        public bool ToggleRegistered { get; init; }
        public bool CycleRegistered { get; init; }

        /// <summary>True only when the requested pair was applied successfully.</summary>
        public bool Success { get; init; }

        /// <summary>
        /// On failure, the registrations that existed before Apply are still active or have
        /// been restored. The caller can restore its saved model without registering again.
        /// </summary>
        public bool PreviousBindingsRestored { get; init; }

        public string Error { get; init; } = string.Empty;
    }

    internal sealed class NativeHotkeyRegistrationApi : IHotkeyRegistrationApi
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public bool Register(IntPtr window, int id, uint modifiers, uint virtualKey, out int errorCode)
        {
            var success = RegisterHotKey(window, id, modifiers, virtualKey);
            errorCode = success ? 0 : Marshal.GetLastWin32Error();
            return success;
        }

        public bool Unregister(IntPtr window, int id) => UnregisterHotKey(window, id);
    }

    /// <summary>Temporarily releases global shortcuts while a dialog records a chord.</summary>
    public sealed class HotkeyCaptureLease : IDisposable
    {
        private HotkeyService? _owner;
        internal Dictionary<int, HotkeyBinding> PreviousBindings { get; }

        /// <summary>Available after Dispose; reports whether the previous pair was restored.</summary>
        public HotkeyRegistrationResult? RestoreResult { get; private set; }

        internal HotkeyCaptureLease(HotkeyService owner, Dictionary<int, HotkeyBinding> previous)
        {
            _owner = owner;
            PreviousBindings = previous;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null) return;
            _owner = null;
            RestoreResult = owner.RestoreAfterCapture(this);
        }
    }

    /// <summary>Registers the two global shortcuts as a pair and dispatches WM_HOTKEY.</summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private const int ToggleId = 0xB001;
        private const int CycleId = 0xB002;

        private IntPtr _hwnd;
        private HwndSource? _source;
        private HotkeyCaptureLease? _captureLease;
        private readonly IHotkeyRegistrationApi _native;
        // Store copies: changing a persisted binding must not change the rollback snapshot.
        private readonly Dictionary<int, HotkeyBinding> _registered = new Dictionary<int, HotkeyBinding>();

        public event Action? ToggleRequested;
        public event Action? CycleRequested;

        public HotkeyService() : this(new NativeHotkeyRegistrationApi()) { }

        public HotkeyService(IHotkeyRegistrationApi native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        /// <summary>Attach once the main window has a handle.</summary>
        public void Attach(IntPtr hwnd)
        {
            if (_hwnd == hwnd) return;
            if (_hwnd != IntPtr.Zero)
                throw new InvalidOperationException("Hotkeys are already attached to a window.");

            _hwnd = hwnd;
            _source = HwndSource.FromHwnd(hwnd);
            _source?.AddHook(WndProc);
        }

        /// <summary>
        /// Apply both assignments together. Invalid or duplicate chords leave the current
        /// registrations untouched; any native failure rolls back to the previous pair.
        /// </summary>
        public HotkeyRegistrationResult Apply(HotkeyBinding? toggle, HotkeyBinding? cycle)
        {
            toggle = Copy(toggle);
            cycle = Copy(cycle);

            if (_captureLease != null)
                return Result(toggle, cycle, false, true, LocalizationService.T("Hotkey.CaptureInProgress"));

            if ((toggle != null && !toggle.IsValid) || (cycle != null && !cycle.IsValid))
                return Result(toggle, cycle, false, true, LocalizationService.T("Hotkey.Invalid"));

            if (toggle != null && SameBinding(toggle, cycle))
                return Result(toggle, cycle, false, true, LocalizationService.T("Hotkey.Duplicate"));

            if (Matches(ToggleId, toggle) && Matches(CycleId, cycle))
                return Result(toggle, cycle, true, false, string.Empty);

            if (_hwnd == IntPtr.Zero)
                return Result(toggle, cycle, false, true, LocalizationService.T("Hotkey.NotReady"));

            var previous = new Dictionary<int, HotkeyBinding>(_registered);
            var errors = new List<string>();
            var applied = Unregister(ToggleId, errors) && Unregister(CycleId, errors)
                && RegisterBinding(ToggleId, toggle, errors) && RegisterBinding(CycleId, cycle, errors);

            if (applied)
                return Result(toggle, cycle, true, false, string.Empty);

            var restored = Restore(previous, errors);
            if (!restored) errors.Add(LocalizationService.T("Hotkey.RestoreFailed"));
            return Result(toggle, cycle, false, restored, string.Join(Environment.NewLine, errors));
        }

        /// <summary>
        /// Release both shortcuts before opening the capture dialog. A failure returns no
        /// lease and restores the previous registrations where possible; do not record then.
        /// </summary>
        public HotkeyCaptureLease? SuspendForCapture(out string error)
        {
            if (_captureLease != null)
            {
                error = LocalizationService.T("Hotkey.CaptureInProgress");
                return null;
            }
            if (_hwnd == IntPtr.Zero)
            {
                error = LocalizationService.T("Hotkey.NotReady");
                return null;
            }

            var previous = new Dictionary<int, HotkeyBinding>(_registered);
            var errors = new List<string>();
            if (!Unregister(ToggleId, errors) || !Unregister(CycleId, errors))
            {
                if (!Restore(previous, errors)) errors.Add(LocalizationService.T("Hotkey.RestoreFailed"));
                error = string.Join(Environment.NewLine, errors);
                return null;
            }

            error = string.Empty;
            _captureLease = new HotkeyCaptureLease(this, previous);
            return _captureLease;
        }

        internal HotkeyRegistrationResult RestoreAfterCapture(HotkeyCaptureLease lease)
        {
            lease.PreviousBindings.TryGetValue(ToggleId, out var toggle);
            lease.PreviousBindings.TryGetValue(CycleId, out var cycle);
            if (!ReferenceEquals(_captureLease, lease))
                return Result(toggle, cycle, false, false, LocalizationService.T("Hotkey.NotReady"));

            _captureLease = null;
            var errors = new List<string>();
            var restored = _hwnd != IntPtr.Zero && Restore(lease.PreviousBindings, errors);
            if (!restored) errors.Add(LocalizationService.T("Hotkey.RestoreFailed"));
            return Result(toggle, cycle, restored, restored, string.Join(Environment.NewLine, errors));
        }

        private bool RegisterBinding(int id, HotkeyBinding? binding, List<string> errors)
        {
            if (binding == null) return true;
            if (_native.Register(_hwnd, id, binding.Modifiers | ModNoRepeat, binding.VirtualKey, out var errorCode))
            {
                _registered[id] = binding;
                return true;
            }

            errors.Add(LocalizationService.F("Hotkey.RegisterFailed", binding.ToString(), errorCode));
            return false;
        }

        private bool Unregister(int id, List<string> errors)
        {
            if (!_registered.TryGetValue(id, out var binding)) return true;
            if (_native.Unregister(_hwnd, id))
            {
                _registered.Remove(id);
                return true;
            }

            // Do not claim a registration disappeared when Windows rejected its removal.
            errors.Add(LocalizationService.F("Hotkey.UnregisterFailed", binding.ToString()));
            return false;
        }

        private bool Restore(Dictionary<int, HotkeyBinding> previous, List<string> errors)
        {
            foreach (var id in new List<int>(_registered.Keys))
            {
                if (previous.TryGetValue(id, out var binding) && Matches(id, binding)) continue;
                Unregister(id, errors);
            }

            foreach (var entry in previous)
            {
                if (Matches(entry.Key, entry.Value)) continue;
                // A failed removal may leave an attempted binding using the same ID.
                if (!_registered.ContainsKey(entry.Key)) RegisterBinding(entry.Key, entry.Value, errors);
            }

            if (_registered.Count != previous.Count) return false;
            foreach (var entry in previous)
                if (!Matches(entry.Key, entry.Value)) return false;
            return true;
        }

        private HotkeyRegistrationResult Result(HotkeyBinding? toggle, HotkeyBinding? cycle,
            bool success, bool restored, string error) => new HotkeyRegistrationResult
        {
            ToggleRegistered = Matches(ToggleId, toggle),
            CycleRegistered = Matches(CycleId, cycle),
            Success = success,
            PreviousBindingsRestored = restored,
            Error = error
        };

        private bool Matches(int id, HotkeyBinding? binding) =>
            _registered.TryGetValue(id, out var current) ? SameBinding(current, binding) : binding == null;

        private static bool SameBinding(HotkeyBinding first, HotkeyBinding? second) =>
            second != null && first.VirtualKey == second.VirtualKey && first.Modifiers == second.Modifiers;

        private static HotkeyBinding? Copy(HotkeyBinding? binding) => binding == null ? null
            : new HotkeyBinding { VirtualKey = binding.VirtualKey, Modifiers = binding.Modifiers };

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                var id = wParam.ToInt32();
                if (_captureLease != null || !_registered.TryGetValue(id, out var binding)) return IntPtr.Zero;
                // A message queued before an edit can retain an ID that now has a new chord.
                var keyData = (ulong)lParam.ToInt64();
                if (((keyData >> 16) & 0xFFFF) != binding.VirtualKey
                    || (keyData & 0x000F) != binding.Modifiers) return IntPtr.Zero;

                if (id == ToggleId) { ToggleRequested?.Invoke(); handled = true; }
                else if (id == CycleId) { CycleRequested?.Invoke(); handled = true; }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            foreach (var id in new List<int>(_registered.Keys)) _native.Unregister(_hwnd, id);
            _registered.Clear();
            _captureLease = null;
            _source?.RemoveHook(WndProc);
            _source = null;
            _hwnd = IntPtr.Zero;
        }
    }
}
