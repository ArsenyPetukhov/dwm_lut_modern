using System.Reflection;
using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class HotkeyServiceTests
{
    private const int ToggleId = 0xB001;
    private const int CycleId = 0xB002;

    [Fact]
    public void Registration_failure_is_returned_and_leaves_no_partial_pair()
    {
        var native = new FakeHotkeyRegistrationApi { RejectRegistration = (_, _, _) => true };
        using var service = AttachedService(native);

        var result = service.Apply(Binding(0x41), null);

        Assert.False(result.Success);
        Assert.False(result.ToggleRegistered);
        Assert.True(result.CycleRegistered);
        Assert.True(result.PreviousBindingsRestored);
        Assert.Contains("1409", result.Error);
        Assert.Empty(native.Active);
    }

    [Fact]
    public void Successful_registration_adds_no_repeat_to_the_modifiers()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);

        var result = service.Apply(Binding(0x41), null);

        Assert.True(result.Success, result.Error);
        Assert.Equal((0x4002u, 0x41u), native.Active[ToggleId]);
    }

    [Fact]
    public void Duplicate_pair_is_rejected_before_any_native_changes()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.Calls.Clear();

        var result = service.Apply(Binding(0x41), Binding(0x41));

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        Assert.True(result.ToggleRegistered);
        Assert.False(result.CycleRegistered);
        Assert.Empty(native.Calls);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Duplicate_initial_pair_does_not_register_either_action()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);

        var result = service.Apply(Binding(0x41), Binding(0x41));

        Assert.False(result.Success);
        Assert.Empty(native.Calls);
        Assert.Empty(native.Active);
    }

    [Fact]
    public void Failure_of_unchanged_cycle_restores_previous_toggle_as_well()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        var rejectCycleOnce = true;
        native.RejectRegistration = (id, _, _) =>
        {
            if (id != CycleId || !rejectCycleOnce) return false;
            rejectCycleOnce = false;
            return true;
        };

        var result = service.Apply(Binding(0x43), Binding(0x42));

        Assert.False(result.Success);
        Assert.False(result.ToggleRegistered);
        Assert.True(result.CycleRegistered);
        Assert.True(result.PreviousBindingsRestored);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Failure_after_first_initial_registration_removes_that_registration()
    {
        var native = new FakeHotkeyRegistrationApi { RejectRegistration = (id, _, _) => id == CycleId };
        using var service = AttachedService(native);

        var result = service.Apply(Binding(0x41), Binding(0x42));

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        Assert.False(result.ToggleRegistered);
        Assert.False(result.CycleRegistered);
        Assert.Empty(native.Active);
    }

    [Fact]
    public void Rollback_failure_is_reported_as_failure_even_with_a_requested_action_active()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.RejectRegistration = (_, _, key) => key == 0x44;
        native.RejectUnregister = id => id == ToggleId && native.Active[id].VirtualKey == 0x43;

        var result = service.Apply(Binding(0x43), Binding(0x44));

        Assert.False(result.Success);
        Assert.False(result.PreviousBindingsRestored);
        Assert.True(result.ToggleRegistered);
        Assert.False(result.CycleRegistered);
        Assert.Contains(LocalizationService.T("Hotkey.RestoreFailed"), result.Error);
        AssertPair(native, 0x43, 0x42);
    }

    [Fact]
    public void Failure_to_restore_a_previous_registration_is_reported()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.RejectRegistration = (_, _, key) => key is 0x41 or 0x44;

        var result = service.Apply(Binding(0x43), Binding(0x44));

        Assert.False(result.Success);
        Assert.False(result.PreviousBindingsRestored);
        Assert.Contains(LocalizationService.T("Hotkey.RestoreFailed"), result.Error);
        Assert.False(native.Active.ContainsKey(ToggleId));
        Assert.Equal(0x42u, native.Active[CycleId].VirtualKey);
    }

    [Fact]
    public void Failed_unregister_preserves_the_existing_pair_and_reports_failure()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.RejectUnregister = id => id == CycleId;

        var result = service.Apply(Binding(0x43), Binding(0x44));

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Rollback_uses_copies_of_registered_bindings()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        var originalToggle = Binding(0x41);
        Assert.True(service.Apply(originalToggle, Binding(0x42)).Success);
        originalToggle.VirtualKey = 0x5A;
        native.RejectRegistration = (_, _, key) => key == 0x44;

        var result = service.Apply(Binding(0x43), Binding(0x44));

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Swapping_the_two_shortcuts_succeeds()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);

        var result = service.Apply(Binding(0x42), Binding(0x41));

        Assert.True(result.Success, result.Error);
        AssertPair(native, 0x42, 0x41);
    }

    [Fact]
    public void Reapplying_the_same_pair_does_not_release_the_shortcuts()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.Calls.Clear();

        var result = service.Apply(Binding(0x41), Binding(0x42));

        Assert.True(result.Success);
        Assert.Empty(native.Calls);
    }

    [Fact]
    public void Invalid_chord_cannot_replace_a_registered_pair()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.Calls.Clear();

        var result = service.Apply(new HotkeyBinding { VirtualKey = 0x43 }, Binding(0x42));

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        Assert.Empty(native.Calls);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Registration_requires_a_window_but_empty_assignments_do_not()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = new HotkeyService(native);

        Assert.True(service.Apply(null, null).Success);
        var result = service.Apply(Binding(0x41), null);

        Assert.False(result.Success);
        Assert.True(result.PreviousBindingsRestored);
        Assert.Empty(native.Calls);
    }

    [Fact]
    public void Messages_for_removed_or_unknown_registration_ids_are_ignored()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        var toggles = 0;
        service.ToggleRequested += () => toggles++;
        Assert.True(service.Apply(Binding(0x41), null).Success);
        Assert.True(DispatchHotkey(service, ToggleId));
        Assert.Equal(1, toggles);
        Assert.True(service.Apply(null, null).Success);

        Assert.False(DispatchHotkey(service, ToggleId));
        Assert.False(DispatchHotkey(service, CycleId));
        Assert.False(DispatchHotkey(service, 12345));
        Assert.Equal(1, toggles);
    }

    [Fact]
    public void Queued_message_for_a_replaced_chord_does_not_dispatch_the_reused_id()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), null).Success);
        Assert.True(service.Apply(Binding(0x43), null).Success);
        var toggles = 0;
        service.ToggleRequested += () => toggles++;

        Assert.False(DispatchHotkey(service, ToggleId, 0x41));
        Assert.True(DispatchHotkey(service, ToggleId, 0x43));
        Assert.Equal(1, toggles);
    }

    [Fact]
    public void Capture_suspends_both_shortcuts_and_cancel_restores_the_pair()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        var toggles = 0;
        service.ToggleRequested += () => toggles++;

        var lease = service.SuspendForCapture(out var error);

        Assert.NotNull(lease);
        Assert.Empty(error);
        Assert.Null(lease.RestoreResult);
        Assert.Empty(native.Active);
        Assert.False(DispatchHotkey(service, ToggleId));
        Assert.Equal(0, toggles);
        lease.Dispose();
        Assert.NotNull(lease.RestoreResult);
        Assert.True(lease.RestoreResult.Success, lease.RestoreResult.Error);
        Assert.True(lease.RestoreResult.PreviousBindingsRestored);
        AssertPair(native, 0x41, 0x42);
        Assert.True(DispatchHotkey(service, ToggleId));
        Assert.Equal(1, toggles);

        native.Calls.Clear();
        lease.Dispose();
        Assert.Empty(native.Calls);
    }

    [Fact]
    public void Capture_does_not_start_if_a_shortcut_cannot_be_released()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.RejectUnregister = id => id == CycleId;

        var lease = service.SuspendForCapture(out var error);

        Assert.Null(lease);
        Assert.NotEmpty(error);
        AssertPair(native, 0x41, 0x42);
    }

    [Fact]
    public void Failed_capture_release_reports_if_rollback_is_incomplete()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        native.RejectUnregister = id => id == CycleId;
        native.RejectRegistration = (id, _, _) => id == ToggleId;

        var lease = service.SuspendForCapture(out var error);

        Assert.Null(lease);
        Assert.Contains(LocalizationService.T("Hotkey.RestoreFailed"), error);
        Assert.False(native.Active.ContainsKey(ToggleId));
        Assert.Equal(0x42u, native.Active[CycleId].VirtualKey);
    }

    [Fact]
    public void Capture_restore_failure_is_available_after_disposal()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        var lease = service.SuspendForCapture(out _);
        Assert.NotNull(lease);
        native.RejectRegistration = (id, _, _) => id == ToggleId;

        lease.Dispose();

        Assert.NotNull(lease.RestoreResult);
        Assert.False(lease.RestoreResult.Success);
        Assert.False(lease.RestoreResult.ToggleRegistered);
        Assert.True(lease.RestoreResult.CycleRegistered);
        Assert.False(lease.RestoreResult.PreviousBindingsRestored);
        Assert.Contains(LocalizationService.T("Hotkey.RestoreFailed"), lease.RestoreResult.Error);
        Assert.False(native.Active.ContainsKey(ToggleId));
        Assert.Equal(0x42u, native.Active[CycleId].VirtualKey);
    }

    [Fact]
    public void Active_capture_blocks_other_captures_and_registration_changes()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        using var lease = service.SuspendForCapture(out _);
        Assert.NotNull(lease);
        native.Calls.Clear();

        Assert.Null(service.SuspendForCapture(out var error));
        var result = service.Apply(Binding(0x43), Binding(0x44));

        Assert.NotEmpty(error);
        Assert.False(result.Success);
        Assert.Empty(native.Calls);
        Assert.Empty(native.Active);
    }

    [Fact]
    public void Disposing_service_during_capture_does_not_reregister_shortcuts()
    {
        var native = new FakeHotkeyRegistrationApi();
        var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        var lease = service.SuspendForCapture(out _);
        Assert.NotNull(lease);
        service.Dispose();
        native.Calls.Clear();

        lease.Dispose();

        Assert.NotNull(lease.RestoreResult);
        Assert.False(lease.RestoreResult.Success);
        Assert.Empty(native.Calls);
        Assert.Empty(native.Active);
    }

    [Fact]
    public void A_candidate_can_be_applied_after_capture_releases_its_lease()
    {
        var native = new FakeHotkeyRegistrationApi();
        using var service = AttachedService(native);
        Assert.True(service.Apply(Binding(0x41), Binding(0x42)).Success);
        var lease = service.SuspendForCapture(out _);
        Assert.NotNull(lease);
        lease.Dispose();

        var result = service.Apply(Binding(0x43), Binding(0x42));

        Assert.True(result.Success, result.Error);
        AssertPair(native, 0x43, 0x42);
    }

    private static HotkeyService AttachedService(FakeHotkeyRegistrationApi native)
    {
        var service = new HotkeyService(native);
        service.Attach(new IntPtr(123));
        return service;
    }

    private static HotkeyBinding Binding(uint key) => new HotkeyBinding { VirtualKey = key, Modifiers = 0x0002 };

    private static void AssertPair(FakeHotkeyRegistrationApi native, uint toggleKey, uint cycleKey)
    {
        Assert.Equal(2, native.Active.Count);
        Assert.Equal((0x4002u, toggleKey), native.Active[ToggleId]);
        Assert.Equal((0x4002u, cycleKey), native.Active[CycleId]);
    }

    private static bool DispatchHotkey(HotkeyService service, int id, uint key = 0x41)
    {
        var keyData = new IntPtr((long)((key << 16) | 0x0002u));
        var arguments = new object[] { new IntPtr(123), 0x0312, new IntPtr(id), keyData, false };
        typeof(HotkeyService).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, arguments);
        return (bool)arguments[4];
    }

    private sealed class FakeHotkeyRegistrationApi : IHotkeyRegistrationApi
    {
        public Dictionary<int, (uint Modifiers, uint VirtualKey)> Active { get; } = new();
        public List<string> Calls { get; } = new();
        public Func<int, uint, uint, bool>? RejectRegistration { get; set; }
        public Func<int, bool>? RejectUnregister { get; set; }

        public bool Register(IntPtr window, int id, uint modifiers, uint virtualKey, out int errorCode)
        {
            Calls.Add($"Register {id}: {modifiers}/{virtualKey}");
            if (RejectRegistration?.Invoke(id, modifiers, virtualKey) == true
                || Active.ContainsKey(id) || Active.Values.Any(value => value == (modifiers, virtualKey)))
            {
                errorCode = 1409;
                return false;
            }

            Active[id] = (modifiers, virtualKey);
            errorCode = 0;
            return true;
        }

        public bool Unregister(IntPtr window, int id)
        {
            Calls.Add($"Unregister {id}");
            if (RejectUnregister?.Invoke(id) == true) return false;
            return Active.Remove(id);
        }
    }
}
