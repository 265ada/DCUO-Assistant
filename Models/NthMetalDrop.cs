namespace DCUOTracker.Models
{
    public class NthMetalDrop
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string MetalType { get; set; } = "";
        public int Quantity { get; set; }
        public int XpValue { get; set; }
        public string Character { get; set; } = "";
        public string Session { get; set; } = "";
        public bool   IsOwn     { get; set; } = true;

        public static int GetXpValue(string metalType) => metalType.ToLower() switch
        {
            "raw"       => 10,
            "extracted" => 20,
            "treated"   => 50,
            "processed" => 100,
            "refined"   => 200,
            "purified"  => 500,
            _           => 10
        };
    }
}

