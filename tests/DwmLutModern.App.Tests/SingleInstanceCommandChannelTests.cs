using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class SingleInstanceCommandChannelTests
{
	[Fact]
	public async Task Secondary_process_receives_the_owner_command_result()
	{
		var pipeName = "DwmLutModern.Tests." + Guid.NewGuid().ToString("N");
		await using var channel = new SingleInstanceCommandChannel(pipeName);
		channel.Start(arguments => Task.FromResult(
			CommandResponse.Ok("handled " + string.Join(' ', arguments))));

		var response = await channel.SendAsync(["--disable", "--exit"], TimeSpan.FromSeconds(5));

		Assert.True(response.Success, response.Error);
		Assert.Equal("handled --disable --exit", response.Output);
	}

	[Fact]
	public async Task Owner_converts_handler_exceptions_to_failed_responses()
	{
		var pipeName = "DwmLutModern.Tests." + Guid.NewGuid().ToString("N");
		await using var channel = new SingleInstanceCommandChannel(pipeName);
		channel.Start(_ => throw new InvalidOperationException("broken command"));

		var response = await channel.SendAsync(["--apply"], TimeSpan.FromSeconds(5));

		Assert.False(response.Success);
		Assert.Contains("broken command", response.Error);
	}
}
