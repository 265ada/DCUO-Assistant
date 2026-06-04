namespace DCUOTracker.Models
{
    public class PlayerReportEntry
    {
        public string Name        { get; set; } = "";
        public int    CR          { get; set; }
        public long   TotalDamage { get; set; }
        public long   MaxHit      { get; set; }
        public double Dps         { get; set; }
        public double Burst       { get; set; }
        public double PctShare    { get; set; }
        public string PowerType   { get; set; } = "";
        public string Role        { get; set; } = "";
        // Performance analytics
        public double Activity    { get; set; }
        public double Crit        { get; set; }
        public double Apm         { get; set; }
        public double MightPct    { get; set; }
        public double SuperPct    { get; set; }
        public double PrecPct     { get; set; }
        public int    Deaths      { get; set; }
        public List<AbilityEntry> TopAbilities { get; set; } = new();
        public List<CastEvent>    Rotation     { get; set; } = new();
        public List<IncomingHit>  DeathRecap   { get; set; } = new();
        public List<float>        DpsCurve     { get; set; } = new(); // DPS sampled every ~5s
    }

    public class FightReport
    {
        public string   Id           { get; set; } = "";
        public string   FightName    { get; set; } = "";
        public DateTime StartTime    { get; set; }
        public DateTime EndTime      { get; set; }
        public double   DurationSecs { get; set; }
        public long     TotalDamage  { get; set; }
        public long     MaxHit       { get; set; }
        public bool     IsPersonalBest { get; set; }
        public bool     IsSparringParse { get; set; }
        public string   Label        { get; set; } = ""; // user-editable
        public List<PlayerReportEntry> Players { get; set; } = new();
        // Sparkline: seconds → group DPS
        public List<float> SparklineDps { get; set; } = new();

        public static FightReport FromFight(FightData fight)
        {
            var id    = DateTime.UtcNow.ToString("yyyyMMddTHHmmss_fffZ");
            long gt   = fight.TotalGroupDamage;
            double dur = fight.Duration.TotalSeconds;

            var players = fight.RankedByDamage.Select(p =>
            {
                double dps = dur > 1 ? p.TotalDamage / dur : 0;
                return new PlayerReportEntry
                {
                    Name        = p.Name,
                    CR          = p.CR,
                    TotalDamage = p.TotalDamage,
                    MaxHit      = p.MaxHitEncounter,
                    Dps         = dps,
                    Burst       = p.BurstDps,
                    PctShare    = gt > 0 ? (double)p.TotalDamage / gt : 0,
                    PowerType   = p.PowerType.ToString(),
                    Role        = p.Role.ToString(),
                    Activity    = p.ActivityPercent,
                    Crit        = p.CritPercent,
                    Apm         = p.Apm,
                    MightPct    = p.MightPct,
                    SuperPct    = p.SuperPct,
                    PrecPct     = p.PrecisionPct,
                    Deaths      = p.Deaths,
                    TopAbilities = p.Abilities.Values
                        .OrderByDescending(a => a.TotalDmg)
                        .Take(8)
                        .Select(a => new AbilityEntry(a.Name, a.TotalDmg, a.AvgHit, (int)a.Hits))
                        .ToList(),
                    // Cap rotation/recap for sane JSON size
                    Rotation   = p.Casts.Count > 150 ? p.Casts.TakeLast(150).ToList() : p.Casts.ToList(),
                    DeathRecap = p.RecentIncoming.TakeLast(12).ToList(),
                    DpsCurve   = p.Sparkline.Select(s => s.dps).ToList()
                };
            }).ToList();

            return new FightReport
            {
                Id           = id,
                FightName    = fight.FightName,
                StartTime    = fight.StartTime,
                EndTime      = fight.EndTime ?? DateTime.Now,
                DurationSecs = dur,
                TotalDamage  = gt,
                MaxHit       = fight.MaxHit,
                IsSparringParse = fight.IsSparringParse,
                Label        = "",
                Players      = players
            };
        }
    }
}


