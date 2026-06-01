using System.Collections.ObjectModel;
using System.Windows;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using DCUOTracker.Data;
using DCUOTracker.Models;
using DCUOTracker.Services;

namespace DCUOTracker
{
    public partial class MainWindow : Window
    {
        private readonly LogWatcher         _watcher;
        private readonly Database           _db;
        private readonly KeyboardHook       _keyboardHook;
        private readonly ChatTracker        _chatTracker;
        private readonly ChatOverlay        _chatOverlay;
        private readonly LfgChannelDetector _lfgDetector;
        private readonly OverlaySettings    _settings;
        private readonly ObservableCollection<NthMetalDrop> _sessionDrops = new();
        private bool _soundEnabled = true;
        private bool _alwaysOnTop  = false;

        // Session item drop totals
        private int _sessionSourceMarks = 0;
        private int _sessionAllyFavors  = 0;
        private int _sessionExobytes    = 0;
        private int _sessionExoStandard  = 0;
        private int _sessionExoFlawed    = 0;
        private int _sessionExoSolid     = 0;
        private int _sessionExoPristine  = 0;
        private int _sessionExoFlawless  = 0;
        private int _sessionExoTimeless  = 0;
        private int _sessionExoPerfected = 0;
        private int _sessionAllianceRare      = 0;
        private int _sessionAllianceEpic      = 0;
        private int _sessionAllianceLegendary = 0;

        private static readonly string LogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "DC Universe Online", "Logs", "combat.log");

        public MainWindow()
        {
            InitializeComponent();

            // Nth Metal tracker
            _db      = new Database();
            _watcher = new LogWatcher(LogPath);
            _watcher.DropDetected     += OnDropDetected;
            _watcher.ItemDropDetected += OnItemDropDetected;
            _watcher.Start();

            DropGrid.ItemsSource = _sessionDrops;
            LogPathText.Text     = LogPath;
            RefreshAllTimeStats();
            RefreshItemStats();

            // Load settings FIRST — single instance shared across all components
            _settings = OverlaySettings.Load();

            // Chat tools
            _keyboardHook = new KeyboardHook();
            _chatTracker  = new ChatTracker(_keyboardHook);
            _chatTracker.ChatStateChanged += OnChatStateChanged;
            _chatTracker.LfgTimerUpdated  += OnLfgTimerUpdated;

            // Floating overlay — pass shared settings so it never creates its own
            _chatOverlay = new ChatOverlay(_settings);

            // LFG channel auto-detect via OCR
            _lfgDetector = new LfgChannelDetector();

            // Apply saved scan region
            if (_settings.HasScanRegion)
            {
                _lfgDetector.SetScanRegion(
                    _settings.ScanX, _settings.ScanY,
                    _settings.ScanWidth, _settings.ScanHeight);
                RegionStatus.Text       = $"Region: {_settings.ScanWidth}×{_settings.ScanHeight} @ {_settings.ScanX},{_settings.ScanY}";
                SelectRegionBtn.Content = "🎯 Region Set ✓";
            }
            _lfgDetector.LfgChannelChanged += (_, isLfg) =>
            {
                _chatTracker.IsLfgModeActive = isLfg ? () => true : null;
                _chatOverlay.SetLfgChannelDetected(isLfg);
            };

            // Give ChatTracker direct access to detector's confirmed LFG state
            _chatTracker.Detector = _lfgDetector;

            // OCR verify on every Enter press — corrects open/closed state
            _chatTracker.OnEnterPressed = () => _lfgDetector.TriggerEnterScan();

            // OCR LFG tab check on message send
            _chatTracker.OnMessageSent = () => _lfgDetector.TriggerScan();

            // OCR-verified chat state overrides keystroke tracking
            _lfgDetector.ChatInputStateChanged += (_, chatOpen) =>
            {
                // Correct internal chat state based on actual OCR result
                _chatTracker.CorrectChatState(chatOpen);
            };
        }

        // ── Nth Metal ────────────────────────────────────────────

        private void OnDropDetected(object? sender, DropEventArgs e)
        {
            var drop = e.Drop;
            _db.InsertDrop(drop);

            if (_soundEnabled)
                SoundAlert.Play(drop.MetalType);

            Dispatcher.Invoke(() =>
            {
                _sessionDrops.Insert(0, drop);
                RefreshSessionStats();
                RefreshAllTimeStats();
            });
        }

