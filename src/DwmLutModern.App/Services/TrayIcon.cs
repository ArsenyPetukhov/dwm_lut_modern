using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DwmLutModern.Services;

namespace DwmLutModern.Services
{
    /// <summary>
    /// System-tray presence. The icon tooltip and menu reflect real state at popup time, and the
    /// "Start with Windows" item is a live checkbox — so autostart is toggleable from the tray as
    /// well as Settings, which the legacy app never allowed.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly AppController _controller;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _applyItem;
        private readonly ToolStripMenuItem _disableItem;
        private readonly ToolStripMenuItem _autostartItem;
        private readonly ToolStripMenuItem _disableAndExitItem;
        private readonly ToolStripMenuItem _openItem;
        private readonly ToolStripMenuItem _exitItem;

        public event Action? OpenRequested;
        public event Action? ApplyRequested;
        public event Action? DisableRequested;
        public event Action<bool>? AutostartToggled;
        public event Action? DisableAndExitRequested;
        public event Action? ExitRequested;

        public TrayIcon(AppController controller)
        {
            _controller = controller;

            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Visible = true,
                Text = "DWM LUT Modern"
            };
            _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();

            var menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("Status") { Enabled = false };
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());

            _applyItem = new ToolStripMenuItem("Apply", null, (_, _) => ApplyRequested?.Invoke());
            menu.Items.Add(_applyItem);

            _disableItem = new ToolStripMenuItem("Disable", null, (_, _) => DisableRequested?.Invoke());
            menu.Items.Add(_disableItem);

            menu.Items.Add(new ToolStripSeparator());

            _autostartItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
            _autostartItem.Click += (_, _) => AutostartToggled?.Invoke(!_autostartItem.Checked);
            menu.Items.Add(_autostartItem);

            menu.Items.Add(new ToolStripSeparator());

            _openItem = new ToolStripMenuItem("", null, (_, _) => OpenRequested?.Invoke());
            menu.Items.Add(_openItem);
            _disableAndExitItem = new ToolStripMenuItem("Disable and exit", null, (_, _) => DisableAndExitRequested?.Invoke());
            menu.Items.Add(_disableAndExitItem);
            _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitRequested?.Invoke());
            menu.Items.Add(_exitItem);

            menu.Opening += (_, _) => Update();
            _notifyIcon.ContextMenuStrip = menu;
            LocalizationService.LanguageChanged += Update;
        }

        public void Update()
        {
            var status = _controller.Status;
            var active = status == InjectionStatus.Active;

            _statusItem.Text = LocalizationService.F("Common.Status", status switch
            {
                InjectionStatus.Active => LocalizationService.T("Common.Active"),
                InjectionStatus.Inactive => LocalizationService.T(_controller.State.IsEnabled ? "Home.Applying" : "Home.Disabled"),
                _ => LocalizationService.T("Common.Unavailable")
            });
            _applyItem.Text = LocalizationService.T("Action.Apply");
            _disableItem.Text = LocalizationService.T("Action.Disable");
            _autostartItem.Text = LocalizationService.T("Settings.StartWithWindows");
            _openItem.Text = LocalizationService.T("Tray.Open");
            _disableAndExitItem.Text = LocalizationService.T("Tray.DisableExit");
            _exitItem.Text = LocalizationService.T("Tray.Exit");

            _notifyIcon.Text = BuildTooltip(active);
			_applyItem.Enabled = !_controller.NoDebug && DwmProfileService.Detect().CanApply && _controller.Monitors.Any(m => m.HasEffectiveLut);
			_disableItem.Enabled = !_controller.NoDebug && _controller.CanRequestDisable;
            _disableAndExitItem.Enabled = !_controller.NoDebug;
            _autostartItem.Checked = _controller.State.StartWithWindows;
        }

        /// <summary>
        /// Build the hover tooltip showing what is applied to each display right now, e.g.
        /// "DWM LUT · D1:AA709A · D2:off". Capped to the NotifyIcon 63-char limit.
        /// </summary>
        private string BuildTooltip(bool active)
        {
            if (!active)
            {
                var inactiveText = "DWM LUT · " + LocalizationService.T(_controller.State.IsEnabled ? "Home.Applying" : "Common.Off");
                return inactiveText.Length <= 63 ? inactiveText : inactiveText.Substring(0, 62) + "…";
            }

            var parts = new System.Collections.Generic.List<string>();
            foreach (var m in _controller.Monitors)
            {
				string? lut = m.ColorMode switch
				{
					Models.DisplayColorMode.Hdr => m.LutMode == Models.LutMode.SdrOnly ? null : m.HdrLutPath,
					Models.DisplayColorMode.Sdr => m.LutMode == Models.LutMode.HdrOnly ? null : m.SdrLutPath,
					_ => null
				};
				var name = m.ColorMode == Models.DisplayColorMode.Unknown
					? LocalizationService.T("Monitor.Check")
					: string.IsNullOrEmpty(lut) ? LocalizationService.T("Common.Off") : System.IO.Path.GetFileNameWithoutExtension(lut);
                parts.Add("D" + m.SourceId + ":" + name);
            }

            var summary = parts.Count == 0 ? LocalizationService.T("Common.Active") : string.Join(" · ", parts);
            var text = "DWM LUT · " + summary;
            return text.Length <= 63 ? text : text.Substring(0, 62) + "…";
        }

        public void ShowBalloon(string title, string message)
        {
            try
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(4000);
            }
            catch
            {
                // ignored
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    var icon = Icon.ExtractAssociatedIcon(exe);
                    if (icon != null) return icon;
                }
            }
            catch
            {
                // fall through
            }

            return SystemIcons.Application;
        }

        public void Dispose()
        {
            LocalizationService.LanguageChanged -= Update;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
