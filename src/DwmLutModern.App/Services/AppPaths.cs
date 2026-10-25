using System;
using System.IO;
using System.Reflection;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Central file locations. Config lives under <c>%AppData%\DwmLutModern</c> so it survives
    /// moving/updating the app and never hits permission problems when the app runs from
    /// Program Files — unlike the legacy build, which kept config.xml next to the exe.
    /// </summary>
    public static class AppPaths
    {
        public const string ProductFolder = "DwmLutModern";

        public static string Root { get; }
        public static string StateFile { get; }
        public static string GuiLogFile { get; }
        public static string AppDirectory { get; }
        public static string NativeDllSource { get; }
        public static string LegacyConfigFile { get; }

		static AppPaths()
		{
			var rootOverride = Environment.GetEnvironmentVariable("DWM_LUT_STATE_ROOT");
			// The override is compiled in only for the screenshot harness. Production builds always
			// ignore inherited values so the elevated app cannot become an arbitrary-path writer.
#if DWM_LUT_SMOKE
			var allowRootOverride = true;
#else
			var allowRootOverride = false;
#endif
			Root = !allowRootOverride || string.IsNullOrWhiteSpace(rootOverride)
				? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductFolder)
				: Path.GetFullPath(rootOverride);
            StateFile = Path.Combine(Root, "state.json");
            GuiLogFile = Path.Combine(Root, "dwm_lut_gui.log");

            AppDirectory = AppContext.BaseDirectory;
            NativeDllSource = Path.Combine(AppDirectory, "dwm_lut.dll");
            LegacyConfigFile = Path.Combine(AppDirectory, "config.xml");
        }

        public static void EnsureRoot()
        {
            Directory.CreateDirectory(Root);
        }
    }
}