        private void RefreshSessionStats()
        {
            int totalDrops = 0, totalXp = 0;
            int raw = 0, extracted = 0, treated = 0, processed = 0, refined = 0, purified = 0;

            foreach (var d in _sessionDrops)
            {
                totalDrops += d.Quantity;
                totalXp    += d.XpValue * d.Quantity;
                switch (d.MetalType.ToLower())
                {
                    case "raw":       raw++;       break;
                    case "extracted": extracted++; break;
                    case "treated":   treated++;   break;
                    case "processed": processed++; break;
                    case "refined":   refined++;   break;
                    case "purified":  purified++;  break;
                }
            }

            SessionDropCount.Text = totalDrops.ToString("N0");
            SessionXp.Text        = totalXp.ToString("N0");
            CountRaw.Text         = raw.ToString();
            CountExtracted.Text   = extracted.ToString();
            CountTreated.Text     = treated.ToString();
            CountProcessed.Text   = processed.ToString();
            CountRefined.Text     = refined.ToString();
            CountPurified.Text    = purified.ToString();

            if (_sessionDrops.Count > 0)
            {
                var last = _sessionDrops[0];
                LastDropText.Text = $"{last.Quantity}x {last.MetalType}";
            }
        }

        private void OnItemDropDetected(object? sender, ItemDropEventArgs e)
        {
            var drop = e.Drop;
            _db.InsertItemDrop(drop);

            if (_soundEnabled)
                SoundAlert.Play(drop.DropType == ItemDropType.SourceMark ? "refined" : "processed");

            Dispatcher.Invoke(() =>
            {
                switch (drop.DropType)
                {
                    case ItemDropType.SourceMark: _sessionSourceMarks += drop.Quantity; break;
                    case ItemDropType.AllyFavor:  _sessionAllyFavors  += drop.Quantity; break;
                    case ItemDropType.Exobyte:
                        _sessionExobytes += drop.Quantity;
                        switch (ExobyteQuality.GetQuality(drop.ItemName))
                        {
                            case "Flawed":    _sessionExoFlawed    += drop.Quantity; break;
                            case "Solid":     _sessionExoSolid     += drop.Quantity; break;
                            case "Pristine":  _sessionExoPristine  += drop.Quantity; break;
                            case "Flawless":  _sessionExoFlawless  += drop.Quantity; break;
                            case "Timeless":  _sessionExoTimeless  += drop.Quantity; break;
                            case "Perfected": _sessionExoPerfected += drop.Quantity; break;
                            default:          _sessionExoStandard  += drop.Quantity; break;
                        }
                        break;
                    case ItemDropType.Alliance:
                        if (drop.ItemName.StartsWith("Rare",      StringComparison.OrdinalIgnoreCase)) _sessionAllianceRare      += drop.Quantity;
                        else if (drop.ItemName.StartsWith("Epic", StringComparison.OrdinalIgnoreCase)) _sessionAllianceEpic      += drop.Quantity;
                        else                                                                            _sessionAllianceLegendary += drop.Quantity;
                        break;
                }
                RefreshItemStats();
            });
        }

