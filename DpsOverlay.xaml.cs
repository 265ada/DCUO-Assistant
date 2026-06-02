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
    }

    public partial class DpsOverlay : Window
    {
        private bool _pinned = true, _forceClose = false;
        private bool _showPowers = true, _showWeapon = true, _showSupercharge = true;
        private bool _showRoleIcons = true, _showPowerIcons = true;
        private FightData? _currentFight;
        private PlayerStats? _selectedPlayer;

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

private void BuildContextMenu()
{
    _ctxMenu = new System.Windows.Controls.ContextMenu { Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(10,14,40)), BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)) };

    _miPowers     = MakeMenuItem("Show My Powers",        true,  MenuShowPowers_Click);
    _miWeapon     = MakeMenuItem("Show Weapon Attacks",   true,  MenuShowWeapon_Click);
    _miSuper      = MakeMenuItem("Show Supercharge",      true,  MenuShowSupercharge_Click);
    _miRoles      = MakeMenuItem("Show Role Icons",       true,  MenuShowRoles_Click);
    _miPowerIcons = MakeMenuItem("Show Power Type Icons", true,  MenuShowPowerType_Click);
    var miReset   = MakeMenuItem("Reset Fight",           false, MenuResetFight_Click);
    miReset.Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,107,53));

    _ctxMenu.Items.Add(_miPowers);
    _ctxMenu.Items.Add(_miWeapon);
    _ctxMenu.Items.Add(_miSuper);
    _ctxMenu.Items.Add(new System.Windows.Controls.Separator());
    _ctxMenu.Items.Add(_miRoles);
    _ctxMenu.Items.Add(_miPowerIcons);
    _ctxMenu.Items.Add(new System.Windows.Controls.Separator());
    _ctxMenu.Items.Add(miReset);

    ContextMenu = _ctxMenu;
}

private static System.Windows.Controls.MenuItem MakeMenuItem(string header, bool checkable, System.Windows.RoutedEventHandler handler)
{
    var mi = new System.Windows.Controls.MenuItem { Header = header, IsCheckable = checkable, IsChecked = checkable, Foreground = WpfMedia.Brushes.White };
    mi.Click += handler;
    return mi;
}

        public void UpdateFight(FightData fight)
        {
            Dispatcher.Invoke(() => {
                _currentFight = fight;
                FightLabel.Text = $"{(fight.IsActive?"⚔":"✓")} {Trunc(fight.FightName,20)}";
                StatsPanel.Visibility = Visibility.Visible;
                TotalDmgLabel.Text = Fmt(fight.TotalGroupDamage);
                MaxHitLabel.Text   = $"Max: {Fmt(fight.MaxHit)}";
                DurationLabel.Text = FmtT(fight.Duration);
                RebuildList(fight);
                if (_selectedPlayer != null && fight.Players.ContainsKey(_selectedPlayer.Name))
                    ShowAbilities(_selectedPlayer);
                Show();
            });
        }

        public void ClearFight()
        {
            Dispatcher.Invoke(() => {
                _currentFight = null; _selectedPlayer = null;
                FightLabel.Text = "⚔ NO FIGHT";
                StatsPanel.Visibility = AbilityPanel.Visibility = Visibility.Collapsed;
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
            long tot = abs.Sum(a=>a.TotalDmg);
            AbilityPowerIcon.Text = PowerDetector.PowerIcon(player.PowerType);
            AbilityPlayerLabel.Text = $"{Trunc(player.Name,14).ToUpper()} POWERS";
            AbilityList.ItemsSource = abs.Select((a,i) => {
                double pct = tot>0?(double)a.TotalDmg/tot:0;
                WpfMedia.Brush cat = a.Category switch {
                    "Weapon" => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(156,163,175)),
                    "Supercharge" => new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(251,191,36)),
                    _ => _palette[i%_palette.Length]
                };
                return new AbilityRow { Name=a.Name, AvgLabel=Fmt(a.AvgHit), TotalLabel=Fmt(a.TotalDmg),
                    PctLabel=$"{pct:P0}", BarColor=_palette[i%_palette.Length], CatColor=cat };
            }).ToList();
            AbilityPanel.Visibility = Visibility.Visible;
        }

        private void CloseAbility_Click(object s, RoutedEventArgs e) { AbilityPanel.Visibility=Visibility.Collapsed; _selectedPlayer=null; }
        private void MenuShowPowers_Click(object s, RoutedEventArgs e) { _showPowers=_miPowers.IsChecked; RefreshAbs(); }
        private void MenuShowWeapon_Click(object s, RoutedEventArgs e) { _showWeapon=_miWeapon.IsChecked; RefreshAbs(); }
        private void MenuShowSupercharge_Click(object s, RoutedEventArgs e) { _showSupercharge=_miSuper.IsChecked; RefreshAbs(); }
        private void MenuShowRoles_Click(object s, RoutedEventArgs e) { _showRoleIcons=_miRoles.IsChecked; if(_currentFight!=null) RebuildList(_currentFight); }
        private void MenuShowPowerType_Click(object s, RoutedEventArgs e) { _showPowerIcons=_miPowerIcons.IsChecked; if(_currentFight!=null) RebuildList(_currentFight); }
        private void MenuResetFight_Click(object s, RoutedEventArgs e) => ClearFight();
        private void RefreshAbs() { if(_selectedPlayer!=null&&AbilityPanel.Visibility==Visibility.Visible) ShowAbilities(_selectedPlayer); }

        private static string Fmt(long n) => n>=1_000_000_000?$"{n/1_000_000_000.0:F1}B":n>=1_000_000?$"{n/1_000_000.0:F1}M":n>=1_000?$"{n/1_000.0:F1}k":n.ToString();
        private static string FmtT(TimeSpan t) => t.TotalHours>=1?$"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}":$"{t.Minutes}:{t.Seconds:D2}";
        private static string Trunc(string s, int m) => s.Length>m?s[..m]+"…":s;

        private void Border_MouseDown(object s, MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) DragMove(); }
        private void Pin_Click(object s, RoutedEventArgs e) {
            _pinned=!_pinned; Topmost=_pinned;
            PinBtn.Foreground=_pinned?new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)):new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(100,100,100));
        }
        private void Close_Click(object s, RoutedEventArgs e) => Hide();
        public void ForceClose() { _forceClose=true; Close(); }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if(_forceClose)return; e.Cancel=true; Hide(); }
    }
}





