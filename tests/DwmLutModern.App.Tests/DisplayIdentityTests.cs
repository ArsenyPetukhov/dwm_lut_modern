using DwmLutModern.Models;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class DisplayIdentityTests
{
	[Fact]
	public void Target_ids_on_different_adapters_are_not_the_same_display()
	{
		var first = new DisplayIdentity("", adapterLuidLow: 10, adapterLuidHigh: 1, targetId: 2);
		var second = new DisplayIdentity("", adapterLuidLow: 11, adapterLuidHigh: 1, targetId: 2);

		Assert.False(first.Matches(second));
	}

	[Fact]
	public void Device_path_remains_the_primary_persistent_identity()
	{
		var saved = new DisplayIdentity("DISPLAY#PANEL-A", adapterLuidLow: 10, adapterLuidHigh: 1, targetId: 2);
		var reconnected = new DisplayIdentity("display#panel-a", adapterLuidLow: 99, adapterLuidHigh: 5, targetId: 7);

		Assert.True(saved.Matches(reconnected));
	}

	[Fact]
	public void Empty_identity_never_matches_another_empty_identity()
	{
		Assert.False(default(DisplayIdentity).Matches(default));
	}
}
