using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DwmLutModern.Services;

namespace DwmLutModern.Models
{
    /// <summary>A persisted virtual-key and Win32 MOD_* modifier flags.</summary>
    public sealed class HotkeyBinding
    {
        public uint VirtualKey { get; set; }

        /// <summary>Alt=1, Ctrl=2, Shift=4, Win=8. MOD_NOREPEAT is registration-only.</summary>
        public uint Modifiers { get; set; }

        /// <summary>
        /// A shortcut must include Ctrl, Alt or Win plus a non-modifier key, or an
        /// unmodified F1-F24 key. Plain typing and Shift-only chords stay available to apps.
        /// </summary>
        public bool IsValid => IsValidCombination(VirtualKey, Modifiers);

        public static bool IsValidCombination(uint virtualKey, uint modifiers)
        {
            if ((virtualKey < 0x08 && virtualKey != 0x03) || virtualKey > 0xFE
                || (modifiers & ~0x000Fu) != 0) return false;
            if (virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C
                or >= 0xA0 and <= 0xA5) return false;

            return (modifiers & 0x000B) != 0
                || (modifiers == 0 && virtualKey >= 0x70 && virtualKey <= 0x87);
        }

        public override string ToString()
        {
            if (VirtualKey == 0) return LocalizationService.T("Common.NotAssigned");

            var parts = new List<string>();
            if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
            if ((Modifiers & 0x0008) != 0) parts.Add("Win");
            parts.Add(KeyName(VirtualKey));
            return string.Join(" + ", parts);
        }

        private static string KeyName(uint vk)
        {
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);

            if (OperatingSystem.IsWindows())
            {
                // MAPVK_VK_TO_VSC_EX preserves the extended-key flag needed to distinguish
                // navigation keys from their numeric-keypad equivalents.
                var scanCode = MapVirtualKey(vk, 4);
                // Pause has a multi-byte E1 scan sequence; GetKeyNameText expects 0x45.
                if (vk == 0x13) scanCode = 0x45;
                if (scanCode != 0)
                {
                    var keyData = (int)((scanCode & 0xFF) << 16);
                    if ((scanCode & 0xFF00) == 0xE000) keyData |= 1 << 24;
                    var text = new StringBuilder(128);
                    if (GetKeyNameText(keyData, text, text.Capacity) > 0) return text.ToString();
                }
            }

            if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A))
                return ((char)vk).ToString();

            return vk switch
            {
                0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter", 0x13 => "Pause",
                0x14 => "Caps Lock", 0x1B => "Esc", 0x20 => "Space", 0x21 => "Page Up",
                0x22 => "Page Down", 0x23 => "End", 0x24 => "Home", 0x25 => "Left",
                0x26 => "Up", 0x27 => "Right", 0x28 => "Down", 0x2C => "Print Screen",
                0x2D => "Insert", 0x2E => "Delete", 0x90 => "Num Lock", 0x91 => "Scroll Lock",
                _ => "VK0x" + vk.ToString("X2")
            };
        }

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW", ExactSpelling = true)]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll", EntryPoint = "GetKeyNameTextW", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(int keyData, StringBuilder text, int size);
    }
}
