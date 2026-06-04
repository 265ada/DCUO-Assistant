using System.IO;
using System.Text.Json;

namespace DCUOTracker.Services
{
    /// <summary>
    /// Persists the player's personal-best parse numbers per power set
    /// (best burst = best 10s DPS, best sustained DPS). Used to show
    /// "NEW BEST" / "% of your best" motivation on the overlay.
    /// </summary>
    public class ParseBests
    {
        public class Best { public double Burst { get; set; } public double Sustained { get; set; } }

        private readonly string _file;
        private Dictionary<string, Best> _data = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true, MaxDepth = 8 };

        public ParseBests()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DCUOTracker");
            Directory.CreateDirectory(dir);
            _file = Path.Combine(dir, "parse_bests.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_file))
                    _data = JsonSerializer.Deserialize<Dictionary<string, Best>>(File.ReadAllText(_file), _opts)
                            ?? new(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Logger.Error("ParseBests.Load", ex); _data = new(StringComparer.OrdinalIgnoreCase); }
        }

        private void Save()
        {
            try
            {
                string tmp = _file + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_data, _opts));
                File.Move(tmp, _file, overwrite: true);
            }
            catch (Exception ex) { Logger.Error("ParseBests.Save", ex); }
        }

        public Best Get(string power)
        {
            lock (_lock)
                return _data.TryGetValue(power, out var b) ? new Best { Burst = b.Burst, Sustained = b.Sustained } : new Best();
        }

        /// <summary>Record a parse. Returns true if a new burst record was set.</summary>
        public bool Report(string power, double burst, double sustained)
        {
            if (string.IsNullOrEmpty(power) || power == "Unknown") return false;
            bool newBurst = false;
            lock (_lock)
            {
                if (!_data.TryGetValue(power, out var b)) { b = new Best(); _data[power] = b; }
                if (burst > b.Burst)         { b.Burst = burst; newBurst = true; }
                if (sustained > b.Sustained) { b.Sustained = sustained; }
                if (newBurst || sustained >= b.Sustained) Save();
            }
            return newBurst;
        }
    }
}
