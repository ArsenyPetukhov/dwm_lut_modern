using System;
using System.ComponentModel;
using System.Windows;
using DwmLutModern.Services;
using DwmLutModern.ViewModels;
using Wpf.Ui.Controls;

namespace DwmLutModern.Views
{
    /// <summary>
    /// The Fluent shell: a Mica window with a left nav that swaps between the Home, LUT library,
    /// and Settings pages, plus a shared error info bar. Closing the window hides it to the tray
    /// rather than exiting; the app decides when to really shut down.
    /// </summary>
    public partial class ShellWindow : FluentWindow
    {
        private readonly AppController _controller;
        private readonly HomePage _homePage;
        private readonly LutLibraryPage _lutPage;
        private readonly SettingsPage _settingsPage;
        private readonly AboutPage _aboutPage;
        private readonly HomeViewModel _homeViewModel;
        private bool _allowClose;

        /// <summary>Raised when the user clicks the window's close button (hide-to-tray).</summary>
        public event Action? CloseToTrayRequested;

        public ShellWindow(AppController controller, HotkeyService hotkeys)
        {
            _controller = controller;
            InitializeComponent();
#if DWM_LUT_SMOKE
			WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.None;
			Background = new System.Windows.Media.SolidColorBrush(
				System.Windows.Media.Color.FromRgb(24, 31, 45));
#endif

            _homeViewModel = new HomeViewModel(controller);
            _homePage = new HomePage { DataContext = _homeViewModel };
            _lutPage = new LutLibraryPage { DataContext = new LutLibraryViewModel(controller) };
            _settingsPage = new SettingsPage(new SettingsViewModel(controller, hotkeys));
            _aboutPage = new AboutPage();

            ContentHost.Content = _homePage;

            _controller.OperationCompleted += OnOperationCompleted;
        }

        private void OnOperationCompleted(object? sender, OperationEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Result.Success)
                {
                    ErrorBar.IsOpen = false;
                    return;
                }

                ErrorBar.Title = e.Result.Message;
                ErrorBar.Message = string.IsNullOrEmpty(e.Result.Diagnostics)
                    ? string.Empty
                    : "Details logged to " + AppPaths.GuiLogFile;
                ErrorBar.IsOpen = true;
            }));
        }

        /// <summary>Invoked by the global "cycle" hotkey.</summary>
        public void CycleSelectedMonitorLut() => _homeViewModel.CycleSelectedLut();

        /// <summary>Rebuild the Home monitor cards (e.g. after the onboarding wizard assigns LUTs).</summary>
        public void RefreshHome() => _homeViewModel.RebuildMonitors();

        public void AllowClose() => _allowClose = true;

        private void NavHome_Checked(object sender, RoutedEventArgs e) => Swap(_homePage);
        private void NavLuts_Checked(object sender, RoutedEventArgs e) => Swap(_lutPage);
        private void NavSettings_Checked(object sender, RoutedEventArgs e) => Swap(_settingsPage);
        private void NavAbout_Checked(object sender, RoutedEventArgs e) => Swap(_aboutPage);

        private void Swap(object page)
        {
            if (ContentHost != null) ContentHost.Content = page;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                CloseToTrayRequested?.Invoke();
            }

            base.OnClosing(e);
        }
    }
}
