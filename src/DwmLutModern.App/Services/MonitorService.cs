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
    /// display identity (device path, then target id, then source id), never by coordinates.
    /// </summary>
    public sealed class MonitorService
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
                var name = targetInfo.DisplayTarget.FriendlyName;
                if (string.IsNullOrEmpty(name)) name = "Display";

                var connector = targetInfo.OutputTechnology.ToString();
                if (connector == "DisplayPortExternal") connector = "DisplayPort";

                var sourceId = path.DisplaySource.SourceId + 1;
                var position = path.Position.X + "," + path.Position.Y;

                var config = Match(state, devicePath, deviceId, sourceId);
                if (config == null)
                {
                    config = new MonitorConfig { DevicePath = devicePath };
                    state.Monitors.Add(config);
                }

                config.DevicePath = devicePath;
                config.TargetId = deviceId;
                config.SourceId = sourceId;
                config.Name = name;
                config.Connector = connector;
                config.Position = position;
                config.IsConnected = true;
                config.IsHdrActive = IsHdrEnabled(path.DisplaySource.SourceId, deviceId);

                connected.Add(config);
            }

            return connected.OrderBy(m => m.SourceId).ToList();
        }

        private static MonitorConfig? Match(AppState state, string devicePath, uint targetId, uint sourceId)
        {
            return state.Monitors.FirstOrDefault(m => !string.IsNullOrEmpty(m.DevicePath) &&
                        string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase))
                ?? state.Monitors.FirstOrDefault(m => m.TargetId != 0 && m.TargetId == targetId)
                ?? state.Monitors.FirstOrDefault(m => m.SourceId != 0 && m.SourceId == sourceId);
        }

        // ---- HDR / advanced color detection ----------------------------------------------

        private static bool IsHdrEnabled(uint sourceId, uint targetId)
        {
            try
            {
                var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_ADVANCED_COLOR_INFO,
                        size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)),
                        adapterId = new LUID(),
                        id = targetId
                    }
                };

                // We don't have the adapter LUID from WindowsDisplayAPI here; query by target id
                // against each active path's adapter until one succeeds.
                foreach (var path in QueryPaths())
                {
                    if (path.targetInfo.id != targetId) continue;
                    info.header.adapterId = path.targetInfo.adapterId;
                    if (DisplayConfigGetDeviceInfo(ref info) == 0)
                    {
                        // Bit 1 (0x2) of value = advanced color enabled (HDR active).
                        return (info.value & 0x2) != 0;
                    }
                }
            }
            catch
            {
                // If detection fails, assume SDR — never block on it.
            }

            return false;
        }

        private static IEnumerable<DISPLAYCONFIG_PATH_INFO> QueryPaths()
        {
            uint pathCount = 0, modeCount = 0;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != 0)
                yield break;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                yield break;

            for (var i = 0; i < pathCount; i++) yield return paths[i];
        }

        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public uint outputTechnology; public uint rotation; public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate; public uint scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DISPLAYCONFIG_MODE_INFO { }

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
