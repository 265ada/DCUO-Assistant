using System.Globalization;
using System.IO;
using System.Text.Json;

namespace DCUOTracker.Services
{
    public class OverlaySettings
    {
        public double Left       { get; set; } = 100;
        public double Top        { get; set; } = 100;
        public bool   IsVertical { get; set; } = false;
        public bool   IsPinned   { get; set; } = true;

        public int  ScanX      { get; set; } = -1;
        public int  ScanY      { get; set; } = -1;
        public int  ScanWidth  { get; set; } = 200;
        public int  ScanHeight { get; set; } = 30;

        public bool HasScanRegion => ScanX >= 0 && ScanY >= 0;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCUOTracker", "overlay-settings.json");

        public static OverlaySettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<OverlaySettings>(json);
                    if (loaded != null)
                    {
                        // M-8: validate scan region bounds on load
                        if (loaded.ScanWidth  <= 0 || loaded.ScanWidth  > 4000) loaded.ScanWidth  = 200;
                        if (loaded.ScanHeight <= 0 || loaded.ScanHeight > 500)  loaded.ScanHeight = 30;
                        if (loaded.ScanX < -1) loaded.ScanX = -1;
                        if (loaded.ScanY < -1) loaded.ScanY = -1;
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("OverlaySettings.Load", ex); // C-1: log instead of swallow
            }
            return new();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Logger.Error("OverlaySettings.Save", ex); // C-1: log instead of swallow
            }
        }
    }
}
