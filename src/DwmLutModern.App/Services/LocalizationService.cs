using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows.Data;

namespace DwmLutModern.Services
{
    /// <summary>A selectable UI language.</summary>
    public sealed class LanguageOption
    {
        public LanguageOption(string code, string name) { Code = code; Name = name; }
        public string Code { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    /// <summary>A stable binding source whose indexer refreshes when the language changes.</summary>
    public sealed class LocalizationSource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string this[string key] => LocalizationService.T(key);

        internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }

    /// <summary>Complete embedded catalogs for the app's ten supported languages.</summary>
    public static class LocalizationService
    {
        public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
        {
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский"),
            new LanguageOption("es", "Español"),
            new LanguageOption("fr", "Français"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("pt", "Português"),
            new LanguageOption("it", "Italiano"),
            new LanguageOption("zh", "中文"),
            new LanguageOption("ja", "日本語"),
            new LanguageOption("ko", "한국어"),
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = LoadCatalogs();
        private static readonly Dictionary<string, string> EnglishMessageKeys = BuildEnglishMessageKeys();
        private static string _current = "en";

        public static string Current => _current;
        public static LocalizationSource Source { get; } = new LocalizationSource();
        public static event Action? LanguageChanged;

        public static void SetLanguage(string? code)
        {
            var normalized = NormalizeLanguage(code);
            if (string.Equals(_current, normalized, StringComparison.Ordinal)) return;
            _current = normalized;
            Source.Refresh();
            LanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (Catalogs.TryGetValue(_current, out var catalog) && catalog.TryGetValue(key, out var text))
                return text;
            return key;
        }

        public static string F(string key, params object?[] args) =>
            string.Format(CultureInfo.GetCultureInfo(_current), T(key), args);

        /// <summary>Translate exact legacy messages; preserve unknown system and diagnostic details.</summary>
        public static string TranslateMessage(string EnglishMessage)
        {
            if (string.IsNullOrEmpty(EnglishMessage)) return EnglishMessage;
            if (EnglishMessageKeys.TryGetValue(EnglishMessage, out var key)) return T(key);

            const string disableStart = "Couldn't fully disable the LUT. ";
            const string disableEnd = " Retry Disable or sign out.";
            foreach (var detailKey in new[] { "Runtime.StillLoaded", "Runtime.StateUnverified" })
            {
                if (Catalogs.TryGetValue("en", out var english) && english.TryGetValue(detailKey, out var detail) &&
                    EnglishMessage == disableStart + detail + disableEnd)
                    return F("Runtime.DisableFailed", T(detailKey));
            }
            return EnglishMessage;
        }

        private static string NormalizeLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) code = CultureInfo.CurrentUICulture.Name;
            var language = code.Trim().Replace('_', '-').Split('-')[0].ToLowerInvariant();
            foreach (var option in Languages)
                if (option.Code == language) return language;
            return "en";
        }

        private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
        {
            var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var assembly = typeof(LocalizationService).Assembly;
            foreach (var language in Languages)
            {
                var resourceName = "DwmLutModern.Localization." + language.Code + ".json";
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null)
                    {
                        Debug.WriteLine("Missing localization resource: " + resourceName);
                        continue;
                    }
                    var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                    if (catalog != null) catalogs[language.Code] = catalog;
                }
                catch (Exception ex) when (ex is JsonException || ex is System.IO.IOException || ex is NotSupportedException)
                {
                    // A damaged installation must not prevent the window from opening.
                    Debug.WriteLine("Cannot read localization resource " + resourceName + ": " + ex.Message);
                }
            }
            return catalogs;
        }

        private static Dictionary<string, string> BuildEnglishMessageKeys()
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Catalogs.TryGetValue("en", out var english))
                foreach (var entry in english) keys.TryAdd(entry.Value, entry.Key);

            // Earlier releases emitted these strings directly from view models and services.
            keys["None"] = "Common.NotAssigned";
            keys["Inactive"] = "Common.Off";
            keys["Unknown"] = "Common.Unavailable";
            keys["Disable incomplete"] = "Home.DisableIncomplete";
            keys["LUT not active"] = "Home.NotActive";
            keys["The app window is not ready for hotkey registration."] = "Hotkey.NotReady";
            keys["Auto-detect (recommended)"] = "Compat.Auto";
            keys["Home"] = "Nav.Home";
            keys["Default. Low GPU cost and a stable noise pattern. Recommended for normal desktop and video playback."] = "Dither.BlueNoiseDetail";
            keys["Tiled Sierra Lite for SDR. Uses more GPU time and may show block edges or shimmer during motion and partial redraws. Uses blue noise if unavailable."] = "Dither.ErrorDiffusionDetail";
            keys["Appearance"] = "Settings.Appearance";
            keys["Startup"] = "Settings.Startup";
            keys["Hotkeys"] = "Settings.Hotkeys";
            keys["Advanced"] = "Settings.Advanced";
            keys["DWM support"] = "Settings.DwmSupport";
            keys["Follow Windows, or force light or dark."] = "Settings.ThemeDesc";
            keys["Interface language. Restart the app to fully apply."] = "Settings.LanguageDesc";
            keys["Creates a logon task that restores your last choice. If you disabled the LUT, it stays disabled after restart."] = "Settings.StartWithWindowsDesc";
            keys["Launch hidden in the notification area. Requires 'Start with Windows'."] = "Settings.StartMinimizedDesc";
            keys["A global shortcut to turn the LUT on or off."] = "Settings.ToggleHotkeyDesc";
            keys["Rotate the selected display's SDR LUT through its saved list."] = "Settings.CycleHotkeyDesc";
            keys["Compatibility mode (only change if a Windows update breaks the LUT)"] = "Settings.CompatHeader";
            keys["Cannot apply: failed to enter debug mode. Run DWM LUT Modern as Administrator."] = "Runtime.AdminApply";
            keys["Cannot disable: failed to enter debug mode. Run DWM LUT Modern as Administrator."] = "Runtime.AdminDisable";
            keys["DWM LUT must be run as Administrator. Could not obtain injection privileges."] = "Runtime.AdminPrivileges";
            keys["A LUT file may be malformed, or this token cannot inject into DWM."] = "Runtime.ApplyHint";
            keys["LUT disabled and verified unloaded."] = "Runtime.Disabled";
            keys["Live-validated support: {0}"] = "Dwm.LiveValidated";
            keys["Exact profile available; validation pending: {0}"] = "Dwm.ExactProfile";
            return keys;
        }
    }
}
