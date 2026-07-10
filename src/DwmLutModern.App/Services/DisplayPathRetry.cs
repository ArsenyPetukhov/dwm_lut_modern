using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using WindowsDisplayAPI.DisplayConfig;

namespace DwmLutModern.Services
{
	/// <summary>Bounded retry for the documented CCD topology-size race.</summary>
	public static class DisplayPathRetry
	{
		private const int ErrorInsufficientBuffer = 122;

		public static T Execute<T>(Func<T> query, int maxAttempts = 3)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

			for (var attempt = 1; ; attempt++)
			{
				try { return query(); }
				catch (Exception ex) when (attempt < maxAttempts && IsInsufficientBuffer(ex))
				{
					// Query again from buffer sizing; never reuse a partially observed path set.
				}
			}
		}

		private static bool IsInsufficientBuffer(Exception exception)
		{
			if (exception is Win32Exception win32) return win32.NativeErrorCode == ErrorInsufficientBuffer;
			if (exception is ExternalException external) return (external.ErrorCode & 0xFFFF) == ErrorInsufficientBuffer;
			return false;
		}
	}

	public sealed class WindowsDisplayPathProvider : IDisplayPathProvider
	{
		public PathInfo[] GetActivePaths() =>
			DisplayPathRetry.Execute(() => PathInfo.GetActivePaths());
	}
}
