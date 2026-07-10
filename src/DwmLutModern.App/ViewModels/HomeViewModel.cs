using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwmLutModern.Models;
using DwmLutModern.Services;

namespace DwmLutModern.ViewModels
{
    /// <summary>
    /// The Home page: the big on/off state, the Apply/Disable actions, and one card per attached
    /// monitor. All state changes funnel through <see cref="AppController"/> so intent stays the
    /// single source of truth.
    /// </summary>
    public sealed partial class HomeViewModel : ObservableObject
    {
        private readonly AppController _controller;
        private readonly Dispatcher _dispatcher;

        public HomeViewModel(AppController controller)
        {
            _controller = controller;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _controller.StateChanged += (_, _) => _dispatcher.BeginInvoke(new Action(Refresh));
            _controller.MonitorsChanged += (_, _) => _dispatcher.BeginInvoke(new Action(RebuildMonitors));
            _controller.RefreshMonitors();
            RebuildMonitors();
            Refresh();
            CheckDwmSupport();
        }

        private void CheckDwmSupport()
        {
            var support = DwmProfileService.Detect();
            ShowSupportWarning = !support.IsVerified;
            SupportWarning = support.IsVerified
                ? string.Empty
                : support.Summary + ". If Apply doesn't change your screen, open Settings → Advanced and pick a Compatibility mode.";
        }

        public ObservableCollection<MonitorViewModel> Monitors { get; } = new ObservableCollection<MonitorViewModel>();

        [ObservableProperty]
        private MonitorViewModel? _selectedMonitor;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private string _statusTitle = "LUT disabled";

        [ObservableProperty]
        private string _statusDetail = "Assign a LUT to a display, then click Apply.";

        [ObservableProperty]
        private bool _statusIsActive;

        [ObservableProperty]
        private bool _hasMultipleMonitors;

        [ObservableProperty]
        private bool _anyLutAssigned;

        [ObservableProperty]
        private bool _showSupportWarning;

        [ObservableProperty]
        private string _supportWarning = string.Empty;

        public bool CanApply => !_controller.NoDebug && !IsBusy && AnyLutAssigned;
        public bool CanDisable => !_controller.NoDebug && !IsBusy && StatusIsActive;

        public void RebuildMonitors()
        {
            var previouslySelected = SelectedMonitor?.Config.DevicePath;

            foreach (var existing in Monitors) existing.Changed -= OnMonitorChanged;
            Monitors.Clear();

            // Use the already-reconciled connected monitors (references into canonical state), so
            // an assignment made on a card persists straight through Save.
            foreach (var config in _controller.Monitors)
            {
                var vm = new MonitorViewModel(config);
                vm.Changed += OnMonitorChanged;
                Monitors.Add(vm);
            }

            HasMultipleMonitors = Monitors.Count > 1;
            SelectedMonitor = Monitors.FirstOrDefault(m => m.Config.DevicePath == previouslySelected)
                              ?? Monitors.FirstOrDefault();
            RecomputeAssignments();
        }

        /// <summary>
        /// Rotate the selected monitor's SDR LUT through that display's saved-LUT history
        /// (invoked by the cycle hotkey), matching the legacy app. Falls back to the first monitor.
        /// </summary>
        public void CycleSelectedLut()
        {
            var monitor = SelectedMonitor ?? (Monitors.Count > 0 ? Monitors[0] : null);
            monitor?.CycleSdr();
        }

        private async void OnMonitorChanged(MonitorViewModel monitor)
        {
            _controller.Save();
            RecomputeAssignments();
            await _controller.ReapplyIfActiveAsync();
        }

        private void RecomputeAssignments()
        {
            AnyLutAssigned = Monitors.Any(m => m.HasSdr || m.HasHdr);
            OnPropertyChanged(nameof(CanApply));
        }

        public void Refresh()
        {
            var status = _controller.Status;
            IsEnabled = _controller.State.IsEnabled;
            StatusIsActive = status == InjectionStatus.Active;

            switch (status)
            {
                case InjectionStatus.Active:
                    StatusTitle = LocalizationService.T("Home.Active");
                    StatusDetail = LocalizationService.T("Home.ActiveDetail");
                    break;
                case InjectionStatus.Inactive:
                    StatusTitle = IsEnabled ? LocalizationService.T("Home.Applying") : LocalizationService.T("Home.Disabled");
                    StatusDetail = IsEnabled
                        ? LocalizationService.T("Home.WillApply")
                        : LocalizationService.T("Home.AssignHint");
                    break;
                default:
                    StatusTitle = LocalizationService.T("Home.Unavailable");
                    StatusDetail = LocalizationService.T("Home.RunAsAdmin");
                    break;
            }

            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanDisable));
        }

        [RelayCommand]
        private async Task ApplyAsync()
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanDisable));
            try
            {
                await _controller.EnableAsync();
            }
            finally
            {
                IsBusy = false;
                Refresh();
            }
        }

        [RelayCommand]
        private async Task DisableAsync()
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanDisable));
            try
            {
                await _controller.DisableAsync();
            }
            finally
            {
                IsBusy = false;
                Refresh();
            }
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanDisable));
        }

        partial void OnStatusIsActiveChanged(bool value) => OnPropertyChanged(nameof(CanDisable));
        partial void OnAnyLutAssignedChanged(bool value) => OnPropertyChanged(nameof(CanApply));
    }
}
