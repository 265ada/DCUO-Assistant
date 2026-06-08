using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using System.Windows.Input;
using WpfMedia = System.Windows.Media;
using DCUOTracker.Models;
using DCUOTracker.Services;

namespace DCUOTracker
{
    public class PlayerRow
    {
        public string Name         { get; init; } = "";
        public string DpsLabel     { get; init; } = "";
        public string PctLabel     { get; init; } = "";
        public double BarWidth     { get; init; }
        public WpfMedia.Brush BarColor  { get; init; } = WpfMedia.Brushes.Cyan;
        public string PowerIcon    { get; init; } = "";
        public string RoleIcon     { get; init; } = "";
        public WpfMedia.Brush RoleColor { get; init; } = WpfMedia.Brushes.White;
        public Visibility PowerIconVis { get; init; } = Visibility.Visible;
        public Visibility RoleIconVis  { get; init; } = Visibility.Visible;
        public string CRLabel      { get; init; } = "";
        public Visibility CRVis        { get; init; } = Visibility.Collapsed;
        public PlayerStats Stats   { get; init; } = null!;
    }

    public class AbilityRow
    {
        public string Name       { get; init; } = "";
        public string AvgLabel   { get; init; } = "";
        public string TotalLabel { get; init; } = "";
        public string PctLabel   { get; init; } = "";
        public WpfMedia.Brush BarColor { get; init; } = WpfMedia.Brushes.Gray;
        public WpfMedia.Brush CatColor { get; init; } = WpfMedia.Brushes.Gray;
        public double BarWidth { get; init; } = 0;
        public string CritLabel { get; init; } = "";
    }

    public partial class DpsOverlay : Window
    {
        private bool _pinned = true, _forceClose = false;
        private bool _showPowers = true, _showWeapon = true, _showSupercharge = true;
        private bool _showRoleIcons = true, _showPowerIcons = true;
        private FightData? _currentFight;
        private PlayerStats? _selectedPlayer;

        // Personal-best parse tracking
        private readonly Services.ParseBests _bests = new();
        private string _pbKey = "";
        private string _pbAnnounced = "";
        private double _pbSnapshot;

        private static readonly WpfMedia.Brush[] _palette = [
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,107,53)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(52,211,153)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(192,132,252)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(251,191,36)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(248,113,113)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(96,165,250)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(244,114,182)),
        ];

        private static WpfMedia.Brush RoleColorBrush(DcuoRole r) => r switch {
            DcuoRole.Tank       => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(96,165,250)),
            DcuoRole.Healer     => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(52,211,153)),
            DcuoRole.Controller => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(251,191,36)),
            _                   => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(248,113,113))
        };

        public DpsOverlay()
{
    InitializeComponent();
    BuildContextMenu();
}

private System.Windows.Controls.ContextMenu _ctxMenu = null!;
private System.Windows.Controls.MenuItem _miPowers = null!, _miWeapon = null!, _miSuper = null!;
private System.Windows.Controls.MenuItem _miRoles = null!, _miPowerIcons = null!;
private System.Windows.Controls.MenuItem _miZone = null!;

private void BuildContextMenu()
{
    _ctxMenu = new System.Windows.Controls.ContextMenu { Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(10,14,40)), BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)) };

    _miPowers     = MakeMenuItem("Show My Powers",        true,  MenuShowPowers_Click);
    _miWeapon     = MakeMenuItem("Show Weapon Attacks",   true,  MenuShowWeapon_Click);
    _miSuper      = MakeMenuItem("Show Supercharge",      true,  MenuShowSupercharge_Click);
    _miRoles      = MakeMenuItem("Show Role Icons",       true,  MenuShowRoles_Click);
    _miPowerIcons = MakeMenuItem("Show Power Type Icons", true,  MenuShowPowerType_Click);
    var miCopy    = MakeMenuItem("Copy Parse to Clipboard", false, MenuCopyParse_Click);
    miCopy.Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255));
    _miZone       = MakeMenuItem("Show Zone Totals (whole instance)", true, MenuZone_Click);
    _miZone.IsChecked = false; // default to single-fight view
    _miZone.Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(124,252,0));
    var miResetZone = MakeMenuItem("Reset Zone Totals", false, MenuResetZone_Click);
    miResetZone.Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(124,252,0));
    var miReset   = MakeMenuItem("Reset Fight",           false, MenuResetFight_Click);
    miReset.Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,107,53));

    _ctxMenu.Items.Add(_miPowers);
    _ctxMenu.Items.Add(_miWeapon);
    _ctxMenu.Items.Add(_miSuper);
    _ctxMenu.Items.Add(new System.Windows.Controls.Separator());
    _ctxMenu.Items.Add(_miRoles);
    _ctxMenu.Items.Add(_miPowerIcons);
    _ctxMenu.Items.Add(new System.Windows.Controls.Separator());
    _ctxMenu.Items.Add(_miZone);
    _ctxMenu.Items.Add(miResetZone);
    _ctxMenu.Items.Add(new System.Windows.Controls.Separator());
    _ctxMenu.Items.Add(miCopy);
    _ctxMenu.Items.Add(miReset);

    ContextMenu = _ctxMenu;
}

