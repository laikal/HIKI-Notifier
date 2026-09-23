using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using HikiNotifier.Models;

namespace HikiNotifier.UI
{
    internal sealed class ProfileListView : ListView
    {
        public ProfileListView()
        {
            OwnerDraw = true;
            DoubleBuffered = true;
            DrawColumnHeader += DrawHeader;
            DrawItem += DrawRow;
            DrawSubItem += DrawCell;
        }

        private void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var background = new SolidBrush(UiTheme.Header))
                e.Graphics.FillRectangle(background, e.Bounds);
            using (var line = new Pen(UiTheme.Divider))
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var bounds = Rectangle.Inflate(e.Bounds, -9, 0);
            using (var headerFont = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, bounds, UiTheme.Text,
                    Flags(e.Header.TextAlign) | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void DrawRow(object sender, DrawListViewItemEventArgs e)
        {
            // Details view can raise DrawItem alone during the first hover.
            // Painting the whole row here would erase cells until DrawSubItem runs again.
        }

        private void DrawCell(object sender, DrawListViewSubItemEventArgs e)
        {
            var profile = e.Item.Tag as ChannelProfile;
            if (profile == null) return;
            using (var background = new SolidBrush(e.Item.Selected ? UiTheme.Selected : UiTheme.Surface))
                e.Graphics.FillRectangle(background, e.Bounds);
            using (var line = new Pen(UiTheme.Divider))
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            var scale = e.Graphics.DpiX / 96f;
            var padding = (int)Math.Round(10 * scale);
            var bounds = Rectangle.FromLTRB(e.Bounds.Left + padding, e.Bounds.Top,
                Math.Max(e.Bounds.Left + padding, e.Bounds.Right - padding), e.Bounds.Bottom);
            var color = UiTheme.Text;
            var alignment = e.Header.TextAlign;
            if (e.ColumnIndex == 1)
            {
                alignment = HorizontalAlignment.Left;
                color = profile.State == LiveState.Live ? UiTheme.Live :
                    profile.State == LiveState.Offline ? UiTheme.Offline : UiTheme.Unknown;
                var diameter = Math.Max(6f, 7f * scale);
                var circle = new RectangleF(bounds.Left, e.Bounds.Top + (e.Bounds.Height - diameter) / 2f,
                    diameter, diameter);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var indicator = new SolidBrush(color)) e.Graphics.FillEllipse(indicator, circle);
                bounds.X += (int)Math.Ceiling(diameter + 8 * scale);
                bounds.Width = Math.Max(0, bounds.Width - (int)Math.Ceiling(diameter + 8 * scale));
            }
            else if (e.ColumnIndex == 3)
                color = profile.NotificationsEnabled ? UiTheme.Live : UiTheme.Muted;
            else if (e.ColumnIndex == 2 && profile.State != LiveState.Live)
                color = UiTheme.Muted;

            TextRenderer.DrawText(e.Graphics, e.SubItem.Text ?? "", Font, bounds, color,
                Flags(alignment) | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static TextFormatFlags Flags(HorizontalAlignment alignment)
        {
            return alignment == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter :
                alignment == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
        }
    }
}
