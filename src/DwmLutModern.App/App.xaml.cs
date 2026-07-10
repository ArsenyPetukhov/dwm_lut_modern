using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DwmLutModern.Models;
using DwmLutModern.Services;
using DwmLutModern.Views;
using Wpf.Ui.Appearance;

namespace DwmLutModern
{
    /// <summary>
    /// Composition root. Builds the service graph, dispatches headless CLI verbs, and — for an
    /// interactive run — shows the Fluent shell, tray, global hotkeys, and the intent-gated
    /// power/session watcher. Everything that can change the LUT routes through AppController.
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _singleInstance;
        private AppStateStore _store = null!;
        private AppController _controller = null!;
        private ToastService _toast = null!;
        private TrayIcon? _tray;
        private HotkeyService? _hotkeys;
        private PowerSessionWatcher? _power;
        private ShellWindow? _shell;
        private DispatcherTimer? _watchdog;
        private bool _exiting;

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                LogFatal(args.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));

            try
            {
                await StartupCoreAsync(e);
            }
            catch (Exception ex)
            {
                LogFatal(ex);
                Shutdown(1);
            }
        }

        private async Task StartupCoreAsync(StartupEventArgs e)
        {
            var options = CliOptions.Parse(e.Args);

            if (options.Help)
            {
                WriteConsole(CliOptions.HelpText);
                Shutdown(0);
                return;
            }

            // ---- core services -----------------------------------------------------------
            _store = new AppStateStore();
            _store.Load();
            var injection = new InjectionService();
            var monitors = new MonitorService();
            var autostart = new AutostartService();
            _controller = new AppController(_store, injection, monitors, autostart);
            _toast = new ToastService();
            _controller.OperationCompleted += OnOperationCompleted;

            if (options.Status)
            {
                _controller.RefreshMonitors();
                WriteConsole(BuildStatusJson());
                Shutdown(0);
                return;
            }

            var runAndExit = options.Exit;

            // A single interactive instance; headless run-and-exit invocations bypass the gate.
            if (!runAndExit)
            {
                _singleInstance = new Mutex(true, @"Local\DwmLutModern_Singleton", out var createdNew);
                if (!createdNew)
                {
                    // Another instance owns the UI. Let it keep control.
                    Shutdown(0);
                    return;
                }
            }

            // ---- headless verbs ----------------------------------------------------------
            await DispatchHeadlessAsync(options);

            if (runAndExit)
            {
                Shutdown(0);
                return;
            }

            // ---- interactive shell -------------------------------------------------------
            LocalizationService.SetLanguage(_controller.State.Language);
            ThemeService.Apply(_controller.State.Theme);

            _hotkeys = new HotkeyService();
            _shell = new ShellWindow(_controller, _hotkeys);
            _shell.CloseToTrayRequested += () => _shell?.Hide();
            ThemeService.WatchSystem(_shell, _controller.State.Theme);

            var startHidden = options.Minimized || _controller.State.StartMinimized;
            if (!startHidden)
            {
                _shell.Show();
            }
            else
            {
                // Realize the handle so hotkeys can bind without showing the window.
                new System.Windows.Interop.WindowInteropHelper(_shell).EnsureHandle();
            }

            var hwnd = new System.Windows.Interop.WindowInteropHelper(_shell).EnsureHandle();
            _hotkeys.Attach(hwnd);
            _hotkeys.Apply(_controller.State.ToggleHotkey, _controller.State.CycleHotkey);
            _hotkeys.ToggleRequested += () => _ = _controller.ToggleAsync();
            _hotkeys.CycleRequested += () => _shell?.CycleSelectedMonitorLut();

            SetupTray();

            _power = new PowerSessionWatcher();
            _power.ReapplyRequested += () => Dispatcher.BeginInvoke(new Action(async () =>
                await _controller.ReapplyIfEnabledAsync(AppOperation.Reapply, silent: true)));
            _power.Start();

            // Reconcile the scheduled task with the saved preference (e.g. task deleted externally).
            _controller.SyncAutostart();

            // Poll status and self-heal the LUT if DWM restarts and drops it while enabled.
            _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _watchdog.Tick += async (_, _) => await _controller.RunWatchdogAsync();
            _watchdog.Start();

            if (!_controller.State.OnboardingComplete)
            {
                ShowOnboarding();
            }
        }

        private async Task DispatchHeadlessAsync(CliOptions options)
        {
            try
            {
                if (options.ApplyMonitor != null)
                {
                    AssignFromCli(options);
                    await _controller.EnableAsync();
                    return;
                }

                if (options.Startup)
                {
                    await _controller.ReapplyIfEnabledAsync(AppOperation.Startup, silent: true);
                    return;
                }

                if (options.Apply) await _controller.EnableAsync();
                else if (options.Disable) await _controller.DisableAsync();
                else if (options.Toggle) await _controller.ToggleAsync();
            }
            catch (Exception ex)
            {
                _toast.Failed(ex.Message);
            }
        }

        private void AssignFromCli(CliOptions options)
        {
            // Monitors returned here are references into canonical state, so edits persist on Save.
            _controller.RefreshMonitors();
            foreach (var monitor in _controller.Monitors)
            {
                var matches = string.Equals(monitor.DevicePath, options.ApplyMonitor, StringComparison.OrdinalIgnoreCase)
                              || monitor.SourceId.ToString() == options.ApplyMonitor;
                if (!matches) continue;

                if (options.SdrPath != null) monitor.SetSdrLut(options.SdrPath);
                if (options.HdrPath != null) monitor.SetHdrLut(options.HdrPath);
            }

            _controller.Save();
        }

        private void SetupTray()
        {
            _tray = new TrayIcon(_controller);
            _tray.OpenRequested += ShowShell;
            _tray.ApplyRequested += () => _ = _controller.EnableAsync();
            _tray.DisableRequested += () => _ = _controller.DisableAsync();
            _tray.AutostartToggled += enabled => _controller.SetAutostart(enabled);
            _tray.DisableAndExitRequested += async () =>
            {
                await _controller.DisableAsync();
                ExitApp();
            };
            _tray.ExitRequested += ExitApp;
            _controller.StateChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() => _tray?.Update()));
            _tray.Update();
        }

        private void ShowShell()
        {
            if (_shell == null) return;
            _shell.Show();
            _shell.WindowState = WindowState.Normal;
            _shell.Activate();
        }

        private void ShowOnboarding()
        {
            try
            {
                var wizard = new OnboardingWindow(_controller) { Owner = _shell };
                wizard.ShowDialog();
            }
            catch
            {
                // Onboarding is non-essential; never block startup on it.
            }
            finally
            {
                _controller.State.OnboardingComplete = true;
                _controller.Save();
                _shell?.RefreshHome();
            }
        }

        private void OnOperationCompleted(object? sender, OperationEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!e.Result.Success)
                {
                    _toast.Failed(e.Result.Message);
                    return;
                }

                // Force an instant desktop recomposite so the color change is not laggy — but only
                // when something actually changed (skip no-op reapplies from spurious display events).
                if (e.Result.Changed && e.Operation is AppOperation.Apply or AppOperation.Disable or AppOperation.Reapply)
                {
                    ScreenRedraw.Flash();
                }

                if (e.Silent) return;

                switch (e.Operation)
                {
                    case AppOperation.Apply:
                        _toast.Applied(e.Result.Message);
                        break;
                    case AppOperation.Disable:
                        _toast.Disabled(e.Result.Message);
                        break;
                }
            }));
        }

        private void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            _shell?.AllowClose();
            Shutdown(0);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _watchdog?.Stop();
            _power?.Dispose();
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _toast?.Failed("Unexpected error: " + e.Exception.Message);
            }
            catch
            {
                // ignored
            }
            e.Handled = true;
        }

        private string BuildStatusJson()
        {
            var status = _controller.Status;
            var payload = new
            {
                enabled = _controller.State.IsEnabled,
                status = status.ToString().ToLowerInvariant(),
                elevated = InjectionService.CurrentProcessIsElevated(),
                resolverMode = _controller.State.ResolverMode,
                startWithWindows = _controller.State.StartWithWindows,
                monitors = _controller.Monitors.Count,
                dwm = DwmProfileService.Detect().Summary
            };
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        private static void LogFatal(Exception ex)
        {
            try
            {
                AppPaths.EnsureRoot();
                var path = System.IO.Path.Combine(AppPaths.Root, "crash.log");
                System.IO.File.AppendAllText(path,
                    DateTimeOffset.Now.ToString("O") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
                System.Windows.MessageBox.Show(
                    "DWM LUT Modern failed to start:\n\n" + ex.Message +
                    "\n\nDetails were written to:\n" + path,
                    "DWM LUT Modern", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last-resort: nothing more we can do.
            }
        }

        private static void WriteConsole(string text)
        {
            try
            {
                AttachConsole(-1);
                Console.WriteLine();
                Console.WriteLine(text);
                FreeConsole();
            }
            catch
            {
                // No console to attach to; ignore.
            }
        }
    }
}
