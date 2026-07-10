using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Registers global hotkeys via Win32 RegisterHotKey and dispatches WM_HOTKEY. Replaces the
    /// legacy low-level keyboard hook (a system-wide liability) with the lighter, well-behaved
    /// hotkey API. Two actions are supported: toggle the LUT on/off (what users expect) and
    /// cycle saved LUTs for the selected monitor.
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int WmHotkey = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int ToggleId = 0xB001;
        private const int CycleId = 0xB002;

        private IntPtr _hwnd;
        private HwndSource? _source;
        private readonly HashSet<int> _registered = new HashSet<int>();

        public event Action? ToggleRequested;
        public event Action? CycleRequested;

        /// <summary>Attach to a window's message loop. Call once the main window has a handle.</summary>
        public void Attach(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _source = HwndSource.FromHwnd(hwnd);
            _source?.AddHook(WndProc);
        }

        /// <summary>(Re)register both hotkeys from persisted state. Safe to call repeatedly.</summary>
        public void Apply(HotkeyBinding? toggle, HotkeyBinding? cycle)
        {
            if (_hwnd == IntPtr.Zero) return;

            Unregister(ToggleId);
            Unregister(CycleId);

            if (toggle is { IsValid: true })
            {
                if (RegisterHotKey(_hwnd, ToggleId, toggle.Modifiers, toggle.VirtualKey))
                    _registered.Add(ToggleId);
            }

            if (cycle is { IsValid: true })
            {
                if (RegisterHotKey(_hwnd, CycleId, cycle.Modifiers, cycle.VirtualKey))
                    _registered.Add(CycleId);
            }
        }

        private void Unregister(int id)
        {
            if (_registered.Remove(id))
            {
                UnregisterHotKey(_hwnd, id);
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
