using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class ScheduledTaskErrorClassifierTests
{
	[Theory]
	[InlineData(unchecked((int)0x80070002))]
	[InlineData(unchecked((int)0x8004130F))]
	public void Known_task_not_found_hresult_is_missing(int hresult)
	{
		Assert.True(ScheduledTaskErrorClassifier.IsTaskNotFound(hresult));
	}

	[Theory]
	[InlineData(unchecked((int)0x80070005))]
	[InlineData(unchecked((int)0x80041315))]
	[InlineData(1)]
	public void Permission_service_and_process_exit_codes_are_not_missing(int hresult)
	{
		Assert.False(ScheduledTaskErrorClassifier.IsTaskNotFound(hresult));
	}
}
