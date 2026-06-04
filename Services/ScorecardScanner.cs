using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    /// <summary>
    /// OCRs the DCUO in-game Scorecard → Leaderboard panel (the menu that shows EVERY
    /// player's stats) and parses it into per-player damage + the instance time, so we can
    /// compute real GROUP DPS. Reads screen pixels only — same approach as the party-frame
    /// scanner, fully TOS-safe (no memory reading).
    /// </summary>
    public class ScorecardScanner : IDisposable
    {
        private static OcrEngine? _engine;
        private bool _disposed;

        private int  _scanX = -1, _scanY = -1, _scanW = 900, _scanH = 600;
        private bool _hasRegion;

        public bool HasRegion => _hasRegion;

        // "Time Since Start: 02:33"
        private static readonly Regex TimeRegex = new(
            @"Time\s*Since\s*Start[:\s]*([0-9]{1,2})\s*[:.]\s*([0-9]{2})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // A number token like 304,247 or 0
        private static readonly Regex NumRegex = new(@"\d[\d,]*", RegexOptions.Compiled);

        // Header/tab words to ignore (not player rows)
        private static readonly HashSet<string> _ignore = new(StringComparer.OrdinalIgnoreCase)
        {
            "name","scorecard","summary","leaderboard","info","feats","leave","time","since","start","on","duty"
        };

        public ScorecardScanner()
        {
            try
            {
                var lang = new Windows.Globalization.Language("en-US");
                _engine  = OcrEngine.TryCreateFromLanguage(lang)
                           ?? OcrEngine.TryCreateFromUserProfileLanguages();
                if (_engine == null) Logger.Warn("ScorecardScanner", "OCR engine unavailable");
            }
            catch (Exception ex) { Logger.Error("ScorecardScanner.ctor", ex); }
        }

        public void SetScanRegion(int x, int y, int w, int h)
        {
            if (w <= 0 || w > 6000 || h <= 0 || h > 3000 || x < 0 || y < 0) return;
            _scanX = x; _scanY = y; _scanW = w; _scanH = h;
            _hasRegion = true;
        }

        public async Task<ScorecardResult?> ScanAsync()
        {
            if (!_hasRegion || _engine == null || _disposed) return null;
            try
            {
                using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
                if (bmp == null) return null;
                using var scaled = ScaleUp(bmp, 2);
                var soft = await ToSoftwareBitmapAsync(scaled);
                if (soft == null) return null;

                var ocr = await _engine.RecognizeAsync(soft);

                int durationSec = 0;
                var entries = new List<ScorecardEntry>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Whole-text pass for the timer (it may sit on its own line)
                var tm = TimeRegex.Match(ocr.Text ?? "");
                if (tm.Success &&
                    int.TryParse(tm.Groups[1].Value, out int mm) &&
                    int.TryParse(tm.Groups[2].Value, out int ss))
                    durationSec = mm * 60 + ss;

                foreach (var line in ocr.Lines)
                {
                    string raw = (line.Text ?? "").Trim();
                    if (raw.Length < 3) continue;
                    if (TimeRegex.IsMatch(raw)) continue; // timer line, not a player

                    var nums = NumRegex.Matches(raw);
                    if (nums.Count == 0) continue;           // header/icon rows have no numbers

                    // Name = text before the first number
                    int firstNumIdx = nums[0].Index;
                    string name = raw[..firstNumIdx].Trim().Trim('-', '.', ':', '|').Trim();
                    if (!IsLikelyName(name)) continue;

                    var vals = new List<long>();
                    foreach (Match m in nums)
                        if (long.TryParse(m.Value.Replace(",", ""), out long v)) vals.Add(v);
                    if (vals.Count == 0) continue;

                    long damage  = vals[0];
                    long healing = vals.Count > 2 ? vals[2] : 0;
                    long power   = vals.Count > 3 ? vals[3] : 0;
                    int  deaths  = (int)Math.Min(int.MaxValue, vals[^1]);

                    if (seen.Add(name))
                        entries.Add(new ScorecardEntry(name, damage, healing, power, deaths));
                }

                if (entries.Count == 0) return null;
                return new ScorecardResult { DurationSec = durationSec, Entries = entries };
            }
            catch (Exception ex) { Logger.Error("ScorecardScanner.ScanAsync", ex); return null; }
        }

        private static bool IsLikelyName(string name)
        {
            if (name.Length < 2 || name.Length > 24) return false;
            int letters = name.Count(char.IsLetter);
            if (letters < 2) return false;
            if (_ignore.Contains(name)) return false;
            // reject if it's mostly the ignore words joined
            string first = name.Split(' ')[0];
            if (_ignore.Contains(first) && name.Split(' ').Length == 1) return false;
            return true;
        }

        private static Bitmap? CaptureRegion(int x, int y, int w, int h)
        {
            try
            {
                var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                return bmp;
            }
            catch (Exception ex) { Logger.Error("ScorecardScanner.Capture", ex); return null; }
        }

        private static Bitmap ScaleUp(Bitmap src, int factor)
        {
            var dst = new Bitmap(src.Width * factor, src.Height * factor);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, dst.Width, dst.Height);
            return dst;
        }

        private static async Task<SoftwareBitmap?> ToSoftwareBitmapAsync(Bitmap bmp)
        {
            try
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var dec = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
                return await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
            catch (Exception ex) { Logger.Error("ScorecardScanner.ToSoftware", ex); return null; }
        }

        public void Dispose() => _disposed = true;
    }
}
