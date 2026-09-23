using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HikiNotifier.UI
{
    internal sealed class ModernButton : Button
    {
        private bool hovered;
        private bool pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
            var scale = graphics.DpiX / 96f;
            var borderWidth = Math.Max(1f, scale);
            var radius = Math.Min(UiTheme.CornerRadius * scale, Math.Min(Width, Height) / 2f);
            var bounds = new RectangleF(borderWidth / 2f, borderWidth / 2f,
                Width - borderWidth, Height - borderWidth);
            using (var path = RoundedPath(bounds, radius))
            using (var background = new SolidBrush(!Enabled ? UiTheme.Header : pressed ? UiTheme.AccentPressed :
                hovered ? UiTheme.AccentHover : UiTheme.Surface))
            using (var border = new Pen(Enabled ? UiTheme.Border : UiTheme.Divider, borderWidth))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }
            var textBounds = Rectangle.Inflate(ClientRectangle, -Padding.Horizontal / 2, -Padding.Vertical / 2);
            TextRenderer.DrawText(graphics, Text, Font, textBounds, Enabled ? UiTheme.Text : UiTheme.Disabled,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -5, -5), UiTheme.Accent, Color.Transparent);
        }

        private static GraphicsPath RoundedPath(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