        private void RefreshItemStats()
        {
            SessionSourceMarks.Text = _sessionSourceMarks.ToString("N0");
            SessionAllyFavors.Text  = _sessionAllyFavors.ToString("N0");
            // Exobyte per-quality session counts
            ExoStandard.Text  = _sessionExoStandard.ToString();
            ExoFlawed.Text    = _sessionExoFlawed.ToString();
            ExoSolid.Text     = _sessionExoSolid.ToString();
            ExoPristine.Text  = _sessionExoPristine.ToString();
            ExoFlawless.Text  = _sessionExoFlawless.ToString();
            ExoTimeless.Text  = _sessionExoTimeless.ToString();
            ExoPerfected.Text = _sessionExoPerfected.ToString();
            ExoTotal.Text     = _sessionExobytes.ToString();
            AllianceRare.Text       = _sessionAllianceRare.ToString("N0");
            AllianceEpic.Text       = _sessionAllianceEpic.ToString("N0");
            AllianceLegendary.Text  = _sessionAllianceLegendary.ToString("N0");

            var all = _db.GetItemDropsAllTime();
            AllTimeSourceMarks.Text = $"{all.Where(d => d.DropType == ItemDropType.SourceMark).Sum(d => d.Quantity):N0} all time";
            AllTimeAllyFavors.Text  = $"{all.Where(d => d.DropType == ItemDropType.AllyFavor).Sum(d => d.Quantity):N0} all time";
            var allAlliance = all.Where(d => d.DropType == ItemDropType.Alliance).ToList();
            int atRare   = allAlliance.Where(d => d.ItemName.StartsWith("Rare",      StringComparison.OrdinalIgnoreCase)).Sum(d => d.Quantity);
            int atEpic   = allAlliance.Where(d => d.ItemName.StartsWith("Epic",      StringComparison.OrdinalIgnoreCase)).Sum(d => d.Quantity);
            int atLeg    = allAlliance.Where(d => d.ItemName.StartsWith("Legendary", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Quantity);
            AllTimeAlliance.Text = $"R:{atRare:N0}  E:{atEpic:N0}  L:{atLeg:N0} all time";

            // Exobytes: total count + XP tooltip
            var exobytes   = all.Where(d => d.DropType == ItemDropType.Exobyte).ToList();
            int exoTotal   = exobytes.Sum(d => d.Quantity);
            int exoTotalXp = exobytes.Sum(d => ExobyteQuality.GetXpValue(d.ItemName) * d.Quantity);
            AllTimeExobytes.Text = $"{exoTotal:N0} all time";

            // Quality breakdown + XP in tooltip
            var byQuality = exobytes
                .GroupBy(d => ExobyteQuality.GetQuality(d.ItemName))
                .OrderByDescending(g => ExobyteQuality.GetXpValue(g.Key + " "))
                .Select(g =>
                {
                    int qty    = g.Sum(d => d.Quantity);
                    int xpEach = ExobyteQuality.GetXpValue(g.Key + " ");
                    int xpTot  = xpEach * qty;
                    return $"{g.Key,-10} {qty,5:N0} drops  ≈ {xpTot,8:N0} XP  ({xpEach} each)";
                });
            AllTimeExobytes.ToolTip =
                $"~{exoTotalXp:N0} Total Augment XP\n" +
                "─────────────────────────────\n" +
                string.Join("\n", byQuality);

            ExoTotal.ToolTip = $"Session: {_sessionExobytes:N0} exobytes\nHover all-time for XP breakdown";

            var last = all.FirstOrDefault();
            if (last != null)
            {
                string quality = last.DropType == ItemDropType.Exobyte
                    ? $"[{ExobyteQuality.GetQuality(last.ItemName)}] " : "";
                LastItemText.Text = $"{last.Quantity}x {quality}{last.ItemName}";
            }
        }

        private void RefreshAllTimeStats()
        {
            var all       = _db.GetAllTime();
            int totalDrops = all.Sum(d => d.Quantity);
            int totalXp    = all.Sum(d => d.XpValue);
            AllTimeDropCount.Text = totalDrops.ToString("N0");
            AllTimeXp.Text        = totalXp.ToString("N0");
        }

        // ── Chat Counter ─────────────────────────────────────────

        private void OnChatStateChanged(object? sender, ChatStateChangedArgs e)
        {
            // Push to floating overlay
            _chatOverlay.UpdateChatState(e);

            // Also update inline bar in main window
            Dispatcher.Invoke(() =>
            {
                CharProgressBar.Value = e.CharCount;

                if (!e.IsActive)
                {
                    CharCountLabel.Text        = "0 / 60";
                    CharCountLabel.Foreground  = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                    CharProgressBar.Foreground = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                    CharProgressBar.Value      = 0;
                    return;
                }

                CharCountLabel.Text = $"{e.CharCount} / 60";

                if (e.AtLimit)
                {
                    CharCountLabel.Foreground  = new WpfBrush(WpfColor.FromRgb(255, 68, 68));
                    CharProgressBar.Foreground = new WpfBrush(WpfColor.FromRgb(255, 68, 68));
                    if (_soundEnabled) Console.Beep(800, 80);
                }
                else if (e.CharCount >= 50)
                {
                    CharCountLabel.Foreground  = new WpfBrush(WpfColor.FromRgb(255, 170, 0));
                    CharProgressBar.Foreground = new WpfBrush(WpfColor.FromRgb(255, 170, 0));
                }
                else
                {
                    CharCountLabel.Foreground  = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                    CharProgressBar.Foreground = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                }
            });
        }

        // ── LFG Timer ────────────────────────────────────────────

        private void OnLfgTimerUpdated(object? sender, LfgTimerArgs e)
        {
            // Push to floating overlay
            _chatOverlay.UpdateLfgTimer(e);

            Dispatcher.Invoke(() =>
            {
                if (e.Expired)
                {
                    LfgPanel.Visibility = Visibility.Collapsed;
                    if (_soundEnabled) { Console.Beep(880, 200); Console.Beep(1100, 200); }
                    return;
                }

                LfgPanel.Visibility  = Visibility.Visible;
                LfgTimerLabel.Text   = $"{e.SecondsRemaining}s";

                if (e.SecondsRemaining <= 10)
                {
                    LfgTimerLabel.Foreground = new WpfBrush(WpfColor.FromRgb(255, 68, 68));
                    LfgPanel.Background      = new WpfBrush(WpfColor.FromRgb(61, 0, 0));
                }
                else if (e.SecondsRemaining <= 30)
                {
                    LfgTimerLabel.Foreground = new WpfBrush(WpfColor.FromRgb(255, 170, 0));
                    LfgPanel.Background      = new WpfBrush(WpfColor.FromRgb(61, 32, 0));
                }
                else
                {
                    LfgTimerLabel.Foreground = new WpfBrush(WpfColor.FromRgb(0, 212, 255));
                    LfgPanel.Background      = new WpfBrush(WpfColor.FromRgb(15, 52, 96));
                }
            });
        }

        // ── Button Handlers ──────────────────────────────────────

        private void ClearSession_Click(object sender, RoutedEventArgs e)
        {
            _sessionDrops.Clear();
            RefreshSessionStats();
        }

        private void ViewAll_Click(object sender, RoutedEventArgs e)
        {
            var all = _db.GetAllTime();
            _sessionDrops.Clear();
            foreach (var d in all)
                _sessionDrops.Add(d);
        }

        private void SoundToggle_Click(object sender, RoutedEventArgs e)
        {
            _soundEnabled          = !_soundEnabled;
            SoundToggleBtn.Content = _soundEnabled ? "🔊 Sound ON" : "🔇 Sound OFF";
        }

        private void PopoutChat_Click(object sender, RoutedEventArgs e)
        {
            if (_chatOverlay.IsVisible)
                _chatOverlay.Hide();
            else
                _chatOverlay.Show();
        }

        // M-7: async to avoid blocking UI thread with t.Join()
        private async void SelectRegion_Click(object sender, RoutedEventArgs e)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<System.Drawing.Rectangle?>();

            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    System.Windows.Forms.Application.EnableVisualStyles();
                    var selector = new Services.RegionSelector();
                    selector.ShowDialog();
                    tcs.SetResult(selector.SelectedRegion);
                }
                catch (Exception ex)
                {
                    DCUOTracker.Services.Logger.Error("SelectRegion", ex);
                    tcs.SetResult(null);
                }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();

