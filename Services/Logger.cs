using System.Collections.Concurrent;
using System.IO;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Async batch logger. Hot path never blocks — all writes go through
    /// a ConcurrentQueue drained by a single background thread every 50ms.
    /// Includes log rotation at 32 MB and soft cap at 50,000 queued lines.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCUOTracker", "app.log");

        private static readonly ConcurrentQueue<string> _queue = new();
        private static readonly AutoResetEvent _signal = new(false);
        private static readonly Thread _writer;
        private static volatile bool _stopping;
        private const int MaxQueuedLines = 50_000;
        private const long MaxFileSizeBytes = 32 * 1024 * 1024; // 32 MB

        static Logger()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); } catch { }
            _writer = new Thread(BackgroundWrite)
            {
                IsBackground = true,
                Name = "DCUO-LogWriter",
                Priority = ThreadPriority.BelowNormal
            };
            _writer.Start();
        }

        public static void Error(string ctx, Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{ex.GetType().Name}] {ex.Message}");
            sb.Append(ex.StackTrace);
            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendLine($"\n--- Inner: [{inner.GetType().Name}] {inner.Message}");
                sb.Append(inner.StackTrace);
                inner = inner.InnerException;
            }
            Write("ERROR", $"[{ctx}] {sb}");
        }

        public static void Warn(string ctx, string msg)  => Write("WARN",  $"[{ctx}] {msg}");
        public static void Info(string ctx, string msg)  => Write("INFO",  $"[{ctx}] {msg}");

        private static void Write(string level, string message)
        {
            if (_queue.Count >= MaxQueuedLines)
            {
                _queue.Enqueue($"[DROPPED] queue full");
                return;
            }
            // INFO-3 fix: UTC timestamps for unambiguous cross-timezone/DST logs
            _queue.Enqueue($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z [{level}] {message}");
            _signal.Set();
        }

        private static void BackgroundWrite()
        {
            var sb = new System.Text.StringBuilder();
            while (!_stopping || !_queue.IsEmpty)
            {
                _signal.WaitOne(50);
                sb.Clear();
                while (_queue.TryDequeue(out var line))
                    sb.AppendLine(line);

                if (sb.Length == 0) continue;
                try
                {
                    // MED-9 fix: rolling rotation .1 → .2 before overwriting
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxFileSizeBytes)
                    {
                        if (File.Exists(LogPath + ".2")) File.Delete(LogPath + ".2");
                        if (File.Exists(LogPath + ".1")) File.Move(LogPath + ".1", LogPath + ".2");
                        File.Move(LogPath, LogPath + ".1", overwrite: true);
                    }

                    File.AppendAllText(LogPath, sb.ToString());
                }
                catch { /* logger must never throw */ }
            }
        }

        public static void Shutdown()
        {
            _stopping = true;
            _signal.Set();
            _writer.Join(2000);
        }
    }
}
