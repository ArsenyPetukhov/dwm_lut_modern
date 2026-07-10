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
            Root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ProductFolder);
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
