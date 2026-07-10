using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Forces DWM to recomposite the whole desktop immediately after a LUT is applied or removed,
    /// by flashing a transparent top-most window over the virtual screen for one frame. Without
    /// this the color change trickles in lazily and looks laggy/"chunky"; the legacy app did the
    /// same trick to switch instantly.
    /// </summary>
    public static class ScreenRedraw
    {
        public static void Flash()
        {
            try
            {
                var overlay = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                    ShowInTaskbar = false,
                    Topmost = true,
                    ResizeMode = ResizeMode.NoResize,
                    ShowActivated = false,
                    Left = SystemParameters.VirtualScreenLeft,
                    Top = SystemParameters.VirtualScreenTop,
                    Width = SystemParameters.VirtualScreenWidth,
                    Height = SystemParameters.VirtualScreenHeight
                };

                overlay.Show();

                // Close on the next tick so DWM composites one frame with the overlay present.
                var timer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(40)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try { overlay.Close(); } catch { /* ignore */ }
                };
                timer.Start();
            }
            catch
            {
                // A redraw is a nicety; never let it crash the app.
            }
        }
    }
}
