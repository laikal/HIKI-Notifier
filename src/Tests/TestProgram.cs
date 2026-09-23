using System;
using System.Drawing;
using System.IO;
using System.Linq;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.UI;

internal static class TestProgram
{
    private static int checks;
    private static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception("FAIL " + name); }
    [STAThread]
    private static void Main(string[] args)
    {
        var id = ChannelProfile.BuiltInId;
        Check(ChzzkClient.ParseLiveUrl(ChannelProfile.UrlFor(id)) == id, "live URL");
        Check(ChzzkClient.ParseLiveUrl("https://chzzk.naver.com/live/" + id + "/") == id, "trailing slash");
        Check(ChzzkClient.ParseLiveUrl("https://evil.example/live/" + id) == null, "host restriction");
        Check(ChzzkClient.ParseLiveUrl("http://chzzk.naver.com/live/" + id) == null, "https restriction");
        Check(ChzzkClient.ParseLiveUrl("https://chzzk.naver.com/" + id) == null, "live path restriction");
        Check(!PollingService.ShouldNotify(LiveState.Unknown, LiveState.Live), "no initial alert");
        Check(!PollingService.ShouldNotify(LiveState.Live, LiveState.Unknown), "network failure");
        Check(!PollingService.ShouldNotify(LiveState.Unknown, LiveState.Live), "reconnect no duplicate");
        Check(PollingService.ShouldNotify(LiveState.Offline, LiveState.Live), "offline to live alert");
        Check(new AppSettings().SoundMode == NotificationSoundMode.BuiltIn &&
              (int)NotificationSoundMode.BuiltIn == 0, "built-in sound default and saved value");
        Check(HikiNotifier.AppInfo.Icon != null, "icon loaded from executable");
        using (var aboutImage = HikiNotifier.AppInfo.LoadAboutImage())
            Check(aboutImage.Width > 0 && aboutImage.Height > 0, "ABOUT image embedded");
        var sound = new SoundService();
        Check(sound.Play(new AppSettings { SoundMode = NotificationSoundMode.Silent }), "silent sound mode");
        Check(!sound.Play(new AppSettings { SoundMode = NotificationSoundMode.CustomWave,
            WavePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav") }), "missing custom WAV is silent");
        using (var embedded = typeof(SoundService).Assembly.GetManifestResourceStream("HikiNotifier.Assets.DefaultNotification.wav"))
        {
            Check(embedded != null && embedded.Length > 44, "built-in WAV embedded");
            var header = new byte[4];
            embedded.Read(header, 0, header.Length);
            Check(System.Text.Encoding.ASCII.GetString(header) == "RIFF", "built-in WAV header");
        }
        var rplayUrl = "https://rplay.live/live/658ace4494bb71283af7ab4c";
        Check(RplayClient.ParseLiveUrl(rplayUrl) == "658ace4494bb71283af7ab4c", "RPLAY URL");
        Check(RplayClient.ParseLiveUrl("https://other.example/live/658ace4494bb71283af7ab4c") == null, "RPLAY host restriction");
        var rplay = RplayClient.CreateProfile(rplayUrl);
        var liveList = RplayClient.ParseLiveList("[{\"creatorOid\":\"658ace4494bb71283af7ab4c\",\"creatorNickname\":\"Creator\",\"title\":\"Live\",\"viewerCount\":0,\"streamState\":\"twitch\",\"unexpected\":true}]");
        RplayClient.Apply(rplay, liveList[rplay.CreatorOid]);
        Check(rplay.State == LiveState.Live && rplay.Name == "Creator" && rplay.ViewerCount == 0 && rplay.LiveTitle == "Live", "RPLAY live data");
        RplayClient.Apply(rplay, null);
        Check(rplay.State == LiveState.Offline && rplay.ViewerCount == null && rplay.Name == "Creator", "RPLAY offline data");
        RplayClient.MarkUnknown(rplay);
        Check(rplay.State == LiveState.Unknown, "RPLAY request failure");
        Check(RplayClient.ParseLiveList("{\"data\":[]}").Count == 0, "RPLAY empty live list");
        var malformedListRejected = false;
        try { RplayClient.ParseLiveList("{\"error\":true}"); } catch (InvalidOperationException) { malformedListRejected = true; }
        Check(malformedListRejected, "RPLAY malformed response is not offline");
        using (var alert = new NotificationForm(ChannelProfile.BuiltIn(true), 0))
        {
            Check(alert.Text == "히키 알리미", "custom alert constructed");
            Check(alert.Controls.Count == 4, "custom alert contents");
            var logo = alert.Controls.OfType<System.Windows.Forms.PictureBox>().Single();
            Check(ReferenceEquals(logo.Image, LogoResources.PlatformLogo(PlatformType.Chzzk)) &&
                  logo.SizeMode == System.Windows.Forms.PictureBoxSizeMode.Zoom, "CHZZK embedded logo");
            var watchButton = alert.Controls.OfType<System.Windows.Forms.Button>().Single();
            Check(alert.ClientSize.Height - watchButton.Bottom >= 12 &&
                  alert.ClientSize.Width - watchButton.Right >= 12, "alert button margins");
            Check(watchButton.Anchor == (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right),
                  "alert button anchor");
        }
        using (var alert = new NotificationForm(ChannelProfile.BuiltIn(true), 0))
        {
            var logo = alert.Controls.OfType<System.Windows.Forms.PictureBox>().Single();
            using (var image = new Bitmap(logo.Width, logo.Height))
                logo.DrawToBitmap(image, logo.ClientRectangle);
            Check(logo.Image.Width == 2172, "embedded logo reused after alert closes");
        }
        foreach (var platform in new[] { PlatformType.Rplay, PlatformType.Twitch })
        using (var alert = new NotificationForm(new ChannelProfile { Platform = platform, Name = "Creator", ViewerCount = 0 }, 0))
        {
            var logo = alert.Controls.OfType<System.Windows.Forms.PictureBox>().Single();
            Check(ReferenceEquals(logo.Image, LogoResources.PlatformLogo(platform)) &&
                  logo.SizeMode == System.Windows.Forms.PictureBoxSizeMode.Zoom, platform + " embedded logo");
            using (var image = new Bitmap(logo.Width, logo.Height))
                logo.DrawToBitmap(image, logo.ClientRectangle);
            Check(alert.Controls.OfType<System.Windows.Forms.Label>().Any(c => c.Text.Contains("0명")), platform + " zero viewers");
        }
        Check(LogoResources.MainLogo("ko").Width == 2172 && LogoResources.MainLogo("ko").Height == 724,
            "Korean main logo source size");
        Check(LogoResources.MainLogo("en").Width == 2172 && LogoResources.MainLogo("en").Height == 724 &&
              !ReferenceEquals(LogoResources.MainLogo("ko"), LogoResources.MainLogo("en")), "English main logo source size");
        var path = Path.Combine(Path.GetTempPath(), "hiki-notifier-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new SettingsService(path);
            var settings = service.Load();
            Check(settings.Profiles.Count == 1 && settings.Profiles[0].IsBuiltIn, "built-in present");
            settings.Profiles[0].NotificationsEnabled = false;
            settings.Profiles.Add(new ChannelProfile { ChannelId = "0123456789abcdef0123456789abcdef", LiveUrl = "wrong", Name = "Test", NotificationsEnabled = true });
            settings.Profiles.Add(rplay);
            rplay.NotificationsEnabled = false;
            service.Save(settings);
            var raw = File.ReadAllText(path);
            Check(!raw.Contains(ChannelProfile.BuiltInId), "built-in not serialized");
            var loaded = service.Load();
            Check(loaded.Profiles.Count == 3 && loaded.Profiles[0].IsBuiltIn, "built-in restored");
            Check(!loaded.Profiles[0].NotificationsEnabled, "built-in alert persisted");
            Check(loaded.Profiles[1].LiveUrl == ChannelProfile.UrlFor("0123456789abcdef0123456789abcdef"), "canonical user URL");
            Check(loaded.Profiles[2].Platform == PlatformType.Rplay && loaded.Profiles[2].CreatorOid == rplay.CreatorOid &&
                  loaded.Profiles[2].LiveUrl == rplayUrl && !loaded.Profiles[2].NotificationsEnabled, "RPLAY settings round trip");
            Console.WriteLine("PASS " + checks);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
