using System.Text.Json.Serialization;

namespace DwmLutModern.Models
{
    /// <summary>Which signal domains a display's LUTs are allowed to apply to.</summary>
    public enum LutMode
    {
        /// <summary>Apply the SDR LUT in SDR mode and the HDR LUT in HDR mode (auto-switch).</summary>
        Auto,
        /// <summary>Only ever apply the SDR LUT; never stage/apply an HDR LUT.</summary>
        SdrOnly,
        /// <summary>Only ever apply the HDR LUT; never stage/apply an SDR LUT.</summary>
        HdrOnly
    }

    /// <summary>
    /// Persisted per-monitor LUT assignment. Keyed to stable display identity
    /// (<see cref="DevicePath"/> / <see cref="TargetId"/>) rather than screen position, so a
    /// monitor keeps its LUT across unplug/replug and rearrangement — addressing the
    /// coordinate-drift class of multi-monitor bugs on recent Windows builds.
    ///
    /// <para>Instances in <c>AppState.Monitors</c> are the single source of truth: the UI binds
    /// to these exact objects, and display enumeration updates them in place, so an edit is
    /// never lost to a divergent "live" copy.</para>
    /// </summary>
    public sealed class MonitorConfig
    {
        /// <summary>Stable OS device path, e.g. <c>\\?\DISPLAY#...</c>. Primary identity key.</summary>
        public string DevicePath { get; set; } = string.Empty;

		/// <summary>Display target id (secondary identity key).</summary>
		public uint TargetId { get; set; }

		/// <summary>Low 32 bits of the adapter LUID that scopes TargetId.</summary>
		public uint AdapterLuidLow { get; set; }

		/// <summary>High 32 bits of the adapter LUID that scopes TargetId.</summary>
		public int AdapterLuidHigh { get; set; }

        /// <summary>1-based display source id, matching the number Windows shows.</summary>
        public uint SourceId { get; set; }

        /// <summary>Friendly monitor name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Connector type, e.g. "DisplayPort", "HDMI".</summary>
        public string Connector { get; set; } = string.Empty;

        /// <summary>Desktop position as "x,y" — used by the native DLL to name staged LUT files.</summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>Path to the LUT applied when the display is in SDR mode. Null/empty = none.</summary>
        public string? SdrLutPath { get; set; }

        /// <summary>Path to the LUT applied when the display is in HDR mode. Null/empty = none.</summary>
        public string? HdrLutPath { get; set; }

        /// <summary>Which domains this display's LUTs may apply to. Default: auto-switch by mode.</summary>
        public LutMode LutMode { get; set; } = LutMode.Auto;

        /// <summary>SDR LUT to actually stage, honoring <see cref="LutMode"/> (null = don't stage SDR).</summary>
        [JsonIgnore]
        public string? EffectiveSdrLut => LutMode == LutMode.HdrOnly ? null : SdrLutPath;

        /// <summary>HDR LUT to actually stage, honoring <see cref="LutMode"/> (null = don't stage HDR).</summary>
        [JsonIgnore]
        public string? EffectiveHdrLut => LutMode == LutMode.SdrOnly ? null : HdrLutPath;

        /// <summary>True if, after applying the mode, there is anything to stage for this display.</summary>
        [JsonIgnore]
        public bool HasEffectiveLut =>
            !string.IsNullOrEmpty(EffectiveSdrLut) || !string.IsNullOrEmpty(EffectiveHdrLut);

        /// <summary>SDR LUTs previously assigned to this display — the quick-switch/cycle history.</summary>
        public System.Collections.Generic.List<string> SdrLuts { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>HDR LUTs previously assigned to this display — the quick-switch/cycle history.</summary>
        public System.Collections.Generic.List<string> HdrLuts { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>True while this display is currently attached. Transient (not persisted).</summary>
        [JsonIgnore]
        public bool IsConnected { get; set; }

        /// <summary>True while Windows HDR / advanced color is on for this display. Transient.</summary>
        [JsonIgnore]
        public bool IsHdrActive { get; set; }

		[JsonIgnore]
		public bool HasAnyLut =>
			!string.IsNullOrEmpty(SdrLutPath) || !string.IsNullOrEmpty(HdrLutPath);

		[JsonIgnore]
		public DisplayIdentity Identity =>
			new DisplayIdentity(DevicePath, AdapterLuidLow, AdapterLuidHigh, TargetId);

        /// <summary>Assign the SDR LUT and remember it in this display's history.</summary>
        public void SetSdrLut(string? path)
        {
            SdrLutPath = string.IsNullOrEmpty(path) ? null : path;
            Remember(SdrLutPath, SdrLuts);
        }

        /// <summary>Assign the HDR LUT and remember it in this display's history.</summary>
        public void SetHdrLut(string? path)
        {
            HdrLutPath = string.IsNullOrEmpty(path) ? null : path;
            Remember(HdrLutPath, HdrLuts);
        }

        private static void Remember(string? path, System.Collections.Generic.List<string> history)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!history.Exists(p => string.Equals(p, path, System.StringComparison.OrdinalIgnoreCase)))
            {
                history.Add(path);
            }
        }
    }
}
