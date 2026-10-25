using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class CliOptionsTests
{
	[Fact]
	public void Help_is_a_headless_command_that_can_be_forwarded_to_the_owner()
	{
		var options = CliOptions.Parse(new[] { "--help" });

		Assert.True(options.IsValid);
		Assert.True(options.Help);
		Assert.True(options.HasHeadlessVerb);
	}

	[Fact]
	public void Unknown_option_is_rejected()
	{
		var options = CliOptions.Parse(["--typo"]);

		Assert.False(options.IsValid);
		Assert.Contains("Unknown option", options.ErrorMessage);
	}

	[Fact]
	public void Missing_monitor_selector_is_rejected()
	{
		var options = CliOptions.Parse(["--apply-monitor"]);

		Assert.False(options.IsValid);
		Assert.Contains("--apply-monitor requires", options.ErrorMessage);
	}

	[Fact]
	public void Monitor_assignment_requires_at_least_one_lut()
	{
		var options = CliOptions.Parse(["--apply-monitor", "1"]);

		Assert.False(options.IsValid);
		Assert.Contains("--sdr or --hdr", options.ErrorMessage);
	}

	[Fact]
	public void Contradictory_actions_are_rejected()
	{
		var options = CliOptions.Parse(["--apply", "--disable"]);

		Assert.False(options.IsValid);
		Assert.Contains("one action", options.ErrorMessage);
	}

	[Fact]
	public void Valid_monitor_assignment_is_accepted()
	{
		var options = CliOptions.Parse(["--apply-monitor", "1", "--sdr", "display.cube"]);

		Assert.True(options.IsValid, options.ErrorMessage);
		Assert.Equal("1", options.ApplyMonitor);
		Assert.Equal("display.cube", options.SdrPath);
	}
}
