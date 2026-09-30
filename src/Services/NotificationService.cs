using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
                settings.Language, notificationOpacity: settings.NotificationOpacity,
                appearance: ForProfile(profile, settings), durationSeconds: settings.NotificationDurationSeconds);
            alert.FormClosed += (s, e) => openAlerts.Remove(alert);
            openAlerts.Add(alert);
            alert.Show();
            sound.Play(settings);
        }
        public void ShowContent(ChannelProfile profile, AppSettings settings, ContentEntry entry, bool explicitReenable = false)
        {
            if ((!settings.NotificationsEnabled && !explicitReenable) || !profile.NotificationsEnabled || entry == null) return;
            ShowAlert(new NotificationForm(profile, openAlerts.Count, NotificationEventType.NewContent,
                settings.Language, entry.Title, entry.Url, notificationOpacity: settings.NotificationOpacity,
                appearance: ForProfile(profile, settings), durationSeconds: settings.NotificationDurationSeconds), settings);
        }
        public void Replay(ChannelProfile profile, AppSettings settings, ReplayableNotification replay)
        {
            if (replay == null) return;
            if (replay.Type == NotificationEventType.NewContent) ShowContent(profile, settings, replay.Content, true);
            else Show(profile, settings, true);
        }
        private static NotificationAppearance ForProfile(ChannelProfile channel, AppSettings settings) =>
            settings.Streamers?.FirstOrDefault(s => s.Id == channel.StreamerProfileId)?.Appearance;

        public PlatformType ShowTest(StreamerProfile streamer, NotificationAppearance appearance, AppSettings settings)
        {
            var platform = ChooseTestPlatform(streamer);
            var profile = new ChannelProfile { Platform = platform, Name = streamer?.DisplayName ?? "HIKI Notifier",
                StreamerProfileId = streamer?.Id, NotificationsEnabled = true };
            ShowAlert(new NotificationForm(profile, openAlerts.Count, NotificationEventType.LiveStarted,
                settings.Language, null, null, true, settings.NotificationOpacity,
                appearance, settings.NotificationDurationSeconds), settings);
            return platform;
        }
        internal PlatformType ChooseTestPlatform(StreamerProfile streamer = null)
        {
            var available = LogoResources.TestPlatforms;
            var inProfile = streamer?.Channels?.Select(c => c.Platform).Distinct()
                .Where(candidate => available.Contains(candidate)).ToArray();
            var candidates = inProfile != null && inProfile.Length > 0 ? inProfile : available;
            if (candidates.Length == 0) return (PlatformType)(-1);
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

    internal static class NotificationColors
    {
        internal static Color Parse(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || !System.Text.RegularExpressions.Regex.IsMatch(value,
                "^#[0-9a-fA-F]{6}$")) return fallback;
            try { return ColorTranslator.FromHtml(value); } catch { return fallback; }
        }
    }

    internal sealed class OutlinedText : Control
    {
        internal Color FillColor { get; set; } = Color.White;
        internal Color BorderColor { get; set; } = Color.FromArgb(32, 32, 32);
        internal int MaxLines { get; set; } = 1;
        internal OutlinedText() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(Text)) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            var font = Font;
            using (var path = new GraphicsPath())
            using (var format = new StringFormat(StringFormat.GenericDefault))
            using (var outline = new Pen(BorderColor, 2f) { LineJoin = LineJoin.Round })
            using (var fill = new SolidBrush(FillColor))
            using (var shadow = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags |= MaxLines == 1 ? StringFormatFlags.NoWrap : StringFormatFlags.LineLimit;
                var lineHeight = font.GetHeight(e.Graphics);
                var layout = new RectangleF(3, 2, Width - 9,
                    Math.Min(Height - 6, (float)Math.Ceiling(lineHeight * Math.Max(1, MaxLines))));
                path.AddString(Text, font.FontFamily, (int)font.Style, e.Graphics.DpiY * font.SizeInPoints / 72f,
                    layout, format);
                using (var shadowPath = (GraphicsPath)path.Clone())
                using (var offset = new Matrix())
                {
                    offset.Translate(2f, 2f);
                    shadowPath.Transform(offset);
                    e.Graphics.FillPath(shadow, shadowPath);
                }
                e.Graphics.DrawPath(outline, path);
                e.Graphics.FillPath(fill, path);
            }
        }
    }

    internal sealed class NotificationForm : Form
    {
        private readonly Timer closeTimer = new Timer();
        private readonly Timer frameTimer = new Timer();
        private readonly Action<string> opener;
        private readonly string targetUrl;
        private readonly bool test;
        private bool clicked;
        private MemoryStream imageStream;
        private Image background;
        private FrameDimension frameDimension;
        private int frameIndex, frameCount;
        private byte[] frameDelays;
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } }
        public NotificationForm(ChannelProfile profile, int slot, NotificationEventType type = NotificationEventType.LiveStarted,
            string language = "ko", string contentTitle = null, string contentUrl = null, bool test = false,
            int notificationOpacity = 100, NotificationAppearance appearance = null, int durationSeconds = 12,
            Action<string> openUrl = null)
        {
            this.test = test; opener = openUrl ?? NotificationService.Open;
            targetUrl = type == NotificationEventType.NewContent ? contentUrl ?? profile.LatestContentUrl : profile.LiveUrl;
            Text = new LanguageService(language).Get("General", "AppName");
            UiTheme.Apply(this);
            FormBorderStyle = FormBorderStyle.None; MinimumSize = new Size(480, 270); MaximumSize = MinimumSize;
            ClientSize = new Size(480, 270); MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true; Opacity = AppSettings.ClampNotificationOpacity(notificationOpacity) / 100.0;
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 14, Math.Max(area.Top + 14, area.Bottom - Height - 14 - slot * (Height + 8)));
            LoadBackground(appearance?.PendingBackgroundPath ?? appearance?.BackgroundPath);
            var textColor = NotificationColors.Parse(appearance?.TextColor, Color.White);
            var outlineColor = NotificationColors.Parse(appearance?.OutlineColor, Color.FromArgb(32, 32, 32));
            var heading = new PictureBox { Image = LogoResources.PlatformLogo(profile.Platform),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent,
                Left = 16, Top = 10, Width = 180, Height = 65 };
            var english = language == "en";
            var titleText = test ? (english ? "Notification Test" : "알림 테스트") :
                type == NotificationEventType.NewContent ? profile.Name + (english ? " — New video uploaded" : " 새 영상이 업로드되었습니다!") :
                profile.Name + (english ? " is live!" : " 방송을 시작했습니다!");
            var name = new OutlinedText { Text = titleText, Font = new Font("Segoe UI", 15, FontStyle.Bold),
                FillColor = textColor, BorderColor = outlineColor, Left = 17, Top = 88, Width = 448, Height = 43 };
            var details = test ? (english ? "HIKI Notifier notifications are working correctly." : "HIKI Notifier의 알림이 정상적으로 표시됩니다.") :
                type == NotificationEventType.NewContent ? (contentTitle ?? profile.ProviderState?.LatestContentTitle ?? "") :
                string.Join("  ·  ", new[] { profile.LiveTitle, profile.Category,
                profile.ViewerCount.HasValue ? profile.ViewerCount.Value.ToString("N0") + "명" : null }.WhereNotEmpty());
            var info = new OutlinedText { Text = details, Font = new Font("Segoe UI", 11.5f, FontStyle.Regular), MaxLines = 2,
                FillColor = textColor, BorderColor = outlineColor, Left = 17, Top = 146, Width = 448, Height = 72 };
            Controls.Add(heading); Controls.Add(name); Controls.Add(info);
            AttachClicks(this);
            closeTimer.Interval = AppSettings.ClampNotificationDuration(durationSeconds) * 1000;
            closeTimer.Tick += (s, e) => Close(); closeTimer.Start();
            Disposed += (s, e) => { closeTimer.Stop(); closeTimer.Dispose(); frameTimer.Stop(); frameTimer.Dispose();
                background?.Dispose(); imageStream?.Dispose(); background = null; imageStream = null; };
        }
        private void AttachClicks(Control control)
        {
            control.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) OpenOrClose(); };
            foreach (Control child in control.Controls) AttachClicks(child);
        }
        private void OpenOrClose()
        {
            if (clicked) return;
            clicked = true;
            if (!test && !string.IsNullOrWhiteSpace(targetUrl)) opener(targetUrl);
            Close();
        }
        private void LoadBackground(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                HikiNotifier.Services.BackgroundImage.Validate(path);
                imageStream = new MemoryStream(File.ReadAllBytes(path));
                background = Image.FromStream(imageStream, true, true);
                if (background.RawFormat.Guid == ImageFormat.Gif.Guid)
                {
                    frameDimension = new FrameDimension(background.FrameDimensionsList[0]);
                    frameCount = background.GetFrameCount(frameDimension);
                    if (frameCount > 1)
                    {
                        try { frameDelays = background.GetPropertyItem(0x5100).Value; } catch (ArgumentException) { }
                        frameTimer.Tick += (s, e) => {
                            frameIndex = (frameIndex + 1) % frameCount;
                            background.SelectActiveFrame(frameDimension, frameIndex);
                            Invalidate(); foreach (Control child in Controls) child.Invalidate(); ScheduleFrame();
                        };
                        ScheduleFrame();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[Notification image] " + ex.GetType().Name);
                background?.Dispose(); imageStream?.Dispose(); background = null; imageStream = null;
            }
        }
        private void ScheduleFrame()
        {
            var ticks = frameDelays != null && frameDelays.Length >= (frameIndex + 1) * 4
                ? BitConverter.ToInt32(frameDelays, frameIndex * 4) : 10;
            frameTimer.Interval = Math.Max(20, Math.Min(60000, ticks * 10));
            frameTimer.Start();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (background == null) { base.OnPaintBackground(e); return; }
            e.Graphics.DrawImageUnscaled(background, 0, 0);
        }
    }

    internal static class NotificationEnumerable
    {
        public static System.Collections.Generic.IEnumerable<string> WhereNotEmpty(this System.Collections.Generic.IEnumerable<string> source)
        { foreach (var item in source) if (!string.IsNullOrWhiteSpace(item)) yield return item; }
    }
}
