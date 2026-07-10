using System;
using System.Globalization;
using System.Windows.Data;
using DwmLutModern.Models;
using DwmLutModern.Services;

namespace DwmLutModern.Views
{
    /// <summary>Friendly, localized labels for the per-display <see cref="LutMode"/> selector.</summary>
    public sealed class LutModeLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                LutMode.Auto => LocalizationService.T("Mode.Auto"),
                LutMode.SdrOnly => LocalizationService.T("Mode.SdrOnly"),
                LutMode.HdrOnly => LocalizationService.T("Mode.HdrOnly"),
                _ => value?.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
