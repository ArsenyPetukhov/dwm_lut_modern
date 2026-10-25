using System.Windows;
using System.Windows.Controls;
using DwmLutModern.ViewModels;

namespace DwmLutModern.Views
{
    public partial class SettingsPage : UserControl
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsPage(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void SetToggleHotkey_Click(object sender, RoutedEventArgs e)
        {
            var binding = HotkeyCaptureWindow.Capture(Window.GetWindow(this));
            if (binding != null) _viewModel.SetToggleHotkey(binding);
        }

        private void SetCycleHotkey_Click(object sender, RoutedEventArgs e)
        {
            var binding = HotkeyCaptureWindow.Capture(Window.GetWindow(this));
            if (binding != null) _viewModel.SetCycleHotkey(binding);
        }
    }
}