private static System.Windows.Controls.MenuItem MakeMenuItem(string header, bool checkable, System.Windows.RoutedEventHandler handler)
{
    var mi = new System.Windows.Controls.MenuItem { Header = header, IsCheckable = checkable, IsChecked = checkable, Foreground = WpfMedia.Brushes.White };
    mi.Click += handler;
    return mi;
}

        // My character name — set from MainWindow so overlay can auto-select
        public string MyCharacterName { get; set; } = "";

        // Zone/instance accumulator (cumulative across all fights) + toggle
        private FightData? _zoneData;
        private FightData? _rawFight;
        private bool _showZone;
        public Action? ResetZoneRequested;
        public void UpdateZone(FightData? zone) => _zoneData = zone;

        public void UpdateFight(FightData fight)
        {
            Dispatcher.Invoke(() => {
                _rawFight = fight;
                // Display either the single fight or the whole-zone cumulative totals
                var f = (_showZone && _zoneData != null) ? _zoneData : fight;
                _currentFight = f;

                string tag = _showZone ? "🗺 ZONE · " : (f.IsSparringParse ? "⊕ PARSE · " : "");
                FightLabel.Text = $"{(f.IsActive?"⚔":"✓")} {tag}{Trunc(f.FightName, tag==""?20:14)}";
                StatsPanel.Visibility = Visibility.Visible;
                TotalDmgLabel.Text = Fmt(f.TotalGroupDamage);
                MaxHitLabel.Text   = $"Max: {Fmt(f.MaxHit)}";
                DurationLabel.Text = FmtT(f.Duration);
                RebuildList(f);

                // Pick the player to show. ALWAYS re-fetch the current instance by name
                // so we never display stale data from a previous fight/character (power-swap fix).
                PlayerStats? target = null;
                if (_selectedPlayer != null && f.Players.TryGetValue(_selectedPlayer.Name, out var cur))
                    target = cur;
                else if (!string.IsNullOrEmpty(MyCharacterName) && f.Players.TryGetValue(MyCharacterName, out var me))
                    target = me;
                else if (f.Players.Count > 0)
                    target = f.RankedByDamage.First();

                if (target != null)
                {
                    _selectedPlayer = target;
                    ShowAbilities(target);
                }
                else
                {
                    AbilityPanel.Visibility = Visibility.Collapsed;
                    MyStatsPanel.Visibility = Visibility.Collapsed;
                }

                Show();
            });
        }

        public void ClearFight()
        {
            Dispatcher.Invoke(() => {
                _currentFight = null; _selectedPlayer = null;
                FightLabel.Text = "⚔ NO FIGHT";
                StatsPanel.Visibility = AbilityPanel.Visibility = MyStatsPanel.Visibility = Visibility.Collapsed;
                PlayerList.ItemsSource = null;
            });
        }

        private void RebuildList(FightData fight)
        {
            long gt = fight.TotalGroupDamage;
            var rows = fight.RankedByDamage.Select((p,i) => {
                double pct = gt > 0 ? (double)p.TotalDamage/gt : 0;
                double dps = fight.Duration.TotalSeconds > 1 ? p.TotalDamage/fight.Duration.TotalSeconds : 0;
                return new PlayerRow {
                    Name = p.Name, DpsLabel = Fmt((long)dps), PctLabel = $"{pct:P0}",
                    BarWidth = Math.Max(1, pct*265), BarColor = _palette[i%_palette.Length],
                    PowerIcon = PowerDetector.PowerIcon(p.PowerType), RoleIcon = PowerDetector.RoleIcon(p.Role),
                    RoleColor = RoleColorBrush(p.Role),
                    PowerIconVis = _showPowerIcons?Visibility.Visible:Visibility.Collapsed,
                    RoleIconVis  = _showRoleIcons ?Visibility.Visible:Visibility.Collapsed,
                    Stats = p
                };
            }).ToList();
            PlayerList.ItemsSource = rows;
        }

        private void PlayerRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton b && b.Tag is PlayerRow r) { _selectedPlayer = r.Stats; ShowAbilities(r.Stats); }
        }

        private void ShowAbilities(PlayerStats player)
        {
            var abs = player.Abilities.Values
                .Where(a => (a.Category=="Power"&&_showPowers)||(a.Category=="Weapon"&&_showWeapon)||(a.Category=="Supercharge"&&_showSupercharge))
                .OrderByDescending(a=>a.TotalDmg).ToList();
            long tot   = abs.Sum(a=>a.TotalDmg);
            double dur = (_currentFight?.Duration.TotalSeconds ?? 1).Clamp(1, 9999);

            AbilityPowerIcon.Text   = PowerDetector.PowerIcon(player.PowerType);
            AbilityPlayerLabel.Text = $"{Trunc(player.Name,14).ToUpper()} ROTATION";

            long maxDmg = abs.Count > 0 ? abs[0].TotalDmg : 1;
            AbilityList.ItemsSource = abs.Select((a,i) => {
                double pct      = tot>0?(double)a.TotalDmg/tot:0;
                double critPct  = a.Hits>0?(double)a.CritHits/a.Hits:0;
                double hitsMin  = a.Hits / (dur/60.0);

                // Efficiency colors — bright enough to see on dark background
                WpfMedia.Brush effColor = pct switch {
                    >0.20 => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,255,120)),    // top — bright green
                    >0.08 => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,200,0)),    // mid — bright gold
                    _     => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,80,80))     // low — bright red
                };
                // Bar width proportional to damage share (max ability = full 255px)
                double barW = maxDmg > 0 ? (double)a.TotalDmg / maxDmg * 255 : 0;

                WpfMedia.Brush catColor = a.Category switch {
                    "Weapon"      => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(156,163,175)),
                    "Supercharge" => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(251,191,36)),
                    _             => effColor
                };

                string critStr   = a.Hits > 1 && critPct > 0 ? $"{critPct:P0} crit · {a.Hits}×" : $"{a.Hits}×";

                return new AbilityRow {
                    Name       = a.Name,
                    AvgLabel   = Fmt(a.AvgHit),
                    TotalLabel = Fmt(a.TotalDmg),
                    PctLabel   = $"{pct:P0}",
                    BarColor   = effColor,
                    CatColor   = catColor,
                    BarWidth   = Math.Max(2, barW),
                    CritLabel  = critStr
                };
            }).ToList();
            AbilityPanel.Visibility = Visibility.Visible;

            UpdateMyStats(player);
        }

        // Populate the MY PERFORMANCE strip for the selected player
        private void UpdateMyStats(PlayerStats p)
        {
            // Sustained parse DPS = total damage / engaged time (first hit -> last hit)
            DateTime end   = p.LastHitTime  ?? _currentFight?.EndTime ?? DateTime.Now;
            DateTime start = p.FirstHitTime ?? end;
            double dur = (end - start).TotalSeconds;
            double sustained = dur > 1 ? p.TotalDamage / dur : p.TotalDamage;

            MyDpsLabel.Text   = Fmt((long)sustained);
            MyBurstLabel.Text = Fmt((long)p.BurstDps);

            double act = p.ActivityPercent;
            MyActivityLabel.Text = $"{act:F0}%";
            var g = Models.DpsGrade.ForActivity(act);
            MyActivityGrade.Text = g.Letter;
            MyActivityGradeBox.Background = BrushFromHex(g.Hex);
            MyActivityGrade.ToolTip = g.Label;

            MyCritLabel.Text = $"{p.CritPercent:F0}%";
            MyApmLabel.Text  = $"{p.Apm:F0}";

            // Might (power) / Super / Precision (weapon) split bar
            long tot = p.TotalDamage;
            if (tot > 0)
            {
                double w = SplitCanvas.ActualWidth > 10 ? SplitCanvas.ActualWidth : 256;
                double powerOnly = (double)p.PowerDamage  / tot;
                double super     = (double)p.SuperDamage  / tot;
                double weapon    = (double)p.WeaponDamage / tot;
                double mW = powerOnly * w, sW = super * w, pW = weapon * w;
                MightSeg.Width = mW;
                SuperSeg.Width = sW; System.Windows.Controls.Canvas.SetLeft(SuperSeg, mW);
                PrecSeg.Width  = pW; System.Windows.Controls.Canvas.SetLeft(PrecSeg, mW + sW);
                MySplitLabel.Text =
                    $"MIGHT {(powerOnly + super):P0}  ·  SC {super:P0}  ·  PREC {weapon:P0}";
            }

            // Healing (HPS) — only shown when you actually heal
            if (p.TotalHealing > 0)
            {
                double hps = dur > 1 ? p.TotalHealing / dur : p.TotalHealing;
                MyHealLabel.Text = $"✚ HEALING {Fmt(p.TotalHealing)}  ·  {Fmt((long)hps)}/s";
                MyHealLabel.Visibility = Visibility.Visible;
            }
            else MyHealLabel.Visibility = Visibility.Collapsed;

            // Personal best (per power) — compare against snapshot taken at fight start
            string power = p.PowerType.ToString();
            string key   = (_currentFight?.StartTime.Ticks ?? 0) + "|" + power;
            if (key != _pbKey) { _pbKey = key; _pbSnapshot = _bests.Get(power).Burst; }
            _bests.Report(power, p.BurstDps, sustained); // persist climbing best
            if (p.Hits > 12)
            {
                if (_pbSnapshot <= 0)
                    SetBest($"First {power} parse — baseline {Fmt((long)p.BurstDps)} burst", "#7A8AA5");
                else if (p.BurstDps > _pbSnapshot)
                {
                    SetBest($"🏆 NEW BURST RECORD!  beat {Fmt((long)_pbSnapshot)}", "#FFD24A");
                    if (_pbAnnounced != _pbKey) { _pbAnnounced = _pbKey; Services.SoundAlert.PlayPersonalBest(); }
                }
                else
                {
                    double ratio = p.BurstDps / _pbSnapshot;
                    SetBest($"Burst {ratio:P0} of best ({Fmt((long)_pbSnapshot)})",
                            ratio >= 0.95 ? "#7CFC00" : "#7A8AA5");
                }
            }
            else MyBestLabel.Text = "";

            // Live coach tip — adaptive advice for improving DPS
            var tip = Models.Coach.Analyze(p, _currentFight?.IsSparringParse ?? false);
            CoachLabel.Text = tip.Label;
            CoachText.Text  = tip.Text;
            var tipBrush = BrushFromHex(tip.Hex);
            CoachLabel.Foreground = tipBrush;
            CoachBox.BorderBrush  = tipBrush;

            MyStatsPanel.Visibility = Visibility.Visible;
        }

        private void SetBest(string text, string hex)
        {
            MyBestLabel.Text = text;
            MyBestLabel.Foreground = BrushFromHex(hex);
        }

        private static WpfMedia.Brush BrushFromHex(string hex) =>
            (WpfMedia.Brush)new WpfMedia.BrushConverter().ConvertFromString(hex)!;

        private void CloseAbility_Click(object s, RoutedEventArgs e) { AbilityPanel.Visibility=Visibility.Collapsed; _selectedPlayer=null; }
        private void MenuShowPowers_Click(object s, RoutedEventArgs e) { _showPowers=_miPowers.IsChecked; RefreshAbs(); }
        private void MenuShowWeapon_Click(object s, RoutedEventArgs e) { _showWeapon=_miWeapon.IsChecked; RefreshAbs(); }
        private void MenuShowSupercharge_Click(object s, RoutedEventArgs e) { _showSupercharge=_miSuper.IsChecked; RefreshAbs(); }
        private void MenuShowRoles_Click(object s, RoutedEventArgs e) { _showRoleIcons=_miRoles.IsChecked; if(_currentFight!=null) RebuildList(_currentFight); }
        private void MenuShowPowerType_Click(object s, RoutedEventArgs e) { _showPowerIcons=_miPowerIcons.IsChecked; if(_currentFight!=null) RebuildList(_currentFight); }
        private void MenuResetFight_Click(object s, RoutedEventArgs e) => ClearFight();
        private void MenuZone_Click(object s, RoutedEventArgs e)
        {
            _showZone = _miZone.IsChecked;
            if (_rawFight != null) UpdateFight(_rawFight); // re-render in the chosen mode
        }
        private void MenuResetZone_Click(object s, RoutedEventArgs e)
        {
            ResetZoneRequested?.Invoke();
            _zoneData = null;
            if (_rawFight != null) UpdateFight(_rawFight);
        }

        // Copy a clean, shareable parse summary to the clipboard
        private void MenuCopyParse_Click(object s, RoutedEventArgs e)
        {
            var p = _selectedPlayer; var f = _currentFight;
            if (p == null || f == null) return;
            DateTime end = p.LastHitTime ?? f.EndTime ?? DateTime.Now;
            DateTime start = p.FirstHitTime ?? end;
            double dur = (end - start).TotalSeconds;
            double sustained = dur > 1 ? p.TotalDamage / dur : p.TotalDamage;
            var top = p.Abilities.Values.OrderByDescending(a => a.TotalDmg).Take(3)
                .Select(a => $"{a.Name} {(p.TotalDamage > 0 ? (double)a.TotalDmg / p.TotalDamage : 0):P0}");
            var g = Models.DpsGrade.ForActivity(p.ActivityPercent);
            string txt =
                $"DCUO Parse — {p.Name} ({p.PowerType})\n" +
                $"DPS {Fmt((long)sustained)} sustained · Burst {Fmt((long)p.BurstDps)}\n" +
                $"Activity {p.ActivityPercent:F0}% (grade {g.Letter}) · Crit {p.CritPercent:F0}% · APM {p.Apm:F0}\n" +
                $"Might {p.MightPct:F0}% · SC {p.SuperPct:F0}% · Prec {p.PrecisionPct:F0}%\n" +
                $"Top: {string.Join(" · ", top)}\n" +
                $"Duration {FmtT(f.Duration)}{(f.IsSparringParse ? " · sparring parse" : "")} · via DCUO QoL";
            try { System.Windows.Clipboard.SetText(txt); }
            catch (Exception ex) { Logger.Error("CopyParse", ex); }
        }
        private void RefreshAbs() { if(_selectedPlayer!=null&&AbilityPanel.Visibility==Visibility.Visible) ShowAbilities(_selectedPlayer); }

        private static string Fmt(long n) => n>=1_000_000_000?$"{n/1_000_000_000.0:F1}B":n>=1_000_000?$"{n/1_000_000.0:F1}M":n>=1_000?$"{n/1_000.0:F1}k":n.ToString();
        private static string FmtT(TimeSpan t) => t.TotalHours>=1?$"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}":$"{t.Minutes}:{t.Seconds:D2}";
        private static string Trunc(string s, int m) => s.Length>m?s[..m]+"…":s;

        public Action? PositionChanged;
        private void Border_MouseDown(object s, MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) { DragMove(); PositionChanged?.Invoke(); } }
        private void Pin_Click(object s, RoutedEventArgs e) {
            _pinned=!_pinned; Topmost=_pinned;
            PinBtn.Foreground=_pinned?new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)):new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(100,100,100));
        }
        private void Close_Click(object s, RoutedEventArgs e) => Hide();
        public void ForceClose() { _forceClose=true; Close(); }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if(_forceClose)return; e.Cancel=true; Hide(); }

        // ── Click-through (mouse passes to the game). Toggled by Ctrl+0 from MainWindow. ──
        public bool ClickThrough { get; private set; } = true;
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Services.ClickThrough.Apply(this, ClickThrough);
            UpdateClickBorder();
        }
        public void SetClickThrough(bool on)
        {
            ClickThrough = on;
            Services.ClickThrough.Apply(this, on);
            UpdateClickBorder();
        }
        private void UpdateClickBorder() =>
            RootBorder.BorderBrush = ClickThrough
                ? new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255))   // cyan = click-through ON
                : new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,210,74));  // gold = interactable (Ctrl+0)
    }
    internal static class DoubleExt
    {
        public static double Clamp(this double v, double min, double max) =>
            v < min ? min : v > max ? max : v;
    }
}