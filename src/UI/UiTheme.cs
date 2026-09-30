using System.Drawing;
using System.Windows.Forms;

namespace HikiNotifier.UI
{
    internal static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(245, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color Text = Color.FromArgb(35, 42, 51);
        public static readonly Color Muted = Color.FromArgb(103, 112, 124);
        public static readonly Color Accent = Color.FromArgb(25, 104, 181);
        public static readonly Color AccentHover = Color.FromArgb(232, 243, 253);
        public static readonly Color AccentPressed = Color.FromArgb(217, 234, 249);
        public static readonly Color Border = Color.FromArgb(25, 104, 181);
        public static readonly Color Header = Color.FromArgb(241, 245, 249);
        public static readonly Color Divider = Color.FromArgb(228, 234, 240);
        public static readonly Color Selected = Color.FromArgb(226, 240, 252);
        public static readonly Color Live = Color.FromArgb(23, 129, 72);
        public static readonly Color Offline = Color.FromArgb(139, 148, 159);
        public static readonly Color Unknown = Color.FromArgb(194, 125, 38);
        public static readonly Color Disabled = Color.FromArgb(164, 173, 184);
        public static readonly Color Error = Color.FromArgb(176, 43, 43);
        public const int ButtonHeight = 32;
        public const int CornerRadius = 5;

        public static void Apply(Form form)
        {
            form.BackColor = Background;
            form.ForeColor = Text;
            form.Font = new Font("Segoe UI", 9F);
            form.Icon = AppInfo.Icon;
        }

        public static void Style(Button button)
        {
            button.BackColor = Surface;
            button.ForeColor = Text;
            button.Font = new Font("Segoe UI", 9F);
            button.Height = ButtonHeight;
            button.Padding = new Padding(6, 2, 6, 2);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = AccentPressed;
        }
    }
}
