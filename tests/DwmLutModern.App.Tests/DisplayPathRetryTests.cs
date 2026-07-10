using System.ComponentModel;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class DisplayPathRetryTests
{
	[Fact]
	public void Insufficient_buffer_is_retried_with_a_fresh_topology_query()
	{
		var calls = 0;
		var result = DisplayPathRetry.Execute(() =>
		{
			calls++;
			if (calls < 3) throw new Win32Exception(122);
			return "stable topology";
		});

		Assert.Equal("stable topology", result);
		Assert.Equal(3, calls);
	}

	[Fact]
	public void Unrelated_display_failure_is_not_retried()
	{
		var calls = 0;

		Assert.Throws<Win32Exception>(() => DisplayPathRetry.Execute<int>(() =>
		{
			calls++;
			throw new Win32Exception(5);
		}));
		Assert.Equal(1, calls);
	}
}
