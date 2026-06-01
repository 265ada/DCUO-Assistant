using System.Windows;
using System.Windows.Input;
using DCUOTracker.Services;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;

namespace DCUOTracker
{
    public partial class ChatOverlay : Window
    {
        private bool _pinned     = true;
        private bool _vertical   = false;
        private bool _chatActive = false;
        private bool _lfgActive  = false;
        private bool _lfgMode    = false;
        private bool _forceClose = false; // L-6: allows MainWindow to truly close

        public bool LfgMode => _lfgMode;

        public void SetLfgChannelDetected(bool detected)
        {
            Dispatcher.Invoke(() =>
            {
                _lfgMode = detected;
                LfgModeBtn.Foreground = detected
                    ? new WpfBrush(WpfColor.FromRgb(255, 170, 0))
                    : new WpfBrush(WpfColor.FromRgb(85, 85, 85));
                LfgModeBtn.Content = detected ? "LFG●" : "LFG";
                LfgModeBtn.ToolTip = detected
                    ? "LFG channel detected (auto)"
                    : "LFG channel not detected";
            });
        }

        private readonly OverlaySettings _settings;

        public ChatOverlay(OverlaySettings settings)
        {
            InitializeComponent();

            // Use shared settings instance from MainWindow
            _settings = settings;
            Left      = _settings.Left;
            Top       = _settings.Top;
            _pinned   = _settings.IsPinned;
            Topmost   = _pinned;

            if (_settings.IsVertical)
                ApplyVertical();

            // Save position whenever window moves
            LocationChanged += (_, _) => SaveSettings();
        }

        // ── Settings persistence ──────────────────────────────────

        private void SaveSettings()
        {
            _settings.Left       = Left;
            _settings.Top        = Top;
            _settings.IsVertical = _vertical;
            _settings.IsPinned   = _pinned;
            _settings.Save();
        }

        // ── Chat state ────────────────────────────────────────────

        public void UpdateChatState(ChatStateChangedArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                int    count  = e.IsActive ? e.CharCount : 0;
                string labelH = $"{count}/60";
                string labelV = $"{count} / 60";

                CharBarH.Value  = count;
                CharBarV.Value  = count;
                CharLabelH.Text = labelH;
                CharLabelV.Text = labelV;

                WpfBrush brush;
                if (!e.IsActive || count == 0)
                    brush = new WpfBrush(WpfColor.FromRgb(0, 255, 136));
                else if (e.AtLimit)
                {
                    brush = new WpfBrush(WpfColor.FromRgb(255, 68, 68));
                    Console.Beep(800, 80);
                }
                else if (count >= 50)
                    brush = new WpfBrush(WpfColor.FromRgb(255, 170, 0));
                else
                    brush = new WpfBrush(WpfColor.FromRgb(0, 255, 136));

                CharBarH.Foreground   = brush;
                CharBarV.Foreground   = brush;
                CharLabelH.Foreground = brush;
                CharLabelV.Foreground = brush;

                _chatActive = e.IsActive;
                RefreshVisibility();
            });
        }

        // ── LFG timer ─────────────────────────────────────────────

        public void UpdateLfgTimer(LfgTimerArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Expired)
                {
                    LfgPanelH.Visibility = Visibility.Collapsed;
                    LfgPanelV.Visibility = Visibility.Collapsed;
                    LfgSepH.Visibility   = Visibility.Collapsed;
                    _lfgActive           = false;
                    RefreshVisibility();
                    return;
                }

                LfgPanelH.Visibility = Visibility.Visible;
                LfgPanelV.Visibility = Visibility.Visible;
                LfgSepH.Visibility   = Visibility.Visible;

                int secs       = e.SecondsRemaining;
                LfgBarH.Value  = secs;
                LfgBarV.Value  = secs;
                LfgLabelH.Text = $"{secs}s";
                LfgLabelV.Text = $"{secs}s";

                WpfBrush brush = secs <= 10
                    ? new WpfBrush(WpfColor.FromRgb(255, 68,  68))
                    : secs <= 30
                        ? new WpfBrush(WpfColor.FromRgb(255, 170, 0))
                        : new WpfBrush(WpfColor.FromRgb(0,   212, 255));

                LfgBarH.Foreground   = brush;
                LfgBarV.Foreground   = brush;
                LfgLabelH.Foreground = brush;
                LfgLabelV.Foreground = brush;

                _lfgActive = true;
                RefreshVisibility();
            });
        }

        private void RefreshVisibility()
        {
            if (_chatActive || _lfgActive)
                Show();
            else
                Hide();
        }

        // ── Orientation ───────────────────────────────────────────

        private void Orient_Click(object sender, RoutedEventArgs e)
        {
            _vertical = !_vertical;
            if (_vertical) ApplyVertical(); else ApplyHorizontal();
            SaveSettings();
        }

        private void ApplyVertical()
        {
            _vertical                   = true;
            HorizontalLayout.Visibility = Visibility.Collapsed;
            VerticalLayout.Visibility   = Visibility.Visible;
            OrientBtn.Content           = "⇄";
        }

        private void ApplyHorizontal()
        {
            _vertical                   = false;
            HorizontalLayout.Visibility = Visibility.Visible;
            VerticalLayout.Visibility   = Visibility.Collapsed;
            OrientBtn.Content           = "⇅";
        }

        // ── Drag ──────────────────────────────────────────────────

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // ── Buttons ───────────────────────────────────────────────

        private void LfgMode_Click(object sender, RoutedEventArgs e)
        {
            _lfgMode = !_lfgMode;
            LfgModeBtn.Foreground = _lfgMode
                ? new WpfBrush(WpfColor.FromRgb(255, 170, 0))
                : new WpfBrush(WpfColor.FromRgb(85, 85, 85));
            LfgModeBtn.ToolTip = _lfgMode
                ? "LFG Mode ON — timer fires every Enter"
                : "LFG Mode OFF — only fires on /lfg command";
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            _pinned  = !_pinned;
            Topmost  = _pinned;
            PinBtn.Foreground = _pinned
                ? new WpfBrush(WpfColor.FromRgb(0, 212, 255))
                : new WpfBrush(WpfColor.FromRgb(100, 100, 100));
            SaveSettings();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Hide();
        }

        // L-6: called by MainWindow to truly destroy window on app exit
        public void ForceClose()
        {
            _forceClose = true;
            SaveSettings();
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_forceClose) return; // allow true close on app exit
            SaveSettings();
            e.Cancel = true;
            Hide();
        }
    }
}
