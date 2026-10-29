using System;
using System.Windows.Markup;
using System.Windows.Data;
using DwmLutModern.Services;

namespace DwmLutModern.Views
{
    /// <summary>
    /// XAML markup extension for localized strings: <c>Text="{views:Loc Nav.Home}"</c>.
    /// Resolves against the language chosen at startup; changing language takes effect on restart.
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }
        public LocExtension(string key) { Key = key; }

        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new Binding("[" + Key + "]")
            {
                Source = LocalizationService.Source,
                Mode = BindingMode.OneWay
            }.ProvideValue(serviceProvider);
    }
}
