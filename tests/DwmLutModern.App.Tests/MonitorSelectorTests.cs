using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class MonitorSelectorTests
{
	[Fact]
	public void Adapter_local_display_number_can_be_ambiguous()
	{
		var monitors = new[]
		{
			new MonitorConfig { DevicePath = "DISPLAY-A", SourceId = 1, AdapterLuidLow = 1 },
			new MonitorConfig { DevicePath = "DISPLAY-B", SourceId = 1, AdapterLuidLow = 2 }
		};

		var matches = MonitorSelector.FindMatches(monitors, "1");

		Assert.Equal(2, matches.Count);
	}

	[Fact]
	public void Exact_device_path_selects_one_display_across_adapters()
	{
		var monitors = new[]
		{
			new MonitorConfig { DevicePath = "DISPLAY-A", SourceId = 1, AdapterLuidLow = 1 },
			new MonitorConfig { DevicePath = "DISPLAY-B", SourceId = 1, AdapterLuidLow = 2 }
		};

		var matches = MonitorSelector.FindMatches(monitors, "display-b");

		Assert.Single(matches);
		Assert.Equal("DISPLAY-B", matches[0].DevicePath);
	}
}
