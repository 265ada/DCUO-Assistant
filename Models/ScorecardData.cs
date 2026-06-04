using System.Collections.Generic;

namespace DCUOTracker.Models
{
    /// <summary>One player's row parsed from the in-game Scorecard → Leaderboard.</summary>
    public record ScorecardEntry(string Name, long Damage, long Healing, long Power, int Deaths);

    /// <summary>A full scorecard scan: instance time + every player's totals.</summary>
    public class ScorecardResult
    {
        public int DurationSec { get; init; }                 // from "Time Since Start MM:SS"
        public List<ScorecardEntry> Entries { get; init; } = new();
        public System.DateTime ScannedAt { get; } = System.DateTime.Now;

        public long TotalDamage
        {
            get { long t = 0; foreach (var e in Entries) t += e.Damage; return t; }
        }
    }
}
