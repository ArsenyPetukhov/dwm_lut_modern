using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Loads and atomically saves <see cref="AppState"/> as JSON under
    /// <c>%AppData%\DwmLutModern\state.json</c>. On first run it migrates a legacy
    /// <c>config.xml</c> sitting next to the exe, preserving LUT assignments and resolver mode.
    /// </summary>
    public sealed class AppStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AppState State { get; private set; } = new AppState();

        public AppState Load()
        {
            AppPaths.EnsureRoot();

            if (File.Exists(AppPaths.StateFile))
            {
                try
                {
                    var json = File.ReadAllText(AppPaths.StateFile);
                    var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
                    if (loaded != null)
                    {
                        loaded.ResolverMode = ResolverModeOption.Normalize(loaded.ResolverMode);
                        State = loaded;
                        return State;
                    }
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Corrupt or unreadable state: fall through to a fresh state rather than crash.
                }
            }
            else if (File.Exists(AppPaths.LegacyConfigFile))
            {
                State = MigrateLegacy(AppPaths.LegacyConfigFile);
                Save();
                return State;
            }

            State = new AppState();
            return State;
        }

        public void Save()
        {
            AppPaths.EnsureRoot();
            var json = JsonSerializer.Serialize(State, JsonOptions);
            var tempPath = AppPaths.StateFile + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(AppPaths.StateFile))
            {
                File.Replace(tempPath, AppPaths.StateFile, null);
            }
            else
            {
                File.Move(tempPath, AppPaths.StateFile);
            }
        }

        /// <summary>
        /// Best-effort import of the legacy XML config. LUT assignments and resolver mode carry
        /// over; <see cref="AppState.IsEnabled"/> deliberately stays false so migration never
        /// silently re-applies a LUT — the user turns it on explicitly once.
        /// </summary>
        private static AppState MigrateLegacy(string configPath)
        {
            var state = new AppState { IsEnabled = false };

            try
            {
                var root = XElement.Load(configPath);
                state.ResolverMode = ResolverModeOption.Normalize((string?)root.Attribute("resolver_mode"));

                foreach (var monitor in root.Descendants("monitor"))
                {
                    var devicePath = (string?)monitor.Attribute("path") ?? string.Empty;
                    if (string.IsNullOrEmpty(devicePath)) continue;

                    var config = new MonitorConfig
                    {
                        DevicePath = devicePath,
                        TargetId = (uint?)monitor.Attribute("target_id") ?? 0,
                        SourceId = (uint?)monitor.Attribute("source_id") ?? 0,
                        Name = (string?)monitor.Attribute("name") ?? string.Empty,
                        Connector = (string?)monitor.Attribute("connector") ?? string.Empty,
                        Position = (string?)monitor.Attribute("position") ?? string.Empty,
                        SdrLutPath = NullIfNone((string?)monitor.Attribute("sdr_lut")),
                        HdrLutPath = NullIfNone((string?)monitor.Attribute("hdr_lut"))
                    };
                    state.Monitors.Add(config);

                    AddToLibrary(state, config.SdrLutPath, LutKind.Sdr);
                    AddToLibrary(state, config.HdrLutPath, LutKind.Hdr);

                    foreach (var saved in monitor.Element("sdr_luts")?.Elements("sdr_lut") ?? Enumerable.Empty<XElement>())
                        AddToLibrary(state, NullIfNone((string?)saved), LutKind.Sdr);
                    foreach (var saved in monitor.Element("hdr_luts")?.Elements("hdr_lut") ?? Enumerable.Empty<XElement>())
                        AddToLibrary(state, NullIfNone((string?)saved), LutKind.Hdr);
                }
            }
            catch (Exception)
            {
                // A malformed legacy file should not block a clean first run.
            }

            return state;
        }

        private static void AddToLibrary(AppState state, string? path, LutKind kind)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (state.Library.Any(l => string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            state.Library.Add(LutEntry.FromPath(path, kind));
        }

        private static string? NullIfNone(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return string.Equals(value, "None", StringComparison.OrdinalIgnoreCase) ? null : value;
        }
    }
}
