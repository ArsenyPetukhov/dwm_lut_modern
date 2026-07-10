using System.Windows;
using System.Windows.Input;
using DwmLutModern.Models;
using Wpf.Ui.Controls;

namespace DwmLutModern.Views
{
    /// <summary>
    /// Modal that captures a single key chord and returns it as a <see cref="HotkeyBinding"/>.
    /// Modifier-only presses are ignored until a real key is added.
    /// </summary>
    public partial class HotkeyCaptureWindow : FluentWindow
    {
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                DialogResult = false;
                return;
            }

            if (IsModifierKey(key))
            {
                PreviewText.Text = DescribeModifiers(Keyboard.Modifiers) + "…";
                return;
            }

            uint modifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= ModAlt;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= ModControl;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= ModShift;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= ModWin;

            var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            _result = new HotkeyBinding { VirtualKey = vk, Modifiers = modifiers };
            PreviewText.Text = _result.ToString();
            DialogResult = true;
        }

        private static bool IsModifierKey(Key key) =>
            key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

        private static string DescribeModifiers(ModifierKeys modifiers)
        {
            var text = "";
            if ((modifiers & ModifierKeys.Control) != 0) text += "Ctrl + ";
            if ((modifiers & ModifierKeys.Alt) != 0) text += "Alt + ";
            if ((modifiers & ModifierKeys.Shift) != 0) text += "Shift + ";
            if ((modifiers & ModifierKeys.Windows) != 0) text += "Win + ";
            return text;
        }
    }
}
