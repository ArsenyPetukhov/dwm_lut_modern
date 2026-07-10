using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwmLutModern.Models;
using Microsoft.Win32;

namespace DwmLutModern.ViewModels
{
    /// <summary>
    /// One monitor card on the Home page. Wraps a persisted <see cref="MonitorConfig"/> and
    /// raises <see cref="Changed"/> whenever an assignment changes. Keeps a per-display history of
    /// SDR/HDR LUTs (like the legacy app) so you can quick-switch between them and cycle with a
    /// hotkey.
    /// </summary>
    public sealed partial class MonitorViewModel : ObservableObject
    {
        private readonly MonitorConfig _config;

        public MonitorViewModel(MonitorConfig config)
        {
            _config = config;

            SdrChoices = new ObservableCollection<string>(config.SdrLuts);
            HdrChoices = new ObservableCollection<string>(config.HdrLuts);
            EnsureListed(config.SdrLutPath, SdrChoices, config.SdrLuts);
            EnsureListed(config.HdrLutPath, HdrChoices, config.HdrLuts);
        }

        /// <summary>Raised after the SDR or HDR assignment changes (path or clear).</summary>
        public event Action<MonitorViewModel>? Changed;

        public MonitorConfig Config => _config;

        public string Name => string.IsNullOrEmpty(_config.Name) ? "Display" : _config.Name;
        public string Connector => string.IsNullOrEmpty(_config.Connector) ? "—" : _config.Connector;
        public string Position => string.IsNullOrEmpty(_config.Position) ? "" : _config.Position;
        public uint SourceId => _config.SourceId;
        public string Header => "Display " + _config.SourceId + " · " + Name;

        public ObservableCollection<string> SdrChoices { get; }
        public ObservableCollection<string> HdrChoices { get; }

        public bool HasSdr => !string.IsNullOrEmpty(_config.SdrLutPath);
        public bool HasHdr => !string.IsNullOrEmpty(_config.HdrLutPath);

        public bool IsHdrActive => _config.IsHdrActive;
        public string ModeBadge => _config.IsHdrActive ? "HDR" : "SDR";

        public LutMode[] Modes { get; } = { LutMode.Auto, LutMode.SdrOnly, LutMode.HdrOnly };

        /// <summary>Per-display domain policy: auto-switch, SDR-only, or HDR-only.</summary>
        public LutMode Mode
        {
            get => _config.LutMode;
            set
            {
                if (_config.LutMode == value) return;
                _config.LutMode = value;
                OnPropertyChanged(nameof(Mode));
                OnPropertyChanged(nameof(ActiveLutText));
                OnPropertyChanged(nameof(ShowHdrWarning));
                Changed?.Invoke(this);
            }
        }

        /// <summary>What is actually being applied to this display right now, given its live
        /// SDR/HDR mode, the per-display policy, and the assigned LUTs.</summary>
        public string ActiveLutText
        {
            get
            {
                if (_config.IsHdrActive)
                {
                    var hdr = _config.EffectiveHdrLut;
                    return string.IsNullOrEmpty(hdr)
                        ? "HDR active → no LUT applied"
                        : "HDR active → " + Path.GetFileName(hdr);
                }
                var sdr = _config.EffectiveSdrLut;
                return string.IsNullOrEmpty(sdr)
                    ? "SDR mode → no LUT applied"
                    : "SDR mode → " + Path.GetFileName(sdr);
            }
        }

        // Warn only in Auto/HDR-only mode: in SDR-only mode, no HDR LUT is expected.
        public bool ShowHdrWarning =>
            _config.IsHdrActive && _config.LutMode != LutMode.SdrOnly && string.IsNullOrEmpty(_config.HdrLutPath);
        public string HdrWarningText =>
            "This display is in HDR mode. Assign an HDR LUT — the SDR LUT is not used while HDR is on.";

        public string? SdrLutPath
        {
            get => _config.SdrLutPath;
            set
            {
                var normalized = string.IsNullOrEmpty(value) ? null : value;
                if (string.Equals(_config.SdrLutPath, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _config.SdrLutPath = normalized;
                EnsureListed(normalized, SdrChoices, _config.SdrLuts);
                OnPropertyChanged(nameof(SdrLutPath));
                OnPropertyChanged(nameof(HasSdr));
                Changed?.Invoke(this);
            }
        }

        public string? HdrLutPath
        {
            get => _config.HdrLutPath;
            set
            {
                var normalized = string.IsNullOrEmpty(value) ? null : value;
                if (string.Equals(_config.HdrLutPath, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _config.HdrLutPath = normalized;
                EnsureListed(normalized, HdrChoices, _config.HdrLuts);
                OnPropertyChanged(nameof(HdrLutPath));
                OnPropertyChanged(nameof(HasHdr));
                OnPropertyChanged(nameof(ShowHdrWarning));
                Changed?.Invoke(this);
            }
        }

        [RelayCommand]
        private void BrowseSdr()
        {
            var path = Browse(_config.SdrLutPath);
            if (path != null) SdrLutPath = path;
        }

        [RelayCommand]
        private void BrowseHdr()
        {
            var path = Browse(_config.HdrLutPath);
            if (path != null) HdrLutPath = path;
        }

        [RelayCommand]
        private void ClearSdr() => SdrLutPath = null;

        [RelayCommand]
        private void ClearHdr() => HdrLutPath = null;

        /// <summary>Remove the current SDR LUT from this display's history and pick the next saved one.</summary>
        [RelayCommand]
        private void RemoveSdr()
        {
            var current = _config.SdrLutPath;
            if (current != null)
            {
                SdrChoices.Remove(current);
                _config.SdrLuts.Remove(current);
            }
            SdrLutPath = SdrChoices.FirstOrDefault();
        }

        [RelayCommand]
        private void RemoveHdr()
        {
            var current = _config.HdrLutPath;
            if (current != null)
            {
                HdrChoices.Remove(current);
                _config.HdrLuts.Remove(current);
            }
            HdrLutPath = HdrChoices.FirstOrDefault();
        }

        /// <summary>Rotate the SDR assignment through this display's saved LUTs (cycle hotkey).</summary>
        public void CycleSdr()
        {
            if (SdrChoices.Count == 0) return;
            var index = SdrChoices.IndexOf(_config.SdrLutPath ?? string.Empty);
            SdrLutPath = SdrChoices[(index + 1) % SdrChoices.Count];
        }

        private static void EnsureListed(string? path, ObservableCollection<string> choices, System.Collections.Generic.List<string> backing)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!choices.Any(c => string.Equals(c, path, StringComparison.OrdinalIgnoreCase))) choices.Add(path);
            if (!backing.Any(c => string.Equals(c, path, StringComparison.OrdinalIgnoreCase))) backing.Add(path);
        }

        private static string? Browse(string? current)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "LUT files (*.cube;*.txt)|*.cube;*.txt|All files (*.*)|*.*",
                InitialDirectory = SafeDirectory(current)
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static string SafeDirectory(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
            }
            catch
            {
                // ignore and fall through
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
