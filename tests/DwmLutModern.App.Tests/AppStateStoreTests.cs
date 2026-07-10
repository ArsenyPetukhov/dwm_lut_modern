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

	public void Dispose()
	{
		if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
	}
}
