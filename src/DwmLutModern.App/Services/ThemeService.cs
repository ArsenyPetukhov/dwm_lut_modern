using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using DwmLutModern.Models;
using Wpf.Ui.Appearance;

namespace DwmLutModern.Services
{
    public static class ThemeService
    {
        private static ThemeMode _mode = ThemeMode.Light;
        private static bool _watching;

        public static void Apply(ThemeMode mode)
        {
            _mode = mode;
            var light = mode == ThemeMode.Light || (mode == ThemeMode.System && SystemUsesLightTheme());
            ApplicationThemeManager.Apply(light ? ApplicationTheme.Light : ApplicationTheme.Dark, updateAccent: false);
            Set("AppBaseBrush", light ? "#FAFAFB" : "#242426");
            Set("AppSurfaceBrush", light ? "#F5FFFFFF" : "#F5303033");
            Set("AppNavigationBrush", light ? "#DDECECEE" : "#F52B2B2E");
            Set("AppStrokeBrush", light ? "#DADADD" : "#49494E");
            Set("AppTextBrush", light ? "#242426" : "#F2F2F4");
            Set("AppSecondaryTextBrush", light ? "#646469" : "#B8B8BF");
            Set("AppLinkBrush", light ? "#36363B" : "#DEDEE3");
            Set("AppSelectionBrush", light ? "#DEDEE3" : "#46464C");
            Set("AppSelectionTextBrush", light ? "#242426" : "#FFFFFF");
            Set("AppFocusBrush", light ? "#4C4C54" : "#C8C8D0");
            Set("GlassAccentBrush", light ? "#343437" : "#DEDEE3");
            Set("GlassChipBrush", light ? "#66D8D8DC" : "#664E4E54");
            Set("GlassChipStrokeBrush", light ? "#33B0B0B5" : "#665D5D65");
            Set("AccentFillColorDefaultBrush", light ? "#343437" : "#DEDEE3");
            Set("AccentFillColorSecondaryBrush", light ? "#505055" : "#C8C8CF");
            Set("TextOnAccentFillColorPrimaryBrush", light ? "#FFFFFF" : "#242426");
            Set("AccentTextFillColorPrimaryBrush", light ? "#36363B" : "#E4E4E9");
            Set("AccentControlElevationFillColorDefaultBrush", light ? "#E4E4E8" : "#45454B");
            if (SystemParameters.HighContrast)
            {
                Application.Current.Resources["AppBaseBrush"] = SystemColors.WindowBrush;
                Application.Current.Resources["AppSurfaceBrush"] = SystemColors.WindowBrush;
                Application.Current.Resources["AppNavigationBrush"] = SystemColors.WindowBrush;
                Application.Current.Resources["AppTextBrush"] = SystemColors.WindowTextBrush;
                Application.Current.Resources["AppSecondaryTextBrush"] = SystemColors.WindowTextBrush;
                Application.Current.Resources["AppStrokeBrush"] = SystemColors.WindowTextBrush;
                Application.Current.Resources["AppSelectionBrush"] = SystemColors.HighlightBrush;
                Application.Current.Resources["AppSelectionTextBrush"] = SystemColors.HighlightTextBrush;
                Application.Current.Resources["AppFocusBrush"] = SystemColors.HighlightBrush;
                Application.Current.Resources["AppLinkBrush"] = SystemColors.HotTrackBrush;
            }
        }

        private static void Set(string key, string color) =>
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

        private static bool SystemUsesLightTheme()
        {
            try
            {
                return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1) is not int value || value != 0;
            }
            catch { return true; }
        }

        public static void WatchSystem(Window window, ThemeMode mode)
        {
            _mode = mode;
            if (_watching) return;
            _watching = true;
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
            window.Closed += (_, _) =>
            {
                SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
                _watching = false;
            };
        }

        private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (Application.Current == null) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => Apply(_mode)));
        }
    }
}
