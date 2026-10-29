using System;
using System.Collections.Generic;
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
	public sealed class AppStateStore : IAppStateStore
	{
		private readonly string _stateFile;
		private readonly string _legacyConfigFile;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

		public AppState State { get; private set; } = new AppState();

		public AppStateStore()
			: this(AppPaths.StateFile, AppPaths.LegacyConfigFile)
		{
		}

		public AppStateStore(string stateFile, string legacyConfigFile)
		{
			_stateFile = stateFile ?? throw new ArgumentNullException(nameof(stateFile));
			_legacyConfigFile = legacyConfigFile ?? throw new ArgumentNullException(nameof(legacyConfigFile));
		}

        public AppState Load()
        {
			EnsureStateDirectory();

			if (File.Exists(_stateFile))
            {
                try
                {
					var json = File.ReadAllText(_stateFile);
                    var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
                    if (loaded != null)
                    {
						State = NormalizeLoadedState(loaded, out var repaired);
						if (repaired) PreserveInvalidState();
                        return State;
                    }
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
                {
					PreserveInvalidState();
					// Corrupt or unreadable state: fall through to a fresh disabled state rather than crash.
                }
            }
			else if (File.Exists(_legacyConfigFile))
			{
				State = MigrateLegacy(_legacyConfigFile);
                Save();
                return State;
            }

            State = new AppState();
            return State;
        }

		private static AppState NormalizeLoadedState(AppState loaded, out bool repaired)
		{
			repaired = false;
			if (loaded.Version > AppState.CurrentVersion)
			{
				repaired = true;
				return new AppState();
			}

			if (loaded.Version <= 0)
			{
				loaded.Version = AppState.CurrentVersion;
				repaired = true;
			}

			if (loaded.Monitors == null)
			{
				loaded.Monitors = new List<MonitorConfig>();
				repaired = true;
			}
			if (loaded.Library == null)
			{
				loaded.Library = new List<LutEntry>();
				repaired = true;
			}

			if (loaded.Monitors.RemoveAll(m => m == null) > 0) repaired = true;
			if (loaded.Library.RemoveAll(l => l == null) > 0) repaired = true;
			foreach (var monitor in loaded.Monitors)
			{
				if (monitor.SdrLuts == null)
				{
					monitor.SdrLuts = new List<string>();
					repaired = true;
				}
				if (monitor.HdrLuts == null)
				{
					monitor.HdrLuts = new List<string>();
					repaired = true;
				}
			}

			loaded.ResolverMode = ResolverModeOption.Normalize(loaded.ResolverMode);
			loaded.SdrDitherMode = SdrDitherModeOption.Normalize(loaded.SdrDitherMode);
			if (repaired) loaded.IsEnabled = false;
			return loaded;
		}

		private void PreserveInvalidState()
		{
			try
			{
				if (!File.Exists(_stateFile)) return;
				var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
				File.Copy(_stateFile, _stateFile + ".invalid-" + suffix, overwrite: false);
			}
			catch
			{
				// Evidence preservation is best-effort; recovery must still start safely.
			}
		}

        public void Save()
        {
			EnsureStateDirectory();
			var json = JsonSerializer.Serialize(State, JsonOptions);
			var tempPath = _stateFile + ".tmp";
            File.WriteAllText(tempPath, json);

			if (File.Exists(_stateFile))
			{
				File.Replace(tempPath, _stateFile, null);
			}
			else
			{
				File.Move(tempPath, _stateFile);
			}
		}

		private void EnsureStateDirectory()
		{
			var directory = Path.GetDirectoryName(_stateFile);
			if (string.IsNullOrWhiteSpace(directory))
			{
				throw new InvalidOperationException("The state file must have a parent directory.");
			}
			Directory.CreateDirectory(directory);
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
