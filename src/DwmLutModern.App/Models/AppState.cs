using System.Collections.Generic;

namespace DwmLutModern.Models
{
    /// <summary>App appearance: follow Windows, or force light/dark.</summary>
    public enum ThemeMode
    {
        System,
        Light,
        Dark
    }

    /// <summary>
    /// The complete persisted state of the application, serialized to
    /// <c>%AppData%\DwmLutModern\state.json</c>.
    ///
    /// <para><see cref="IsEnabled"/> is the single source of truth the legacy app never had:
    /// it records the user's *intent* (did they leave the LUT on or off?) so that autostart,
    /// wake-from-sleep, and display-change events never re-apply a LUT the user disabled.</para>
    /// </summary>
    public sealed class AppState
    {
		public const int CurrentVersion = 1;

        /// <summary>Schema version, for forward-compatible migrations.</summary>
		public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// The user's desired on/off state. When true, the LUT should be applied and kept
        /// applied across reboots and wake events; when false, it must stay off until the
        /// user explicitly re-enables it. This is the core of the sleep/reboot bug fix.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>Compatibility resolver mode passed to the native DLL. "auto" by default.</summary>
        public string ResolverMode { get; set; } = "auto";

        /// <summary>SDR-only dithering choice. Missing/unknown values keep blue noise.</summary>
        public string SdrDitherMode { get; set; } = SdrDitherModeOption.BlueNoise;

        /// <summary>Create/refresh a logon scheduled task that restores the user's intent at startup.</summary>
        public bool StartWithWindows { get; set; }

        /// <summary>Start hidden in the tray rather than showing the window.</summary>
        public bool StartMinimized { get; set; }

        /// <summary>Window appearance: follow Windows (default), or force Light/Dark.</summary>
        public ThemeMode Theme { get; set; } = ThemeMode.Light;

        /// <summary>UI language culture code (e.g. "en", "ru"). Empty = follow Windows.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Whether the first-run onboarding wizard has been completed.</summary>
        public bool OnboardingComplete { get; set; }

        /// <summary>Global hotkey that toggles the LUT on/off. Null/empty disables it.</summary>
        public HotkeyBinding? ToggleHotkey { get; set; }

        /// <summary>Global hotkey that cycles saved LUTs for the selected monitor. Null/empty disables it.</summary>
        public HotkeyBinding? CycleHotkey { get; set; }

        /// <summary>Per-monitor LUT assignments, keyed by stable display identity.</summary>
        public List<MonitorConfig> Monitors { get; set; } = new List<MonitorConfig>();

        /// <summary>Imported LUT library plus recents, shown on the LUTs page.</summary>
        public List<LutEntry> Library { get; set; } = new List<LutEntry>();
    }
}
