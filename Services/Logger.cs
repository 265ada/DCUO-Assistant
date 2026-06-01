using System.IO;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Simple append-only file logger — replaces bare catch{} blocks.
    /// Thread-safe via lock. Never throws.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCUOTracker", "app.log");

        private static readonly object _lock = new();

        public static void Error(string context, Exception ex)
            => Write("ERROR", $"[{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        public static void Warn(string context, string message)
            => Write("WARN", $"[{context}] {message}");

        public static void Info(string context, string message)
            => Write("INFO", $"[{context}] {message}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    File.AppendAllText(LogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { /* logger must never throw */ }
        }
    }
}
