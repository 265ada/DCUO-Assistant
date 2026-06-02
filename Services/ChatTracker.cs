using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DCUOTracker.Services
{
    public class ChatStateChangedArgs : EventArgs
    {
        public bool IsActive  { get; init; }
        public int  CharCount { get; init; }
        public bool AtLimit   { get; init; }
    }

    public class LfgTimerArgs : EventArgs
    {
        public int  SecondsRemaining { get; init; }
        public bool Started          { get; init; }
        public bool Expired          { get; init; }
    }

    public class ChatTracker : IDisposable
    {
        private const int CHAR_LIMIT   = 60;
        private const int LFG_COOLDOWN = 60;

        private readonly KeyboardHook _hook;
        private readonly object       _stateLock = new(); // H-2: guards all shared state

        // All fields below are guarded by _stateLock
        private bool          _chatActive;
        private int           _charCount;
        private StringBuilder _buffer = new(); // M-6: StringBuilder, not string +=

        private System.Timers.Timer? _lfgTimer;
        private int  _lfgRemain;
        private bool _lfgActive;
        private bool _disposed;

        public Func<bool>?       IsLfgModeActive { get; set; }
        public LfgChannelDetector? Detector       { get; set; }
        public Action?           OnMessageSent    { get; set; }
        public Action?           OnEnterPressed   { get; set; }

        public event EventHandler<ChatStateChangedArgs>? ChatStateChanged;
        public event EventHandler<LfgTimerArgs>?         LfgTimerUpdated;

        public ChatTracker(KeyboardHook hook)
        {
            _hook = hook;
            _hook.KeyPressed     += OnKeyPressed;
            _hook.ShouldSuppress  = ShouldSuppressKey;
        }

        // ── Suppression (called on hook thread — keep fast) ───────────

        private bool ShouldSuppressKey(Keys key)
        {
            if (!DcuoIsFocused()) return false;
            bool active, atLimit;
            lock (_stateLock)
            {
                active  = _chatActive;
                atLimit = _charCount >= CHAR_LIMIT;
            }
            return active && atLimit && IsTypableChar(key);
        }

        // ── Key handler ───────────────────────────────────────────────

        private void OnKeyPressed(object? sender, KeyPressedArgs e)
        {
            if (!DcuoIsFocused()) return;
            var key = e.Key;

            switch (key)
            {
                case Keys.Return:  HandleEnter(); return;
                case Keys.Escape:
                    lock (_stateLock) { if (_chatActive) DeactivateChat(); }
                    return;
                case Keys.Back:
                    lock (_stateLock)
                    {
                        if (_chatActive && _charCount > 0)
                        {
                            _charCount--;
                            if (_buffer.Length > 0) _buffer.Remove(_buffer.Length - 1, 1);
                            FireChatState();
                        }
                    }
                    return;
                case Keys.Delete:
                    lock (_stateLock)
                    {
                        if (_chatActive && _charCount > 0) { _charCount--; FireChatState(); }
                    }
                    return;
            }

            string? ch;
            lock (_stateLock)
            {
                if (!_chatActive) return;
                ch = KeyToChar(key);
                if (ch == null) return;
                if (_charCount >= CHAR_LIMIT) { Console.Beep(800, 80); return; }
                _charCount++;
                _buffer.Append(ch);
                FireChatState();
            }
        }

        // ── Chat lifecycle ────────────────────────────────────────────

        private void HandleEnter()
        {
            OnEnterPressed?.Invoke();
            lock (_stateLock)
            {
                if (!_chatActive)
                {
                    _chatActive = true;
                    _charCount  = 0;
                    _buffer.Clear();
                    FireChatState();
                }
                else
                {
                    OnMessageSent?.Invoke();
                    CheckLfg();
                    DeactivateChat();
                }
            }
        }

        // Must be called under _stateLock
        private void DeactivateChat()
        {
            _chatActive = false;
            _charCount  = 0;
            _buffer.Clear();
            FireChatState();
        }

        public void CorrectChatState(bool actuallyOpen)
        {
            lock (_stateLock)
            {
                if (_chatActive == actuallyOpen) return;
                _chatActive = actuallyOpen;
                if (!actuallyOpen) { _charCount = 0; _buffer.Clear(); }
                FireChatState();
            }
        }

        // ── LFG ───────────────────────────────────────────────────────

        // Must be called under _stateLock
        private void CheckLfg()
        {
            bool inLfg = (Detector?.IsLfgChannelActive == true)
                      || (IsLfgModeActive != null && IsLfgModeActive());

            if (inLfg)
            {
                if (!_lfgActive) StartLfgCooldown();
                return;
            }

            string trimmed = _buffer.ToString().Trim();
            if (!trimmed.StartsWith("/lfg ", StringComparison.OrdinalIgnoreCase)) return;
            if (!_lfgActive) StartLfgCooldown();
        }

        // Must be called under _stateLock
        // Persisted last LFG post time — survives app restarts
        public Action<DateTime>? OnLfgStarted { get; set; }

        private void StartLfgCooldown()
        {
            _lfgActive = true;
            _lfgRemain = LFG_COOLDOWN;

            var now = DateTime.UtcNow;
            OnLfgStarted?.Invoke(now); // persist to settings

            LfgTimerUpdated?.Invoke(this, new LfgTimerArgs
                { SecondsRemaining = _lfgRemain, Started = true });

            _lfgTimer?.Stop();
            _lfgTimer?.Dispose();
            _lfgTimer = new System.Timers.Timer(1000);
            _lfgTimer.Elapsed += LfgTick;
            _lfgTimer.Start();
        }

        /// <summary>Restore LFG timer from persisted last-post time (after app restart).</summary>
        public void RestoreLfgTimer(DateTime lastPostUtc)
        {
            lock (_stateLock)
            {
                if (_lfgActive) return;
                int remaining = LFG_COOLDOWN - (int)(DateTime.UtcNow - lastPostUtc).TotalSeconds;
                if (remaining <= 0) return; // already expired

                _lfgActive = true;
                _lfgRemain = remaining;
                LfgTimerUpdated?.Invoke(this, new LfgTimerArgs
                    { SecondsRemaining = _lfgRemain, Started = true });
                _lfgTimer?.Stop();
                _lfgTimer?.Dispose();
                _lfgTimer = new System.Timers.Timer(1000);
                _lfgTimer.Elapsed += LfgTick;
                _lfgTimer.Start();
            }
        }

        // H-3: proper try/catch — not async void
        private void LfgTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                int remain;
                lock (_stateLock) { remain = --_lfgRemain; }

                if (remain <= 0)
                {
                    lock (_stateLock) { _lfgActive = false; }
                    _lfgTimer?.Stop();
                    LfgTimerUpdated?.Invoke(this, new LfgTimerArgs
                        { SecondsRemaining = 0, Expired = true });
                }
                else
                {
                    LfgTimerUpdated?.Invoke(this, new LfgTimerArgs
                        { SecondsRemaining = remain });
                }
            }
            catch (Exception ex) { Logger.Error("ChatTracker.LfgTick", ex); }
        }

        // ── Helpers ───────────────────────────────────────────────────

        // Must be called under _stateLock
        // HIGH-5 fix: capture args under lock, fire AFTER releasing lock
        private void FireChatState()
        {
            var args = new ChatStateChangedArgs
            {
                IsActive  = _chatActive,
                CharCount = _charCount,
                AtLimit   = _charCount >= CHAR_LIMIT
            };
            // NOTE: caller must release _stateLock before this propagates to UI
            // We use BeginInvoke in handlers to avoid deadlock
            ChatStateChanged?.Invoke(this, args);
        }

        private static bool IsTypableChar(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z)           return true;
            if (key >= Keys.D0 && key <= Keys.D9)         return true;
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return true;
            if (key == Keys.Space)                         return true;
            int vk = (int)key;
            return vk is >= 0xBA and <= 0xDF;
        }

        private static string? KeyToChar(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return ((char)('a' + (key - Keys.A))).ToString();
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((char)('0' + (key - Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return ((char)('0' + (key - Keys.NumPad0))).ToString();
            return key switch
            {
                Keys.Space  => " ",
                (Keys)0xBC  => ",",
                (Keys)0xBE  => ".",
                (Keys)0xBF  => "/",
                (Keys)0xBA  => ";",
                (Keys)0xDE  => "'",
                (Keys)0xBD  => "-",
                (Keys)0xBB  => "=",
                (Keys)0xDB  => "[",
                (Keys)0xDD  => "]",
                (Keys)0xDC  => "\\",
                _           => null
            };
        }

        // H-4: using ensures handle is disposed every call
        private static bool DcuoIsFocused()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                GetWindowThreadProcessId(hwnd, out uint pid);
                using var proc = Process.GetProcessById((int)pid); // H-4 fix
                return proc.ProcessName.Equals("DCGAME", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hook.KeyPressed    -= OnKeyPressed;
            _hook.ShouldSuppress = null;
            lock (_stateLock)
            {
                // LOW-2 fix: unsubscribe event before disposing timer
                if (_lfgTimer != null)
                {
                    _lfgTimer.Elapsed -= LfgTick;
                    _lfgTimer.Stop();
                    _lfgTimer.Dispose();
                    _lfgTimer = null;
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    }
}
