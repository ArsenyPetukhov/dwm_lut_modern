using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DwmLutModern.Models;
using DwmLutModern.ViewModels;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace DwmLutModern.Views
{
    /// <summary>
    /// Home page code-behind. Handles dropping a LUT — either a file from Explorer or a
    /// <see cref="LutEntry"/> dragged from the LUT library — onto a monitor card to assign it.
    /// </summary>
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void Monitor_Drop(object sender, DragEventArgs e)
        {
            var monitor = ((FrameworkElement)sender).DataContext as MonitorViewModel
                          ?? FindMonitorUnderPointer(e);
            if (monitor == null) return;

            string? path = null;
            var toHdr = false;

            if (e.Data.GetDataPresent(typeof(LutEntry)))
            {
                if (e.Data.GetData(typeof(LutEntry)) is LutEntry entry)
                {
                    path = entry.Path;
                    toHdr = entry.Kind == LutKind.Hdr;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                path = files?.FirstOrDefault(f =>
                    f.EndsWith(".cube", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
                toHdr = path != null && Path.GetFileName(path).IndexOf("hdr", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.IsNullOrEmpty(path)) return;

            if (toHdr) monitor.HdrLutPath = path;
            else monitor.SdrLutPath = path;

            e.Handled = true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

        /// <summary>
        /// Flash a big number on each physical screen (like Windows' display "Identify"), so the
        /// user can tell which on-card "Display N" maps to which physical monitor. Positioned in
        /// physical pixels via MoveWindow so it is correct under per-monitor DPI.
        /// </summary>
        private void Identify_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as HomeViewModel;

            foreach (var screen in WinFormsScreen.AllScreens)
            {
                var number = MatchDisplayNumber(vm, screen);
                var bounds = screen.Bounds;

                var overlay = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
                    ShowInTaskbar = false,
                    Topmost = true,
                    ResizeMode = ResizeMode.NoResize,
                    ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Content = new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x00, 0x78, 0xD4)),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(40, 20, 40, 24),
                        Child = new TextBlock
                        {
                            Text = number.ToString(),
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 120,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                };

                overlay.SourceInitialized += (_, _) =>
                {
                    var hwnd = new WindowInteropHelper(overlay).Handle;
                    MoveWindow(hwnd, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
                };
                overlay.Show();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
                timer.Tick += (_, _) => { timer.Stop(); try { overlay.Close(); } catch { /* ignore */ } };
                timer.Start();
            }
        }

        private static int MatchDisplayNumber(HomeViewModel? vm, WinFormsScreen screen)
        {
            if (vm != null)
            {
                var position = screen.Bounds.X + "," + screen.Bounds.Y;
                var match = vm.Monitors.FirstOrDefault(m => m.Position == position);
                if (match != null) return (int)match.SourceId;
            }

            // Fallback: 1-based index in AllScreens order.
            return Array.IndexOf(WinFormsScreen.AllScreens, screen) + 1;
        }

        private MonitorViewModel? FindMonitorUnderPointer(DragEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var element = source as FrameworkElement;
                while (element != null)
                {
                    if (element.DataContext is MonitorViewModel monitor) return monitor;
                    element = element.Parent as FrameworkElement;
                }
            }

            return null;
        }
    }
}
