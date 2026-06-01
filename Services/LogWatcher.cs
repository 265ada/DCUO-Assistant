using System.IO;
using System.Text.RegularExpressions;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    public class DropEventArgs : EventArgs
    {
        public NthMetalDrop Drop { get; }
        public DropEventArgs(NthMetalDrop drop) => Drop = drop;
    }

    public class ItemDropEventArgs : EventArgs
    {
        public ItemDrop Drop { get; }
        public ItemDropEventArgs(ItemDrop drop) => Drop = drop;
    }

    public class LogWatcher : IDisposable
    {
        private static readonly Regex NthMetalPattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+(Raw|Extracted|Treated|Processed|Refined|Purified)\s+Nth\s+Metals?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SourceMarkPattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+Source\s+Marks?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AllyFavorPattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+Ally\s+Favors?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PerfectedExobytePattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+(Perfected\s+Exobyte\s+-\s+Gen\.\s*\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ExobytePattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+(.+?)\s+Exobytes?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AlliancePattern = new(
            @"\[Items\]\s+(.+?)\s+received\s+(\d+)\s+(Rare|Epic|Legendary)\s+Alliance",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // H-5: validate log path stays within expected directory
        private static readonly string ExpectedBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "DC Universe Online", "Logs");

        private readonly string _logPath;
        private long _lastPosition;
        private readonly System.Timers.Timer _timer;
        private bool _disposed;

        public string SessionId { get; } = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        public event EventHandler<DropEventArgs>?     DropDetected;
        public event EventHandler<ItemDropEventArgs>? ItemDropDetected;

        public LogWatcher(string logPath)
        {
            // H-5: canonicalize + validate path
            var canonical = Path.GetFullPath(logPath);
            if (!canonical.StartsWith(ExpectedBase, StringComparison.OrdinalIgnoreCase))
                Logger.Warn("LogWatcher", $"Log path outside expected directory: {canonical}");

            _logPath = canonical;

            if (File.Exists(_logPath))
                _lastPosition = new FileInfo(_logPath).Length;

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += PollSafe;
        }

        public void Start() => _timer.Start();

        public void Stop()
        {
            _timer.Stop(); // M-9: Stop before Dispose
        }

        // H-3: proper try/catch on timer callback — not async void
        private void PollSafe(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try { Poll(); }
            catch (Exception ex) { Logger.Error("LogWatcher.Poll", ex); }
        }

        private void Poll()
        {
            if (_disposed || !File.Exists(_logPath)) return;

            using var fs     = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var m = NthMetalPattern.Match(line);
                if (m.Success)
                {
                    DropDetected?.Invoke(this, new DropEventArgs(new NthMetalDrop
                    {
                        Timestamp = DateTime.Now,
                        Character = m.Groups[1].Value,
                        Quantity  = int.Parse(m.Groups[2].Value),
                        MetalType = m.Groups[3].Value,
                        XpValue   = NthMetalDrop.GetXpValue(m.Groups[3].Value),
                        Session   = SessionId
                    }));
                    continue;
                }

                m = SourceMarkPattern.Match(line);
                if (m.Success) { FireItem(m.Groups[1].Value, int.Parse(m.Groups[2].Value), ItemDropType.SourceMark, "Source Mark"); continue; }

                m = AllyFavorPattern.Match(line);
                if (m.Success) { FireItem(m.Groups[1].Value, int.Parse(m.Groups[2].Value), ItemDropType.AllyFavor, "Ally Favor"); continue; }

                m = PerfectedExobytePattern.Match(line);
                if (m.Success) { FireItem(m.Groups[1].Value, int.Parse(m.Groups[2].Value), ItemDropType.Exobyte, m.Groups[3].Value.Trim()); continue; }

                m = ExobytePattern.Match(line);
                if (m.Success) { FireItem(m.Groups[1].Value, int.Parse(m.Groups[2].Value), ItemDropType.Exobyte, m.Groups[3].Value.Trim()); continue; }

                m = AlliancePattern.Match(line);
                if (m.Success) { FireItem(m.Groups[1].Value, int.Parse(m.Groups[2].Value), ItemDropType.Alliance, $"{m.Groups[3].Value} Alliance"); }
            }

            _lastPosition = fs.Position;
        }

        private void FireItem(string character, int qty, ItemDropType type, string name)
        {
            ItemDropDetected?.Invoke(this, new ItemDropEventArgs(new ItemDrop
            {
                Timestamp = DateTime.Now,
                Character = character,
                Quantity  = qty,
                DropType  = type,
                ItemName  = name,
                Session   = SessionId
            }));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();    // M-9: stop before dispose
            _timer.Dispose();
        }
    }
}
