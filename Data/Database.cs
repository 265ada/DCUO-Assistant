using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using DCUOTracker.Models;
using DCUOTracker.Services;

namespace DCUOTracker.Data
{
    public class Database
    {
        private readonly string _connectionString;

        public Database()
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DCUOTracker", "drops.db");

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _connectionString = $"Data Source={dbPath}";
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                Exec(conn, @"CREATE TABLE IF NOT EXISTS NthMetalDrops (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    MetalType TEXT NOT NULL,
                    Quantity  INTEGER NOT NULL,
                    TotalXp   INTEGER NOT NULL,
                    Character TEXT NOT NULL,
                    Session   TEXT NOT NULL)");

                // Migrate old XpValue column name if upgrading from previous version
                try
                {
                    Exec(conn, "ALTER TABLE NthMetalDrops RENAME COLUMN XpValue TO TotalXp");
                }
                catch { /* column already named TotalXp or doesn't exist — ignore */ }

                Exec(conn, @"CREATE TABLE IF NOT EXISTS ItemDrops (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    DropType  TEXT NOT NULL,
                    ItemName  TEXT NOT NULL,
                    Quantity  INTEGER NOT NULL,
                    Character TEXT NOT NULL,
                    Session   TEXT NOT NULL)");
            }
            catch (Exception ex) { Logger.Error("Database.Initialize", ex); }
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // ── Nth Metal ─────────────────────────────────────────────────

        public void InsertDrop(NthMetalDrop drop)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO NthMetalDrops
                    (Timestamp,MetalType,Quantity,TotalXp,Character,Session)
                    VALUES ($ts,$type,$qty,$xp,$char,$session)";
                cmd.Parameters.AddWithValue("$ts",      drop.Timestamp.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$type",    drop.MetalType);
                cmd.Parameters.AddWithValue("$qty",     drop.Quantity);
                cmd.Parameters.AddWithValue("$xp",      drop.XpValue * drop.Quantity); // store total
                cmd.Parameters.AddWithValue("$char",    drop.Character);
                cmd.Parameters.AddWithValue("$session", drop.Session);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Logger.Error("Database.InsertDrop", ex); }
        }

        public List<NthMetalDrop> GetAllTime()
        {
            var list = new List<NthMetalDrop>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM NthMetalDrops ORDER BY Timestamp DESC";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new NthMetalDrop
                    {
                        Id        = r.GetInt32(0),
                        // M-2: InvariantCulture + round-trip format
                        Timestamp = DateTime.ParseExact(r.GetString(1), "o",
                                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        MetalType = r.GetString(2),
                        Quantity  = r.GetInt32(3),
                        XpValue   = r.GetInt32(4), // L-4: TotalXp stored, don't multiply again
                        Character = r.GetString(5),
                        Session   = r.GetString(6)
                    });
                }
            }
            catch (Exception ex) { Logger.Error("Database.GetAllTime", ex); }
            return list;
        }

        // ── Item Drops ────────────────────────────────────────────────

        public void InsertItemDrop(ItemDrop drop)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO ItemDrops
                    (Timestamp,DropType,ItemName,Quantity,Character,Session)
                    VALUES ($ts,$type,$name,$qty,$char,$session)";
                cmd.Parameters.AddWithValue("$ts",      drop.Timestamp.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$type",    drop.DropType.ToString());
                cmd.Parameters.AddWithValue("$name",    drop.ItemName);
                cmd.Parameters.AddWithValue("$qty",     drop.Quantity);
                cmd.Parameters.AddWithValue("$char",    drop.Character);
                cmd.Parameters.AddWithValue("$session", drop.Session);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Logger.Error("Database.InsertItemDrop", ex); }
        }

        public List<ItemDrop> GetItemDropsAllTime()
        {
            var list = new List<ItemDrop>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM ItemDrops ORDER BY Timestamp DESC";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    // M-1: Enum.TryParse instead of Enum.Parse — won't crash on bad DB data
                    if (!Enum.TryParse<ItemDropType>(r.GetString(2), out var dropType))
                    {
                        Logger.Warn("Database.GetItemDropsAllTime", $"Unknown DropType: {r.GetString(2)}");
                        continue;
                    }

                    // M-2: InvariantCulture parsing
                    if (!DateTime.TryParseExact(r.GetString(1), "o",
                            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
                        ts = DateTime.Now;

                    list.Add(new ItemDrop
                    {
                        Id        = r.GetInt32(0),
                        Timestamp = ts,
                        DropType  = dropType,
                        ItemName  = r.GetString(3),
                        Quantity  = r.GetInt32(4),
                        Character = r.GetString(5),
                        Session   = r.GetString(6)
                    });
                }
            }
            catch (Exception ex) { Logger.Error("Database.GetItemDropsAllTime", ex); }
            return list;
        }
    }
}
