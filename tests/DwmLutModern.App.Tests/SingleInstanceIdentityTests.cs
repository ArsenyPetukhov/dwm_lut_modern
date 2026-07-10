using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class SingleInstanceIdentityTests
{
	[Fact]
	public void Smoke_scope_cannot_collide_with_the_production_owner()
	{
		var production = SingleInstanceIdentity.Create("S-1-5-21-123", null);
		var smoke = SingleInstanceIdentity.Create("S-1-5-21-123", @"C:\isolated\state");

		Assert.NotEqual(production, smoke);
	}

	[Fact]
	public void Same_user_and_scope_are_stable_and_pipe_safe()
	{
		var first = SingleInstanceIdentity.Create(@"DOMAIN\user", @"C:\isolated\state");
		var second = SingleInstanceIdentity.Create(@"DOMAIN\user", @"c:\isolated\state");

		Assert.Equal(first, second);
		Assert.Matches("^[A-Za-z0-9_]+$", first);
	}
}
