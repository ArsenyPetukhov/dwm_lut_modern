using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class HotkeyServiceTests
{
	[Fact]
	public void Registration_failure_is_returned_to_the_caller()
	{
		var native = new FakeHotkeyRegistrationApi { ErrorCode = 1409 };
		using var service = new HotkeyService(native);
		service.Attach(new IntPtr(123));

		var result = service.Apply(new HotkeyBinding { VirtualKey = 0x41, Modifiers = 0x0002 }, null);

		Assert.False(result.ToggleRegistered);
		Assert.Contains("1409", result.Error);
	}

	[Fact]
	public void Successful_registration_adds_no_repeat_to_the_modifiers()
	{
		var native = new FakeHotkeyRegistrationApi { RegisterResult = true };
		using var service = new HotkeyService(native);
		service.Attach(new IntPtr(123));

		var result = service.Apply(new HotkeyBinding { VirtualKey = 0x41, Modifiers = 0x0002 }, null);

		Assert.True(result.Success, result.Error);
		Assert.Equal(0x4002u, native.LastModifiers);
	}

	private sealed class FakeHotkeyRegistrationApi : IHotkeyRegistrationApi
	{
		public bool RegisterResult { get; set; }
		public int ErrorCode { get; set; }
		public uint LastModifiers { get; private set; }

		public bool Register(IntPtr window, int id, uint modifiers, uint virtualKey, out int errorCode)
		{
			LastModifiers = modifiers;
			errorCode = ErrorCode;
			return RegisterResult;
		}

		public bool Unregister(IntPtr window, int id) => true;
	}
}
