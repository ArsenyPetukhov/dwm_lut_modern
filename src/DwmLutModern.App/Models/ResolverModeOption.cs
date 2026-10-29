using System.Collections.Generic;
using System.ComponentModel;
using DwmLutModern.Services;

namespace DwmLutModern.Models
{
    /// <summary>
    /// A user-facing compatibility mode. Everyone should stay on "auto"; the manual options
    /// exist only for troubleshooting a Windows build the auto-detector doesn't yet know, and
    /// live behind the Advanced expander in Settings.
    /// </summary>
    public sealed class ResolverModeOption : INotifyPropertyChanged
    {
        public ResolverModeOption(string value, string label, string detail)
        {
            Value = value;
            _label = label;
            _detail = detail;
            LocalizationService.LanguageChanged += () =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
            };
        }

        public string Value { get; }
        private readonly string _label;
        private readonly string _detail;
        public string Label => Value == "auto" ? LocalizationService.T("Compat.Auto") :
            Value == "canary-29617" ? LocalizationService.T("Compat.Canary") : _label;
        public string Detail => LocalizationService.T(Value switch {
            "auto" => "Compat.AutoDetail", "win10-22h2-19045" => "Compat.Win10Detail",
            "win11-22h2-23h2" => "Compat.LegacyDetail", "win11-24h2-26100" => "Compat.ModernDetail",
            "win11-25h2-26200" => "Compat.LatestDetail", "win11-26h1-28000" => "Compat.InsiderDetail",
            "canary-29617" => "Compat.CanaryDetail", _ => "Compat.AutoDetail"
        });
        public override string ToString() => Label;
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>The canonical list of resolver modes shown in Advanced settings.</summary>
        public static IReadOnlyList<ResolverModeOption> All { get; } = new[]
        {
            new ResolverModeOption("auto", "Auto-detect (recommended)", "Uses a verified DWM profile when known, otherwise the safest fallback for this Windows build."),
            new ResolverModeOption("win10-22h2-19045", "Windows 10 22H2 (19045)", "Manual fallback for Windows 10 22H2. Verified Win10 profiles are still pending."),
            new ResolverModeOption("win11-22h2-23h2", "Windows 11 22H2 / 23H2", "Manual fallback for older Windows 11 builds before 24H2."),
            new ResolverModeOption("win11-24h2-26100", "Windows 11 24H2 (26100)", "Manual fallback for the 26100 compositor family."),
            new ResolverModeOption("win11-25h2-26200", "Windows 11 25H2 / 26H2", "Uses known 26200/26300 profiles, then the modern fallback path."),
            new ResolverModeOption("win11-26h1-28000", "Windows 11 26H1 Insider", "Requires a known 28000/28120 profile and fails closed if DWM does not match."),
            new ResolverModeOption("canary-29617", "Canary 29617 (experimental)", "Uses only the compiled Canary profile. Static payload validation passed; live DWM is not validated."),
        };

        /// <summary>Maps legacy/raw resolver strings onto the canonical set, defaulting to "auto".</summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "auto";
            switch (value)
            {
                case "exact-profile-only": return "auto";
                case "legacy-win10-signatures": return "win10-22h2-19045";
                case "legacy-win11-signatures": return "win11-22h2-23h2";
                case "24h2-signatures": return "win11-24h2-26100";
                case "25h2-signatures": return "win11-25h2-26200";
            }

            foreach (var option in All)
            {
                if (option.Value == value) return value;
            }

            return "auto";
        }
    }
}
