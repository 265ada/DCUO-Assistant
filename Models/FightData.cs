using System.Collections.Concurrent;

namespace DCUOTracker.Models
{
    // Shared named record for ability data in reports (INFO-4 fix: proper JSON field names)
    public record AbilityEntry(string Ability, long Total, long Avg, int Hits);

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

        public Dictionary<string, AbilityStats> Abilities { get; } = new();
        public double LiveDps { get; set; }

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

        public void AddHit(string ability, long damage, bool crit, string target, DateTime now)
        {
            FirstHitTime ??= now;
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

            if (Abilities.Count % 5 == 0 || PowerType == DcuoPower.Unknown)
                PowerType = PowerDetector.DetectPower(Abilities.Keys);
        }

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


