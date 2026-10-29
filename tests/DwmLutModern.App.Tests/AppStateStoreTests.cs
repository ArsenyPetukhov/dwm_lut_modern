using DwmLutModern.Models;
using DwmLutModern.Services;
using Xunit;

namespace DwmLutModern.App.Tests;

public sealed class AppStateStoreTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "DwmLutModern.Tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public void Desired_state_and_adapter_identity_round_trip()
	{
		var stateFile = Path.Combine(_root, "state.json");
		var store = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml"));
		store.State.IsEnabled = true;
		store.State.Monitors.Add(new MonitorConfig
		{
			DevicePath = "DISPLAY#A",
			AdapterLuidLow = 42,
			AdapterLuidHigh = 7,
			TargetId = 3,
			SdrLutPath = "calibration.cube"
		});
		store.Save();
		Assert.False(File.Exists(stateFile + ".tmp"));

		var reloaded = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml")).Load();

		Assert.True(reloaded.IsEnabled);
		Assert.Equal(42u, reloaded.Monitors[0].AdapterLuidLow);
		Assert.Equal(7, reloaded.Monitors[0].AdapterLuidHigh);
	}

	[Fact]
	public void Legacy_migration_never_enables_the_lut()
	{
		Directory.CreateDirectory(_root);
		var legacyFile = Path.Combine(_root, "config.xml");
		File.WriteAllText(legacyFile,
			"<config resolver_mode=\"auto\"><monitor path=\"DISPLAY#A\" target_id=\"2\" source_id=\"1\" sdr_lut=\"sdr.cube\" /></config>");

		var state = new AppStateStore(Path.Combine(_root, "state.json"), legacyFile).Load();

		Assert.False(state.IsEnabled);
		Assert.Equal("sdr.cube", state.Monitors[0].SdrLutPath);
	}

	[Fact]
	public void Corrupt_state_recovers_disabled_without_overwriting_the_evidence()
	{
		Directory.CreateDirectory(_root);
		var stateFile = Path.Combine(_root, "state.json");
		File.WriteAllText(stateFile, "{ this is not valid json");

		var state = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml")).Load();

		Assert.False(state.IsEnabled);
		Assert.Empty(state.Monitors);
		Assert.Equal("{ this is not valid json", File.ReadAllText(stateFile));
	}

	[Fact]
	public void Null_required_collections_are_repaired_and_enabled_intent_is_disarmed()
	{
		Directory.CreateDirectory(_root);
		var stateFile = Path.Combine(_root, "state.json");
		File.WriteAllText(stateFile, "{\"Version\":1,\"IsEnabled\":true,\"Monitors\":null,\"Library\":null}");

		var state = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml")).Load();

		Assert.False(state.IsEnabled);
		Assert.NotNull(state.Monitors);
		Assert.NotNull(state.Library);
		Assert.Empty(state.Monitors);
		Assert.Empty(state.Library);
		Assert.NotEmpty(Directory.GetFiles(_root, "state.json.invalid-*"));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Invalid_shortcut_pair_is_cleared_without_changing_saved_lut_intent(bool enabled)
	{
		var stateFile = Path.Combine(_root, "state.json");
		var store = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml"));
		store.State.IsEnabled = enabled;
		store.State.ToggleHotkey = new HotkeyBinding { VirtualKey = 0x41 };
		store.State.CycleHotkey = new HotkeyBinding { VirtualKey = 0x42, Modifiers = 0x0004 };
		store.State.StartWithWindows = true;
		store.State.Theme = ThemeMode.Dark;
		store.State.Language = "ru";
		store.State.Monitors.Add(new MonitorConfig { DevicePath = "DISPLAY#A", SdrLutPath = "calibration.cube" });
		store.Save();
		var original = File.ReadAllText(stateFile);

		var state = store.Load();

		Assert.Null(state.ToggleHotkey);
		Assert.Null(state.CycleHotkey);
		Assert.Equal(enabled, state.IsEnabled);
		Assert.True(state.StartWithWindows);
		Assert.Equal(ThemeMode.Dark, state.Theme);
		Assert.Equal("ru", state.Language);
		Assert.Equal("calibration.cube", Assert.Single(state.Monitors).SdrLutPath);
		var backup = Assert.Single(Directory.GetFiles(_root, "state.json.invalid-*"));
		Assert.Equal(original, File.ReadAllText(backup));
		Assert.Equal(original, File.ReadAllText(stateFile));

		store.Save();
		var reloaded = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml")).Load();
		Assert.Null(reloaded.ToggleHotkey);
		Assert.Null(reloaded.CycleHotkey);
		Assert.Equal(enabled, reloaded.IsEnabled);
		Assert.Single(Directory.GetFiles(_root, "state.json.invalid-*"));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Duplicate_valid_shortcuts_keep_toggle_and_clear_cycle_without_changing_lut_intent(bool enabled)
	{
		var stateFile = Path.Combine(_root, "state.json");
		var store = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml"));
		store.State.IsEnabled = enabled;
		store.State.ToggleHotkey = new HotkeyBinding { VirtualKey = 0x41, Modifiers = 0x0002 };
		store.State.CycleHotkey = new HotkeyBinding { VirtualKey = 0x41, Modifiers = 0x0002 };
		store.Save();
		var original = File.ReadAllText(stateFile);

		var state = store.Load();

		Assert.Equal(enabled, state.IsEnabled);
		Assert.NotNull(state.ToggleHotkey);
		Assert.Equal(0x41u, state.ToggleHotkey.VirtualKey);
		Assert.Equal(0x0002u, state.ToggleHotkey.Modifiers);
		Assert.Null(state.CycleHotkey);
		var backup = Assert.Single(Directory.GetFiles(_root, "state.json.invalid-*"));
		Assert.Equal(original, File.ReadAllText(backup));
	}

	[Fact]
	public void Shortcut_repairs_do_not_override_structural_safety_repairs()
	{
		Directory.CreateDirectory(_root);
		var stateFile = Path.Combine(_root, "state.json");
		var original = "{\"Version\":1,\"IsEnabled\":true,\"Monitors\":null,\"Library\":[],"
			+ "\"ToggleHotkey\":{\"VirtualKey\":65,\"Modifiers\":0},\"CycleHotkey\":{\"VirtualKey\":66,\"Modifiers\":2}}";
		File.WriteAllText(stateFile, original);

		var state = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml")).Load();

		Assert.False(state.IsEnabled);
		Assert.Empty(state.Monitors);
		Assert.Null(state.ToggleHotkey);
		Assert.NotNull(state.CycleHotkey);
		Assert.True(state.CycleHotkey.IsValid);
		Assert.Equal(0x42u, state.CycleHotkey.VirtualKey);
		var backup = Assert.Single(Directory.GetFiles(_root, "state.json.invalid-*"));
		Assert.Equal(original, File.ReadAllText(backup));
	}

	[Fact]
	public void Different_valid_shortcuts_do_not_require_a_backup_or_change_lut_intent()
	{
		var stateFile = Path.Combine(_root, "state.json");
		var store = new AppStateStore(stateFile, Path.Combine(_root, "legacy.xml"));
		store.State.IsEnabled = true;
		store.State.ToggleHotkey = new HotkeyBinding { VirtualKey = 0x41, Modifiers = 0x0002 };
		store.State.CycleHotkey = new HotkeyBinding { VirtualKey = 0x87 };
		store.Save();

		var state = store.Load();

		Assert.True(state.IsEnabled);
		Assert.NotNull(state.ToggleHotkey);
		Assert.NotNull(state.CycleHotkey);
		Assert.Equal(0x41u, state.ToggleHotkey.VirtualKey);
		Assert.Equal(0x87u, state.CycleHotkey.VirtualKey);
		Assert.Empty(Directory.GetFiles(_root, "state.json.invalid-*"));
	}

	public void Dispose()
	{
		if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
	}
}
