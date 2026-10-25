using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
	public sealed class HotkeyRegistrationResult
	{
		public bool ToggleRegistered { get; init; }
		public bool CycleRegistered { get; init; }
		public string Error { get; init; } = string.Empty;
		public bool Success => ToggleRegistered && CycleRegistered;
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

    /// <summary>
    /// Registers global hotkeys via Win32 RegisterHotKey and dispatches WM_HOTKEY. Replaces the
    /// legacy low-level keyboard hook (a system-wide liability) with the lighter, well-behaved
    /// hotkey API. Two actions are supported: toggle the LUT on/off (what users expect) and
    /// cycle saved LUTs for the selected monitor.
    /// </summary>
	public sealed class HotkeyService : IDisposable
	{
		private const int WmHotkey = 0x0312;
		private const uint ModNoRepeat = 0x4000;

        private const int ToggleId = 0xB001;
        private const int CycleId = 0xB002;

		private IntPtr _hwnd;
		private HwndSource? _source;
		private readonly IHotkeyRegistrationApi _native;
		private readonly HashSet<int> _registered = new HashSet<int>();

		public event Action? ToggleRequested;
		public event Action? CycleRequested;

		public HotkeyService()
			: this(new NativeHotkeyRegistrationApi())
		{
		}

		public HotkeyService(IHotkeyRegistrationApi native)
		{
			_native = native ?? throw new ArgumentNullException(nameof(native));
		}

        /// <summary>Attach to a window's message loop. Call once the main window has a handle.</summary>
        public void Attach(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _source = HwndSource.FromHwnd(hwnd);
            _source?.AddHook(WndProc);
        }

        /// <summary>(Re)register both hotkeys from persisted state. Safe to call repeatedly.</summary>
		public HotkeyRegistrationResult Apply(HotkeyBinding? toggle, HotkeyBinding? cycle)
		{
			if (_hwnd == IntPtr.Zero)
			{
				var nothingToRegister = toggle is not { IsValid: true } && cycle is not { IsValid: true };
				return new HotkeyRegistrationResult
				{
					ToggleRegistered = nothingToRegister,
					CycleRegistered = nothingToRegister,
					Error = nothingToRegister ? string.Empty : "The app window is not ready for hotkey registration."
				};
			}

			Unregister(ToggleId);
			Unregister(CycleId);

			var errors = new List<string>();
			var toggleRegistered = RegisterBinding(ToggleId, "Toggle LUT", toggle, errors);
			var cycleRegistered = RegisterBinding(CycleId, "Cycle LUT", cycle, errors);
			return new HotkeyRegistrationResult
			{
				ToggleRegistered = toggleRegistered,
				CycleRegistered = cycleRegistered,
				Error = string.Join(Environment.NewLine, errors)
			};
		}

		private bool RegisterBinding(int id, string label, HotkeyBinding? binding, List<string> errors)
		{
			if (binding is not { IsValid: true }) return true;
			if (_native.Register(_hwnd, id, binding.Modifiers | ModNoRepeat, binding.VirtualKey, out var errorCode))
			{
				_registered.Add(id);
				return true;
			}

			errors.Add(label + " hotkey couldn't be registered (Win32 error " + errorCode + "). It may already be in use or reserved by Windows.");
			return false;
		}

        private void Unregister(int id)
        {
            if (_registered.Remove(id))
            {
				_native.Unregister(_hwnd, id);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                var id = wParam.ToInt32();
                if (id == ToggleId) { ToggleRequested?.Invoke(); handled = true; }
                else if (id == CycleId) { CycleRequested?.Invoke(); handled = true; }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister(ToggleId);
            Unregister(CycleId);
            _source?.RemoveHook(WndProc);
        }
    }
}
