using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwmLutModern.Models;
using DwmLutModern.Services;

namespace DwmLutModern.ViewModels
{
    /// <summary>
    /// Settings: the live "start with Windows" toggle, start-minimized, the (advanced)
    /// compatibility resolver, DWM support diagnostics, and hotkey bindings.
    /// </summary>
    public sealed partial class SettingsViewModel : ObservableObject
    {
        private readonly AppController _controller;
        private readonly HotkeyService _hotkeys;
        private bool _loading;

        public SettingsViewModel(AppController controller, HotkeyService hotkeys)
        {
            _controller = controller;
            _hotkeys = hotkeys;
            ResolverModes = new List<ResolverModeOption>(ResolverModeOption.All);

            _loading = true;
            _startWithWindows = controller.State.StartWithWindows;
            _startMinimized = controller.State.StartMinimized;
            _selectedResolverMode = FindResolver(controller.State.ResolverMode);
            _selectedDitherMode = FindDitherMode(controller.State.SdrDitherMode);
            _selectedTheme = controller.State.Theme;
            _selectedLanguage = FindLanguage(controller.State.Language);
            _loading = false;

            RefreshDwmSupport();
            RefreshHotkeyText();
            LocalizationService.LanguageChanged += RefreshLanguage;
        }

        public IReadOnlyList<ResolverModeOption> ResolverModes { get; }

        public IReadOnlyList<SdrDitherModeOption> DitherModes { get; } = SdrDitherModeOption.All;

        [ObservableProperty]
        private SdrDitherModeOption _selectedDitherMode;

        public string DitherDetail => SelectedDitherMode?.Detail ?? string.Empty;

        partial void OnSelectedDitherModeChanged(SdrDitherModeOption value)
        {
            OnPropertyChanged(nameof(DitherDetail));
            if (_loading || value == null) return;
            _controller.State.SdrDitherMode = value.Value;
            _controller.Save();
            _ = ApplyDitherModeAsync();
        }

        private async Task ApplyDitherModeAsync()
        {
            var result = await _controller.ReapplyIfActiveAsync();
            SettingsError = result.Success ? string.Empty : result.Message;
        }

        private static SdrDitherModeOption FindDitherMode(string value)
        {
            var normalized = SdrDitherModeOption.Normalize(value);
            foreach (var option in SdrDitherModeOption.All)
                if (option.Value == normalized) return option;
            return SdrDitherModeOption.All[0];
        }

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private ResolverModeOption _selectedResolverMode;

        public LocalizedOption<ThemeMode>[] ThemeOptions { get; } = {
            new LocalizedOption<ThemeMode>(ThemeMode.System, "Theme.System"),
            new LocalizedOption<ThemeMode>(ThemeMode.Light, "Theme.Light"),
            new LocalizedOption<ThemeMode>(ThemeMode.Dark, "Theme.Dark")
        };

        [ObservableProperty]
        private ThemeMode _selectedTheme;

        public IReadOnlyList<LanguageOption> Languages { get; } = LocalizationService.Languages;

        [ObservableProperty]
        private LanguageOption _selectedLanguage;

        [ObservableProperty]
        private string _dwmSupportSummary = "";

        [ObservableProperty]
        private string _dwmSupportDetail = "";

        [ObservableProperty]
        private bool _dwmIsVerified;

        [ObservableProperty]
        private string _toggleHotkeyText = "None";

		[ObservableProperty]
		private string _cycleHotkeyText = "None";

		[ObservableProperty]
		private string _settingsError = string.Empty;

		public bool HasSettingsError => !string.IsNullOrEmpty(SettingsError);

		partial void OnSettingsErrorChanged(string value) => OnPropertyChanged(nameof(HasSettingsError));

        public string ResolverDetail => SelectedResolverMode?.Detail ?? "";
        public bool HasToggleHotkey => _controller.State.ToggleHotkey is { IsValid: true };
        public bool HasCycleHotkey => _controller.State.CycleHotkey is { IsValid: true };

        public HotkeyCaptureLease? BeginShortcutCapture()
        {
            var lease = _hotkeys.SuspendForCapture(out var error);
            if (lease == null) SettingsError = error;
            return lease;
        }

        public bool CompleteShortcutCapture(HotkeyCaptureLease lease)
        {
            if (lease.RestoreResult is not { Success: false }) return true;
            SettingsError = lease.RestoreResult.Error;
            return false;
        }

        private void RefreshLanguage()
        {
            OnPropertyChanged(nameof(DitherDetail));
            OnPropertyChanged(nameof(ResolverDetail));
            RefreshDwmSupport();
            RefreshHotkeyText();
            SettingsError = string.Empty;
        }

