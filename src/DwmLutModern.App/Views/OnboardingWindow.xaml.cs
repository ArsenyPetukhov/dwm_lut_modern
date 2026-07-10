using System.Linq;
using System.Windows;
using DwmLutModern.Models;
using DwmLutModern.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace DwmLutModern.Views
{
    /// <summary>
    /// Three-step first-run wizard: welcome, choose a LUT for the main display, and offer
    /// autostart. Applying and autostart both honor the same intent model as the rest of the app.
    /// </summary>
    public partial class OnboardingWindow : FluentWindow
    {
        private readonly AppController _controller;
        private int _step = 1;
        private string? _chosenSdr;
        private string? _chosenHdr;

        public OnboardingWindow(AppController controller)
        {
            _controller = controller;
            InitializeComponent();
			MonitorPicker.ItemsSource = _controller.RefreshMonitors();
			MonitorPicker.SelectedIndex = 0;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_step < 3)
            {
                _step++;
                UpdateStep();
            }
            else
            {
                Finish();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_step > 1)
            {
                _step--;
                UpdateStep();
            }
        }

        private void UpdateStep()
        {
            Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = _step > 1 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Content = _step == 3
                ? DwmLutModern.Services.LocalizationService.T("Onb.Finish")
                : DwmLutModern.Services.LocalizationService.T("Onb.Next");
        }

        private void BrowseSdr_Click(object sender, RoutedEventArgs e)
        {
            var path = Browse();
            if (path == null) return;
            _chosenSdr = path;
            ChosenSdr.Text = System.IO.Path.GetFileName(path);
        }

        private void BrowseHdr_Click(object sender, RoutedEventArgs e)
        {
            var path = Browse();
            if (path == null) return;
            _chosenHdr = path;
            ChosenHdr.Text = System.IO.Path.GetFileName(path);
        }

        private static string? Browse()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "LUT files (*.cube;*.txt)|*.cube;*.txt|All files (*.*)|*.*"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private async void Finish()
        {
            NextButton.IsEnabled = false;
            OnboardingError.IsOpen = false;

            try
            {
                var anyLut = !string.IsNullOrEmpty(_chosenSdr) || !string.IsNullOrEmpty(_chosenHdr);
                if (anyLut)
                {
					var selected = MonitorPicker.SelectedItem as MonitorConfig;
					if (selected == null)
                    {
						ShowError("Choose a connected display for these LUTs.");
                        return;
                    }

					// Re-enumerate at commit time and require one stable-identity match. Never silently
					// move onboarding LUTs to whichever adapter-local source happens to sort first.
					var matches = _controller.RefreshMonitors()
						.Where(m => m.Identity.Matches(selected.Identity)).ToList();
					if (matches.Count != 1)
					{
						ShowError("The selected display changed or became ambiguous. Choose it again and retry.");
						MonitorPicker.ItemsSource = _controller.Monitors;
						MonitorPicker.SelectedIndex = -1;
						return;
					}
					var main = matches[0];

                    if (!string.IsNullOrEmpty(_chosenSdr))
                    {
                        main.SetSdrLut(_chosenSdr);
                        _controller.State.Library.Add(LutEntry.FromPath(_chosenSdr, LutKind.Sdr));
                    }
                    if (!string.IsNullOrEmpty(_chosenHdr))
                    {
                        main.SetHdrLut(_chosenHdr);
                        _controller.State.Library.Add(LutEntry.FromPath(_chosenHdr, LutKind.Hdr));
                    }
                    _controller.Save();
                }

                if (!_controller.SetAutostart(AutostartToggle.IsChecked == true))
                {
                    ShowError("Windows startup couldn't be changed. Choose another option or try again.");
                    return;
                }

                if (anyLut)
                {
                    var result = await _controller.EnableAsync();
                    if (!result.Success)
                    {
                        ShowError(result.Message);
                        return;
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                NextButton.IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            OnboardingError.Message = string.IsNullOrWhiteSpace(message)
                ? "Setup couldn't be completed. Review the settings and try again."
                : message;
            OnboardingError.IsOpen = true;
        }
    }
}
