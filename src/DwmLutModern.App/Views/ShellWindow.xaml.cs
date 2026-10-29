using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
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
        private readonly DocumentationPage _documentationPage;
        private readonly HomeViewModel _homeViewModel;
        private bool _allowClose;

        /// <summary>Raised when the user clicks the window's close button (hide-to-tray).</summary>
        public event Action? CloseToTrayRequested;

        public ShellWindow(AppController controller, HotkeyService hotkeys)
        {
            _controller = controller;
            InitializeComponent();

            _homeViewModel = new HomeViewModel(controller);
            _homePage = new HomePage { DataContext = _homeViewModel };
            _lutPage = new LutLibraryPage { DataContext = new LutLibraryViewModel(controller) };
            _settingsPage = new SettingsPage(new SettingsViewModel(controller, hotkeys));
            _aboutPage = new AboutPage();
            _documentationPage = new DocumentationPage();

            ContentHost.Content = _homePage;

            _controller.OperationCompleted += OnOperationCompleted;
            LocalizationService.LanguageChanged += RefreshVersion;
            Closed += (_, _) => LocalizationService.LanguageChanged -= RefreshVersion;
            RefreshVersion();
        }

        private void RefreshVersion() => SidebarVersion.Text = LocalizationService.F("Common.Version",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "5");

        private void OnOperationCompleted(object? sender, OperationEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Result.Success)
                {
                    ErrorBar.IsOpen = false;
                    return;
                }

                ErrorBar.Title = LocalizationService.TranslateMessage(e.Result.Message);
                ErrorBar.Message = string.IsNullOrEmpty(e.Result.Diagnostics)
                    ? string.Empty
                    : LocalizationService.F("Runtime.DetailsLogged", AppPaths.GuiLogFile);
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
        private void NavDocumentation_Checked(object sender, RoutedEventArgs e) => Swap(_documentationPage);

        public void ShowDocumentation(string topic)
        {
            NavDocumentation.IsChecked = true;
            _documentationPage.OpenTopic(topic);
        }

        private void Swap(object? page)
        {
            if (ContentHost == null || page == null || ReferenceEquals(ContentHost.Content, page)) return;
            ContentHost.Content = page;
            if (SystemParameters.ClientAreaAnimation && InputManager.Current.MostRecentInputDevice is MouseDevice)
            {
                ContentHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.94, 1,
                    TimeSpan.FromMilliseconds(100)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
            else
            {
                ContentHost.BeginAnimation(OpacityProperty, null);
                ContentHost.Opacity = 1;
            }
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
