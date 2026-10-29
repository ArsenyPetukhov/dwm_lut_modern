using System;
using System.Collections.Generic;
using System.ComponentModel;
using DwmLutModern.Services;

namespace DwmLutModern.Models
{
    public sealed class SdrDitherModeOption : INotifyPropertyChanged
    {
        public const string BlueNoise = "blue-noise";
        public const string ErrorDiffusion = "error-diffusion";

        private SdrDitherModeOption(string value, string labelKey, string detailKey)
        {
            Value = value;
            _labelKey = labelKey;
            _detailKey = detailKey;
            LocalizationService.LanguageChanged += () =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
            };
        }

        private readonly string _labelKey;
        private readonly string _detailKey;
        public string Value { get; }
        public string Label => LocalizationService.T(_labelKey);
        public string Detail => LocalizationService.T(_detailKey);
        public override string ToString() => Label;
        public event PropertyChangedEventHandler? PropertyChanged;

        public static IReadOnlyList<SdrDitherModeOption> All { get; } = new[]
        {
            new SdrDitherModeOption(BlueNoise, "Dither.BlueNoise", "Dither.BlueNoiseDetail"),
            new SdrDitherModeOption(ErrorDiffusion, "Dither.ErrorDiffusion", "Dither.ErrorDiffusionDetail")
        };

        public static string Normalize(string? value) =>
            string.Equals(value, ErrorDiffusion, StringComparison.OrdinalIgnoreCase) ? ErrorDiffusion : BlueNoise;
    }
}
