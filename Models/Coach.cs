using System.Linq;

namespace DCUOTracker.Models
{
    /// <summary>
    /// Live DPS coach. Looks at the selected player's stats and surfaces the single
    /// most useful piece of advice for improving damage — adaptive, not generic.
    /// </summary>
    public static class Coach
    {
        public readonly record struct Tip(string Label, string Text, string Hex);

        private const string Red   = "#FF5050";
        private const string Gold  = "#FFC800";
        private const string Blue  = "#60A5FA";
        private const string Green = "#7CFC00";
        private const string Grey  = "#9CA3AF";

        public static Tip Analyze(PlayerStats p, bool sparring)
            => Analyze(p.ActivityPercent, p.CritPercent, p.PrecisionPct, p.SuperPct,
                       p.Hits, p.PowerType.ToString(), sparring);

        public static Tip Analyze(double act, double crit, double prec, double sup,
                                  long hits, string power, bool sparring)
        {
            if (hits < 8)
                return new("WARMING UP", "Collecting data… keep attacking for a read.", Grey);

            // 1) Downtime is the #1 DPS killer — flag it first
            if (act < 75)
                return new("TIGHTEN ROTATION",
                    $"{act:F0}% activity — idle ~{100 - act:F0}% of the fight. Queue your next ability so there are no gaps.",
                    act < 60 ? Red : Gold);

            // 2) Crit investment
            if (crit < 22 && hits > 25)
                return new("LOW CRIT",
                    $"Crit {crit:F0}%. Add Critical Attack Chance + Critical Attack Damage skill points and Might gear.",
                    Gold);

            // 3) Might build leaning too hard on weapon attacks
            if (prec > 55)
                return new("TOO WEAPON-HEAVY",
                    $"{prec:F0}% of damage is weapon hits. Superpowered builds want powers leading — cast more abilities between combos.",
                    Gold);

            // 4) Not using supercharge in a long fight
            if (sup <= 0.1 && hits > 60)
                return new("USE SUPERCHARGE",
                    "No supercharge damage yet — pop it in burst windows for a big chunk.",
                    Blue);

            // 5) Doing well — give the power-specific pro tip (from the DPS build)
            var tips = BuildForPower(power)?.Dps?.Tips;
            if (!string.IsNullOrEmpty(tips))
                return new($"{power.ToUpper()} TIP", tips, Green);

            return new("ON TRACK", $"{act:F0}% uptime, {crit:F0}% crit. Clean execution — keep it flowing.", Green);
        }

        private static PowerBuild? BuildForPower(string power)
        {
            if (string.IsNullOrEmpty(power) || power == "Unknown") return null;
            return BuildLibrary.All.FirstOrDefault(b =>
                b.Power.Equals(power, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
