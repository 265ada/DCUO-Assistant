using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DCUOTracker.Services
{
    /// <summary>
    /// WinForms overlay that lets user drag-select a screen region.
    /// Much more reliable than WPF for transparent fullscreen overlays.
    /// </summary>
    public class RegionSelector : Form
    {
        private Point _start;
        private Point _current;
        private bool  _dragging;

        public Rectangle? SelectedRegion { get; private set; }

        public RegionSelector()
        {
            // Cover all screens
            var all = Screen.AllScreens;
            int left   = all.Min(s => s.Bounds.Left);
            int top    = all.Min(s => s.Bounds.Top);
            int right  = all.Max(s => s.Bounds.Right);
            int bottom = all.Max(s => s.Bounds.Bottom);

            FormBorderStyle = FormBorderStyle.None;
            Bounds          = new Rectangle(left, top, right - left, bottom - top);
            TopMost         = true;
            BackColor       = Color.Black;
            Opacity         = 0.4;
            Cursor          = Cursors.Cross;
            DoubleBuffered  = true;
            ShowInTaskbar   = false;

            KeyPreview = true;
            KeyDown   += (_, e) => { if (e.KeyCode == Keys.Escape) { SelectedRegion = null; Close(); } };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _start    = e.Location;
                _current  = e.Location;
                _dragging = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            _current = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragging) return;
            _dragging = false;
            _current  = e.Location;

            int x = Math.Min(_start.X, _current.X) + Left;
            int y = Math.Min(_start.Y, _current.Y) + Top;
            int w = Math.Abs(_current.X - _start.X);
            int h = Math.Abs(_current.Y - _start.Y);

            if (w > 5 && h > 5)
                SelectedRegion = new Rectangle(x, y, w, h);

            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!_dragging) return;

            int x = Math.Min(_start.X, _current.X);
            int y = Math.Min(_start.Y, _current.Y);
            int w = Math.Abs(_current.X - _start.X);
            int h = Math.Abs(_current.Y - _start.Y);

            using var pen   = new Pen(Color.Cyan, 2);
            using var brush = new SolidBrush(Color.FromArgb(50, 0, 212, 255));

            e.Graphics.FillRectangle(brush, x, y, w, h);
            e.Graphics.DrawRectangle(pen,   x, y, w, h);

            // Instruction + size label
            string label = $"{w} × {h}  |  Click & drag to select • ESC cancel";
            using var font  = new Font("Segoe UI", 11, FontStyle.Bold);
            using var bg    = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            using var fg    = new SolidBrush(Color.Cyan);
            var size = e.Graphics.MeasureString(label, font);
            e.Graphics.FillRectangle(bg, 10, 10, size.Width + 16, size.Height + 8);
            e.Graphics.DrawString(label, font, fg, 18, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            if (!_dragging)
            {
                // Draw instruction when not yet dragging
                using var font = new Font("Segoe UI", 14, FontStyle.Bold);
                using var fg   = new SolidBrush(Color.White);
                string msg = "Click and drag over the LFG chat input area  •  ESC to cancel";
                var size = e.Graphics.MeasureString(msg, font);
                float cx = (Width  - size.Width)  / 2;
                float cy = (Height - size.Height) / 2;
                using var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
                e.Graphics.FillRectangle(bg, cx - 12, cy - 8, size.Width + 24, size.Height + 16);
                e.Graphics.DrawString(msg, font, fg, cx, cy);
            }
        }

        // Needed to make the form click-through-free
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(nint hWnd);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetForegroundWindow(Handle);
            Activate();
        }
    }
}
