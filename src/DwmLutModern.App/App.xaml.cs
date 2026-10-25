using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
		private SingleInstanceCommandChannel? _commandChannel;
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
#if DWM_LUT_SMOKE
			// PrintWindow cannot capture a hardware-composed WPF/Mica surface reliably. The smoke
			// binary is test-only, so render it in software while leaving production untouched.
			System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
#endif
			var options = CliOptions.Parse(e.Args);

			if (!options.IsValid)
			{
				WriteConsole(options.ErrorMessage + Environment.NewLine + Environment.NewLine + CliOptions.HelpText);
				Shutdown(2);
				return;
			}
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

			// One authoritative process owns state and DWM operations. Every later GUI or CLI launch
			// forwards its original command over a current-user-only named pipe.
			var instanceKey = GetInstanceKey();
			_singleInstance = new Mutex(true, @"Local\DwmLutModern_" + instanceKey, out var createdNew);
			_commandChannel = new SingleInstanceCommandChannel("DwmLutModern." + instanceKey);
			if (!createdNew)
			{
				var forwarded = await _commandChannel.SendAsync(e.Args, TimeSpan.FromSeconds(10));
				WriteConsole(forwarded.Success ? forwarded.Output : forwarded.Error);
				Shutdown(forwarded.ExitCode);
				return;
			}
			_commandChannel.Start(HandleForwardedCommandAsync);

			// ---- headless verbs ----------------------------------------------------------
			var initialResult = await ExecuteCommandAsync(options);
			if (!initialResult.Success) WriteConsole(initialResult.Error);
			else if (!string.IsNullOrWhiteSpace(initialResult.Output)) WriteConsole(initialResult.Output);

			if (options.Exit || options.Status)
			{
				Shutdown(initialResult.ExitCode);
				return;
			}

            // ---- interactive shell -------------------------------------------------------
            LocalizationService.SetLanguage(_controller.State.Language);
            ThemeService.Apply(_controller.State.Theme);

            _hotkeys = new HotkeyService();
            _shell = new ShellWindow(_controller, _hotkeys);
            _shell.CloseToTrayRequested += () => _shell?.Hide();
            ThemeService.WatchSystem(_shell, _controller.State.Theme);

			var startHidden = options.Minimized ||
				(options.Startup && _controller.State.StartWithWindows && _controller.State.StartMinimized);
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
			var hotkeyResult = _hotkeys.Apply(_controller.State.ToggleHotkey, _controller.State.CycleHotkey);
			if (!hotkeyResult.Success) _toast.Failed(hotkeyResult.Error);
            _hotkeys.ToggleRequested += () => _ = _controller.ToggleAsync();
            _hotkeys.CycleRequested += () => _shell?.CycleSelectedMonitorLut();

            SetupTray();

            _power = new PowerSessionWatcher();
            _power.ReapplyRequested += () => Dispatcher.BeginInvoke(new Action(async () =>
                await _controller.ReapplyIfEnabledAsync(AppOperation.Reapply, silent: true)));
            _power.Start();

            // Reconcile the scheduled task with the saved preference (e.g. task deleted externally).
            if (!_controller.SyncAutostart())
            {
                _toast.Failed("Windows startup couldn't be repaired. Review Startup in Settings.");
            }

            // Poll status and self-heal the LUT if DWM restarts and drops it while enabled.
            _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _watchdog.Tick += async (_, _) => await _controller.RunWatchdogAsync();
            _watchdog.Start();

            if (!_controller.State.OnboardingComplete)
            {
                ShowOnboarding();
            }
        }

		private async Task<CommandResponse> HandleForwardedCommandAsync(string[] arguments)
		{
			return await Dispatcher.InvokeAsync(async () =>
			{
				var options = CliOptions.Parse(arguments);
				if (!options.IsValid) return CommandResponse.Fail(options.ErrorMessage, 2);

				var response = await ExecuteCommandAsync(options);
				if (!options.HasHeadlessVerb && !options.Exit) ShowShell();
				if (options.Exit)
				{
					_ = Dispatcher.BeginInvoke(new Action(ExitApp), DispatcherPriority.ApplicationIdle);
				}
				return response;
			}).Task.Unwrap();
		}

		private async Task<CommandResponse> ExecuteCommandAsync(CliOptions options)
		{
			try
			{
				if (options.Help) return CommandResponse.Ok(CliOptions.HelpText);

				if (options.Status)
				{
					_controller.RefreshMonitors();
					return CommandResponse.Ok(BuildStatusJson());
				}

				InjectionResult? result = null;
				if (options.ApplyMonitor != null)
				{
					var assignmentError = AssignFromCli(options);
					if (assignmentError != null) return CommandResponse.Fail(assignmentError, 2);
					result = await _controller.EnableAsync();
				}
				else if (options.Startup)
				{
					result = await _controller.ReapplyIfEnabledAsync(AppOperation.Startup, silent: true);
				}
				else if (options.Apply) result = await _controller.EnableAsync();
				else if (options.Disable) result = await _controller.DisableAsync();
				else if (options.Toggle) result = await _controller.ToggleAsync();

				if (result == null) return CommandResponse.Ok();
				return result.Success
					? CommandResponse.Ok(result.Message)
					: CommandResponse.Fail(result.Message +
						(string.IsNullOrWhiteSpace(result.Diagnostics) ? string.Empty : Environment.NewLine + result.Diagnostics));
			}
			catch (Exception ex)
			{
				return CommandResponse.Fail(ex.Message);
			}
		}

		private string? AssignFromCli(CliOptions options)
		{
			foreach (var path in new[] { options.SdrPath, options.HdrPath })
			{
				if (path != null && !System.IO.File.Exists(path)) return "LUT file not found: " + path;
			}

			// Monitors returned here are references into canonical state, so edits persist on Save.
			_controller.RefreshMonitors();
			var matches = MonitorSelector.FindMatches(_controller.Monitors, options.ApplyMonitor!);
			if (matches.Count == 0) return "No connected display matches '" + options.ApplyMonitor + "'.";
			if (matches.Count > 1)
			{
				return "Display selector '" + options.ApplyMonitor +
					"' is ambiguous across adapters. Use the exact device path from --status.";
			}

			var monitor = matches[0];
			if (options.SdrPath != null) monitor.SetSdrLut(options.SdrPath);
			if (options.HdrPath != null) monitor.SetHdrLut(options.HdrPath);
			_controller.Save();
			return null;
		}

        private void SetupTray()
        {
            _tray = new TrayIcon(_controller);
            _tray.OpenRequested += ShowShell;
            _tray.ApplyRequested += () => _ = _controller.EnableAsync();
            _tray.DisableRequested += () => _ = _controller.DisableAsync();
			_tray.AutostartToggled += enabled =>
			{
				if (!_controller.SetAutostart(enabled))
				{
					_toast.Failed("Windows startup couldn't be changed. The previous setting is still active.");
				}
			};
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
                if (wizard.ShowDialog() == true)
                {
                    _controller.State.OnboardingComplete = true;
                    _controller.Save();
                }
            }
            catch
            {
                // Onboarding is non-essential; never block startup on it.
            }
            finally
            {
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
			if (_commandChannel != null)
			{
				_commandChannel.DisposeAsync().AsTask().GetAwaiter().GetResult();
			}
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
            var lastResult = _controller.LastOperationResult;
            var payload = new
            {
                desiredEnabled = _controller.State.IsEnabled,
                observedStatus = status.ToString().ToLowerInvariant(),
                lastOperation = _controller.LastOperation?.ToString().ToLowerInvariant(),
                lastOperationSucceeded = lastResult?.Success,
                lastError = lastResult is { Success: false } ? lastResult.Message : null,
                // Retained for scripts written against the v5 preview status schema.
                enabled = _controller.State.IsEnabled,
                status = status.ToString().ToLowerInvariant(),
                elevated = InjectionService.CurrentProcessIsElevated(),
                resolverMode = _controller.State.ResolverMode,
                startWithWindows = _controller.State.StartWithWindows,
                monitors = _controller.Monitors.Count,
				displays = _controller.Monitors.Select(m => new
				{
					number = m.SourceId,
					devicePath = m.DevicePath,
					adapterLuid = m.AdapterLuidHigh.ToString("X8") + m.AdapterLuidLow.ToString("X8"),
					targetId = m.TargetId,
					name = m.Name,
					position = m.Position,
					colorMode = m.ColorMode.ToString().ToLowerInvariant(),
					hdrActive = m.ColorMode == DisplayColorMode.Unknown
						? (bool?)null
						: m.IsHdrActive
				}).ToArray(),
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

		private static string GetInstanceKey()
		{
			var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
#if DWM_LUT_SMOKE
			return SingleInstanceIdentity.Create(sid, AppPaths.Root);
#else
			return SingleInstanceIdentity.Create(sid, null);
#endif
		}
	}
}
