using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LetMeSee
{
    internal class SurfacePanel : Panel
    {
        public bool Paper { get; set; }
        public SurfacePanel() { DoubleBuffered = true; BackColor = DesignTokens.Surface; Padding = new Padding(20); }
        private GraphicsPath Outline()
        {
            int diameter = DesignTokens.CardRadius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(Width - diameter - 1, 0, diameter, diameter, 270, 90);
            path.AddArc(Width - diameter - 1, Height - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(0, Height - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 25 || Height < 25) return;
            using (GraphicsPath path = Outline())
            {
                Region previous = Region; Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Paper && !DesignTokens.HighContrast)
            {
                // Seeded, low opacity grain, generated locally. No third-party texture or network.
                Random random = new Random(24);
                using (SolidBrush ink = new SolidBrush(Color.FromArgb(15, DesignTokens.Ink)))
                    for (int i = 0; i < Width * Height / 65; i++)
                        e.Graphics.FillRectangle(ink, random.Next(Math.Max(1, Width)), random.Next(Math.Max(1, Height)), 1, 1);
            }
            if (Width >= 25 && Height >= 25)
                using (Pen border = new Pen(DesignTokens.Border))
                using (GraphicsPath outline = Outline())
                { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.DrawPath(border, outline); }
        }
    }

    internal class ActionButton : Button
    {
        public ActionButton(string text, bool primary)
        {
            Text = text; AccessibleName = text.Replace("&", ""); Height = DesignTokens.ControlHeight;
            AutoSize = false; FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            Font = DesignTokens.Action;
            BackColor = primary ? DesignTokens.Accent : DesignTokens.Raised;
            ForeColor = primary ? DesignTokens.OnAccent : DesignTokens.Text;
            FlatAppearance.BorderColor = DesignTokens.Border; FlatAppearance.BorderSize = primary ? 0 : 1;
            UseVisualStyleBackColor = false;
        }
        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            if (Focused) ControlPaint.DrawFocusRectangle(pevent.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ForeColor, BackColor);
        }
    }

    // One finite opening sweep; respects the Windows animation setting and app preference.
    internal class SonarPanel : Panel
    {
        private readonly Timer timer = new Timer();
        private int frame;
        public SonarPanel()
        {
            DoubleBuffered = true; BackColor = DesignTokens.Paper; AccessibleName = "Ilustração de radar do LetMeSee";
            timer.Interval = 30;
            timer.Tick += delegate { frame++; Invalidate(); if (frame >= 40) timer.Stop(); };
        }
        public void Play(bool enabled)
        {
            timer.Stop(); frame = enabled && DesignTokens.MotionEnabled ? 0 : 40;
            if (frame == 0) timer.Start(); Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            int side = Math.Min(Width - 32, Height - 32); if (side <= 0) return;
            Rectangle r = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
            using (Pen pen = new Pen(DesignTokens.Ink, 1.5f))
            {
                for (int i = 0; i < 3; i++) { Rectangle ring = Rectangle.Inflate(r, -i * side / 7, -i * side / 7); g.DrawEllipse(pen, ring); }
                g.DrawLine(pen, r.Left - 8, r.Top + side / 2, r.Right + 8, r.Top + side / 2);
                g.DrawLine(pen, r.Left + side / 2, r.Top - 8, r.Left + side / 2, r.Bottom + 8);
            }
            using (SolidBrush sweep = new SolidBrush(Color.FromArgb(85, DesignTokens.Orange)))
                g.FillPie(sweep, r, -95 + frame * 9, 54);
            using (SolidBrush dot = new SolidBrush(DesignTokens.Orange))
            {
                g.FillEllipse(dot, r.Left + side * .23f, r.Top + side * .62f, 13, 13);
                g.FillEllipse(dot, r.Left + side * .67f, r.Top + side * .23f, 10, 10);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
