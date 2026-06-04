using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using DCUOTracker.Models;

namespace DCUOTracker.Services
{
    public class LfgChannelDetector : IDisposable
    {
        public event EventHandler<bool>? LfgChannelChanged;
        public event EventHandler<bool>? ChatInputStateChanged;

        private static OcrEngine? _engine;

        // SemaphoreSlim(1,1) — only ONE scan at a time, prevents race conditions
        private readonly SemaphoreSlim _scanLock = new(1, 1);

        private System.Timers.Timer? _retryTimer;
        private System.Timers.Timer? _cursorPollTimer;
        private bool _disposed;
        private bool _lastLfg;
        private bool _lastChatOpen;

        private int  _scanX = -1, _scanY = -1, _scanW = 200, _scanH = 30;
        private bool _hasRegion;

        public bool IsLfgChannelActive => _lastLfg;

        // Channel names — EXACT short tokens only
        // If OCR returns more than ~20 chars it's a chat message, not input label
        private static readonly string[] ChannelNames =
            ["LFG", "Group", "League", "Tell", "Default",
             "Combat", "Loot", "General", "Trade", "Shout", "System"];

        public void SetScanRegion(int x, int y, int w, int h)
        {
            if (w <= 0 || w > 4000 || h <= 0 || h > 500 || x < 0 || y < 0) return;
            _scanX = x; _scanY = y; _scanW = w; _scanH = h;
            _hasRegion = true;
        }

        public bool HasRegion => _hasRegion;

