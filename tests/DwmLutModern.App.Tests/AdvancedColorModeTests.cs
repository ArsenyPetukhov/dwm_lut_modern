using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class AdvancedColorModeTests
{
	[Theory]
	[InlineData(0, 0u, DisplayColorMode.Sdr)]
	[InlineData(0, 1u, DisplayColorMode.Sdr)]
	[InlineData(0, 2u, DisplayColorMode.Hdr)]
	[InlineData(0, 3u, DisplayColorMode.Hdr)]
	public void Successful_query_maps_the_advanced_color_enabled_bit(
		int win32Result,
		uint value,
		DisplayColorMode expected)
	{
		Assert.Equal(expected, AdvancedColorModeParser.Parse(win32Result, value));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(-1)]
	[InlineData(87)]
	public void Failed_query_is_unknown_instead_of_being_reported_as_sdr(int win32Result)
	{
		Assert.Equal(DisplayColorMode.Unknown, AdvancedColorModeParser.Parse(win32Result, 0));
	}
}
