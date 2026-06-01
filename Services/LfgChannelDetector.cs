using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace DCUOTracker.Services
{
    public class LfgChannelDetector : IDisposable
    {
        public event EventHandler<bool>? LfgChannelChanged;
        public event EventHandler<bool>? ChatInputStateChanged;

        private static OcrEngine? _engine;
        private System.Timers.Timer? _retryTimer;
        private System.Timers.Timer? _cursorPollTimer;
        private bool _disposed     = false;
        private bool _lastLfg      = false;
        private bool _lastChatOpen = false;

        private int  _scanX = -1, _scanY = -1, _scanW = 200, _scanH = 30;
        private bool _hasRegion = false;

        public bool IsLfgChannelActive => _lastLfg;

        private static readonly string[] ChannelNames =
        [
            "LFG", "Group", "League", "Tell", "Default",
            "Combat", "Loot", "General", "Trade", "Shout", "System"
        ];

        public void SetScanRegion(int x, int y, int w, int h)
        {
            // M-8: validate bounds before accepting
            if (w <= 0 || w > 4000 || h <= 0 || h > 500 || x < 0 || y < 0)
            {
                Logger.Warn("LfgChannelDetector.SetScanRegion",
                    $"Invalid region ignored: x={x} y={y} w={w} h={h}");
                return;
            }
            _scanX = x; _scanY = y; _scanW = w; _scanH = h;
            _hasRegion = true;
        }

        public LfgChannelDetector()
        {
            try
            {
                var lang = new Windows.Globalization.Language("en-US");
                _engine  = OcrEngine.TryCreateFromLanguage(lang)
                           ?? OcrEngine.TryCreateFromUserProfileLanguages();
                if (_engine == null)
                    Logger.Warn("LfgChannelDetector", "OCR engine unavailable — channel detection disabled");
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ctor", ex); }
        }

        // ── Enter pressed — scan + start polling ─────────────────────

        public void TriggerEnterScan()
        {
            _ = SafeRunAsync(async () =>
            {
                await Task.Delay(180);
                await RunFullScan();
            });
        }

        public void TriggerScan()
        {
            CancelRetry();
            _ = SafeRunAsync(async () =>
            {
                bool isLfg = await ScanForLfgAsync();
                NotifyLfgIfChanged(isLfg);
                if (!isLfg) ScheduleLfgRetry();
            });
        }

        // ── Full scan ─────────────────────────────────────────────────

        private async Task RunFullScan()
        {
            if (_engine == null || !_hasRegion) return;
            try
            {
                using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
                if (bmp == null) return;

                using var up = ScaleUp(bmp, 3);
                var soft     = await ToSoftwareBitmapAsync(up);
                if (soft == null) return;

                var result  = await _engine.RecognizeAsync(soft);
                string text = (result.Text ?? "").Trim();

                bool chatOpen = ChannelNames.Any(ch =>
                    text.Contains(ch, StringComparison.OrdinalIgnoreCase));
                bool isLfg = text.Contains("LFG", StringComparison.OrdinalIgnoreCase);

                NotifyChatIfChanged(chatOpen);

                if (chatOpen)
                {
                    NotifyLfgIfChanged(isLfg);
                    if (!isLfg) ScheduleLfgRetry();
                    StartCursorPolling();
                }
                else
                {
                    NotifyLfgIfChanged(false);
                    StopCursorPolling();
                    CancelRetry();
                }
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.RunFullScan", ex); }
        }

        // ── Cursor polling ────────────────────────────────────────────

        private void StartCursorPolling()
        {
            if (_cursorPollTimer != null) return;
            _cursorPollTimer = new System.Timers.Timer(400);
            // H-3: proper try/catch — not async void
            _cursorPollTimer.Elapsed += (_, _) => _ = SafeRunAsync(async () =>
            {
                if (_disposed) { StopCursorPolling(); return; }
                bool cursorVisible = await ScanForCursorAsync();
                NotifyChatIfChanged(cursorVisible);
                if (!cursorVisible) StopCursorPolling();
            });
            _cursorPollTimer.Start();
        }

        private void StopCursorPolling()
        {
            _cursorPollTimer?.Stop();
            _cursorPollTimer?.Dispose();
            _cursorPollTimer = null;
        }

        // ── OCR scan methods ──────────────────────────────────────────

        private async Task<bool> ScanForCursorAsync()
        {
            if (_engine == null || !_hasRegion) return false;
            try
            {
                using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
                if (bmp == null) return false;
                using var up = ScaleUp(bmp, 3);
                var soft     = await ToSoftwareBitmapAsync(up);
                if (soft == null) return false;
                var result   = await _engine.RecognizeAsync(soft);
                string text  = (result.Text ?? "").Trim();
                return ChannelNames.Any(ch =>
                    text.Contains(ch, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ScanForCursor", ex); return false; }
        }

        private async Task<bool> ScanForLfgAsync()
        {
            if (_engine == null || !_hasRegion) return false;
            try
            {
                using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
                if (bmp == null) return false;
                using var up  = ScaleUp(bmp, 3);
                var soft      = await ToSoftwareBitmapAsync(up);
                if (soft == null) return false;
                var result    = await _engine.RecognizeAsync(soft);
                string text   = (result.Text ?? "").Trim();
                bool chatOpen = ChannelNames.Any(ch =>
                    text.Contains(ch, StringComparison.OrdinalIgnoreCase));
                bool isLfg    = text.Contains("LFG", StringComparison.OrdinalIgnoreCase);
                return chatOpen && isLfg;
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ScanForLfg", ex); return false; }
        }

        // ── Notify helpers ────────────────────────────────────────────

        private void NotifyLfgIfChanged(bool isLfg)
        {
            if (isLfg == _lastLfg) return;
            _lastLfg = isLfg;
            LfgChannelChanged?.Invoke(this, isLfg);
        }

        private void NotifyChatIfChanged(bool isOpen)
        {
            if (isOpen == _lastChatOpen) return;
            _lastChatOpen = isOpen;
            ChatInputStateChanged?.Invoke(this, isOpen);
        }

        // ── Retry ─────────────────────────────────────────────────────

        private void ScheduleLfgRetry()
        {
            if (_disposed) return;
            _retryTimer           = new System.Timers.Timer(120_000);
            _retryTimer.AutoReset = false;
            // H-3: proper try/catch
            _retryTimer.Elapsed  += (_, _) => _ = SafeRunAsync(async () =>
            {
                if (_disposed) return;
                bool isLfg = await ScanForLfgAsync();
                NotifyLfgIfChanged(isLfg);
            });
            _retryTimer.Start();
        }

        private void CancelRetry()
        {
            _retryTimer?.Stop();
            _retryTimer?.Dispose();
            _retryTimer = null;
        }

        // ── Safe async wrapper — prevents async void crash ────────────

        private static async Task SafeRunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.SafeRun", ex); }
        }

        // ── Image helpers ─────────────────────────────────────────────

        private static Bitmap? CaptureRegion(int x, int y, int w, int h)
        {
            try
            {
                var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                return bmp;
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.CaptureRegion", ex); return null; }
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
                return await dec.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ToSoftwareBitmap", ex); return null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopCursorPolling();
            CancelRetry();
        }
    }
}
