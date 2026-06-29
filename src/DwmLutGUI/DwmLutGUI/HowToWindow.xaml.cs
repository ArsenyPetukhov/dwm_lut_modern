using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DwmLutGUI
{
    public partial class HowToWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public HowToWindow()
        {
            InitializeComponent();
            ApplyDarkMode();
        }

        private void ApplyDarkMode()
        {
            IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
            int useDarkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