            var result = await tcs.Task; // UI thread stays alive

            if (result is { } r)
            {
                _lfgDetector.SetScanRegion(r.X, r.Y, r.Width, r.Height);
                _settings.ScanX      = r.X;
                _settings.ScanY      = r.Y;
                _settings.ScanWidth  = r.Width;
                _settings.ScanHeight = r.Height;
                _settings.Save();

                RegionStatus.Text       = $"Region: {r.Width}×{r.Height} @ {r.X},{r.Y}";
                SelectRegionBtn.Content = "🎯 Region Set ✓";
            }
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            _alwaysOnTop           = !_alwaysOnTop;
            Topmost                = _alwaysOnTop;
            AlwaysOnTopBtn.Content = _alwaysOnTop ? "📌 ON TOP" : "📌 OFF";
            AlwaysOnTopBtn.Foreground = _alwaysOnTop
                ? new WpfBrush(WpfColor.FromRgb(0, 212, 255))
                : new WpfBrush(WpfColor.FromRgb(100, 100, 100));
        }

        protected override void OnClosed(EventArgs e)
        {
            _settings.Save();
            _watcher.Stop();
            _watcher.Dispose();
            _chatTracker.Dispose();
            _keyboardHook.Dispose();
            _lfgDetector.Dispose();
            // L-6: use ForceClose so OnClosing cancel doesn't leak the HWND
            _chatOverlay.Dispatcher.Invoke(() =>
            {
                try { _chatOverlay.ForceClose(); }
                catch (Exception ex) { DCUOTracker.Services.Logger.Error("MainWindow.OnClosed", ex); }
            });
            base.OnClosed(e);
        }
    }
}

