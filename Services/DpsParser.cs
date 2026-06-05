using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    public class FightEventArgs : EventArgs
    {
        public FightData Fight { get; }
        public FightEventArgs(FightData fight) => Fight = fight;
    }

    public class DpsParser : IDisposable
    {
        private static readonly Regex LineRegex = new(
            @"^(\d{16})\s+(\{.+?\})\s+\[(\w+)\s+(\w+)\]\s+(.+)$",
            RegexOptions.Compiled);

        private static readonly Regex DmgText = new(
            @"^(.+?)'s\s+(.+?)\s+(critically damaged|damaged|knocked out)\s+(.+?)\s+for\s+([\d,]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HealText = new(
            @"^(.+?)'s\s+(.+?)\s+healed\s+(.+?)\s+for\s+([\d,]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "X's Ability knocked out Y" (no "for N") — pure knockout/death event
        private static readonly Regex KoText = new(
            @"^(.+?)'s\s+(.+?)\s+knocked out\s+(.+?)\.?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Combat timeout: how long with NO damage before a fight is considered over.
        // Was 5s — too short, it reset mid-encounter on every lull (boss pauses, running to
        // adds, mechanics) and wiped your metrics. 18s rides through lulls but still separates
        // distinct sparring parses / pulls. Stall detection still pauses live-DPS decay at 5s.
        private const double FIGHT_GAP_SECONDS  = 18.0;
        private const double STALL_THRESHOLD    = 5.0;
        private const long   UNIX_MICRO_EPOCH   = 621355968000000000L; // ticks at Unix epoch

        private readonly string _logPath;
        private long _lastPos;
        private readonly System.Timers.Timer _timer;
        private bool _disposed;

        private FightData? _currentFight;
        private DateTime   _lastEventTime  = DateTime.MinValue;
        private DateTime   _lastPacketTime = DateTime.Now;
        private bool       _isStalled      = false;
        private DateTime   _lastSparkSample = DateTime.MinValue;

        // HIGH-2 fix: lock protects _rollWindow and History
        private readonly object _histLock = new();
        private readonly Dictionary<string, List<(DateTime t, long dmg)>> _rollWindow
            = new(StringComparer.OrdinalIgnoreCase);

        public FightData? CurrentFight => _currentFight;

        private readonly List<FightData> _history = new();
        public IReadOnlyList<FightData> History
        {
            get { lock (_histLock) { return _history.ToList(); } }
        }

        public event EventHandler<FightEventArgs>? FightUpdated;
        public event EventHandler<FightEventArgs>? FightEnded;
        public event EventHandler<FightEventArgs>? FightStarted;

        public DpsParser(string logPath)
        {
            _logPath = logPath;
            if (File.Exists(_logPath))
                _lastPos = new FileInfo(_logPath).Length;

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (_, _) => PollSafe();
        }

        public void Start() => _timer.Start();
        public void Stop()  => _timer.Stop();

        private void PollSafe()
        {
            try { Poll(); }
            catch (Exception ex) { Logger.Error("DpsParser.Poll", ex); }
        }

        private void Poll()
        {
            if (!File.Exists(_logPath)) return;

            using var fs     = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastPos, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            bool hadLines = false;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                ProcessLine(line);
                hadLines = true;
            }
            _lastPos = fs.Position;

            // Stall detection
            if (hadLines)
            {
                _lastPacketTime = DateTime.Now;
                if (_isStalled) { _isStalled = false; Logger.Info("DpsParser", "Stall resolved"); }
            }
            else
            {
                if (!_isStalled && (DateTime.Now - _lastPacketTime).TotalSeconds >= STALL_THRESHOLD)
                {
                    _isStalled = true;
                    Logger.Info("DpsParser", "Stall detected — pausing DPS decay");
                }
            }

            if (_currentFight != null && _currentFight.IsActive)
            {
                double gap = (DateTime.Now - _lastEventTime).TotalSeconds;
                if (gap >= FIGHT_GAP_SECONDS)
                    EndFight();
                else
                {
                    if (!_isStalled)
                        UpdateLiveDps();

                    // Sparkline every 5s
                    if ((DateTime.Now - _lastSparkSample).TotalSeconds >= 5)
                    {
                        _lastSparkSample = DateTime.Now;
                        float elapsed = (float)_currentFight.Duration.TotalSeconds;
                        foreach (var ps in _currentFight.Players.Values)
                        {
                            float dps = elapsed > 0 ? (float)(ps.TotalDamage / elapsed) : 0;
                            ps.AddSparkPoint(elapsed, dps);
                        }
                    }

                    FightUpdated?.Invoke(this, new FightEventArgs(_currentFight));
                }
            }
        }

        private void ProcessLine(string line)
        {
            var m = LineRegex.Match(line);
            if (!m.Success) return;

            string eventType = m.Groups[3].Value;
            string direction = m.Groups[4].Value;
            string text      = m.Groups[5].Value;
            string json      = m.Groups[2].Value;

            // MED-8 fix: use log timestamp, fall back to DateTime.Now
            DateTime eventTime = ParseLogTimestamp(m.Groups[1].Value);

            // MED-3 fix: JsonDocument disposed properly after use
            JsonElement? j = null;
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                j   = doc.RootElement;
            }
            catch { }

            try
            {
                if (eventType == "Damage" && direction == "Out")
                    HandleDamage(text, j, eventTime);
                else if (eventType == "Damage" && direction == "In")
                    HandleIncoming(text, j, eventTime);
                else if (eventType == "Healing" && direction == "Out")
                    HandleHeal(text, j, eventTime);
                else if (eventType == "Power" && direction == "Out")
                    HandlePower(text, j, eventTime);
            }
            finally
            {
                doc?.Dispose();
            }
        }

        // MED-8: parse 16-digit microsecond log timestamp
        private static DateTime ParseLogTimestamp(string raw)
        {
            if (long.TryParse(raw, out long micros))
            {
                try
                {
                    long ticks = UNIX_MICRO_EPOCH + micros * 10; // micros → 100ns ticks
                    return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                }
                catch { }
            }
            return DateTime.Now;
        }

        private void HandleDamage(string text, JsonElement? j, DateTime eventTime)
        {
            var m = DmgText.Match(text);
            if (!m.Success) return;

            string source  = JsonExtensions.TryGetProp(j, "inm") ?? m.Groups[1].Value;
            string ability = JsonExtensions.TryGetProp(j, "anm") ?? m.Groups[2].Value;
            bool   crit    = m.Groups[3].Value.Contains("critically", StringComparison.OrdinalIgnoreCase);
            string target  = JsonExtensions.TryGetProp(j, "tnm") ?? m.Groups[4].Value;
            long   dmg     = ParseValue(m.Groups[5].Value);

            if (dmg <= 0) return;

            EnsureFight(target, eventTime);
            _lastEventTime = eventTime;

            var player = _currentFight!.GetOrAdd(source);
            player.AddHit(ability, dmg, crit, target, eventTime);

            // HIGH-2 fix: lock roll window
            lock (_histLock)
            {
                if (!_rollWindow.TryGetValue(source, out var list))
                {
                    list = new List<(DateTime, long)>();
                    _rollWindow[source] = list;
                }
                list.Add((DateTime.Now, dmg));
            }
        }

        // Incoming damage to the player/pets — feeds death recap. Never starts a fight.
        private void HandleIncoming(string text, JsonElement? j, DateTime eventTime)
        {
            if (_currentFight == null || !_currentFight.IsActive) return;
            double sec = (eventTime - _currentFight.StartTime).TotalSeconds;

            var m = DmgText.Match(text);
            if (m.Success)
            {
                string src    = JsonExtensions.TryGetProp(j, "inm") ?? m.Groups[1].Value;
                string ability= JsonExtensions.TryGetProp(j, "anm") ?? m.Groups[2].Value;
                bool   ko     = m.Groups[3].Value.Contains("knocked out", StringComparison.OrdinalIgnoreCase);
                bool   crit   = m.Groups[3].Value.Contains("critically", StringComparison.OrdinalIgnoreCase);
                string victim = JsonExtensions.TryGetProp(j, "tnm") ?? m.Groups[4].Value;
                long   dmg    = ParseValue(m.Groups[5].Value);

                _lastEventTime = eventTime;
                var v = _currentFight.GetOrAdd(victim);
                if (dmg > 0) v.AddIncoming(sec, src, ability, dmg, crit);
                if (ko) v.MarkDeath(eventTime);
                return;
            }

            // Pure knockout with no damage number
            var k = KoText.Match(text);
            if (k.Success)
            {
                string victim = JsonExtensions.TryGetProp(j, "tnm") ?? k.Groups[3].Value;
                _currentFight.GetOrAdd(victim).MarkDeath(eventTime);
                _lastEventTime = eventTime;
            }
        }

        private void HandleHeal(string text, JsonElement? j, DateTime eventTime)
        {
            var m = HealText.Match(text);
            if (!m.Success) return;
            string source = JsonExtensions.TryGetProp(j, "inm") ?? m.Groups[1].Value;
            long   heal   = ParseValue(m.Groups[4].Value);
            if (heal <= 0 || _currentFight == null) return;
            _lastEventTime = eventTime;
            _currentFight.GetOrAdd(source).AddHeal(heal);
        }

        private void HandlePower(string text, JsonElement? j, DateTime eventTime)
        {
            var m = HealText.Match(text);
            if (!m.Success) return;
            string source = JsonExtensions.TryGetProp(j, "inm") ?? m.Groups[1].Value;
            long   power  = ParseValue(m.Groups[4].Value);
            if (power <= 0 || _currentFight == null) return;
            _lastEventTime = eventTime;
            _currentFight.GetOrAdd(source).AddPowerGiven(power);
        }

        private void EnsureFight(string mainTarget, DateTime eventTime)
        {
            if (_currentFight != null && _currentFight.IsActive) return;

            _currentFight = new FightData
            {
                FightName = mainTarget,
                StartTime = eventTime,
                IsActive  = true,
                IsSparringParse = mainTarget.Contains("Sparring Target", StringComparison.OrdinalIgnoreCase)
            };

            lock (_histLock)
            {
                _rollWindow.Clear();
                _history.Insert(0, _currentFight);
                // LOW-4 fix: cap history at 200 entries
                if (_history.Count > 200) _history.RemoveAt(_history.Count - 1);
            }

            FightStarted?.Invoke(this, new FightEventArgs(_currentFight));
        }

        private void EndFight()
        {
            if (_currentFight == null) return;
            _currentFight.Freeze();
            FightEnded?.Invoke(this, new FightEventArgs(_currentFight));
        }

        private void UpdateLiveDps()
        {
            if (_currentFight == null) return;
            var cutoff = DateTime.Now.AddSeconds(-5);

            lock (_histLock)
            {
                foreach (var (name, events) in _rollWindow)
                {
                    events.RemoveAll(e => e.t < cutoff);
                    if (_currentFight.Players.TryGetValue(name, out var ps))
                        ps.LiveDps = events.Sum(e => e.dmg) / 5.0;
                }
            }
        }

        private static long ParseValue(string textValue)
            => long.TryParse(textValue.Replace(",", ""), out long r) ? r : 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
        }
    }

    internal static class JsonExtensions
    {
        public static string? TryGetProp(JsonElement? el, string prop)
        {
            if (!el.HasValue) return null;
            return el.Value.TryGetProperty(prop, out var v) ? v.GetString() : null;
        }
    }
}
