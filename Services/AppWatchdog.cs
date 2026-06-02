namespace DCUOTracker.Services
{
    public class AppWatchdog : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private int  _tickCount;
        private int  _overlayUpdates;
        private bool _disposed;

        public void RecordOverlayUpdate() =>
            System.Threading.Interlocked.Increment(ref _overlayUpdates);

        public AppWatchdog()
        {
            _timer = new System.Threading.Timer(Tick, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void Tick(object? _)
        {
            if (_disposed) return;
            // MED-6 fix: never swallow exceptions silently in watchdog
            try
            {
                int tick    = System.Threading.Interlocked.Increment(ref _tickCount);
                int updates = System.Threading.Interlocked.Exchange(ref _overlayUpdates, 0);
                long mem    = GC.GetTotalMemory(false) / 1024 / 1024;
                Logger.Info("Watchdog", $"Heartbeat #{tick} | updates_30s={updates} | mem={mem}MB");
            }
            catch (Exception ex)
            {
                // Secondary write — can't use main logger if it's the problem
                try { Logger.Error("Watchdog.Tick", ex); } catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }
}
