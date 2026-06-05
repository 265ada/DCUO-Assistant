using System.Windows;
using System.Windows.Input;
using WpfMedia = System.Windows.Media;
using DCUOTracker.Models;

namespace DCUOTracker
{
    public class ScoreRow
    {
        public string Rank        { get; init; } = "";
        public string Name        { get; init; } = "";
        public string DpsLabel    { get; init; } = "";
        public string DamageLabel { get; init; } = "";
        public double BarWidth    { get; init; }
        public WpfMedia.Brush BarColor  { get; init; } = WpfMedia.Brushes.Cyan;
        public WpfMedia.Brush NameColor { get; init; } = WpfMedia.Brushes.White;
    }

    public partial class ScorecardOverlay : Window
    {
        private bool _forceClose;
        public string MyCharacterName { get; set; } = "";

        private static readonly WpfMedia.Brush[] _palette =
        [
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,107,53)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(52,211,153)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(192,132,252)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(96,165,250)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(244,114,182)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(156,163,175)),
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(248,113,113)),
        ];
        private static readonly WpfMedia.Brush _gold =
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,210,74));

        public ScorecardOverlay() => InitializeComponent();

        public void ShowResult(ScorecardResult res)
        {
            Dispatcher.Invoke(() =>
            {
                var ranked = res.Entries.OrderByDescending(e => e.Damage).ToList();
                long max = ranked.Count > 0 ? Math.Max(1, ranked[0].Damage) : 1;
                double dur = res.DurationSec > 0 ? res.DurationSec : 0;

                long totalDmg = res.TotalDamage;
                double groupDps = dur > 0 ? totalDmg / dur : 0;
                SubLabel.Text = dur > 0
                    ? $"⏱ {res.DurationSec/60}:{res.DurationSec%60:D2}   ·   group {Fmt((long)groupDps)} DPS   ·   {ranked.Count} players"
                    : $"no timer found — showing totals   ·   {ranked.Count} players";

                var rows = ranked.Select((e, i) =>
                {
                    bool me = !string.IsNullOrEmpty(MyCharacterName) &&
                              e.Name.Equals(MyCharacterName, StringComparison.OrdinalIgnoreCase);
                    double dps = dur > 0 ? e.Damage / dur : 0;
                    return new ScoreRow
                    {
                        Rank        = (i + 1).ToString(),
                        Name        = e.Name,
                        DpsLabel    = dur > 0 ? Fmt((long)dps) : "—",
                        DamageLabel = Fmt(e.Damage),
                        BarWidth    = Math.Max(1, (double)e.Damage / max * 175),
                        BarColor    = me ? _gold : _palette[i % _palette.Length],
                        NameColor   = me ? _gold : WpfMedia.Brushes.White
                    };
                }).ToList();

                ScoreList.ItemsSource = rows;
                Show();
                Activate();
            });
        }

        private static string Fmt(long n) =>
            n >= 1_000_000_000 ? $"{n/1_000_000_000.0:F1}B" :
            n >= 1_000_000     ? $"{n/1_000_000.0:F1}M" :
            n >= 1_000         ? $"{n/1_000.0:F1}k" : n.ToString();

        public Action? PositionChanged;
        private void Border_MouseDown(object s, MouseButtonEventArgs e)
        { if (e.ChangedButton == MouseButton.Left) { DragMove(); PositionChanged?.Invoke(); } }

        private void Close_Click(object s, RoutedEventArgs e) => Hide();
        public void ForceClose() { _forceClose = true; Close(); }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        { if (_forceClose) return; e.Cancel = true; Hide(); }

        // ── Click-through (toggled by Ctrl+0) ──
        public bool ClickThrough { get; private set; } = true;
        protected override void OnSourceInitialized(System.EventArgs e)
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
                ? new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0,212,255))
                : new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255,210,74));
    }
}
