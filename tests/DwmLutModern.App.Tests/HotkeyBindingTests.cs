using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class HotkeyBindingTests
{
    // Capture and registration use this same policy, so a chord rejected here cannot
    // become a global shortcut through either the dialog or persisted settings.
    [Theory]
    [InlineData(0x41u, 0x0002u)]
    [InlineData(0x03u, 0x0002u)]
    [InlineData(0x41u, 0x0001u)]
    [InlineData(0x20u, 0x0008u)]
    [InlineData(0x41u, 0x0006u)]
    [InlineData(0x41u, 0x000Fu)]
    [InlineData(0x70u, 0u)]
    [InlineData(0x7Bu, 0u)]
    [InlineData(0x7Cu, 0u)]
    [InlineData(0x87u, 0u)]
    [InlineData(0x87u, 0x0002u)]
    public void Capture_and_registration_allow_safe_keyboard_combinations(uint key, uint modifiers)
    {
        Assert.True(new HotkeyBinding { VirtualKey = key, Modifiers = modifiers }.IsValid);
    }

    [Theory]
    [InlineData(0x41u, 0u)]
    [InlineData(0x31u, 0u)]
    [InlineData(0x20u, 0u)]
    [InlineData(0x41u, 0x0004u)]
    [InlineData(0x70u, 0x0004u)]
    [InlineData(0x10u, 0x0002u)]
    [InlineData(0x11u, 0x0001u)]
    [InlineData(0x12u, 0x0002u)]
    [InlineData(0x5Bu, 0x0002u)]
    [InlineData(0x5Cu, 0x0002u)]
    [InlineData(0xA0u, 0x0002u)]
    [InlineData(0xA3u, 0x0001u)]
    [InlineData(0xA5u, 0x0002u)]
    [InlineData(0u, 0x0002u)]
    [InlineData(0x01u, 0x0002u)]
    [InlineData(0xFFu, 0x0002u)]
    [InlineData(0x41u, 0x4002u)]
    public void Capture_and_registration_reject_typing_modifier_only_and_invalid_combinations(uint key, uint modifiers)
    {
        Assert.False(new HotkeyBinding { VirtualKey = key, Modifiers = modifiers }.IsValid);
    }

    [Theory]
    [InlineData(0x70u, "F1")]
    [InlineData(0x7Bu, "F12")]
    [InlineData(0x7Cu, "F13")]
    [InlineData(0x87u, "F24")]
    public void Function_key_names_cover_the_full_supported_range(uint key, string expected)
    {
        Assert.Equal(expected, new HotkeyBinding { VirtualKey = key }.ToString());
    }

    [Fact]
    public void Modifier_display_order_matches_capture()
    {
        Assert.Equal("Ctrl + Alt + Shift + Win + F24",
            new HotkeyBinding { VirtualKey = 0x87, Modifiers = 0x000F }.ToString());
    }

    [Fact]
    public void Empty_assignment_uses_the_localized_status()
    {
        Assert.Equal(LocalizationService.T("Common.NotAssigned"), new HotkeyBinding().ToString());
    }

    [Fact]
    public void Windows_key_names_replace_hex_codes_for_numeric_keypad_keys()
    {
        if (!OperatingSystem.IsWindows()) return;
        var text = new HotkeyBinding { VirtualKey = 0x60, Modifiers = 0x0002 }.ToString();

        Assert.DoesNotContain("VK0x", text);
    }
}
