using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwmLutModern.Models;
using DwmLutModern.Services;
using Microsoft.Win32;

namespace DwmLutModern.ViewModels
{
    /// <summary>
    /// The LUTs page: an imported library plus recents. Each entry can be tagged SDR/HDR/Any and
    /// assigned to a chosen display's SDR or HDR slot (or dragged onto a monitor card on Home).
    /// </summary>
    public sealed partial class LutLibraryViewModel : ObservableObject
    {
        private readonly AppController _controller;
        private readonly Dispatcher _dispatcher;

        public LutLibraryViewModel(AppController controller)
        {
            _controller = controller;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Entries = new ObservableCollection<LutEntry>(
                controller.State.Library.OrderByDescending(l => l.LastUsedTicks));

            Monitors = new ObservableCollection<MonitorConfig>(controller.Monitors);
            SelectedMonitor = Monitors.FirstOrDefault();

            _controller.MonitorsChanged += (_, _) => _dispatcher.BeginInvoke(new Action(RefreshMonitors));
        }

        public ObservableCollection<LutEntry> Entries { get; }
        public ObservableCollection<MonitorConfig> Monitors { get; }
        public LutKind[] Kinds { get; } = { LutKind.Any, LutKind.Sdr, LutKind.Hdr };

        [ObservableProperty]
        private MonitorConfig? _selectedMonitor;

        public bool IsEmpty => Entries.Count == 0;
        public bool HasMonitors => Monitors.Count > 0;

        private void RefreshMonitors()
        {
            var previous = SelectedMonitor?.DevicePath;
            Monitors.Clear();
            foreach (var monitor in _controller.Monitors) Monitors.Add(monitor);
            SelectedMonitor = Monitors.FirstOrDefault(m => m.DevicePath == previous) ?? Monitors.FirstOrDefault();
            OnPropertyChanged(nameof(HasMonitors));
        }

        [RelayCommand]
        private void Import()
        {
            var dialog = new OpenFileDialog
            {
                Filter = LocalizationService.T("Lut.FileFilter"),
                Multiselect = true
            };
            if (dialog.ShowDialog() != true) return;

            foreach (var path in dialog.FileNames)
            {
                var kind = path.IndexOf("hdr", StringComparison.OrdinalIgnoreCase) >= 0 ? LutKind.Hdr : LutKind.Any;
                Add(path, kind);
            }
            Persist();
        }

        [RelayCommand]
        private void Remove(LutEntry? entry)
        {
            if (entry == null) return;
            Entries.Remove(entry);
            _controller.State.Library.RemoveAll(l =>
                string.Equals(l.Path, entry.Path, StringComparison.OrdinalIgnoreCase));
            Persist();
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task AssignSdr(LutEntry? entry)
        {
            if (entry == null || SelectedMonitor == null) return;
            entry.LastUsedTicks = DateTime.UtcNow.Ticks;
            _controller.Save();
            await _controller.AssignLutAsync(SelectedMonitor, entry.Path, hdr: false);
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task AssignHdr(LutEntry? entry)
        {
            if (entry == null || SelectedMonitor == null) return;
            entry.LastUsedTicks = DateTime.UtcNow.Ticks;
            _controller.Save();
            await _controller.AssignLutAsync(SelectedMonitor, entry.Path, hdr: true);
        }

        public void UpdateKind(LutEntry entry, LutKind kind)
        {
            entry.Kind = kind;
            Persist();
        }

        public LutEntry Add(string path, LutKind kind = LutKind.Any)
        {
            var existing = Entries.FirstOrDefault(l =>
                string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var entry = LutEntry.FromPath(path, kind);
            Entries.Insert(0, entry);
            _controller.State.Library.Add(entry);
            return entry;
        }

        private void Persist()
        {
            _controller.Save();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
