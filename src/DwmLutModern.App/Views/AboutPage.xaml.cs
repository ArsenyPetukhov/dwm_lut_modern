using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using DwmLutModern.Services;

namespace DwmLutModern.Views
{
    public partial class AboutPage : UserControl
    {
        private readonly UpdateService _updates = new UpdateService();
        private string _releasesUrl = UpdateService.ReleasesPage;

        public AboutPage()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = LocalizationService.F("Common.Version", version.ToString(3));
            }
            LocalizationService.LanguageChanged += () =>
                VersionText.Text = LocalizationService.F("Common.Version", version?.ToString(3) ?? "5");
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            UpdateStatus.Text = LocalizationService.T("Update.Checking");
            OpenReleasesButton.Visibility = Visibility.Collapsed;
            try
            {
                var result = await _updates.CheckAsync();
                UpdateStatus.Text = result.Message;
                _releasesUrl = result.ReleasesUrl;
                OpenReleasesButton.Visibility = result.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private void OpenReleases_Click(object sender, RoutedEventArgs e) => Open(_releasesUrl);

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Open(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Opening a browser is best-effort.
            }
        }
    }
}
