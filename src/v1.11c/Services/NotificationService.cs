using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Random random = new Random();
        private PlatformType? lastTestPlatform;

        public void Show(ChannelProfile profile, AppSettings settings, bool explicitReenable = false)
        {
            if ((!settings.NotificationsEnabled && !explicitReenable) || !profile.NotificationsEnabled) return;
            openAlerts.RemoveAll(existing => existing.IsDisposed);
            var alert = new NotificationForm(profile, openAlerts.Count, NotificationEventType.LiveStarted,
                settings.Language, notificationOpacity: settings.NotificationOpacity);
            alert.FormClosed += (s, e) => openAlerts.Remove(alert);
            openAlerts.Add(alert);
            alert.Show();
            sound.Play(settings);
        }
        public void ShowContent(ChannelProfile profile, AppSettings settings, YouTubeEntry entry, bool explicitReenable = false)
        {
            if ((!settings.NotificationsEnabled && !explicitReenable) || !profile.NotificationsEnabled || entry == null) return;
            ShowAlert(new NotificationForm(profile, openAlerts.Count, NotificationEventType.NewContent,
                settings.Language, entry.Title, entry.Url, notificationOpacity: settings.NotificationOpacity), settings);
        }
        public void Replay(ChannelProfile profile, AppSettings settings, ReplayableNotification replay)
        {
            if (replay == null) return;
            if (replay.Type == NotificationEventType.NewContent) ShowContent(profile, settings, replay.Content, true);
            else Show(profile, settings, true);
        }
        public PlatformType ShowTest(AppSettings settings)
        {
            var platform = ChooseTestPlatform();
            var profile = new ChannelProfile { Platform = platform, Name = "HIKI Notifier", NotificationsEnabled = true };
            ShowAlert(new NotificationForm(profile, openAlerts.Count, NotificationEventType.LiveStarted,
                settings.Language, null, null, true, settings.NotificationOpacity), settings);
            return platform;
        }
        internal PlatformType ChooseTestPlatform()
        {
            var candidates = LogoResources.TestPlatforms;
            var choices = candidates.Length > 1 && lastTestPlatform.HasValue
                ? candidates.Where(p => p != lastTestPlatform.Value).ToArray() : candidates;
            var platform = choices[random.Next(choices.Length)];
            lastTestPlatform = platform;
            return platform;
        }
        private void ShowAlert(NotificationForm alert, AppSettings settings)
        {
            openAlerts.RemoveAll(existing => existing.IsDisposed);
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
        public NotificationForm(ChannelProfile profile, int slot, NotificationEventType type = NotificationEventType.LiveStarted,
            string language = "ko", string contentTitle = null, string contentUrl = null, bool test = false,
            int notificationOpacity = 100)
        {
            Text = "히키 알리미";
            UiTheme.Apply(this);
            Opacity = AppSettings.ClampNotificationOpacity(notificationOpacity) / 100.0;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false; ShowIcon = true;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(388, 200);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 14, Math.Max(area.Top + 14, area.Bottom - Height - 14 - slot * (Height + 8)));
            var heading = new PictureBox { Image = LogoResources.PlatformLogo(profile.Platform),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = UiTheme.Background,
                Left = 16, Top = 10, Width = 150, Height = 44 };
            var english = language == "en";
            var titleText = test ? (english ? "Notification Test" : "알림 테스트") :
                type == NotificationEventType.NewContent ? profile.Name + (english ? " — New video uploaded" : " 새 영상이 업로드되었습니다!") :
                profile.Name + (english ? " is live!" : " 방송을 시작했습니다!");
            var name = new Label { Text = titleText, Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Left = 16, Top = 61, Width = 350, Height = 26 };
            var details = test ? (english ? "HIKI Notifier notifications are working correctly." : "HIKI Notifier의 알림이 정상적으로 표시됩니다.") :
                type == NotificationEventType.NewContent ? (contentTitle ?? profile.ProviderState?.LatestContentTitle ?? "") :
                string.Join("  ·  ", new[] { profile.LiveTitle, profile.Category,
                profile.ViewerCount.HasValue ? profile.ViewerCount.Value.ToString("N0") + "명" : null }.WhereNotEmpty());
            var info = new Label { Text = details, Left = 16, Top = 96, Width = 350, Height = 37, AutoEllipsis = true };
            var open = new ModernButton { Text = type == NotificationEventType.NewContent ?
                (english ? "Watch video" : "영상 보러가기") : (english ? "Watch live" : "방송 보러가기"),
                Width = 130, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            UiTheme.Style(open);
            open.Location = new Point(ClientSize.Width - open.Width - 16, ClientSize.Height - open.Height - 16);
            open.Enabled = !test;
            open.Click += (s, e) => { NotificationService.Open(type == NotificationEventType.NewContent ? contentUrl : profile.LiveUrl); Close(); };
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
