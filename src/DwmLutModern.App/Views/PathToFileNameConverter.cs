using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace DwmLutModern.Views
{
    /// <summary>Shows just the file name of a LUT path in list/combo items (falls back to "None").</summary>
    public sealed class PathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            return string.IsNullOrEmpty(path) ? "None" : Path.GetFileName(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
