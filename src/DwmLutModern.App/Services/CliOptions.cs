using System;
using System.Collections.Generic;
using System.Linq;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Parsed command line. Replaces the legacy ad-hoc single-dash flags with a documented
    /// double-dash surface used by the autostart task, the tray, and manual/scripted control.
    /// </summary>
	public sealed class CliOptions
	{
		private readonly List<string> _errors = new List<string>();

        public bool Apply { get; private set; }
        public bool Disable { get; private set; }
        public bool Toggle { get; private set; }
        public bool Status { get; private set; }
        public bool Startup { get; private set; }
        public bool Minimized { get; private set; }
        public bool Exit { get; private set; }
        public bool Help { get; private set; }

        /// <summary>Target monitor for --apply-monitor: 1-based source id or device path.</summary>
        public string? ApplyMonitor { get; private set; }
        public string? SdrPath { get; private set; }
		public string? HdrPath { get; private set; }
		public bool IsValid => _errors.Count == 0;
		public string ErrorMessage => string.Join(Environment.NewLine, _errors);

        /// <summary>True when any headless verb was given (the app should act, maybe exit).</summary>
        public bool HasHeadlessVerb =>
            Apply || Disable || Toggle || Status || Startup || Help || ApplyMonitor != null;

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var queue = new Queue<string>(NormalizeLegacy(args));

            while (queue.Count > 0)
            {
                var arg = queue.Dequeue();
                switch (arg.ToLowerInvariant())
                {
                    case "--apply": options.Apply = true; break;
                    case "--disable": options.Disable = true; break;
                    case "--toggle": options.Toggle = true; break;
                    case "--status": options.Status = true; break;
                    case "--startup": options.Startup = true; break;
                    case "--minimized":
                    case "--tray": options.Minimized = true; break;
                    case "--exit": options.Exit = true; break;
                    case "--help":
                    case "-h":
                    case "-?": options.Help = true; break;
					case "--apply-monitor": options.ApplyMonitor = options.NextRequired(queue, "--apply-monitor"); break;
					case "--sdr": options.SdrPath = options.NextRequired(queue, "--sdr"); break;
					case "--hdr": options.HdrPath = options.NextRequired(queue, "--hdr"); break;
					default: options._errors.Add("Unknown option: " + arg); break;
				}
			}

			options.Validate();
			return options;
		}

		private string? NextRequired(Queue<string> queue, string option)
		{
			if (queue.Count == 0 || queue.Peek().StartsWith("-", StringComparison.Ordinal))
			{
				_errors.Add(option + " requires a value.");
				return null;
			}

			return queue.Dequeue();
		}

		private void Validate()
		{
			if (Help) return;

			var actionCount = new[] { Apply, Disable, Toggle, Status, Startup, ApplyMonitor != null }.Count(value => value);
			if (actionCount > 1)
			{
				_errors.Add("Specify only one action: --apply, --disable, --toggle, --status, --startup, or --apply-monitor.");
			}

			if (ApplyMonitor != null && SdrPath == null && HdrPath == null)
			{
				_errors.Add("--apply-monitor requires --sdr or --hdr.");
			}

			if (ApplyMonitor == null && (SdrPath != null || HdrPath != null))
			{
				_errors.Add("--sdr and --hdr require --apply-monitor.");
			}
		}

        /// <summary>Accept the legacy single-dash flags so old shortcuts/tasks keep working.</summary>
        private static IEnumerable<string> NormalizeLegacy(string[] args)
        {
            foreach (var arg in args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "-apply": yield return "--apply"; break;
                    case "-disable": yield return "--disable"; break;
                    case "-minimize": yield return "--minimized"; break;
                    case "-exit": yield return "--exit"; break;
                    default: yield return arg; break;
                }
            }
        }

        public const string HelpText =
            "DWM LUT Modern — system-wide 3D LUT color correction for Windows 11.\n\n" +
            "Usage: DwmLutModern.exe [options]\n\n" +
            "  --apply                 Turn the LUT on and remember that intent.\n" +
            "  --disable               Turn the LUT off and remember that intent.\n" +
            "  --toggle                Toggle the LUT on/off.\n" +
            "  --startup               Restore the saved intent (used by the logon task). Applies\n" +
            "                          only if the LUT was left enabled; otherwise does nothing.\n" +
            "  --status                Print current status as JSON and exit.\n" +
            "  --apply-monitor <id>    Select a monitor by 1-based number or device path, combine\n" +
            "                          with --sdr/--hdr to assign LUTs, then apply.\n" +
            "  --sdr <path>            LUT (.cube/.txt) to assign for SDR with --apply-monitor.\n" +
            "  --hdr <path>            LUT (.cube/.txt) to assign for HDR with --apply-monitor.\n" +
            "  --minimized, --tray     Start hidden in the notification area.\n" +
            "  --exit                  After running the above, exit immediately (headless).\n" +
            "  --help, -h              Show this help.\n";
    }
}
