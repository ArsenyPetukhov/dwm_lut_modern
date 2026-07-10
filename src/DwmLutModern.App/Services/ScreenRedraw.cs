using System;
using System.Runtime.InteropServices;

namespace DwmLutModern.Services
{
	/// <summary>
	/// Invalidates the desktop after the native shader is applied or removed, then waits for the
	/// calling process's queued DWM surface work. This avoids the legacy color change appearing in
	/// chunks without constructing a temporary transparent top-most WPF window.
	/// </summary>
	public static class ScreenRedraw
	{
		private const uint RdwInvalidate = 0x0001;
		private const uint RdwErase = 0x0004;
		private const uint RdwAllChildren = 0x0080;
		private const uint RdwUpdateNow = 0x0100;

		[DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr window, IntPtr updateRect, IntPtr updateRegion, uint flags);

		[DllImport("dwmapi.dll")]
		private static extern int DwmFlush();

		public static void Flash()
		{
			try
			{
				RedrawWindow(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
					RdwInvalidate | RdwErase | RdwAllChildren | RdwUpdateNow);
				DwmFlush();
			}
			catch
			{
				// Redraw is best-effort. Injection state remains authoritative.
			}
		}
	}
}
