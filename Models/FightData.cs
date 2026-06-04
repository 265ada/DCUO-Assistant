using System.Collections.Concurrent;

namespace DCUOTracker.Models
{
    // Shared named record for ability data in reports (INFO-4 fix: proper JSON field names)
    public record AbilityEntry(string Ability, long Total, long Avg, int Hits);

    // Cast timeline entry — one ability activation
    public record CastEvent(double Sec, string Ability, string Category, long Damage, bool Crit);

    // Incoming damage entry — for death recap
    public record IncomingHit(double Sec, string Source, string Ability, long Damage, bool Crit);

    public class AbilityStats
    {
        public string Name     { get; set; } = "";
        public string Category { get; set; } = "Power";
        public long   TotalDmg { get; set; }
        public long   Hits     { get; set; }
        public long   CritHits { get; set; }
        public long   MaxHit   { get; set; }
        public long   AvgHit   => Hits > 0 ? TotalDmg / Hits : 0;
    }
    public class PlayerStats
    {
        public string    Name             { get; set; } = "";
        public int       CR               { get; set; } = 0;
        public long      TotalDamage      { get; set; }
        public long      WeaponDamage     { get; set; }
        public long      PowerDamage      { get; set; }
        public long      SuperDamage      { get; set; }
        public long      TotalHealing     { get; set; }
        public long      TotalPowerGiven  { get; set; }
        public long      TotalDamageTaken { get; set; }
        public long      Hits             { get; set; }
        public long      CritHits         { get; set; }
        public long      MaxHitEncounter  { get; set; }
        public long      MaxHitSession    { get; set; }
        public long      Kills            { get; set; }
        public DcuoPower PowerType        { get; set; } = DcuoPower.Unknown;
        public DcuoRole  Role             { get; set; } = DcuoRole.DPS;
        public DateTime? FirstHitTime     { get; set; }
        public DateTime? LastHitTime      { get; set; }

        public Dictionary<string, AbilityStats> Abilities { get; } = new();
        public double LiveDps { get; set; }

        // ── Activity tracking: sum of "busy" stretches (gaps <= cap count as active) ──
        private const double ActivityGapCap = 2.0; // seconds; longer gap = downtime
        private double _activeSeconds;

        // ── Burst: best rolling 10-second DPS window ──
        private const double BurstWindow = 10.0;
        private readonly object _burstLock = new();
        private readonly List<(DateTime t, long dmg)> _burstBuf = new();
        public double BurstDps { get; private set; }

        // ── Cast sequence (rotation timeline) + cast count (APM) ──
        private const double NewCastGap = 1.4; // same-ability hits within this = same cast
        private readonly object _castLock = new();
        private readonly List<CastEvent> _casts = new();
        private string   _lastCastAbility = "";
        private DateTime _lastCastTime     = DateTime.MinValue;
        public int CastCount   { get; private set; }
        public int SuperCount  { get; private set; }
        public IReadOnlyList<CastEvent> Casts
        {
            get { lock (_castLock) { return _casts.ToList(); } }
        }

        // ── Incoming damage (death recap) ──
        private readonly object _inLock = new();
        private readonly List<IncomingHit> _incoming = new();
        public bool   IsDead    { get; private set; }
        public DateTime? DeathTime { get; private set; }
        public IReadOnlyList<IncomingHit> RecentIncoming
        {
            get { lock (_inLock) { return _incoming.ToList(); } }
        }

        // LOW-7 fix: lock protects sparkline from timer-thread vs UI-thread race
        private readonly object _sparklockObj = new();
        private readonly List<(float sec, float dps)> _sparkline = new();
        public IReadOnlyList<(float sec, float dps)> Sparkline
        {
            get { lock (_sparklockObj) { return _sparkline.ToList(); } }
        }
        public void AddSparkPoint(float sec, float dps)
        {
            lock (_sparklockObj) { _sparkline.Add((sec, dps)); }
        }

        public double CritPercent => Hits > 0 ? (double)CritHits / Hits * 100 : 0;

        public double ActiveTimeDps(DateTime fightEnd)
        {
            if (FirstHitTime == null || TotalDamage == 0) return 0;
            double secs = (fightEnd - FirstHitTime.Value).TotalSeconds;
            return secs > 1 ? TotalDamage / secs : 0;
        }

        // Activity %: how much of your engaged time you were actually attacking.
        // 100% = no gaps over ActivityGapCap. Low % = lots of downtime / clipping rotation.
        public double ActivityPercent
        {
            get
            {
                if (FirstHitTime == null || LastHitTime == null) return 0;
                double span = (LastHitTime.Value - FirstHitTime.Value).TotalSeconds;
                if (span < 1) return 100;
                return Math.Min(100.0, _activeSeconds / span * 100.0);
            }
        }

        // Abilities per minute (cast cadence)
        public double Apm
        {
            get
            {
                if (FirstHitTime == null || LastHitTime == null) return 0;
                double mins = (LastHitTime.Value - FirstHitTime.Value).TotalMinutes;
                return mins > 0.01 ? CastCount / mins : 0;
            }
        }

        // Might (superpower) vs Precision (weapon) damage shares
        public long MightDamage => PowerDamage + SuperDamage;
        public double MightPct
        {
            get { long t = TotalDamage; return t > 0 ? (double)MightDamage / t * 100 : 0; }
        }
        public double PrecisionPct
        {
            get { long t = TotalDamage; return t > 0 ? (double)WeaponDamage / t * 100 : 0; }
        }
        public double SuperPct
        {
            get { long t = TotalDamage; return t > 0 ? (double)SuperDamage / t * 100 : 0; }
        }

