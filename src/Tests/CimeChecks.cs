using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.UI;

internal static class CimeChecks
{
    private static string Fixture(bool active, string opened = "2026-09-25T01:16:51.000Z")
    {
        // Minimal public shape observed on cheriihanavi/live and funzinnu/live, 2026-09-25.
        // Playback, messages and unrelated page data are deliberately omitted.
        var live = new { id = "1000052", state = active ? "ACTIVE" : "INACTIVE",
            title = "Creator's \"live\" \\ test", openedAt = active ? opened : null, curViewerCnt = 0,
            channel = new { id = "1000201", slug = "example", name = "Creator", isLive = active } };
        var root = new { key = "/@[:slug]/live", args = new[] {
            new { path = "/@[:slug]/live", bodyData = new { live } } } };
        var json = new JavaScriptSerializer().Serialize(root);
        return "<script>(window.__RUNE_DATA__=window.__RUNE_DATA__||[]).push(JSON.parse('" +
            json.Replace("\\", "\\\\").Replace("'", "\\'") + "'));</script>";
    }
    public static void Run(Action<bool, string> check)
    {
        foreach (var url in new[] { "https://ci.me/@example/live", "https://ci.me/@example", "ci.me/@example/live", "ci.me/@example" })
            check(CimeClient.ParseHandle(url) == "example", "CIME URL " + url);
        check(CimeClient.ParseHandle("https://evil.example/@example/live") == null &&
            CimeClient.ParseHandle("https://ci.me/@example/video/123") == null &&
            CimeClient.ParseHandle("ftp://ci.me/@example") == null, "CIME rejects foreign host and unsupported routes");
        var live = CimeClient.ParseHtml(Fixture(true), "example");
        check(live.State == LiveState.Live && live.Viewers == 0 && live.Title == "Creator's \"live\" \\ test" &&
            live.SessionId.StartsWith("1000052:"), "CIME RUNE live metadata and JS escaping");
        check(CimeClient.ParseHtml(Fixture(false), "example").State == LiveState.Offline, "CIME confirmed inactive");
        check(CimeClient.ParseHtml(Fixture(true).Replace("\"isLive\":true", "\"isLive\":false"), "example").State == LiveState.Live &&
            CimeClient.ParseHtml(Fixture(false).Replace("\"isLive\":false", "\"isLive\":true"), "example").State == LiveState.Offline,
            "CIME primary state wins over lagging channel flag");
        check(CimeClient.ParseHtml(Fixture(true), "other").State == LiveState.Unknown, "CIME channel binding");
        check(CimeClient.ParseHtml("<html>live live live</html>", "example").State == LiveState.Unknown &&
            CimeClient.ParseHtml(Fixture(true).Replace("ACTIVE", "CHANGED"), "example").State == LiveState.Unknown &&
            CimeClient.ParseHtml(Fixture(true).Replace("bodyData", "changed"), "example").State == LiveState.Unknown,
            "CIME missing or changed structure stays Unknown");
        check(CimeClient.ParseHtml(Fixture(true) + Fixture(false), "example").State == LiveState.Unknown,
            "CIME contradictory route data rejected");
        using (var client = new CimeClient(new FaultHandler(new HttpRequestException("fixture"))))
            check(client.GetStatusAsync("example", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown,
                "CIME network failure Unknown");
        using (var client = new CimeClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
            check(client.GetStatusAsync("example", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown,
                "CIME HTTP failure Unknown");

        var next = live;
        var provider = new CimeProvider((handle, token) => Task.FromResult(next));
        var profile = new ChannelProfile { Platform = PlatformType.Cime, ChannelId = "example",
            LiveUrl = CimeClient.UrlFor("example"), AutoOpenLive = true };
        int alerts = 0, opens = 0;
        var opener = new AutoOpenService(url => { check(url == profile.LiveUrl, "CIME AutoOpen canonical URL"); opens++; });
        Action poll = () => provider.PollAsync(new[] { profile }, (p, previous, entry) => {
            if (PollingService.ShouldNotify(previous, p.State)) alerts++;
            opener.OnUpdated(p, previous, provider.Capabilities);
        }, CancellationToken.None).GetAwaiter().GetResult();
        poll(); poll();
        check(alerts == 0 && opens == 0, "CIME startup Live baseline stays silent");
        next = new CimeStatus(); poll(); next = live; poll();
        check(alerts == 0 && opens == 0, "CIME Unknown recovery stays silent");
        next = CimeClient.ParseHtml(Fixture(false), "example"); poll();
        next = CimeClient.ParseHtml(Fixture(true, "2026-09-26T01:00:00Z"), "example"); poll(); poll();
        check(alerts == 1 && opens == 1, "CIME actual Offline to Live alerts and opens once");
        next = new CimeStatus(); poll();
        next = CimeClient.ParseHtml(Fixture(true, "2026-09-26T01:00:00Z"), "example"); poll();
        check(alerts == 1 && opens == 1, "CIME Live Unknown Live same session remains silent");
        profile.AutoOpenLive = false; poll(); profile.AutoOpenLive = true; poll();
        check(opens == 1, "CIME enabling AutoOpen during Live does not open");
        check(provider.GetReplayableNotification(profile)?.Type == NotificationEventType.LiveStarted,
            "CIME notification replay uses existing contract");
        check(LogoResources.TestPlatforms.Contains(PlatformType.Cime), "CIME included in test notification logos");
        using (var alert = new NotificationForm(profile, 0, notificationOpacity: 85))
            check(alert.Controls.OfType<PictureBox>().Any(b => ReferenceEquals(b.Image, LogoResources.PlatformLogo(PlatformType.Cime))),
                "CIME actual notification logo");
        var asm = typeof(LogoResources).Assembly;
        foreach (var asset in new[] { "Kick", "Spoon", "TwitCasting", "JP_logo", "CIME", "SOOP" })
            using (var stream = (asset == "CIME" ? typeof(CimeProvider).Assembly : asset == "SOOP" ? typeof(SoopProvider).Assembly : asm).GetManifestResourceStream("HikiNotifier.Assets." + asset + ".png"))
                check(stream != null && stream.Length > 0, "embedded resource " + asset);
        check(!ReferenceEquals(LogoResources.MainLogo("ko"), LogoResources.MainLogo("ja")) &&
            !ReferenceEquals(LogoResources.MainLogo("ja"), LogoResources.MainLogo("en")), "distinct ko ja en logos");
        foreach (var code in new[] { "fr", "de", "zh", "", null })
            check(ReferenceEquals(LogoResources.MainLogo(code), LogoResources.MainLogo("en")), "default English logo " + code);
        var language = new LanguageService("ko");
        foreach (var code in new[] { "ko", "ja", "en" })
        {
            language.Language = code;
            check(language.Language == code && language.Get("Profiles", "InvalidUrl").Contains("CIME"),
                "shipped language and CIME text " + code);
            check(ReferenceEquals(LogoResources.MainLogo(language.Language), LogoResources.MainLogo(code)),
                "runtime language code logo " + code);
        }
        using (var chzzk = new ChzzkClient()) using (var rplay = new RplayClient())
        using (var youtube = new YouTubeClient()) using (var cime = new CimeClient())
        {
            var registry = TestProviders.Create(chzzk, rplay, youtube, cime: cime);
            check(registry.ForUrl("ci.me/@example")?.Platform == PlatformType.Cime, "CIME registry route");
            using (var form = new ProfileEditForm(language, registry))
                check(((Button)form.Controls["addChannelButton"]).ContextMenuStrip.Items.Cast<ToolStripItem>()
                    .Any(i => i.Text == "CIME"), "CIME channel menu");
        }
        var root = Path.Combine(Path.GetTempPath(), "hiki-cime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new SettingsService(Path.Combine(root, "settings.json"));
            var settings = storage.Load();
            var streamer = new StreamerProfile { DisplayName = "CIME fixture" };
            profile.StreamerProfileId = streamer.Id; streamer.Channels.Add(profile); settings.Streamers.Add(streamer);
            storage.Save(settings);
            var restored = storage.Load().Streamers.Single(s => s.Id == streamer.Id).Channels.Single();
            check(restored.Platform == PlatformType.Cime && restored.LiveUrl == profile.LiveUrl &&
                restored.ProviderState.LastLiveSessionId == profile.ProviderState.LastLiveSessionId,
                "CIME profile and existing provider state roundtrip");
        }
        finally { Directory.Delete(root, true); }
    }
    public static void LiveProbe()
    {
        using (var client = new CimeClient())
        foreach (var handle in new[] { "cheriihanavi", "funzinnu" })
        {
            var status = client.GetStatusAsync(handle, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine(handle + ": " + status.State + " title=" + status.Title +
                " viewers=" + status.Viewers + " session=" + status.SessionId);
            if (status.State == LiveState.Unknown) throw new Exception("CIME online parse failed");
        }
    }
}
