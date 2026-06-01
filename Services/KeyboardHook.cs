using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Low-level global keyboard hook.
    /// NOTE: WH_KEYBOARD_LL is a system-wide hook and will be flagged by AV as keylogger-like.
    /// All suppression logic is guarded by DcuoIsFocused() in ChatTracker.
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;

        public event EventHandler<KeyPressedArgs>? KeyPressed;

        /// <summary>
        /// If returns true for a key, the keypress is swallowed before reaching any app.
        /// Kept internal — only ChatTracker should set this.
        /// </summary>
        internal Func<System.Windows.Forms.Keys, bool>? ShouldSuppress { get; set; }

        private readonly nint                 _hookId;
        private readonly LowLevelKeyboardProc _proc;
        private bool _disposed;

        public KeyboardHook()
        {
            _proc   = HookCallback;
            _hookId = SetHook(_proc);

            if (_hookId == nint.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Failed to install keyboard hook. Check process permissions.");
        }

        private nint SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule!;
            // Use user32.dll handle — more robust than main module in .NET hosting
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle("user32.dll"), 0);
        }

        private nint HookCallback(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                int  vkCode = Marshal.ReadInt32(lParam);
                var  key    = (System.Windows.Forms.Keys)vkCode;

                try
                {
                    if (ShouldSuppress != null && ShouldSuppress(key))
                        return (nint)1;

                    KeyPressed?.Invoke(this, new KeyPressedArgs(key));
                }
                catch (Exception ex)
                {
                    Logger.Error("KeyboardHook.HookCallback", ex);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ShouldSuppress = null;
            UnhookWindowsHookEx(_hookId);
        }

        private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern nint GetModuleHandle(string lpModuleName);
    }

    public class KeyPressedArgs : EventArgs
    {
        public System.Windows.Forms.Keys Key { get; }
        public KeyPressedArgs(System.Windows.Forms.Keys key) => Key = key;
    }
}
