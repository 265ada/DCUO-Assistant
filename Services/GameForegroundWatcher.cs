using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Polls foreground window at 1 Hz.
    /// Fires GameGainedFocus / GameLostFocus based on whether DCGAME.EXE is active.
    /// Respects PersistOverlay setting to skip hiding on alt-tab.
    /// </summary>
    public class GameForegroundWatcher : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private bool _lastWasGame = false;
        private nint _lastHwnd   = nint.Zero;
        private bool _disposed;

        public bool PersistOverlay { get; set; } = false;
        public bool IsGameForeground => _lastWasGame;

        public event Action? GameGainedFocus;
        public event Action? GameLostFocus;

        [DllImport("user32.dll", SetLastError = true)]
        static extern nint GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);

        public GameForegroundWatcher()
        {
            _timer = new System.Timers.Timer(1000);
            // MED-7 fix: log unexpected exceptions in poll
            _timer.Elapsed += (_, _) =>
            {
                try { Poll(); }
                catch (Exception ex) { Logger.Error("FgWatcher.Poll", ex); }
            };
        }

        public void Start() => _timer.Start();
        public void Stop()  => _timer.Stop();

        private void Poll()
        {
            if (PersistOverlay) return;

            var hwnd = GetForegroundWindow();
            if (hwnd == _lastHwnd) return; // same window, skip process lookup
            _lastHwnd = hwnd;

            bool isGame = false;
            try
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                using var proc = Process.GetProcessById((int)pid);
                isGame = proc.ProcessName.Equals("DCGAME", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            if (isGame == _lastWasGame) return;
            _lastWasGame = isGame;

            if (isGame) GameGainedFocus?.Invoke();
            else        GameLostFocus?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
