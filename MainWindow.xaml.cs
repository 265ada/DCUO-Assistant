using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
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
        private readonly DpsParser            _dpsParser;
        private readonly DpsOverlay          _dpsOverlay;
        private readonly PartyFrameScanner   _partyScanner;
        private readonly FightReportStore    _reportStore;
        private readonly GameForegroundWatcher _fgWatcher;
        private readonly AppWatchdog         _watchdog;
        private GlobalHotkey?                _hideAllHotkey;
        private readonly ObservableCollection<NthMetalDrop> _sessionDrops = new();
        private readonly ObservableCollection<NthMetalDrop> _partyDrops  = new();
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

            // Load settings FIRST â€” used throughout constructor
            _settings = OverlaySettings.Load();

            // Nth Metal tracker
            _db      = new Database();
            _watcher = new LogWatcher(LogPath);
            _watcher.DropDetected     += OnDropDetected;
            _watcher.ItemDropDetected += OnItemDropDetected;
            _watcher.Start();

            DropGrid.ItemsSource = _sessionDrops;
            LogPathText.Text     = LogPath;
            VersionLabel.Text    = $"v{AutoUpdater.CurrentVersion}";
            if (!string.IsNullOrEmpty(_settings.MyCharacterName))
            {
                try { MyCharBox.Text = _settings.MyCharacterName; } catch {}
            }
            RefreshAllTimeStats();
            RefreshItemStats();

            // Restore last drop-log view mode on launch
            if (_settings.ViewAllTimeMode)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(LoadAllTimeDrops));

            // DPS parser + overlay + fight reports
            _dpsOverlay   = new DpsOverlay();
            _dpsParser    = new DpsParser(LogPath);
            _partyScanner = new PartyFrameScanner();
            _reportStore  = new FightReportStore();
            LoadReports(); // must be after _reportStore init
            _watchdog     = new AppWatchdog();

            // Foreground watcher â€” auto-hide overlays when not in game
            _fgWatcher = new GameForegroundWatcher
            {
                PersistOverlay = _settings.PersistOverlay
            };
            _fgWatcher.GameLostFocus   += () => Dispatcher.Invoke(() => {
                if (!_settings.PersistOverlay) _dpsOverlay.Hide();
            });
            _fgWatcher.GameGainedFocus += () => Dispatcher.Invoke(() => {
                if (_dpsParser.CurrentFight != null) _dpsOverlay.Show();
            });
            _fgWatcher.Start();

            // Load saved party frame region
            if (_settings.HasScanRegion) { /* chat region already loaded */ }
            // Party frame region stored separately in settings if we add PartyX/Y/W/H fields
            // For now trigger scan on every fight update
            _partyScanner.RolesDetected += (_, roles) =>
            {
                var fight = _dpsParser.CurrentFight;
                if (fight == null) return;
                foreach (var (name, role) in roles)
                    if (fight.Players.TryGetValue(name, out var ps))
                        ps.SetRoleFromPartyFrame(role);
            };

            _partyScanner.CRDetected += (_, crs) =>
            {
                var fight = _dpsParser.CurrentFight;
                if (fight == null) return;
                foreach (var (name, cr) in crs)
                    if (fight.Players.TryGetValue(name, out var ps))
                        ps.CR = cr;
            };

            _dpsParser.FightUpdated += (_, e) => {
                _partyScanner.TriggerScan();
                _dpsOverlay.UpdateFight(e.Fight);
                _watchdog.RecordOverlayUpdate();
            };
            _dpsParser.FightStarted += (_, e) => {
                _partyScanner.TriggerScan();
                _dpsOverlay.UpdateFight(e.Fight);
            };
            _dpsParser.FightEnded += (_, e) => {
                _dpsOverlay.UpdateFight(e.Fight);   // show frozen stats
                _reportStore.SaveFight(e.Fight);    // auto-save report
            };
            _dpsParser.Start();

            // Chat tools
            _keyboardHook = new KeyboardHook();
            _chatTracker  = new ChatTracker(_keyboardHook);
            _chatTracker.ChatStateChanged += OnChatStateChanged;
            _chatTracker.LfgTimerUpdated  += OnLfgTimerUpdated;

            // Floating overlay â€” pass shared settings so it never creates its own
            _chatOverlay = new ChatOverlay(_settings);

            // LFG channel auto-detect via OCR
            _lfgDetector = new LfgChannelDetector();

            // Apply saved scan region
            if (_settings.HasScanRegion)
            {
                _lfgDetector.SetScanRegion(
                    _settings.ScanX, _settings.ScanY,
                    _settings.ScanWidth, _settings.ScanHeight);
                RegionStatus.Text = _settings.ScanWidth + "x" + _settings.ScanHeight + " OK";
            }
            _lfgDetector.LfgChannelChanged += (_, isLfg) =>
            {
                _chatTracker.IsLfgModeActive = isLfg ? () => true : null;
                _chatOverlay.SetLfgChannelDetected(isLfg);
            };

            _chatTracker.Detector = _lfgDetector;

            // LFG timer persistence â€” save timestamp when timer starts
            _chatTracker.OnLfgStarted = utc => {
                _settings.LastLfgPostUtc = utc;
                _settings.Save();
            };

            // Restore LFG timer if still active from before restart
            if (_settings.LastLfgPostUtc.HasValue)
                _chatTracker.RestoreLfgTimer(_settings.LastLfgPostUtc.Value);

            // OCR verify on every Enter press â€” corrects open/closed state
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

        // â”€â”€ Nth Metal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool IsMyCharacter(string n) => string.IsNullOrEmpty(_settings.MyCharacterName) || n.Equals(_settings.MyCharacterName, StringComparison.OrdinalIgnoreCase);

        private void OnDropDetected(object? sender, DropEventArgs e)
        {
            var drop   = e.Drop;
            drop.IsOwn = IsMyCharacter(drop.Character);

            // Auto-fill character box if not yet set
            if (string.IsNullOrEmpty(_settings.MyCharacterName) && !string.IsNullOrEmpty(drop.Character))
                Dispatcher.BeginInvoke(() => { try { MyCharBox.Text = drop.Character; } catch {} });

            _db.InsertDrop(drop);

            if (_soundEnabled && drop.IsOwn)
                SoundAlert.Play(drop.MetalType);

            Dispatcher.Invoke(() =>
            {
                if (drop.IsOwn)
                {
                    _sessionDrops.Insert(0, drop);
                    RefreshSessionStats();
                    RefreshAllTimeStats();
                }
                else
                {
                    _partyDrops.Insert(0, drop);
                }
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
            var drop   = e.Drop;
            drop.IsOwn = IsMyCharacter(drop.Character);
            _db.InsertItemDrop(drop);

            if (_soundEnabled && drop.IsOwn)
                SoundAlert.Play(drop.DropType == ItemDropType.SourceMark ? "refined" : "processed");

            if (!drop.IsOwn) return; // don't count others' items in my stats

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

            // Only count MY drops in all-time item stats
            var all = _db.GetItemDropsAllTime().Where(d => IsMyCharacter(d.Character)).ToList();
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
                    return $"{g.Key,-10} {qty,5:N0} drops  â‰ˆ {xpTot,8:N0} XP  ({xpEach} each)";
                });
            AllTimeExobytes.ToolTip =
                $"~{exoTotalXp:N0} Total Augment XP\n" +
                "â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€\n" +
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
            // Only count MY drops in all-time stats
            var all        = _db.GetAllTime().Where(d => IsMyCharacter(d.Character)).ToList();
            int totalDrops = all.Sum(d => d.Quantity);
            int totalXp    = all.Sum(d => d.XpValue);
            AllTimeDropCount.Text = totalDrops.ToString("N0");
            AllTimeXp.Text        = totalXp.ToString("N0");
        }

        // â”€â”€ Chat Counter â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ LFG Timer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ Button Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ClearSession_Click(object sender, RoutedEventArgs e)
        {
            _settings.ViewAllTimeMode = false;
            _settings.Save();
            _sessionDrops.Clear();
            _partyDrops.Clear();
            RefreshSessionStats();
        }

        private void ViewAll_Click(object sender, RoutedEventArgs e)
        {
            _settings.ViewAllTimeMode = true;
            _settings.Save();
            LoadAllTimeDrops();
        }

        private void LoadAllTimeDrops()
        {
            var all = _db.GetAllTime();
            _sessionDrops.Clear();
            _partyDrops.Clear();
            foreach (var d in all)
            {
                d.IsOwn = IsMyCharacter(d.Character);
                if (d.IsOwn) _sessionDrops.Add(d);
                else         _partyDrops.Add(d);
            }
        }

        private void SoundToggle_Click(object sender, RoutedEventArgs e)
        {
            _soundEnabled = !_soundEnabled;
            SoundToggleBtn_Icon.Text = "◉";
            SoundToggleBtn_Icon.Foreground = _soundEnabled
                ? new WpfBrush(WpfColor.FromRgb(255, 255, 255))
                : new WpfBrush(WpfColor.FromRgb(40, 70, 50));
            SoundToggleBtn_Label.Text = _soundEnabled ? "SOUND ON" : "SOUND OFF";
            SoundToggleBtn_Label.Foreground = _soundEnabled
                ? new WpfBrush(WpfColor.FromRgb(0, 255, 136))
                : new WpfBrush(WpfColor.FromRgb(100, 40, 40));
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

                RegionStatus.Text = $"{r.Width}Ã—{r.Height}";
            }
        }

        private void DpsOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (_dpsOverlay.IsVisible)
                _dpsOverlay.Hide();
            else
                _dpsOverlay.Show();
        }

        private async void SelectPartyRegion_Click(object sender, RoutedEventArgs e)
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
                catch (Exception ex) { Logger.Error("SelectPartyRegion", ex); tcs.SetResult(null); }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();

            var result = await tcs.Task;
            if (result is { } r)
            {
                _partyScanner.SetScanRegion(r.X, r.Y, r.Width, r.Height);
                SelectPartyRegionBtn.Content = "ðŸ‘¥ Party Frame Set âœ“";
                _partyScanner.TriggerScan();
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.Content = "âŸ³ Checking...";
            CheckUpdateBtn.IsEnabled = false;
            try
            {
                var update = await AutoUpdater.CheckForUpdateAsync();
                if (update == null)
                {
                    CheckUpdateBtn_Icon.Text = "âœ“";
                    CheckUpdateBtn_Icon.Foreground = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                    await Task.Delay(2000);
                    CheckUpdateBtn_Icon.Text = "âŸ³";
                    CheckUpdateBtn_Icon.Foreground = new WpfBrush(WpfColor.FromRgb(58, 90, 122));
                }
                else
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Update available: v{update.Version}\n\nDownload and install now?\nApp will restart automatically.",
                        "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        CheckUpdateBtn.Content = "âŸ³ Downloading...";
                        await AutoUpdater.DownloadAndInstallAsync(update,
                            new Progress<int>(p => CheckUpdateBtn.Content = $"âŸ³ {p}%"));
                    }
                    else CheckUpdateBtn.Content = "âŸ³ Check Update";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CheckUpdate_Click", ex);
                CheckUpdateBtn.Content = "âŸ³ Check Update";
            }
            finally { CheckUpdateBtn.IsEnabled = true; }
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void TabMyDrops_Click(object sender, RoutedEventArgs e)
        {
            DropGrid.Visibility = Visibility.Visible;
            PartyDropGrid.Visibility = Visibility.Collapsed;
            var tb1 = TabMyDrops.Content as System.Windows.Controls.TextBlock;
            var tb2 = TabPartyDrops.Content as System.Windows.Controls.TextBlock;
            if (tb1 != null) tb1.Foreground = new WpfBrush(WpfColor.FromRgb(0, 212, 255));
            if (tb2 != null) tb2.Foreground = new WpfBrush(WpfColor.FromRgb(58, 90, 122));
            TabMyDrops.BorderBrush = new WpfBrush(WpfColor.FromRgb(0, 212, 255));
            TabMyDrops.BorderThickness = new Thickness(0,0,0,2);
            TabPartyDrops.BorderBrush = new WpfBrush(WpfColor.FromRgb(26, 58, 90));
            TabPartyDrops.BorderThickness = new Thickness(0,0,0,1);
        }

        private void TabPartyDrops_Click(object sender, RoutedEventArgs e)
        {
            DropGrid.Visibility = Visibility.Collapsed;
            PartyDropGrid.Visibility = Visibility.Visible;
            var tb1 = TabMyDrops.Content as System.Windows.Controls.TextBlock;
            var tb2 = TabPartyDrops.Content as System.Windows.Controls.TextBlock;
            if (tb1 != null) tb1.Foreground = new WpfBrush(WpfColor.FromRgb(58, 90, 122));
            if (tb2 != null) tb2.Foreground = new WpfBrush(WpfColor.FromRgb(0, 212, 255));
            TabPartyDrops.BorderBrush = new WpfBrush(WpfColor.FromRgb(0, 212, 255));
            TabPartyDrops.BorderThickness = new Thickness(0,0,0,2);
            TabMyDrops.BorderBrush = new WpfBrush(WpfColor.FromRgb(26, 58, 90));
            TabMyDrops.BorderThickness = new Thickness(0,0,0,1);
        }

        private void SaveChar_Click(object sender, RoutedEventArgs e) => ApplyCharacterName();

        private void MyCharBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Return) ApplyCharacterName(); }

        private void ApplyCharacterName()
        {
            string name = MyCharBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            _settings.MyCharacterName = name;
            _settings.Save();
            var toParty = _sessionDrops.Where(d => !IsMyCharacter(d.Character)).ToList();
            foreach (var d in toParty) { _sessionDrops.Remove(d); _partyDrops.Insert(0, d); }
            var toMine = _partyDrops.Where(d => IsMyCharacter(d.Character)).ToList();
            foreach (var d in toMine) { _partyDrops.Remove(d); _sessionDrops.Insert(0, d); }
            RefreshSessionStats(); RefreshAllTimeStats();
        }


        private void PersistOverlay_Click(object sender, RoutedEventArgs e)
        {
            _settings.PersistOverlay  = !_settings.PersistOverlay;
            _fgWatcher.PersistOverlay = _settings.PersistOverlay;
            // Icon → white when active
            var persistIcon = (System.Windows.Controls.TextBlock)FindName("PersistOverlayBtn_Icon");
            if (persistIcon != null)
                persistIcon.Foreground = _settings.PersistOverlay
                    ? new WpfBrush(WpfColor.FromRgb(255, 255, 255))
                    : new WpfBrush(WpfColor.FromRgb(30, 80, 90));
            PersistOverlayBtn_Label.Text = _settings.PersistOverlay ? "ACTIVE" : "PERSIST";
            PersistOverlayBtn_Label.Foreground = _settings.PersistOverlay
                ? new WpfBrush(WpfColor.FromRgb(34, 211, 238))
                : new WpfBrush(WpfColor.FromRgb(42, 106, 122));
            _settings.Save();
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            _alwaysOnTop = !_alwaysOnTop;
            Topmost      = _alwaysOnTop;
            // Icon → white when active
            var topIcon = (System.Windows.Controls.TextBlock)FindName("AlwaysOnTopBtn_Icon");
            if (topIcon != null)
                topIcon.Foreground = _alwaysOnTop
                    ? new WpfBrush(WpfColor.FromRgb(255, 255, 255))
                    : new WpfBrush(WpfColor.FromRgb(60, 40, 100));
            AlwaysOnTopBtn_Label.Foreground = _alwaysOnTop
                ? new WpfBrush(WpfColor.FromRgb(167, 139, 250))
                : new WpfBrush(WpfColor.FromRgb(61, 42, 106));
            AlwaysOnTopBtn_Label.Text = _alwaysOnTop ? "ON TOP" : "TOP";
        }

        // â”€â”€ Reports tab â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private List<FightReport> _loadedReports = new();
        private FightReport?      _selectedReport;

        private static readonly WpfBrush[] _rptPalette =
        [
            new WpfBrush(WpfColor.FromRgb(255,107,53)), new WpfBrush(WpfColor.FromRgb(0,212,255)),
            new WpfBrush(WpfColor.FromRgb(52,211,153)), new WpfBrush(WpfColor.FromRgb(192,132,252)),
            new WpfBrush(WpfColor.FromRgb(251,191,36)), new WpfBrush(WpfColor.FromRgb(248,113,113)),
            new WpfBrush(WpfColor.FromRgb(96,165,250)), new WpfBrush(WpfColor.FromRgb(244,114,182)),
        ];

        // View models
        private record ReportListItem(string FightName, string DateLabel, string DpsLabel,
            bool IsPersonalBest, string Id, Visibility PbVis);

        private record AbilityVm(string Ability, string TotalFmt, string AvgFmt);

        private class PlayerVm
        {
            public string Name        { get; init; } = "";
            public string CRLabel     { get; init; } = "";
            public string TotalLabel  { get; init; } = "";
            public string DpsLabel    { get; init; } = "";
            public string MaxHitLabel { get; init; } = "";
            public string PctLabel    { get; init; } = "";
            public string RoleIcon    { get; init; } = "";
            public double BarW        { get; init; }
            public WpfBrush BarColor  { get; init; } = System.Windows.Media.Brushes.Cyan;
            public Visibility AbVis   { get; set; } = Visibility.Collapsed;
            public List<AbilityVm> TopAbilities { get; init; } = new();
        }

        private void LoadReports()
        {
            _loadedReports = _reportStore.LoadAll();
            ReportList.ItemsSource = _loadedReports.Select(r => new ReportListItem(
                r.FightName,
                r.StartTime.ToString("MM/dd HH:mm"),
                $"{FormatNum((long)(r.TotalDamage / Math.Max(1, r.DurationSecs)))} DPS",
                r.IsPersonalBest,
                r.Id,
                r.IsPersonalBest ? Visibility.Visible : Visibility.Collapsed
            )).ToList();
        }

        private void ReportRefresh_Click(object sender, RoutedEventArgs e) => LoadReports();

        private void ReportDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedReport == null) return;
            _reportStore.Delete(_selectedReport.Id);
            _selectedReport = null;
            ReportSummaryPanel.Visibility = Visibility.Collapsed;
            RptLabelPanel.Visibility      = Visibility.Collapsed;
            RptLbHeader.Visibility        = Visibility.Collapsed;
            RptNoSelection.Visibility     = Visibility.Visible;
            RptLeaderboard.ItemsSource    = null;
            LoadReports();
        }

        private void ReportList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportList.SelectedItem is not ReportListItem item) return;
            _selectedReport = _loadedReports.FirstOrDefault(r => r.Id == item.Id);
            if (_selectedReport == null) return;
            ShowReportDetail(_selectedReport);
        }

        private void ShowReportDetail(FightReport r)
        {
            double dur = r.DurationSecs;
            long groupDps = dur > 0 ? (long)(r.TotalDamage / dur) : 0;

            RptTotalDmg.Text  = FormatNum(r.TotalDamage);
            RptDuration.Text  = FormatTime(TimeSpan.FromSeconds(dur));
            RptMaxHit.Text    = FormatNum(r.MaxHit);
            RptGroupDps.Text  = FormatNum(groupDps);

            ReportSummaryPanel.Visibility = Visibility.Visible;
            RptLabelPanel.Visibility      = Visibility.Visible;
            RptLbHeader.Visibility        = Visibility.Visible;
            RptNoSelection.Visibility     = Visibility.Collapsed;
            RptLabelBox.Text              = r.Label;

            long gt = r.TotalDamage;
            var rows = r.Players.Select((p, i) =>
            {
                double dps = dur > 0 ? p.TotalDamage / dur : 0;
                var vm = new PlayerVm
                {
                    Name        = p.Name,
                    CRLabel     = p.CR > 0 ? $"CR {p.CR}" : "",
                    TotalLabel  = FormatNum(p.TotalDamage),
                    DpsLabel    = FormatNum((long)dps),
                    MaxHitLabel = FormatNum(p.MaxHit),
                    PctLabel    = gt > 0 ? $"{(double)p.TotalDamage/gt:P0}" : "â€”",
                    RoleIcon    = PowerDetector.RoleIcon(Enum.TryParse<DcuoRole>(p.Role, out var ro) ? ro : DcuoRole.DPS),
                    BarW        = gt > 0 ? Math.Max(1, (double)p.TotalDamage / gt * 300) : 1,
                    BarColor    = _rptPalette[i % _rptPalette.Length],
                    TopAbilities = p.TopAbilities
                        .Select(a => new AbilityVm(a.Ability, FormatNum(a.Total), FormatNum(a.Avg)))
                        .ToList(),
                    AbVis = Visibility.Visible // show abilities in report view
                };
                return vm;
            }).ToList();

            RptLeaderboard.ItemsSource = rows;
        }

        private void RptSaveLabel_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedReport == null) return;
            _reportStore.UpdateLabel(_selectedReport.Id, RptLabelBox.Text.Trim());
            _selectedReport.Label = RptLabelBox.Text.Trim();
            LoadReports();
        }

        private static string FormatNum(long n) => n switch
        {
            >= 1_000_000_000 => $"{n / 1_000_000_000.0:F1}B",
            >= 1_000_000     => $"{n / 1_000_000.0:F1}M",
            >= 1_000         => $"{n / 1_000.0:F1}k",
            _                => n.ToString()
        };

        private static string FormatTime(TimeSpan t)
            => t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Boss-key: F9 hides/shows all overlays (no modifier = pure F9)
            _hideAllHotkey = new GlobalHotkey(this, Mod.None, VKey.F9);
            _hideAllHotkey.HotkeyPressed += ToggleAllOverlays;
        }

        private bool _overlaysHidden = false;
        private void ToggleAllOverlays()
        {
            Dispatcher.Invoke(() =>
            {
                _overlaysHidden = !_overlaysHidden;
                if (_overlaysHidden) { _dpsOverlay.Hide(); _chatOverlay.Hide(); }
                else                 { if (_dpsParser.CurrentFight != null) _dpsOverlay.Show(); }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _settings.Save();
            Logger.Shutdown();
            _dpsParser.Stop();
            _dpsParser.Dispose();
            _partyScanner.Dispose();
            _fgWatcher.Dispose();
            _watchdog.Dispose();
            _hideAllHotkey?.Dispose();
            _dpsOverlay.Dispatcher.Invoke(() => { try { _dpsOverlay.ForceClose(); } catch { } });
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




