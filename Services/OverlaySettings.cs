using System.IO;
using System.Text.Json;

namespace DCUOTracker.Services
{
    public class OverlaySettings
    {
        // Overlay position + state
        public double Left        { get; set; } = 100;
        public double Top         { get; set; } = 100;
        public bool   IsVertical  { get; set; } = false;
        public bool   IsPinned    { get; set; } = true;

        // Chat OCR region
        public int  ScanX      { get; set; } = -1;
        public int  ScanY      { get; set; } = -1;
        public int  ScanWidth  { get; set; } = 200;
        public int  ScanHeight { get; set; } = 30;

        // Scorecard OCR region (in-game Leaderboard panel → group DPS)
        public int ScoreX      { get; set; } = -1;
        public int ScoreY      { get; set; } = -1;
        public int ScoreWidth  { get; set; } = 900;
        public int ScoreHeight { get; set; } = 600;
        public bool HasScoreRegion => ScoreX >= 0 && ScoreY >= 0;

        // LFG timer persistence (timer survives app restart)
        public DateTime? LastLfgPostUtc { get; set; } = null;

        // DPS overlay
        public bool PersistOverlay  { get; set; } = false; // multi-monitor: keep visible when alt-tabbed
        public bool BossOnlyMeter   { get; set; } = false; // dual meter toggle
        public double DpsOverlayLeft { get; set; } = 20;
        public double DpsOverlayTop  { get; set; } = 200;

        // Global hotkeys
        public string HideAllHotkey { get; set; } = "F9"; // boss-key

        // My character name — only my drops count toward stats
        public string MyCharacterName { get; set; } = "";

        // Remember last drop log view mode
        public bool ViewAllTimeMode { get; set; } = false;

        // Dismissed update version (don't nag same version twice)
        public string DismissedVersion { get; set; } = "";

        public bool HasScanRegion => ScanX >= 0 && ScanY >= 0;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCUOTracker", "overlay-settings.json");

        public static OverlaySettings Load()
        {
            try
            {
                // MED-5 fix: correct .tmp recovery — check main, then fallback to .tmp
                string? src = File.Exists(FilePath)          ? FilePath
                            : File.Exists(FilePath + ".tmp") ? FilePath + ".tmp"
                            : null;
                if (src != null)
                {
                    var json   = File.ReadAllText(src);
                    var loaded = JsonSerializer.Deserialize<OverlaySettings>(json);
                    if (loaded != null)
                    {
                        // Validate scan region bounds
                        if (loaded.ScanWidth  <= 0 || loaded.ScanWidth  > 4000) loaded.ScanWidth  = 200;
                        if (loaded.ScanHeight <= 0 || loaded.ScanHeight > 500)  loaded.ScanHeight = 30;
                        if (loaded.ScanX < -1) loaded.ScanX = -1;
                        if (loaded.ScanY < -1) loaded.ScanY = -1;
                        return loaded;
                    }
                }
            }
            catch (Exception ex) { Logger.Error("OverlaySettings.Load", ex); }
            return new();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string tmp = FilePath + ".tmp";
                // Atomic write: write to .tmp then rename — crash-safe
                File.WriteAllText(tmp, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch (Exception ex) { Logger.Error("OverlaySettings.Save", ex); }
        }
    }
}
