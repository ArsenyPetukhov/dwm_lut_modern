using System.Windows;
using DwmLutModern.Models;
using Wpf.Ui.Appearance;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Applies the app's light/dark/system theme via WPF-UI. In System mode it follows Windows
    /// and keeps following live changes; in Light/Dark it forces that theme.
    /// </summary>
    public static class ThemeService
    {
        public static void Apply(ThemeMode mode)
        {
            switch (mode)
            {
                case ThemeMode.Light:
                    ApplicationThemeManager.Apply(ApplicationTheme.Light, updateAccent: true);
                    break;
                case ThemeMode.Dark:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark, updateAccent: true);
                    break;
                default:
                    ApplicationThemeManager.ApplySystemTheme();
                    break;
            }
        }

        /// <summary>In System mode, follow live Windows theme changes for the given window.</summary>
        public static void WatchSystem(Window window, ThemeMode mode)
        {
            if (mode == ThemeMode.System)
            {
                SystemThemeWatcher.Watch(window);
            }
        }
    }
}