		partial void OnStartWithWindowsChanged(bool value)
		{
			if (_loading) return;
			if (_controller.SetAutostart(value))
			{
				SettingsError = string.Empty;
				if (!value) StartMinimized = false;
				return;
			}

			SettingsError = LocalizationService.T("Runtime.StartupChangeFailed");
			_loading = true;
			StartWithWindows = _controller.State.StartWithWindows;
			StartMinimized = _controller.State.StartMinimized;
			_loading = false;
		}

        partial void OnStartMinimizedChanged(bool value)
        {
            if (_loading) return;
            _controller.State.StartMinimized = value;
            _controller.Save();
        }

        partial void OnSelectedThemeChanged(ThemeMode value)
        {
            if (_loading) return;
            _controller.State.Theme = value;
            _controller.Save();
            ThemeService.Apply(value);
        }

        partial void OnSelectedLanguageChanged(LanguageOption value)
        {
            if (_loading || value == null) return;
            _controller.State.Language = value.Code;
            _controller.Save();
            LocalizationService.SetLanguage(value.Code);
        }

        private LanguageOption FindLanguage(string code)
        {
            var normalized = string.IsNullOrWhiteSpace(code) ? LocalizationService.Current : code;
            foreach (var lang in Languages)
            {
                if (string.Equals(lang.Code, normalized, StringComparison.OrdinalIgnoreCase)) return lang;
            }
            return Languages[0];
        }

        partial void OnSelectedResolverModeChanged(ResolverModeOption value)
        {
            OnPropertyChanged(nameof(ResolverDetail));
            if (_loading || value == null) return;
            _controller.State.ResolverMode = value.Value;
            _controller.Save();
            _ = _controller.ReapplyIfActiveAsync();
        }

		public void SetToggleHotkey(HotkeyBinding? binding)
		{
			SetShortcutPair(binding, _controller.State.CycleHotkey);
		}

		public void SetCycleHotkey(HotkeyBinding? binding)
		{
			SetShortcutPair(_controller.State.ToggleHotkey, binding);
		}

        private void SetShortcutPair(HotkeyBinding? toggle, HotkeyBinding? cycle)
        {
            var previousToggle = _controller.State.ToggleHotkey;
            var previousCycle = _controller.State.CycleHotkey;
            var result = _hotkeys.Apply(toggle, cycle);
            if (!result.Success)
            {
                SettingsError = result.Error;
                if (!result.PreviousBindingsRestored)
                {
                    var restore = _hotkeys.Apply(previousToggle, previousCycle);
                    if (!restore.Success) SettingsError += Environment.NewLine + restore.Error;
                }
                RefreshHotkeyText();
                return;
            }
            _controller.State.ToggleHotkey = toggle;
            _controller.State.CycleHotkey = cycle;
            try
            {
                _controller.Save();
                SettingsError = string.Empty;
            }
            catch (Exception)
            {
                _controller.State.ToggleHotkey = previousToggle;
                _controller.State.CycleHotkey = previousCycle;
                var restore = _hotkeys.Apply(previousToggle, previousCycle);
                SettingsError = LocalizationService.T("Runtime.SettingsSaveFailed");
                if (!restore.Success) SettingsError += Environment.NewLine + restore.Error;
            }
            RefreshHotkeyText();
        }

        [RelayCommand]
        private void ClearToggleHotkey() => SetToggleHotkey(null);

        [RelayCommand]
        private void ClearCycleHotkey() => SetCycleHotkey(null);

        [RelayCommand]
        private void RefreshDwmSupport()
        {
            var support = DwmProfileService.Detect();
            DwmSupportSummary = LocalizationService.TranslateMessage(support.Summary);
            DwmSupportDetail = support.WindowsBuild + " · " + support.Machine + " · " + support.Detail;
            DwmIsVerified = support.IsVerified;
        }

        private void RefreshHotkeyText()
        {
            ToggleHotkeyText = HasToggleHotkey ? _controller.State.ToggleHotkey!.ToString() : LocalizationService.T("Common.NotAssigned");
            CycleHotkeyText = HasCycleHotkey ? _controller.State.CycleHotkey!.ToString() : LocalizationService.T("Common.NotAssigned");
            OnPropertyChanged(nameof(HasToggleHotkey));
            OnPropertyChanged(nameof(HasCycleHotkey));
        }

        private ResolverModeOption FindResolver(string value)
        {
            foreach (var option in ResolverModes)
            {
                if (option.Value == value) return option;
            }
            return ResolverModes[0];
        }
    }
}
