namespace DCUOTracker.Models
{
    public class PlayerReportEntry
    {
        public string Name        { get; set; } = "";
        public int    CR          { get; set; }
        public long   TotalDamage { get; set; }
        public long   MaxHit      { get; set; }
        public double Dps         { get; set; }
        public double PctShare    { get; set; }
        public string PowerType   { get; set; } = "";
        public string Role        { get; set; } = "";
        public List<AbilityEntry> TopAbilities { get; set; } = new();
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
                    PctShare    = gt > 0 ? (double)p.TotalDamage / gt : 0,
                    PowerType   = p.PowerType.ToString(),
                    Role        = p.Role.ToString(),
                    TopAbilities = p.Abilities.Values
                        .OrderByDescending(a => a.TotalDmg)
                        .Take(8)
                        .Select(a => new AbilityEntry(a.Name, a.TotalDmg, a.AvgHit, (int)a.Hits))
                        .ToList()
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
                Label        = "",
                Players      = players
            };
        }
    }
}


