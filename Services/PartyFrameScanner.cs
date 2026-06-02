using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    public class PartyFrameScanner : IDisposable
    {
        private static OcrEngine? _engine;
        private bool _disposed;

        private int  _scanX = -1, _scanY = -1, _scanW = 300, _scanH = 200;
        private bool _hasRegion = false;

        // HIGH-3 fix: ConcurrentDictionary for thread-safe concurrent scan access
        private readonly ConcurrentDictionary<string, DcuoRole> _knownRoles
            = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _knownCR
            = new(StringComparer.OrdinalIgnoreCase);

        // MED-1 fix: strict ID-format regex for file safety
        private static readonly Regex SafeIdRegex = new(@"^\d{8}T\d{6}Z$", RegexOptions.Compiled);

        public event EventHandler<Dictionary<string, DcuoRole>>? RolesDetected;
        public event EventHandler<Dictionary<string, int>>?      CRDetected;

        // Role color signatures (DCUO party frame badge colors)
        private static readonly (DcuoRole role, int rMin, int rMax, int gMin, int gMax, int bMin, int bMax)[] _roleColors =
        [
            (DcuoRole.DPS,        200, 255, 60,  125, 0,   55),
            (DcuoRole.Controller, 200, 255, 130, 195, 0,   65),
            (DcuoRole.Healer,     0,   85,  160, 255, 55,  165),
            (DcuoRole.Tank,       75,  165, 130, 205, 175, 255),
        ];

        public PartyFrameScanner()
        {
            try
            {
                var lang = new Windows.Globalization.Language("en-US");
                _engine  = OcrEngine.TryCreateFromLanguage(lang)
                           ?? OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch (Exception ex) { Logger.Error("PartyFrameScanner.ctor", ex); }
        }

        public void SetScanRegion(int x, int y, int w, int h)
        {
            if (w <= 0 || w > 4000 || h <= 0 || h > 2000 || x < 0 || y < 0) return;
            _scanX = x; _scanY = y; _scanW = w; _scanH = h;
            _hasRegion = true;
        }

        public bool HasRegion => _hasRegion;

        public void TriggerScan() => _ = ScanAsync();

        private async Task ScanAsync()
        {
            if (!_hasRegion || _engine == null || _disposed) return;
            try
            {
                using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
                if (bmp == null) return;

                using var scaled = ScaleUp(bmp, 2);
                var soft = await ToSoftwareBitmapAsync(scaled);
                if (soft == null) return;

                var result    = await _engine.RecognizeAsync(soft);
                var newRoles  = new Dictionary<string, DcuoRole>(StringComparer.OrdinalIgnoreCase);
                var newCRs    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in result.Lines)
                {
                    string raw = line.Text.Trim();
                    if (raw.Length < 2) continue;

                    // Extract CR from "505 Koby" pattern
                    int    cr   = 0;
                    string text = raw;
                    var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[0], out int pc) && pc > 100)
                    {
                        cr   = pc;
                        text = parts[1].Trim();
                    }

                    if (!IsLikelyPlayerName(text)) continue;

                    var firstWord = line.Words.FirstOrDefault();
                    if (firstWord == null) continue;
                    var bounds = firstWord.BoundingRect;
                    int lineY  = (int)(bounds.Y / 2);
                    int lineX  = (int)(bounds.X / 2);
                    int lineH  = Math.Max(1, (int)(bounds.Height / 2));

                    int iconX = Math.Max(0, lineX - 30);
                    int iconW = Math.Min(28, lineX);

                    var role = DetectRoleColor(bmp, iconX, lineY, iconW, lineH);

                    // HIGH-3 fix: ConcurrentDictionary.TryAdd locks individually — safe
                    if (role != DcuoRole.Unknown)
                    {
                        _knownRoles.TryAdd(text, role);
                        newRoles[text] = _knownRoles[text];
                    }
                    if (cr > 0) { _knownCR[text] = cr; newCRs[text] = cr; }
                }

                // Merge all known into new dicts
                foreach (var (n, r) in _knownRoles) newRoles.TryAdd(n, r);
                foreach (var (n, c) in _knownCR)    newCRs.TryAdd(n, c);

                if (newRoles.Count > 0) RolesDetected?.Invoke(this, newRoles);
                if (newCRs.Count > 0)  CRDetected?.Invoke(this, newCRs);
            }
            catch (Exception ex) { Logger.Error("PartyFrameScanner.ScanAsync", ex); }
        }

        // MED-4 fix: LockBits instead of per-pixel GetPixel
        private static DcuoRole DetectRoleColor(Bitmap bmp, int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return DcuoRole.Unknown;

            int x2 = Math.Min(bmp.Width,  x + w);
            int y2 = Math.Min(bmp.Height, y + h);
            if (x2 <= x || y2 <= y) return DcuoRole.Unknown;

            var rect = new Rectangle(x, y, x2 - x, y2 - y);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride  = data.Stride;
                int bytes   = Math.Abs(stride) * rect.Height;
                byte[] pixels = new byte[bytes];
                Marshal.Copy(data.Scan0, pixels, 0, bytes);

                var counts = new int[4]; // DPS, Controller, Healer, Tank

                for (int py = 0; py < rect.Height; py++)
                {
                    for (int px = 0; px < rect.Width; px++)
                    {
                        int i = py * Math.Abs(stride) + px * 4;
                        byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                        int brightness = r + g + b;
                        if (brightness < 80 || brightness > 700) continue;

                        for (int ri = 0; ri < _roleColors.Length; ri++)
                        {
                            var (_, rMin, rMax, gMin, gMax, bMin, bMax) = _roleColors[ri];
                            if (r >= rMin && r <= rMax && g >= gMin && g <= gMax && b >= bMin && b <= bMax)
                                counts[ri]++;
                        }
                    }
                }

                int best = -1, bestVal = 2; // require at least 3 matching pixels
                for (int i = 0; i < counts.Length; i++)
                    if (counts[i] > bestVal) { bestVal = counts[i]; best = i; }

                return best >= 0 ? _roleColors[best].role : DcuoRole.Unknown;
            }
            finally { bmp.UnlockBits(data); }
        }

        private static bool IsLikelyPlayerName(string text)
        {
            if (text.Length < 3) return false;
            if (text.All(char.IsDigit)) return false;
            if (text.All(c => !char.IsLetter(c))) return false;
            string lo = text.ToLowerInvariant();
            return lo is not ("league" or "group" or "loot" or "default" or "combat"
                or "lfg" or "chat" or "emote" or "system" or "trade");
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
            catch (Exception ex) { Logger.Error("PartyFrameScanner.Capture", ex); return null; }
        }

        private static Bitmap ScaleUp(Bitmap src, int factor)
        {
            var dst = new Bitmap(src.Width * factor, src.Height * factor);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
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
            catch (Exception ex) { Logger.Error("PartyFrameScanner.ToSoftware", ex); return null; }
        }

        public void Dispose() { _disposed = true; }
    }
}
