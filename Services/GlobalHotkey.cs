using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Registers a system-wide hotkey. Fires HotkeyPressed when triggered.
    /// Uses unique IDs to prevent cross-fire bugs.
    /// </summary>
    public class GlobalHotkey : IDisposable
    {
        private readonly Window _owner;
        private readonly int    _id;
        private HwndSource?     _source;
        private bool            _registered;
        private static int      _nextId = 9001;

        public event Action? HotkeyPressed;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(nint hWnd, int id);

        public GlobalHotkey(Window owner, uint modifiers, uint vk)
        {
            _owner = owner;
            _id    = System.Threading.Interlocked.Increment(ref _nextId);
            _pendingMod = modifiers;
            _pendingVk  = vk;
            owner.Closed += (_, _) => Dispose();

            // If the window is already loaded (e.g. live rebind after startup),
            // register immediately rather than waiting for Loaded event.
            if (owner.IsLoaded)
                RegisterNow();
            else
                owner.Loaded += OnLoaded;
        }

        private uint _pendingMod, _pendingVk;

        private void OnLoaded(object sender, RoutedEventArgs e) => RegisterNow();

        private void RegisterNow()
        {
            var helper = new WindowInteropHelper(_owner);
            _source    = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(WndProc);
            _registered = RegisterHotKey(helper.Handle, _id, _pendingMod, _pendingVk);
            if (!_registered)
                Logger.Warn("GlobalHotkey", $"RegisterHotKey failed for id={_id}");
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return nint.Zero;
        }

        private bool _disposed;
        public void Dispose()
        {
            // LOW-3 fix: guard against double-dispose
            if (_disposed) return;
            _disposed = true;
            if (_registered)
            {
                var helper = new WindowInteropHelper(_owner);
                UnregisterHotKey(helper.Handle, _id);
                _registered = false;
            }
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }
    }

    /// <summary>Common virtual key codes.</summary>
    public static class VKey
    {
        public const uint F9  = 0x78;
        public const uint F10 = 0x79;
        public const uint F11 = 0x7A;
        public const uint F12 = 0x7B;
        public const uint D0  = 0x30; // top-row '0' key
    }

    /// <summary>Modifier flags for RegisterHotKey.</summary>
    public static class Mod
    {
        public const uint None  = 0x0000;
        public const uint Alt   = 0x0001;
        public const uint Ctrl  = 0x0002;
        public const uint Shift = 0x0004;
    }
}
