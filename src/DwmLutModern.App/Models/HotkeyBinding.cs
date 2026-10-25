using System;
using System.Collections.Generic;
using System.Text;

namespace DwmLutModern.Models
{
    /// <summary>
    /// A persisted global hotkey: a virtual-key plus modifier flags. Serializable and
    /// round-trippable to the Win32 RegisterHotKey representation used by HotkeyService.
    /// </summary>
    public sealed class HotkeyBinding
    {
        /// <summary>Win32 virtual-key code (VK_*).</summary>
        public uint VirtualKey { get; set; }

        /// <summary>Win32 MOD_* modifier flags (Alt=1, Ctrl=2, Shift=4, Win=8).</summary>
        public uint Modifiers { get; set; }

        public bool IsValid => VirtualKey != 0;

        public override string ToString()
        {
            if (!IsValid) return "None";

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
            // Printable ASCII range maps directly.
            if (vk >= 0x30 && vk <= 0x5A) return ((char)vk).ToString();
            switch (vk)
            {
                case 0x13: return "Pause";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x70: return "F1";
                case 0x71: return "F2";
                case 0x72: return "F3";
                case 0x73: return "F4";
                case 0x74: return "F5";
                case 0x75: return "F6";
                case 0x76: return "F7";
                case 0x77: return "F8";
                case 0x78: return "F9";
                case 0x79: return "F10";
                case 0x7A: return "F11";
                case 0x7B: return "F12";
                default: return "VK0x" + vk.ToString("X2");
            }
        }
    }
}
