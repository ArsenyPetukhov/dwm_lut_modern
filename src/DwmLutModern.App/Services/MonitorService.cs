using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DwmLutModern.Models;
using WindowsDisplayAPI.DisplayConfig;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Enumerates attached displays and reconciles them into the canonical
    /// <c>AppState.Monitors</c> list — updating names/positions/HDR state and adding new displays
	/// while preserving saved LUT assignments. Returns the same objects that live in state, so
	/// the UI edits exactly what gets persisted (no divergent "live" copy). Matching is by stable
	/// device path, then the adapter-LUID/target-ID pair; source IDs and coordinates never claim a
	/// saved assignment because both are topology-local.
    /// </summary>
	public sealed class MonitorService : IMonitorService
    {
        /// <summary>
        /// Update <paramref name="state"/> in place from the currently attached displays and
        /// return the connected monitors (references into <c>state.Monitors</c>), in source order.
        /// </summary>
        public IReadOnlyList<MonitorConfig> Reconcile(AppState state)
        {
            foreach (var monitor in state.Monitors) monitor.IsConnected = false;

            var connected = new List<MonitorConfig>();

            foreach (var path in PathInfo.GetActivePaths())
            {
                if (path.IsCloneMember) continue;

			var targetInfo = path.TargetsInfo[0];
			var deviceId = targetInfo.DisplayTarget.TargetId;
			var devicePath = targetInfo.DisplayTarget.DevicePath;
			var adapterId = targetInfo.DisplayTarget.Adapter.AdapterId;
			var identity = new DisplayIdentity(devicePath, adapterId.LowPart, adapterId.HighPart, deviceId);
                var name = targetInfo.DisplayTarget.FriendlyName;
                if (string.IsNullOrEmpty(name)) name = "Display";

                var connector = targetInfo.OutputTechnology.ToString();
                if (connector == "DisplayPortExternal") connector = "DisplayPort";

                var sourceId = path.DisplaySource.SourceId + 1;
                var position = path.Position.X + "," + path.Position.Y;

			var config = Match(state, identity);
                if (config == null)
                {
                    config = new MonitorConfig { DevicePath = devicePath };
                    state.Monitors.Add(config);
                }

			config.DevicePath = devicePath;
			config.TargetId = deviceId;
			config.AdapterLuidLow = adapterId.LowPart;
			config.AdapterLuidHigh = adapterId.HighPart;
                config.SourceId = sourceId;
                config.Name = name;
                config.Connector = connector;
                config.Position = position;
                config.IsConnected = true;
			config.IsHdrActive = IsHdrEnabled(adapterId.LowPart, adapterId.HighPart, deviceId);

                connected.Add(config);
            }

            return connected.OrderBy(m => m.SourceId).ToList();
        }

	private static MonitorConfig? Match(AppState state, DisplayIdentity identity)
	{
		return state.Monitors.FirstOrDefault(m => m.Identity.Matches(identity));
	}

        // ---- HDR / advanced color detection ----------------------------------------------

	private static bool IsHdrEnabled(uint adapterLuidLow, int adapterLuidHigh, uint targetId)
        {
            try
            {
                var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_ADVANCED_COLOR_INFO,
                        size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)),
				adapterId = new LUID { LowPart = adapterLuidLow, HighPart = adapterLuidHigh },
				id = targetId
			}
		};

		if (DisplayConfigGetDeviceInfo(ref info) == 0)
		{
			// Bit 1 (0x2) of value = advanced color enabled (HDR active).
			return (info.value & 0x2) != 0;
		}
            }
            catch
            {
                // If detection fails, assume SDR — never block on it.
            }

            return false;
        }

	[DllImport("user32.dll")]
	private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        private enum DISPLAYCONFIG_DEVICE_INFO_TYPE { GET_ADVANCED_COLOR_INFO = 9 }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value; // bit0: advancedColorSupported, bit1: advancedColorEnabled
            public uint colorEncoding;
            public uint bitsPerColorChannel;
        }
    }
}
