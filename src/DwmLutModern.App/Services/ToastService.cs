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
            Show(LocalizationService.T("Toast.Applied"), string.IsNullOrEmpty(detail) ? LocalizationService.T("Toast.AppliedDetail") : LocalizationService.TranslateMessage(detail));
        }

        public void Disabled(string detail)
        {
            Show(LocalizationService.T("Toast.Disabled"), string.IsNullOrEmpty(detail) ? LocalizationService.T("Toast.DisabledDetail") : LocalizationService.TranslateMessage(detail));
        }

        public void Failed(string message)
        {
            Show(LocalizationService.T("Toast.Error"), LocalizationService.TranslateMessage(message));
        }

        public void Info(string title, string message)
        {
            Show(LocalizationService.TranslateMessage(title), LocalizationService.TranslateMessage(message));
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
