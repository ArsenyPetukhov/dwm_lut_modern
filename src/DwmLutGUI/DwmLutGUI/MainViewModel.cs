using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;

namespace DwmLutGUI
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _activeText;
        private MonitorData _selectedMonitor;
        private bool _isActive;
        private Key _toggleKey;
        private bool _autostartAsked;
        private string _resolverMode;
        private DwmSupportStatus _supportStatus;

        private readonly string _configPath;

        private bool _configChanged;
        private XElement _lastConfig;
        private XElement _activeConfig;

        public MainViewModel()
        {
            UpdateActiveStatus();
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            _configPath = AppDomain.CurrentDomain.BaseDirectory + "config.xml";

            _allMonitors = new List<MonitorData>();
            Monitors = new ObservableCollection<MonitorData>();
            ResolverModes = new ObservableCollection<ResolverModeOption>
            {
                new ResolverModeOption("auto", "Auto-detect (recommended)", "Uses a verified DWM profile when known, otherwise tries the safest fallback for this Windows build."),
                new ResolverModeOption("win10-22h2-19045", "Windows 10 22H2 (19045)", "Manual fallback for Windows 10 22H2. Verified Win10 profiles are still pending."),
                new ResolverModeOption("win11-22h2-23h2", "Windows 11 22H2 / 23H2", "Manual fallback for older Windows 11 builds before 24H2."),
                new ResolverModeOption("win11-24h2-26100", "Windows 11 24H2 (26100)", "Manual fallback for the 26100 compositor family."),
                new ResolverModeOption("win11-25h2-26200", "Windows 11 25H2 / 26H2", "Uses known 26200/26300 profiles, then the modern fallback path."),
                new ResolverModeOption("win11-26h1-28000", "Windows 11 26H1 Insider", "Requires a known 28000/28120 profile and fails closed if DWM does not match."),
                new ResolverModeOption("canary-29617", "Canary 29617 (experimental)", "Uses only the compiled Canary profile. Static payload validation passed; live DWM is not validated.")
            };
            _resolverMode = "auto";
            RefreshSupportStatus();
            UpdateMonitors();

            CanApply = !Injector.NoDebug;
            MonitorData.StaticPropertyChanged += MonitorDataOnStaticPropertyChanged;
        }

        private void MonitorDataOnStaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SdrLutPath));
            OnPropertyChanged(nameof(HdrLutPath));
            SaveConfig();
        }

        public string ActiveText
        {
            private set
            {
                if (value == _activeText) return;
                _activeText = value;
                OnPropertyChanged();
            }
            get => _activeText;
        }

        public MonitorData SelectedMonitor
        {
            set
            {
                if (value == _selectedMonitor) return;
                _selectedMonitor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SdrLutPath));
                OnPropertyChanged(nameof(HdrLutPath));
            }
            get => _selectedMonitor;
        }

        private void UpdateConfigChanged()
        {
            _configChanged = _lastConfig != _activeConfig && !XNode.DeepEquals(_lastConfig, _activeConfig);
        }

        private void SaveConfig()
        {
            if (_allMonitors.Count == 0)
                return;

            var xElem = new XElement("monitors",
                new XAttribute("lut_toggle", _toggleKey),
                new XAttribute("autostart_asked", _autostartAsked),
                new XAttribute("resolver_mode", _resolverMode),
                _allMonitors.Select(x =>
                    new XElement("monitor", new XAttribute("path", x.DevicePath),
                        x.TargetId != 0 ? new XAttribute("target_id", x.TargetId) : null,
                        x.SourceId != 0 ? new XAttribute("source_id", x.SourceId) : null,
                        !string.IsNullOrEmpty(x.Name) ? new XAttribute("name", x.Name) : null,
                        !string.IsNullOrEmpty(x.Connector) ? new XAttribute("connector", x.Connector) : null,
                        !string.IsNullOrEmpty(x.Position) ? new XAttribute("position", x.Position) : null,
                        x.SdrLutPath != null ? new XAttribute("sdr_lut", x.SdrLutPath) : null,
                        x.HdrLutPath != null ? new XAttribute("hdr_lut", x.HdrLutPath) : null,
                        x.SdrLuts != null ? new XElement("sdr_luts", x.SdrLuts.Select(s => new XElement("sdr_lut", s))) : null,
                        x.HdrLuts != null ? new XElement("hdr_luts", x.HdrLuts.Select(s => new XElement("hdr_lut", s))) : null)));

            var tempPath = _configPath + ".tmp";
            xElem.Save(tempPath);
            if (File.Exists(_configPath))
            {
                File.Replace(tempPath, _configPath, null);
            }
            else
            {
                File.Move(tempPath, _configPath);
            }

            _lastConfig = xElem;
            UpdateConfigChanged();
            UpdateActiveStatus();
        }

        public string SdrLutPath
        {
            set
            {
                if (SelectedMonitor == null || SelectedMonitor.SdrLutPath == value) return;
                SelectedMonitor.SdrLutPath = value;
                OnPropertyChanged();

                SaveConfig();
            }
            get => SelectedMonitor?.SdrLutPath;
        }

        public string HdrLutPath
        {
            set
            {
                if (SelectedMonitor == null || SelectedMonitor.HdrLutPath == value) return;
                SelectedMonitor.HdrLutPath = value;
                OnPropertyChanged();

                SaveConfig();
            }
            get => SelectedMonitor?.HdrLutPath;
        }

        public Key ToggleKey
        {
            set
            {
                if (value == _toggleKey) return;
                _toggleKey = value;
                OnPropertyChanged();
                SaveConfig();
            }
            get => _toggleKey;
        }

        public bool AutostartAsked
        {
            set
            {
                if (value == _autostartAsked) return;
                _autostartAsked = value;
                OnPropertyChanged();
                SaveConfig();
            }
            get => _autostartAsked;
        }

        public string ResolverMode
        {
            set
            {
                value = NormalizeResolverMode(value);
                if (value == _resolverMode) return;
                _resolverMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedResolverMode));
                OnPropertyChanged(nameof(CompatibilityModeText));
                OnPropertyChanged(nameof(CompatibilityModeDetail));
                OnPropertyChanged(nameof(SupportText));
                SaveConfig();
            }
            get => _resolverMode;
        }

        public ResolverModeOption SelectedResolverMode => ResolverModes.FirstOrDefault(x => x.Value == ResolverMode) ?? ResolverModes.First();

        public string CompatibilityModeText => SelectedResolverMode.Label;

        public string CompatibilityModeDetail => SelectedResolverMode.Detail;

        public string SupportText
        {
            get
            {
                var status = _supportStatus?.Summary ?? "DWM support unknown";
                return ResolverMode == "auto" ? status : status + " / manual: " + CompatibilityModeText;
            }
        }

        public string SupportDetail => _supportStatus?.Detail ?? string.Empty;

        public bool IsActive
        {
            set
            {
                if (value == _isActive) return;
                _isActive = value;
                OnPropertyChanged();
            }
            get => _isActive;
        }

        public bool CanApply { get; }

        private List<MonitorData> _allMonitors { get; }
        public ObservableCollection<MonitorData> Monitors { get; }
        public ObservableCollection<ResolverModeOption> ResolverModes { get; }

        public void RefreshSupportStatus()
        {
            _supportStatus = DwmSupportStatus.Detect();
            OnPropertyChanged(nameof(SupportText));
            OnPropertyChanged(nameof(SupportDetail));
        }

        public void UpdateMonitors()
        {
            var selectedPath = SelectedMonitor?.DevicePath;
            _allMonitors.Clear();
            Monitors.Clear();
            List<XElement> config = null;
            XElement configRoot = null;
            if (File.Exists(_configPath))
            {
                try
                {
                    configRoot = XElement.Load(_configPath);
                    config = configRoot.Descendants("monitor").ToList();
                    _toggleKey = (Key)Enum.Parse(typeof(Key), (string)configRoot.Attribute("lut_toggle"));
                    _autostartAsked = (bool?)configRoot.Attribute("autostart_asked") ?? false;
                    _resolverMode = NormalizeResolverMode((string)configRoot.Attribute("resolver_mode"));
                }
                catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    config = null;
                    _toggleKey = Key.Pause;
                    _autostartAsked = false;
                    _resolverMode = "auto";
                }
            }
            else
            {
                _toggleKey = Key.Pause;
                _autostartAsked = false;
                _resolverMode = "auto";
            }
            OnPropertyChanged(nameof(ToggleKey));
            OnPropertyChanged(nameof(ResolverMode));
            OnPropertyChanged(nameof(SelectedResolverMode));
            OnPropertyChanged(nameof(CompatibilityModeText));
            OnPropertyChanged(nameof(CompatibilityModeDetail));
            OnPropertyChanged(nameof(SupportText));

            var paths = WindowsDisplayAPI.DisplayConfig.PathInfo.GetActivePaths();
            foreach (var path in paths)
            {
                if (path.IsCloneMember) continue;
                var targetInfo = path.TargetsInfo[0];
                var deviceId = targetInfo.DisplayTarget.TargetId;
                var devicePath = targetInfo.DisplayTarget.DevicePath;
                var name = targetInfo.DisplayTarget.FriendlyName;
                if (string.IsNullOrEmpty(name))
                {
                    name = "???";
                }

                var connector = targetInfo.OutputTechnology.ToString();
                if (connector == "DisplayPortExternal")
                {
                    connector = "DisplayPort";
                }

                var position = path.Position.X + "," + path.Position.Y;

                string sdrLutPath = null;
                string hdrLutPath = null;

                var sourceId = path.DisplaySource.SourceId + 1;
                var settings = config?.FirstOrDefault(x => (string)x.Attribute("path") == devicePath) ??
                               config?.FirstOrDefault(x => (uint?)x.Attribute("target_id") == deviceId) ??
                               config?.FirstOrDefault(x => (uint?)x.Attribute("source_id") == sourceId) ??
                               config?.FirstOrDefault(x => (uint?)x.Attribute("id") == deviceId);

                if (settings != null)
                {
                    sdrLutPath = (string)settings.Attribute("sdr_lut");
                    hdrLutPath = (string)settings.Attribute("hdr_lut");
                }
                var sdrLutPaths = settings?.Element("sdr_luts")?.Elements("sdr_lut").Select(x => (string)x).ToList();
                var hdrLutPaths = settings?.Element("hdr_luts")?.Elements("hdr_lut").Select(x => (string)x).ToList();
                var monitor = new MonitorData(devicePath, sourceId, deviceId, name, connector, position,
                    sdrLutPath, hdrLutPath);
                if (sdrLutPaths != null) monitor.SdrLuts = new ObservableCollection<string>(sdrLutPaths);
                if (hdrLutPaths != null) monitor.HdrLuts = new ObservableCollection<string>(hdrLutPaths);
                _allMonitors.Add(monitor);
                Monitors.Add(monitor);
            }

            if (config != null)
            {
                foreach (var monitor in config)
                {
                    var path = (string)monitor.Attribute("path");
                    if (path == null || Monitors.Any(x => x.DevicePath == path)) continue;

                    var sdrLutPath = (string)monitor.Attribute("sdr_lut");
                    var hdrLutPath = (string)monitor.Attribute("hdr_lut");

                    var sdrLutPaths = monitor.Element("sdr_luts")?.Elements("sdr_lut").Select(x => (string)x).ToList();
                    var hdrLutPaths = monitor.Element("hdr_luts")?.Elements("hdr_lut").Select(x => (string)x).ToList();
                    var newMonitorData = new MonitorData(path, sdrLutPath, hdrLutPath);
                    if (sdrLutPaths != null) newMonitorData.SdrLuts = new ObservableCollection<string>(sdrLutPaths);
                    if (hdrLutPaths != null) newMonitorData.HdrLuts = new ObservableCollection<string>(hdrLutPaths);
                    _allMonitors.Add(newMonitorData);
                }
            }

            if (selectedPath == null) return;

            var previous = Monitors.FirstOrDefault(monitor => monitor.DevicePath == selectedPath);
            if (previous != null)
            {
                SelectedMonitor = previous;
            }
        }

        public void ReInject()
        {
            Injector.Uninject();
            if (!Monitors.All(monitor =>
                    string.IsNullOrEmpty(monitor.SdrLutPath) && string.IsNullOrEmpty(monitor.HdrLutPath)))
            {
                Injector.Inject(Monitors, ResolverMode);
            }

            _activeConfig = _lastConfig;
            UpdateConfigChanged();

            UpdateActiveStatus();
        }

        public void Uninject()
        {
            Injector.Uninject();
            UpdateActiveStatus();
        }

        private void UpdateActiveStatus()
        {
            var status = Injector.GetStatus();
            if (status != null)
            {
                IsActive = (bool)status;
                if (status == true)
                {
                    ActiveText = "Active" + (_configChanged ? " (changed)" : "");
                }
                else
                {
                    ActiveText = "Inactive";
                }
            }
            else
            {
                IsActive = false;
                ActiveText = "???";
            }
        }

        public void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdateMonitors();
            if (!_configChanged)
            {
                ReInject();
            }
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateActiveStatus();
            RefreshSupportStatus();
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string NormalizeResolverMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "auto";
            switch (value)
            {
                case "exact-profile-only":
                    return "auto";
                case "legacy-win10-signatures":
                    return "win10-22h2-19045";
                case "legacy-win11-signatures":
                    return "win11-22h2-23h2";
                case "24h2-signatures":
                    return "win11-24h2-26100";
                case "25h2-signatures":
                    return "win11-25h2-26200";
            }

            return ResolverModes != null && ResolverModes.Any(x => x.Value == value) ? value : "auto";
        }
    }
}
