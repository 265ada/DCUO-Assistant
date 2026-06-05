using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Toggles mouse click-through (WS_EX_TRANSPARENT) on a borderless WPF overlay window.
    /// When click-through is ON, mouse clicks pass straight to the game behind it.
    /// </summary>
    public static class ClickThrough
    {
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public static void Apply(Window window, bool clickThrough)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return; // no handle yet — caller re-applies on source-init
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                ex |= WS_EX_LAYERED;
                if (clickThrough) ex |=  WS_EX_TRANSPARENT;
                else              ex &= ~WS_EX_TRANSPARENT;
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            }
            catch (Exception ex) { Logger.Error("ClickThrough.Apply", ex); }
        }
    }
}
