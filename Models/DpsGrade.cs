namespace DCUOTracker.Models
{
    /// <summary>
    /// FFLogs/parse-culture style performance grading.
    /// Higher tiers use the classic colored-parse palette
    /// (green = low, blue, purple, orange, gold/pink = top).
    /// </summary>
    public readonly record struct Grade(string Letter, string Hex, string Label)
    {
        public static readonly Grade None = new("—", "#9CA3AF", "");
    }

    public static class DpsGrade
    {
        // Parse palette (low -> high)
        private const string Grey   = "#9CA3AF"; // F  — needs work
        private const string Green  = "#1EFF00"; // D  — ok
        private const string Blue   = "#3B9DFF"; // C  — good
        private const string Purple = "#C084FC"; // B  — great
        private const string Orange = "#FF8000"; // A  — excellent
        private const string Gold   = "#FFD24A"; // S  — top tier

        /// <summary>Grade ACTIVITY % — pure skill metric, build-independent.</summary>
        public static Grade ForActivity(double pct) => pct switch
        {
            >= 95 => new("S", Gold,   "Flawless uptime"),
            >= 88 => new("A", Orange, "Excellent uptime"),
            >= 78 => new("B", Purple, "Good uptime"),
            >= 65 => new("C", Blue,   "Some downtime"),
            >= 50 => new("D", Green,  "Choppy rotation"),
            _     => new("F", Grey,   "Lots of downtime"),
        };

        /// <summary>
        /// Grade a value against a personal best (ratio = current / best).
        /// Used for DPS/burst where absolute numbers vary by CR/gear.
        /// </summary>
        public static Grade ForRatio(double ratio) => ratio switch
        {
            >= 1.00 => new("S", Gold,   "New personal best!"),
            >= 0.95 => new("A", Orange, "Near your best"),
            >= 0.85 => new("B", Purple, "Strong run"),
            >= 0.70 => new("C", Blue,   "Decent"),
            >= 0.50 => new("D", Green,  "Below par"),
            _       => new("F", Grey,   "Off your best"),
        };

        /// <summary>Generic 0-100 score to grade (e.g. crit% expectations).</summary>
        public static Grade ForScore(double score) => score switch
        {
            >= 90 => new("S", Gold,   ""),
            >= 75 => new("A", Orange, ""),
            >= 60 => new("B", Purple, ""),
            >= 45 => new("C", Blue,   ""),
            >= 30 => new("D", Green,  ""),
            _     => new("F", Grey,   ""),
        };
    }
}
