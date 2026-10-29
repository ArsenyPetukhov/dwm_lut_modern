using System.ComponentModel;
using DwmLutModern.Services;

namespace DwmLutModern.Models
{
    public sealed class LocalizedOption<T> : INotifyPropertyChanged
    {
        private readonly string _key;
        public LocalizedOption(T value, string key)
        {
            Value = value;
            _key = key;
            LocalizationService.LanguageChanged += () => PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(Label)));
        }
        public T Value { get; }
        public string Label => LocalizationService.T(_key);
        public override string ToString() => Label;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
