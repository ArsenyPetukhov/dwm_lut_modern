using System;
using System.Windows;
using System.Windows.Input;
using DwmLutModern.Models;
using DwmLutModern.Services;
using Wpf.Ui.Controls;

namespace DwmLutModern.Views
{
    /// <summary>Records a candidate chord; only Save or Enter accepts it.</summary>
    public partial class HotkeyCaptureWindow : FluentWindow
    {
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        private HotkeyBinding? _candidate;
        private HotkeyBinding? _result;

        public HotkeyCaptureWindow()
        {
            InitializeComponent();
        }

        public static HotkeyBinding? Capture(Window? owner)
        {
            var window = new HotkeyCaptureWindow { Owner = owner };
            return window.ShowDialog() == true ? window._result : null;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            CaptureTarget.Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            if (key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                return;
            }

            // Leave ordinary Tab/Shift+Tab and focused button activation to WPF.
            if (key == Key.Tab && (modifiers & ~ModifierKeys.Shift) == ModifierKeys.None) return;
            if (!CaptureTarget.IsKeyboardFocusWithin) return;

            if (key == Key.Enter && modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                SaveCandidate();
                return;
            }

            e.Handled = true;
            if (e.IsRepeat) return;

            if (IsModifierKey(key))
            {
                _candidate = null;
                SaveButton.IsEnabled = false;
                ValidationText.Visibility = Visibility.Hidden;
                CaptureTarget.Text = DescribeModifiers(modifiers) + "…";
                return;
            }

            _candidate = new HotkeyBinding
            {
                VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key),
                Modifiers = ToNativeModifiers(modifiers)
            };
            CaptureTarget.Text = _candidate.ToString();
            SaveButton.IsEnabled = _candidate.IsValid;
            ValidationText.Visibility = _candidate.IsValid ? Visibility.Hidden : Visibility.Visible;
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            if (!CaptureTarget.IsKeyboardFocusWithin || _candidate != null) return;
            CaptureTarget.Text = Keyboard.Modifiers == ModifierKeys.None
                ? LocalizationService.T("Hotkey.Recording")
                : DescribeModifiers(Keyboard.Modifiers) + "…";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveCandidate();

        private void SaveCandidate()
        {
            if (_candidate is not { IsValid: true }) return;
            _result = new HotkeyBinding { VirtualKey = _candidate.VirtualKey, Modifiers = _candidate.Modifiers };
            DialogResult = true;
        }

        private static uint ToNativeModifiers(ModifierKeys modifiers)
        {
            uint result = 0;
            if ((modifiers & ModifierKeys.Alt) != 0) result |= ModAlt;
            if ((modifiers & ModifierKeys.Control) != 0) result |= ModControl;
            if ((modifiers & ModifierKeys.Shift) != 0) result |= ModShift;
            if ((modifiers & ModifierKeys.Windows) != 0) result |= ModWin;
            return result;
        }

        private static bool IsModifierKey(Key key) =>
            key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

        private static string DescribeModifiers(ModifierKeys modifiers)
        {
            var text = string.Empty;
            if ((modifiers & ModifierKeys.Control) != 0) text += "Ctrl + ";
            if ((modifiers & ModifierKeys.Alt) != 0) text += "Alt + ";
            if ((modifiers & ModifierKeys.Shift) != 0) text += "Shift + ";
            if ((modifiers & ModifierKeys.Windows) != 0) text += "Win + ";
            return text;
        }
    }
}
