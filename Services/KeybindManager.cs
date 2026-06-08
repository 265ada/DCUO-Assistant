namespace DCUOTracker.Services
{
    /// <summary>
    /// Manages a set of named global hotkeys. Each action can be registered,
    /// re-registered (live rebind), or unregistered independently.
    /// </summary>
    public class KeybindManager : IDisposable
    {
        private readonly System.Windows.Window _owner;
        private readonly Dictionary<string, GlobalHotkey> _active = new();
        private bool _disposed;

        public KeybindManager(System.Windows.Window owner) => _owner = owner;

        /// <summary>Register (or re-register) a named action hotkey.</summary>
        public void Register(string actionId, uint mod, uint vk, Action callback)
        {
            Unregister(actionId);        // dispose previous bind for this action
            if (vk == 0) return;         // 0 = unassigned — nothing to register

            var hk = new GlobalHotkey(_owner, mod, vk);
            hk.HotkeyPressed += callback;
            _active[actionId] = hk;
        }

        /// <summary>Unregister and dispose the hotkey for a named action.</summary>
        public void Unregister(string actionId)
        {
            if (_active.TryGetValue(actionId, out var hk))
            {
                hk.Dispose();
                _active.Remove(actionId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var hk in _active.Values) hk.Dispose();
            _active.Clear();
        }
    }
}
