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
            var anyLut = !string.IsNullOrEmpty(_chosenSdr) || !string.IsNullOrEmpty(_chosenHdr);
            if (anyLut)
            {
                // Reconciled monitors are references into canonical state, so these edits persist.
                var main = _controller.RefreshMonitors().FirstOrDefault();
                if (main != null)
                {
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
            }

            _controller.SetAutostart(AutostartToggle.IsChecked == true);

            if (anyLut)
            {
                await _controller.EnableAsync();
            }

            DialogResult = true;
            Close();
        }
    }
}
