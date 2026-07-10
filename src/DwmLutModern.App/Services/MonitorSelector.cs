using System;
using System.Collections.Generic;
using System.Linq;
using DwmLutModern.Models;

namespace DwmLutModern.Services
{
	/// <summary>Pure CLI display selection policy; callers must require exactly one match.</summary>
	public static class MonitorSelector
	{
		public static IReadOnlyList<MonitorConfig> FindMatches(
			IEnumerable<MonitorConfig> monitors,
			string selector)
		{
			if (monitors == null) throw new ArgumentNullException(nameof(monitors));
			if (string.IsNullOrWhiteSpace(selector)) return Array.Empty<MonitorConfig>();

			return monitors.Where(monitor =>
				string.Equals(monitor.DevicePath, selector, StringComparison.OrdinalIgnoreCase) ||
				monitor.SourceId.ToString() == selector).ToList();
		}
	}
}
