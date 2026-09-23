using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.UI;

namespace HikiNotifier.Services
{
    // A small native WinForms alert. No Start Menu shortcut, AppUserModelID, or WinRT registration.
    internal sealed class NotificationService
    {
        private readonly SoundService sound = new SoundService();
        private readonly List<NotificationForm> openAlerts = new List<NotificationForm>();

        public void Show(ChannelProfile profile, AppSettings settings, bool explicitReenable = false)
        {
            if ((!settings.NotificationsEnabled && !explicitReenable) || !profile.NotificationsEnabled) return;
            openAlerts.RemoveAll(existing => existing.IsDisposed);
            var alert = new NotificationForm(profile, openAlerts.Count);
            alert.FormClosed += (s, e) => openAlerts.Remove(alert);
            openAlerts.Add(alert);
            alert.Show();
            sound.Play(settings);
        }
        public bool TestSound(AppSettings settings) => sound.Play(settings);
        public static void Open(string url)
        { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { } }
    }

    internal sealed class NotificationForm : Form
    {
        private readonly Timer timer = new Timer { Interval = 12000 };
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } }
        public NotificationForm(ChannelProfile profile, int slot)
        {
            Text = "히키 알리미";
            UiTheme.Apply(this);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false; ShowIcon = true;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(388, 200);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 14, Math.Max(area.Top + 14, area.Bottom - Height - 14 - slot * (Height + 8)));
            var heading = new PictureBox { Image = LogoResources.PlatformLogo(profile.Platform),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = UiTheme.Background,
                Left = 16, Top = 10, Width = 150, Height = 44 };
            var name = new Label { Text = profile.Name + " 방송을 시작했습니다!", Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Left = 16, Top = 61, Width = 350, Height = 26 };
            var details = string.Join("  ·  ", new[] { profile.LiveTitle, profile.Category,
                profile.ViewerCount.HasValue ? profile.ViewerCount.Value.ToString("N0") + "명" : null }.WhereNotEmpty());
            var info = new Label { Text = details, Left = 16, Top = 96, Width = 350, Height = 37, AutoEllipsis = true };
            var open = new ModernButton { Text = "방송 보러가기", Width = 130, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            UiTheme.Style(open);
            open.Location = new Point(ClientSize.Width - open.Width - 16, ClientSize.Height - open.Height - 16);
            open.Click += (s, e) => { NotificationService.Open(profile.LiveUrl); Close(); };
            Controls.Add(heading); Controls.Add(name); Controls.Add(info); Controls.Add(open);
            timer.Tick += (s, e) => Close(); timer.Start();
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); };
        }
    }

    internal static class NotificationEnumerable
    {
        public static System.Collections.Generic.IEnumerable<string> WhereNotEmpty(this System.Collections.Generic.IEnumerable<string> source)
        { foreach (var item in source) if (!string.IsNullOrWhiteSpace(item)) yield return item; }
    }
}