        public void AddHit(string ability, long damage, bool crit, string target, DateTime now)
        {
            // Activity: accumulate the gap since last hit if it's a "busy" stretch
            if (LastHitTime != null)
            {
                double gap = (now - LastHitTime.Value).TotalSeconds;
                if (gap > 0 && gap <= ActivityGapCap) _activeSeconds += gap;
            }
            FirstHitTime ??= now;
            LastHitTime  =  now;

            TotalDamage += damage;
            Hits++;
            if (crit) CritHits++;
            if (damage > MaxHitEncounter) MaxHitEncounter = damage;
            if (damage > MaxHitSession)   MaxHitSession   = damage;

            string cat = PowerDetector.ClassifyAbility(ability);
            if      (cat == "Weapon")      WeaponDamage += damage;
            else if (cat == "Supercharge") SuperDamage  += damage;
            else                           PowerDamage  += damage;

            if (!Abilities.TryGetValue(ability, out var ab))
            {
                ab = new AbilityStats { Name = ability, Category = cat };
                Abilities[ability] = ab;
            }
            ab.TotalDmg += damage;
            ab.Hits++;
            if (crit) ab.CritHits++;
            if (damage > ab.MaxHit) ab.MaxHit = damage;

            // Burst: rolling best 10s window
            lock (_burstLock)
            {
                _burstBuf.Add((now, damage));
                var cut = now.AddSeconds(-BurstWindow);
                _burstBuf.RemoveAll(e => e.t < cut);
                double sum = 0; foreach (var e in _burstBuf) sum += e.dmg;
                double d = sum / BurstWindow;
                if (d > BurstDps) BurstDps = d;
            }

            // Cast detection (approximate — log only has damage ticks, not cast starts).
            // New cast if: a long gap passed (any ability), OR the ability switched but
            // not within ~0.45s (which filters interleaved DoT ticks on burn/poison powers).
            double sec       = FirstHitTime.HasValue ? (now - FirstHitTime.Value).TotalSeconds : 0;
            double sinceLast = (now - _lastCastTime).TotalSeconds;
            bool newCast = sinceLast > NewCastGap ||
                           (ability != _lastCastAbility && sinceLast >= 0.45);
            if (newCast)
            {
                CastCount++;
                if (cat == "Supercharge") SuperCount++;
                _lastCastAbility = ability;
                lock (_castLock)
                {
                    _casts.Add(new CastEvent(sec, ability, cat, damage, crit));
                    if (_casts.Count > 600) _casts.RemoveAt(0);
                }
            }
            _lastCastTime = now;

            if (Abilities.Count % 5 == 0 || PowerType == DcuoPower.Unknown)
                PowerType = PowerDetector.DetectPower(Abilities.Keys);
        }

        // Record incoming damage (death recap). Caller passes seconds since fight start.
        public void AddIncoming(double sec, string source, string ability, long damage, bool crit)
        {
            TotalDamageTaken += damage;
            lock (_inLock)
            {
                _incoming.Add(new IncomingHit(sec, source, ability, damage, crit));
                if (_incoming.Count > 40) _incoming.RemoveAt(0);
            }
        }

        public void MarkDeath(DateTime when)
        {
            if (IsDead) return;
            IsDead = true; DeathTime = when; Deaths++;
        }
        public int Deaths { get; private set; }

        public void AddHeal(long heal)        => TotalHealing    += heal;
        public void AddPowerGiven(long power) => TotalPowerGiven += power;
        public void SetRoleFromPartyFrame(DcuoRole role) => Role = role;
    }

    public class FightData
    {
        public string    FightName { get; set; } = "Unknown";
        public DateTime  StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime   { get; set; }
        public bool      IsActive  { get; set; } = true;
        public bool      IsFrozen  { get; set; } = false;
        // Sparring Target parse (DCUO players parse on these dummies for clean DPS)
        public bool      IsSparringParse { get; set; } = false;

        // HIGH-1 fix: Interlocked for thread-safe static max hit
        private static long _allTimeMaxHit = 0;
        public static long AllTimeMaxHit
        {
            get => Interlocked.Read(ref _allTimeMaxHit);
            private set
            {
                long prev;
                do { prev = Interlocked.Read(ref _allTimeMaxHit); }
                while (value > prev &&
                       Interlocked.CompareExchange(ref _allTimeMaxHit, value, prev) != prev);
            }
        }

        // LOW-5 fix: expose read-only dict; only GetOrAdd can mutate
        private readonly Dictionary<string, PlayerStats> _players
            = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, PlayerStats> Players => _players;

        public long TotalGroupDamage  => _players.Values.Sum(p => p.TotalDamage);
        public long TotalGroupHealing => _players.Values.Sum(p => p.TotalHealing);
        public long MaxHit            => _players.Values.Select(p => p.MaxHitEncounter)
                                                        .DefaultIfEmpty(0).Max();
        public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;

        public IEnumerable<PlayerStats> RankedByDamage =>
            _players.Values.OrderByDescending(p => p.TotalDamage);

        public PlayerStats GetOrAdd(string name)
        {
            if (!_players.TryGetValue(name, out var ps))
            {
                ps = new PlayerStats { Name = name };
                _players[name] = ps;
            }
            return ps;
        }

        public void Freeze()
        {
            IsActive = false;
            IsFrozen = true;
            EndTime  = DateTime.Now;
            long max = MaxHit;
            if (max > 0) AllTimeMaxHit = max; // thread-safe via CAS
        }
    }
}