        public LfgChannelDetector()
        {
            try
            {
                var lang = new Windows.Globalization.Language("en-US");
                _engine  = OcrEngine.TryCreateFromLanguage(lang)
                           ?? OcrEngine.TryCreateFromUserProfileLanguages();
                if (_engine == null)
                    Logger.Warn("LfgChannelDetector", "OCR engine unavailable");
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ctor", ex); }
        }

        // Chat prefixes that indicate which channel a sent message went to
        private static readonly (string prefix, bool isLfg)[] MessagePrefixes =
        [
            ("[LFG]",    true),
            ("[Group]",  false),
            ("[League]", false),
            ("[Tell]",   false),
            ("[Say]",    false),
            ("[Shout]",  false),
        ];

        // ── Public triggers ───────────────────────────────────────────

        public void TriggerEnterScan() => _ = SafeRunAsync(async () =>
        {
            await Task.Delay(200); // wait for game UI to update after Enter
            await RunFullScan();

            // Also scan the WIDER region (input + recent chat lines above)
            // to detect if the sent message has [LFG] prefix
            await ScanForPostedMessageAsync();
        });

        public void TriggerScan() => _ = SafeRunAsync(async () =>
        {
            CancelRetry();
            // After sending — check recent chat lines for [LFG] sent message
            bool lfgFromMessage = await ScanForPostedMessageAsync();
            if (lfgFromMessage) return; // already handled

            bool isLfg = await ScanForLfgAsync();
            NotifyLfgIfChanged(isLfg);
            if (!isLfg) ScheduleLfgRetry();
        });

        /// <summary>
        /// Scans a TALLER region (input line + recent chat messages above it)
        /// to detect if the most recently sent message had an [LFG] or other prefix.
        /// Extend scan height upward to capture the sent message line.
        /// </summary>
        private async Task<bool> ScanForPostedMessageAsync()
        {
            if (_engine == null || !_hasRegion) return false;
            if (!await _scanLock.WaitAsync(100)) return false;
            try
            {
                // Expand scan region UPWARD to capture recent chat messages
                // (sent message appears just above the input line)
                int extendUp = _scanH * 3; // capture ~3 input-line heights above
                int extY     = Math.Max(0, _scanY - extendUp);
                int extH     = _scanH + extendUp;

                using var bmp = CaptureRegion(_scanX, extY, _scanW + 200, extH);
                if (bmp == null) return false;
                using var up  = ScaleUp(bmp, 3);
                var soft      = await ToSoftwareBitmapAsync(up);
                if (soft == null) return false;
                var result    = await _engine.RecognizeAsync(soft);
                string text   = result.Text ?? "";

                // Look for channel prefix in captured text
                foreach (var (prefix, isLfg) in MessagePrefixes)
                {
                    if (text.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info("LfgDetector", $"Posted message detected: {prefix}");
                        if (isLfg)
                        {
                            NotifyLfgIfChanged(true);
                            return true;
                        }
                        // Non-LFG channel confirmed — not LFG
                        NotifyLfgIfChanged(false);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ScanPosted", ex); return false; }
            finally { _scanLock.Release(); }
        }

        // ── Core scan — one at a time via semaphore ───────────────────

        private async Task RunFullScan()
        {
            if (_engine == null || !_hasRegion) return;

            // Semaphore prevents concurrent scans racing each other
            if (!await _scanLock.WaitAsync(100)) return; // skip if scan already running
            try
            {
                string text = await OcrRegionAsync();

                bool chatOpen = IsInputLabel(text);
                bool isLfg    = chatOpen && text.Contains("LFG", StringComparison.OrdinalIgnoreCase);

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
            finally { _scanLock.Release(); }
        }

        private async Task<bool> ScanForCursorAsync()
        {
            if (_engine == null || !_hasRegion) return false;
            if (!await _scanLock.WaitAsync(50)) return _lastChatOpen; // return last known if busy
            try
            {
                string text = await OcrRegionAsync();
                return IsInputLabel(text);
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ScanForCursor", ex); return false; }
            finally { _scanLock.Release(); }
        }

        private async Task<bool> ScanForLfgAsync()
        {
            if (_engine == null || !_hasRegion) return false;
            if (!await _scanLock.WaitAsync(50)) return _lastLfg;
            try
            {
                string text = await OcrRegionAsync();
                return IsInputLabel(text) && text.Contains("LFG", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ScanForLfg", ex); return false; }
            finally { _scanLock.Release(); }
        }

        /// <summary>
        /// Checks if OCR text is a short input-label (channel name only).
        /// Rejects long strings — those are chat MESSAGES containing "[LFG]", not the input indicator.
        /// </summary>
        private static bool IsInputLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();

            // Input label is SHORT — just the channel name, maybe with cursor char
            // Chat messages are long (player: text). Reject anything >25 chars.
            if (trimmed.Length > 25) return false;

            // Must start with a known channel name
            return ChannelNames.Any(ch =>
                trimmed.StartsWith(ch, StringComparison.OrdinalIgnoreCase));
        }

        // ── OCR helper — scales up region for better accuracy ─────────

        private async Task<string> OcrRegionAsync()
        {
            using var bmp = CaptureRegion(_scanX, _scanY, _scanW, _scanH);
            if (bmp == null) return "";
            using var up  = ScaleUp(bmp, 3);
            var soft      = await ToSoftwareBitmapAsync(up);
            if (soft == null) return "";
            var result    = await _engine!.RecognizeAsync(soft);
            return result.Text ?? "";
        }

        // ── Cursor polling ────────────────────────────────────────────

        private void StartCursorPolling()
        {
            if (_cursorPollTimer != null) return;
            _cursorPollTimer = new System.Timers.Timer(500); // 500ms — less aggressive
            _cursorPollTimer.Elapsed += (_, _) => _ = SafeRunAsync(async () =>
            {
                if (_disposed) { StopCursorPolling(); return; }
                bool visible = await ScanForCursorAsync();
                NotifyChatIfChanged(visible);
                if (!visible) StopCursorPolling();
            });
            _cursorPollTimer.Start();
        }

        private void StopCursorPolling()
        {
            _cursorPollTimer?.Stop();
            _cursorPollTimer?.Dispose();
            _cursorPollTimer = null;
        }

        // ── LFG retry ─────────────────────────────────────────────────

        private void ScheduleLfgRetry()
        {
            if (_disposed) return;
            _retryTimer           = new System.Timers.Timer(120_000);
            _retryTimer.AutoReset = false;
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

        // ── Notify ────────────────────────────────────────────────────

        private void NotifyLfgIfChanged(bool v)
        {
            if (v == _lastLfg) return;
            _lastLfg = v;
            LfgChannelChanged?.Invoke(this, v);
        }

        private void NotifyChatIfChanged(bool v)
        {
            if (v == _lastChatOpen) return;
            _lastChatOpen = v;
            ChatInputStateChanged?.Invoke(this, v);
        }

        // ── Safe async wrapper ────────────────────────────────────────

        private static async Task SafeRunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Logger.Error("LfgChannelDetector", ex); }
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
            catch (Exception ex) { Logger.Error("LfgChannelDetector.Capture", ex); return null; }
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
            catch (Exception ex) { Logger.Error("LfgChannelDetector.ToSoftware", ex); return null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopCursorPolling();
            CancelRetry();
            _scanLock.Dispose();
        }
    }
}
