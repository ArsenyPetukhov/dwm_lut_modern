using System;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Fires Windows toast notifications for key events (applied / disabled / failure),
    /// replacing the legacy app's blocking MessageBox popups. All calls are best-effort: a
    /// notification failure must never take down the app.
    /// </summary>
    public sealed class ToastService
    {
        public void Applied(string detail)
        {
            Show("LUT applied", string.IsNullOrEmpty(detail) ? "Your color profile is now active." : detail);
        }

        public void Disabled(string detail)
        {
            Show("LUT disabled", string.IsNullOrEmpty(detail) ? "Your desktop is back to its default colors." : detail);
        }

        public void Failed(string message)
        {
            Show("DWM LUT error", message);
        }

        public void Info(string title, string message)
        {
            Show(title, message);
        }

        private static void Show(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch (Exception)
            {
                // Toasts are optional feedback; never let them crash the app.
            }
        }

        /// <summary>Remove pending notifications and release the COM activator on shutdown.</summary>
        public void Uninstall()
        {
            try
            {
                ToastNotificationManagerCompat.Uninstall();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
