namespace DCUOTracker.Models
{
    public enum ItemDropType
    {
        SourceMark,
        AllyFavor,
        Exobyte,
        Alliance          // Rare/Epic/Legendary Alliance
    }

    public static class ExobyteQuality
    {
        public static string GetQuality(string itemName)
        {
            if (itemName.StartsWith("Flawed",    StringComparison.OrdinalIgnoreCase)) return "Flawed";
            if (itemName.StartsWith("Solid",     StringComparison.OrdinalIgnoreCase)) return "Solid";
            if (itemName.StartsWith("Pristine",  StringComparison.OrdinalIgnoreCase)) return "Pristine";
            if (itemName.StartsWith("Flawless",  StringComparison.OrdinalIgnoreCase)) return "Flawless";
            if (itemName.StartsWith("Timeless",  StringComparison.OrdinalIgnoreCase)) return "Timeless";
            if (itemName.StartsWith("Perfected", StringComparison.OrdinalIgnoreCase)) return "Perfected";
            return "Standard"; // Risen, Raging, etc.
        }

        /// <summary>Approximate augment XP per exobyte by quality tier.</summary>
        public static int GetXpValue(string itemName)
        {
            return GetQuality(itemName) switch
            {
                "Flawed"    => 50,
                "Solid"     => 100,
                "Pristine"  => 200,
                "Flawless"  => 500,
                "Timeless"  => 1000,
                "Perfected" => 2000,
                _           => 100  // Standard (Risen, Raging, etc.)
            };
        }
    }

    public class ItemDrop
    {
        public int          Id        { get; set; }
        public DateTime     Timestamp { get; set; }
        public ItemDropType DropType  { get; set; }
        public string       ItemName  { get; set; } = ""; // e.g. "Risen", "Solid Mogo"
        public int          Quantity  { get; set; }
        public string       Character { get; set; } = "";
        public string       Session   { get; set; } = "";
        public bool     IsOwn     { get; set; } = true;
    }
}



