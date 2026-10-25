using System;

namespace DwmLutModern.Models
{
	/// <summary>
	/// Persistent monitor path plus the adapter-scoped target that currently represents it.
	/// Target IDs are only meaningful together with the adapter LUID.
	/// </summary>
	public readonly struct DisplayIdentity
	{
		public DisplayIdentity(string? devicePath, uint adapterLuidLow, int adapterLuidHigh, uint targetId)
		{
			DevicePath = devicePath ?? string.Empty;
			AdapterLuidLow = adapterLuidLow;
			AdapterLuidHigh = adapterLuidHigh;
			TargetId = targetId;
		}

		public string DevicePath { get; }
		public uint AdapterLuidLow { get; }
		public int AdapterLuidHigh { get; }
		public uint TargetId { get; }

		public bool HasDevicePath => !string.IsNullOrWhiteSpace(DevicePath);
		public bool HasAdapterTarget => AdapterLuidLow != 0 || AdapterLuidHigh != 0;

		public bool Matches(DisplayIdentity other)
		{
			if (HasDevicePath && other.HasDevicePath)
			{
				return string.Equals(DevicePath, other.DevicePath, StringComparison.OrdinalIgnoreCase);
			}

			return HasAdapterTarget && other.HasAdapterTarget &&
				AdapterLuidLow == other.AdapterLuidLow &&
				AdapterLuidHigh == other.AdapterLuidHigh &&
				TargetId == other.TargetId;
		}

		public override string ToString()
		{
			return HasDevicePath
				? DevicePath
				: AdapterLuidHigh.ToString("X8") + AdapterLuidLow.ToString("X8") + ":" + TargetId;
		}
	}
}
