using System.IO;
using System.Text.Json;

namespace DCUOTracker.Services
{
    public class KeybindEntry
    {
        public string ActionId   { get; set; } = "";
        public uint   Modifiers  { get; set; } = 0;   // Mod.Ctrl / Shift / Alt flags
        public uint   VirtualKey { get; set; } = 0;   // 0 = unassigned
    }

    public class KeybindSettings
    {
        public List<KeybindEntry> Binds { get; set; } = new();

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCUOTracker", "keybinds.json");

        public static KeybindSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json   = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<KeybindSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception ex) { Logger.Error("KeybindSettings.Load", ex); }
            return new KeybindSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch (Exception ex) { Logger.Error("KeybindSettings.Save", ex); }
        }

        public KeybindEntry? GetBind(string actionId)
            => Binds.FirstOrDefault(b => b.ActionId == actionId);

        public void SetBind(string actionId, uint mod, uint vk)
        {
            var e = Binds.FirstOrDefault(b => b.ActionId == actionId);
            if (e != null) { e.Modifiers = mod; e.VirtualKey = vk; }
            else Binds.Add(new KeybindEntry { ActionId = actionId, Modifiers = mod, VirtualKey = vk });
            Save();
        }

        public void ClearBind(string actionId)
        {
            var e = Binds.FirstOrDefault(b => b.ActionId == actionId);
            if (e != null) { e.Modifiers = 0; e.VirtualKey = 0; Save(); }
        }
    }
}
