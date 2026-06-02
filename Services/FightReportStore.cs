using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    public class FightReportStore
    {
        private readonly string _dir;
        private readonly string _pbFile;
        private Dictionary<string, double> _personalBests = new(StringComparer.OrdinalIgnoreCase);

        // MED-1 fix: strict ID format regex matching generated "20240101T120000Z" pattern
        private static readonly Regex SafeIdRegex = new(@"^\d{8}T\d{6}Z$", RegexOptions.Compiled);

        // MED-2 fix: depth-limited deserializer options
        private static readonly JsonSerializerOptions _readOpts = new()
        {
            MaxDepth = 10,
            PropertyNameCaseInsensitive = false
        };

        private static readonly JsonSerializerOptions _writeOpts = new()
        {
            WriteIndented = true
        };

        public FightReportStore()
        {
            _dir    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                          "DCUOTracker", "fights");
            _pbFile = Path.Combine(_dir, "personal_bests.json");
            Directory.CreateDirectory(_dir);
            LoadPersonalBests();
        }

        public void SaveFight(FightData fight)
        {
            if (fight.TotalGroupDamage == 0) return;
            try
            {
                var report = FightReport.FromFight(fight);

                double groupDps = fight.Duration.TotalSeconds > 1
                    ? fight.TotalGroupDamage / fight.Duration.TotalSeconds : 0;
                string pbKey = fight.FightName;
                if (!_personalBests.TryGetValue(pbKey, out double best) || groupDps > best)
                {
                    _personalBests[pbKey] = groupDps;
                    report.IsPersonalBest  = true;
                    SavePersonalBests();
                }

                string path = Path.Combine(_dir, $"fight-{report.Id}.json");
                AtomicWrite(path, JsonSerializer.Serialize(report, _writeOpts));
                Prune(50);
            }
            catch (Exception ex) { Logger.Error("FightReportStore.Save", ex); }
        }

        public List<FightReport> LoadAll()
        {
            var list = new List<FightReport>();
            try
            {
                foreach (var f in Directory.GetFiles(_dir, "fight-*.json").OrderByDescending(f => f))
                {
                    try
                    {
                        var r = JsonSerializer.Deserialize<FightReport>(File.ReadAllText(f), _readOpts);
                        if (r != null) list.Add(r);
                    }
                    catch { /* skip corrupt */ }
                }
            }
            catch (Exception ex) { Logger.Error("FightReportStore.LoadAll", ex); }
            return list;
        }

        public void UpdateLabel(string id, string label)
        {
            if (!IsSafeId(id)) return;
            try
            {
                string path = Path.Combine(_dir, $"fight-{id}.json");
                if (!File.Exists(path)) return;
                var r = JsonSerializer.Deserialize<FightReport>(File.ReadAllText(path), _readOpts);
                if (r == null) return;
                r.Label = label;
                AtomicWrite(path, JsonSerializer.Serialize(r, _writeOpts));
            }
            catch (Exception ex) { Logger.Error("FightReportStore.UpdateLabel", ex); }
        }

        public void Delete(string id)
        {
            if (!IsSafeId(id)) return;
            try { File.Delete(Path.Combine(_dir, $"fight-{id}.json")); }
            catch (Exception ex) { Logger.Error("FightReportStore.Delete", ex); }
        }

        private void Prune(int keep)
        {
            foreach (var f in Directory.GetFiles(_dir, "fight-*.json").OrderByDescending(f => f).Skip(keep))
                try { File.Delete(f); } catch { }
        }

        private void LoadPersonalBests()
        {
            try
            {
                if (File.Exists(_pbFile))
                    _personalBests = JsonSerializer.Deserialize<Dictionary<string, double>>(
                        File.ReadAllText(_pbFile), _readOpts) ?? new(StringComparer.OrdinalIgnoreCase);
            }
            catch { _personalBests = new(StringComparer.OrdinalIgnoreCase); }
        }

        private void SavePersonalBests()
        {
            try { AtomicWrite(_pbFile, JsonSerializer.Serialize(_personalBests, _writeOpts)); }
            catch (Exception ex) { Logger.Error("FightReportStore.SavePB", ex); }
        }

        // Atomic write: write to .tmp then rename
        private static void AtomicWrite(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }

        // MED-1 fix: strict safe ID — matches "20240101T120000_123Z" (with ms) or "20240101T120000Z"
        private static bool IsSafeId(string id) =>
            !string.IsNullOrEmpty(id) &&
            (SafeIdRegex.IsMatch(id) ||
             System.Text.RegularExpressions.Regex.IsMatch(id, @"^\d{8}T\d{6}_\d{3}Z$"));
    }
}
