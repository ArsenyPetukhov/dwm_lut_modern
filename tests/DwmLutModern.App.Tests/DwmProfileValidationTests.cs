using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class DwmProfileValidationTests
{
	[Theory]
	[InlineData("local-built")]
	[InlineData("built")]
	[InlineData("experimental")]
	public void Build_state_is_not_mislabeled_as_verified(string validationState)
	{
		Assert.False(DwmProfileService.IsVerifiedValidationState(validationState));
	}

	[Theory]
	[InlineData("verified-stable")]
	[InlineData("live-validated")]
	public void Live_validation_states_are_verified(string validationState)
	{
		Assert.True(DwmProfileService.IsVerifiedValidationState(validationState));
	}

	[Fact]
	public void Unsupported_support_classification_blocks_apply()
	{
		var support = new DwmSupport { Level = DwmSupportLevel.Unsupported };

		Assert.False(support.CanApply);
	}

	[Fact]
	public void Community_notice_is_shown_only_until_a_profile_is_verified()
	{
		var pending = new DwmSupport { IsVerified = false };
		var verified = new DwmSupport { IsVerified = true };

		Assert.True(pending.ShouldShowCommunityNotice);
		Assert.False(verified.ShouldShowCommunityNotice);
		Assert.Contains("report the issue", DwmProfileService.CommunityNotice);
	}
}
