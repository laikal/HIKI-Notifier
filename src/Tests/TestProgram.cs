using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
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
        try { if (args.Length == 2 && args[0] == "--module-startup") ModuleChecks.Smoke(args[1]); else Run(args); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        if (args.Length == 0)
        {
            ModuleChecks.Run(Check); V120Checks.Run(Check); TwitchChecks.Run(Check); CimeChecks.Run(Check);
            Console.WriteLine("PASS " + checks); return;
        }
        if (args.Length == 1 && args[0] == "--v120-checks") { V120Checks.Run(Check); Console.WriteLine("PASS v1.20C " + checks); return; }
        if (args.Length == 1 && args[0] == "--provider-regression")
        { ModuleChecks.Run(Check); TwitchChecks.Run(Check); CimeChecks.Run(Check); Console.WriteLine("PASS providers " + checks); return; }
        if (args.Length == 1 && args[0] == "--live-cime") { CimeChecks.LiveProbe(); return; }
        if (args.Length == 1 && args[0] == "--cime-checks") { CimeChecks.Run(Check); Console.WriteLine("PASS " + checks); return; }
        if (args.Length == 1 && args[0] == "--live-twitch") { TwitchChecks.LiveProbe(); return; }
        if (args.Length == 1 && args[0] == "--live-html")
        {
            using (var client = new YouTubeClient())
            {
                var snapshot = client.GetHtmlSnapshotAsync(
                    "https://www.youtube.com/@%ED%9E%88%ED%82%A4%EB%AA%A8%EB%A6%AC%EB%84%A4%EC%BD%94",
                    "UCFXuQjfDXfCRCk7xfuKu2ew", CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("LIVE_HTML_COUNTS videos=" + (snapshot.Videos?.Count.ToString() ?? "unavailable") +
                    " shorts=" + (snapshot.Shorts?.Count.ToString() ?? "unavailable"));
                foreach (var error in snapshot.Errors) Console.WriteLine("LIVE_HTML_ERROR " + error.Message);
                Check(snapshot.Videos?.Count > 0 && snapshot.Shorts?.Count > 0, "live channel HTML tabs parsed");
                Console.WriteLine("LIVE_HTML videos=" + snapshot.Videos[0].ContentId + " shorts=" +
                    snapshot.Shorts[0].ContentId + " title=" + snapshot.Shorts[0].Title);
            }
            return;
        }
        if (args.Length == 1 && args[0] == "--live-fallback")
        {
            using (var client = new YouTubeClient())
            {
                var profile = new ChannelProfile { Platform = PlatformType.YouTube,
                    ChannelId = "UCFXuQjfDXfCRCk7xfuKu2ew",
                    LiveUrl = "https://www.youtube.com/@%ED%9E%88%ED%82%A4%EB%AA%A8%EB%A6%AC%EB%84%A4%EC%BD%94",
                    ProviderState = new ProviderState { LastSeenContentId = "eGDGvf99JR8",
                        LatestContentTitle = "[치지직클립] 우물우물" } };
                var alerts = new List<string>();
                new YouTubeProvider(client).PollAsync(new[] { profile },
                    (p, previous, entry) => { if (entry != null) alerts.Add(entry.ContentId); },
                    CancellationToken.None).GetAwaiter().GetResult();
                Check(alerts.Contains("TzJAR1xLNl4") &&
                    profile.ProviderState.LastSeenContentId == "TzJAR1xLNl4",
                    "live fallback detects new Shorts from stored baseline");
                Console.WriteLine("LIVE_FALLBACK alerts=" + string.Join(",", alerts));
            }
            return;
        }
        if (args.Length == 1 && args[0] != "--legacy-checks")
        {
            var migrationRoot = Path.Combine(Path.GetTempPath(), "hiki-real-legacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(migrationRoot);
            try
            {
                var copy = Path.Combine(migrationRoot, "settings.json");
                File.Copy(args[0], copy);
                var source = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(copy, Encoding.UTF8));
                var legacyCount = source.Profiles.Count(p => p != null && !p.IsBuiltIn && p.ChannelId != BuiltInChannel.BuiltInId);
                var storage = new SettingsService(copy);
                var result = storage.Load();
                Check(storage.LoadErrors.Count == 0 && result.Streamers.Count == legacyCount + 1,
                    "actual legacy settings migrate without loss");
                Check(Directory.GetFiles(Path.Combine(migrationRoot, "profiles"), "*.json").Length == legacyCount,
                    "actual legacy profile file count");
                Check(File.Exists(copy + ".migrated.bak"), "actual legacy backup");
                Console.WriteLine("PASS actual legacy " + checks);
                return;
            }
            finally { Directory.Delete(migrationRoot, true); }
        }
        var clickTime = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
        var easterEggUrls = new List<string>();
        var easterEgg = new LogoEasterEgg(url => easterEggUrls.Add(url), now: () => clickTime, random: new Random(123));
        for (var i = 0; i < 14; i++) easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 0, "logo 14 clicks do not open URL");
        easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 1 && LogoEasterEgg.IsCandidateUrl(easterEggUrls[0]),
            "logo 15th click opens exactly one allowed URL");
        for (var i = 0; i < 15; i++) easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 2 && LogoEasterEgg.IsCandidateUrl(easterEggUrls[1]),
            "logo counter resets after trigger");
        for (var i = 0; i < 7; i++) easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        clickTime = clickTime.AddSeconds(5);
        for (var i = 0; i < 14; i++) easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 2, "logo idle timeout discards partial count");
        easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 3, "logo requires 15 new clicks after timeout");
        for (var i = 0; i < 150; i++) easterEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(easterEggUrls.Count == 13 && easterEggUrls.All(LogoEasterEgg.IsCandidateUrl) &&
            easterEggUrls.Distinct().Count() > 1, "logo random selection stays within five candidates and varies");
        var launchAttempts = 0;
        var failLaunch = true;
        var failedLaunchEgg = new LogoEasterEgg(url => {
            launchAttempts++;
            if (failLaunch) throw new InvalidOperationException("simulated shell failure");
        }, now: () => clickTime, random: new Random(321));
        for (var i = 0; i < 15; i++) failedLaunchEgg.RegisterClickAsync().GetAwaiter().GetResult();
        failLaunch = false;
        for (var i = 0; i < 15; i++) failedLaunchEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(launchAttempts == 2, "browser launch failure is contained and counter recovers");
        var regularVideos = new List<ContentEntry> {
            new ContentEntry { ContentId = "d9LiGMMNz50", Url = "https://www.youtube.com/watch?v=d9LiGMMNz50" },
            new ContentEntry { ContentId = "TzJAR1xLNl4", Url = "https://www.youtube.com/shorts/TzJAR1xLNl4" },
            new ContentEntry { ContentId = "bad", Url = "https://www.youtube.com/watch?v=bad" } };
        var videoOpens = new List<string>();
        var videoEgg = new LogoEasterEgg(videoOpens.Add,
            () => Task.FromResult<IReadOnlyList<ContentEntry>>(regularVideos));
        for (var i = 0; i < 15; i++) videoEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(videoOpens.SequenceEqual(new[] { "https://www.youtube.com/watch?v=d9LiGMMNz50" }),
            "Easter Egg uses only parsed regular videos, excluding Shorts and invalid IDs");
        var emptyOpens = new List<string>();
        var emptyEgg = new LogoEasterEgg(emptyOpens.Add,
            () => Task.FromResult<IReadOnlyList<ContentEntry>>(new List<ContentEntry>()));
        for (var i = 0; i < 15; i++) emptyEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(emptyOpens.Count == 1 && LogoEasterEgg.IsCandidateUrl(emptyOpens[0]),
            "empty videos list uses existing URL fallback");
        var failedFetchOpens = new List<string>();
        var failedFetchEgg = new LogoEasterEgg(failedFetchOpens.Add,
            () => Task.FromException<IReadOnlyList<ContentEntry>>(new HttpRequestException("offline")));
        for (var i = 0; i < 15; i++) failedFetchEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(failedFetchOpens.Count == 1 && LogoEasterEgg.IsCandidateUrl(failedFetchOpens[0]),
            "network failure uses existing URL fallback");
        var parseFailureOpens = new List<string>();
        var parseFailureEgg = new LogoEasterEgg(parseFailureOpens.Add,
            () => Task.FromException<IReadOnlyList<ContentEntry>>(new InvalidOperationException("HTML changed")));
        for (var i = 0; i < 15; i++) parseFailureEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(parseFailureOpens.Count == 1 && LogoEasterEgg.IsCandidateUrl(parseFailureOpens[0]),
            "HTML parser failure uses existing URL fallback");
        var pendingVideos = new TaskCompletionSource<IReadOnlyList<ContentEntry>>();
        var fetches = 0;
        var pendingOpens = new List<string>();
        var pendingEgg = new LogoEasterEgg(pendingOpens.Add, () => { fetches++; return pendingVideos.Task; });
        for (var i = 0; i < 14; i++) pendingEgg.RegisterClickAsync().GetAwaiter().GetResult();
        var fetchTask = pendingEgg.RegisterClickAsync();
        for (var i = 0; i < 30; i++) pendingEgg.RegisterClickAsync().GetAwaiter().GetResult();
        Check(fetches == 1 && !fetchTask.IsCompleted && pendingOpens.Count == 0,
            "concurrent Easter Egg clicks do not duplicate the fetch");
        pendingVideos.SetResult(regularVideos);
        fetchTask.GetAwaiter().GetResult();
        Check(pendingOpens.Count == 1, "completed Easter Egg fetch opens once");
        Check(HikiNotifier.AppInfo.Version == "1.20C" && HikiNotifier.AppInfo.NumericVersion == "1.20.2.0",
            "central display and numeric versions");
        Check(SoopClient.ParseChannelId("demo_id") == "demo_id" &&
            SoopClient.ParseChannelId("https://play.sooplive.co.kr/Demo_ID/direct") == "demo_id" &&
            SoopClient.ParseChannelId("https://evil.example/demo_id") == null, "SOOP identifiers and URL normalization");
        var soopLive = SoopClient.Parse("{\"CHANNEL\":{\"RESULT\":1,\"BNO\":\"42\",\"TITLE\":\"Live now\",\"BJNICK\":\"Creator\"}}");
        Check(soopLive.State == LiveState.Live && soopLive.SessionId == "42" && soopLive.Title == "Live now", "SOOP live response");
        Check(SoopClient.Parse("{\"CHANNEL\":{\"RESULT\":0}} ").State == LiveState.Offline, "SOOP offline response");
        Check(SoopClient.Parse("{broken").State == LiveState.Unknown &&
            SoopClient.Parse("{\"CHANNEL\":{\"RESULT\":1}}").State == LiveState.Unknown, "SOOP malformed response stays unknown");
        Check(LogoResources.PlatformLogo(PlatformType.Soop).Width > 0, "SOOP embedded logo");
        var soopNext = new SoopStatus { State = LiveState.Offline };
        var soopProvider = new SoopProvider((channelKey, token) => Task.FromResult(soopNext));
        var soopProfile = new ChannelProfile { Platform = PlatformType.Soop, ChannelId = "demo_id", State = LiveState.Offline };
        var soopAlerts = 0;
        Action soopPoll = () => soopProvider.PollAsync(new[] { soopProfile }, (p, previous, entry) =>
        { if (PollingService.ShouldNotify(previous, p.State)) soopAlerts++; }, CancellationToken.None).GetAwaiter().GetResult();
        soopPoll(); soopNext = new SoopStatus { State = LiveState.Live, SessionId = "100", Title = "First" }; soopPoll(); soopPoll();
        soopNext = new SoopStatus(); soopPoll(); soopNext = new SoopStatus { State = LiveState.Live, SessionId = "100" }; soopPoll();
        Check(soopAlerts == 1, "SOOP same session survives UNKNOWN without duplicate alert");
        soopNext = new SoopStatus { State = LiveState.Live, SessionId = "101" }; soopPoll();
        Check(soopAlerts == 2 && soopProfile.ProviderState.LastLiveSessionId == "101", "SOOP distinct BNO alerts and persists session identity");
        var id = BuiltInChannel.BuiltInId;
        var shippedLanguages = new LanguageService("ko");
        Check(shippedLanguages.Packs.Any(pack => pack.Code == "ko" && pack.Name == "한국어") &&
            shippedLanguages.Packs.Any(pack => pack.Code == "en" && pack.Name == "English") &&
            shippedLanguages.Get("General", "Settings") == "설정",
            "shipped Korean and English packs discovered and translated");
        Check(shippedLanguages.Get("Settings", "NotificationOpacity") == "알림창 투명도" &&
            new LanguageService("en").Get("Settings", "NotificationOpacity") == "Notification opacity",
            "notification opacity localized in both shipped languages");
        Check(ChzzkClient.ParseLiveUrl(BuiltInChannel.UrlFor(id)) == id, "CHZZK URL");
        Check(ChzzkClient.ParseLiveUrl("https://evil.example/live/" + id) == null, "CHZZK host");
        Check(!PollingService.ShouldNotify(LiveState.Unknown, LiveState.Live), "unknown is not a live start");
        Check(PollingService.ShouldNotify(LiveState.Offline, LiveState.Live), "offline to live");
        var openedLive = new List<string>();
        var autoOpen = new AutoOpenService(openedLive.Add);
        var autoProfile = new ChannelProfile { Platform = PlatformType.Chzzk,
            LiveUrl = BuiltInChannel.UrlFor(id), AutoOpenLive = true,
            State = LiveState.Live, LiveSessionId = "session-1" };
        autoOpen.OnUpdated(autoProfile, LiveState.Unknown, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 0, "startup Unknown to Live does not auto-open");
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live;
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 1 && openedLive[0] == autoProfile.LiveUrl,
            "Offline to Live auto-opens exactly once");
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Unknown;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live;
        autoOpen.OnUpdated(autoProfile, LiveState.Unknown, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 1, "Live polling and Unknown recovery do not duplicate auto-open");
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live;
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 1, "same live session ID does not open twice after state wobble");
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live; autoProfile.LiveSessionId = "session-2";
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 2, "new live session opens once");
        autoProfile.AutoOpenLive = false;
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live; autoProfile.LiveSessionId = "session-3";
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 2, "AutoOpen OFF suppresses new live opening");
        autoProfile.AutoOpenLive = true;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 2, "AutoOpen OFF to ON during Live does not open immediately");
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live; autoProfile.LiveSessionId = "session-4";
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(openedLive.Count == 3, "next new live after enabling AutoOpen opens once");
        autoProfile.State = LiveState.Offline;
        autoOpen.OnUpdated(autoProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        autoProfile.State = LiveState.Live; autoProfile.LiveSessionId = "session-5";
        autoOpen.OnUpdated(autoProfile, LiveState.Offline, PlatformCapabilities.NewContent);
        Check(openedLive.Count == 3, "provider without live capability never auto-opens");
        var unidentifiedOpens = 0;
        var unidentified = new AutoOpenService(url => unidentifiedOpens++);
        var unidentifiedProfile = new ChannelProfile { Platform = PlatformType.Rplay,
            LiveUrl = "https://rplay.live/live/658ace4494bb71283af7ab4c", AutoOpenLive = true,
            State = LiveState.Offline };
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Unknown, PlatformCapabilities.LiveStatus);
        unidentifiedProfile.State = LiveState.Live;
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        unidentifiedProfile.State = LiveState.Offline;
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        unidentifiedProfile.State = LiveState.Live;
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(unidentifiedOpens == 1, "unidentified session resists one Offline sample wobble");
        unidentifiedProfile.State = LiveState.Offline;
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Live, PlatformCapabilities.LiveStatus);
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        unidentifiedProfile.State = LiveState.Live;
        unidentified.OnUpdated(unidentifiedProfile, LiveState.Offline, PlatformCapabilities.LiveStatus);
        Check(unidentifiedOpens == 2, "unidentified session reopens after stable Offline state");
        var rplayUrl = "https://rplay.live/live/658ace4494bb71283af7ab4c";
        var rplay = RplayClient.CreateProfile(rplayUrl);
        var live = RplayClient.ParseLiveList("[{\"creatorOid\":\"658ace4494bb71283af7ab4c\",\"creatorNickname\":\"Creator\",\"livestreamId\":\"live-99\",\"title\":\"Live\",\"viewerCount\":0,\"streamState\":\"twitch\"}]");
        RplayClient.Apply(rplay, live[rplay.CreatorOid]);
        Check(rplay.Platform == PlatformType.Rplay && rplay.State == LiveState.Live && rplay.ViewerCount == 0 &&
            rplay.LiveSessionId == "live-99", "RPLAY batch data and optional session ID");
        RplayClient.MarkUnknown(rplay);
        Check(rplay.State == LiveState.Unknown, "RPLAY network failure");
        var statusHandler = new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, "{\"code\":200,\"content\":{\"status\":\"CLOSE\"}}"),
            ScriptedHandler.Response(HttpStatusCode.OK, "{\"code\":200,\"content\":{\"status\":\"OPEN\",\"liveId\":\"live-77\",\"liveTitle\":\"Live title\",\"concurrentUserCount\":0}}"),
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "error"));
        using (var statusClient = new ChzzkClient(statusHandler))
        {
            var chzzk = BuiltInChannel.BuiltIn(true);
            statusClient.RefreshAsync(chzzk, CancellationToken.None).GetAwaiter().GetResult();
            Check(chzzk.State == LiveState.Offline, "CHZZK offline response");
            var previous = chzzk.State;
            statusClient.RefreshAsync(chzzk, CancellationToken.None).GetAwaiter().GetResult();
            Check(chzzk.State == LiveState.Live && chzzk.LiveSessionId == "live-77" && chzzk.LiveTitle == "Live title" &&
                chzzk.ViewerCount == 0 && PollingService.ShouldNotify(previous, chzzk.State),
                "CHZZK live title, zero viewers and start transition");
            statusClient.RefreshAsync(chzzk, CancellationToken.None).GetAwaiter().GetResult();
            Check(chzzk.State == LiveState.Unknown && chzzk.ViewerCount == null,
                "CHZZK network failure remains unknown");
        }
        var rplayHandler = new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, "[]"),
            ScriptedHandler.Response(HttpStatusCode.OK,
                "[{\"creatorOid\":\"658ace4494bb71283af7ab4c\",\"title\":\"RPLAY live\",\"viewerCount\":23,\"streamState\":\"twitch\"}]"),
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "error"));
        using (var rplayPollClient = new RplayClient(rplayHandler))
        {
            var provider = new RplayProvider(rplayPollClient);
            var channel = RplayClient.CreateProfile(rplayUrl);
            var starts = 0;
            Action<ChannelProfile, LiveState, ContentEntry> onUpdate = (p, prior, entry) =>
            { if (PollingService.ShouldNotify(prior, p.State)) starts++; };
            provider.PollAsync(new[] { channel }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(channel.State == LiveState.Offline, "RPLAY absent from public live list is offline");
            provider.PollAsync(new[] { channel }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(channel.Platform == PlatformType.Rplay && channel.State == LiveState.Live &&
                channel.LiveTitle == "RPLAY live" && channel.ViewerCount == 23 && starts == 1,
                "RPLAY creator mapping, twitch stream state and live notification");
            provider.PollAsync(new[] { channel }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(channel.State == LiveState.Unknown && starts == 1, "RPLAY list failure is unknown");
        }
        Check(YouTubeClient.IsChannelUrl("https://youtube.com/@hiki") &&
            YouTubeClient.IsChannelUrl("https://www.youtube.com/channel/UCabc123"), "YouTube channel URLs");
        Check(!YouTubeClient.IsChannelUrl("https://youtube.com/watch?v=abc") &&
            !YouTubeClient.IsChannelUrl("https://youtu.be/abc"), "reject video URLs");
        Check(YouTubeClient.DirectChannelId("https://youtube.com/channel/UCabc123") == "UCabc123", "direct channel ID");
        using (var chzzkClient = new ChzzkClient())
        using (var rplayClient = new RplayClient())
        using (var youtubeClient = new YouTubeClient())
        {
            var registry = TestProviders.Create(chzzkClient, rplayClient, youtubeClient);
            Check(registry.ForUrl("https://youtube.com/@hiki").Platform == PlatformType.YouTube &&
                registry.ForUrl(rplayUrl).Platform == PlatformType.Rplay, "provider URL detection");
            Check(registry.ForPlatform(PlatformType.YouTube).PollInterval.TotalSeconds == 180 &&
                registry.ForPlatform(PlatformType.Chzzk).PollInterval.TotalSeconds == 30 &&
                registry.ForPlatform(PlatformType.Rplay).PollInterval.TotalSeconds == 30, "provider poll intervals");
            Check((registry.ForPlatform(PlatformType.YouTube).Capabilities & PlatformCapabilities.LiveStatus) == 0,
                "YouTube has no live status capability");
            foreach (var platform in new[] { PlatformType.Chzzk, PlatformType.Rplay })
            {
                var liveProfile = new ChannelProfile { Platform = platform, State = LiveState.Live,
                    NotificationsEnabled = true };
                var provider = registry.ForPlatform(platform);
                Check(NotificationReplay.ForChange(provider, liveProfile, false, true)?.Type == NotificationEventType.LiveStarted,
                    platform + " live OFF to ON replay");
                Check(NotificationReplay.ForChange(provider, liveProfile, true, true) == null &&
                    NotificationReplay.ForChange(provider, liveProfile, true, false) == null,
                    platform + " unchanged and OFF do not replay");
                liveProfile.State = LiveState.Unknown;
                Check(NotificationReplay.ForChange(provider, liveProfile, false, true) == null,
                    platform + " unknown does not replay");
                liveProfile.State = LiveState.Offline;
                Check(NotificationReplay.ForChange(provider, liveProfile, false, true) == null,
                    platform + " offline does not replay");
            }
            var youtubeReplay = new ChannelProfile { Platform = PlatformType.YouTube,
                NotificationsEnabled = true, ProviderState = new ProviderState {
                    LastSeenContentId = "TzJAR1xLNl4", LatestContentTitle = "Short title",
                    LatestContentUrl = "https://www.youtube.com/shorts/TzJAR1xLNl4",
                    RecentContentIds = new List<string> { "TzJAR1xLNl4" } } };
            var youtubeProvider = registry.ForPlatform(PlatformType.YouTube);
            var replay = NotificationReplay.ForChange(youtubeProvider, youtubeReplay, false, true);
            Check(replay?.Type == NotificationEventType.NewContent && replay.Content.ContentId == "TzJAR1xLNl4" &&
                youtubeReplay.ProviderState.LastSeenContentId == "TzJAR1xLNl4" &&
                youtubeReplay.ProviderState.RecentContentIds.SequenceEqual(new[] { "TzJAR1xLNl4" }),
                "YouTube OFF to ON replays latest without mutating detection state");
            Check(NotificationReplay.ForChange(youtubeProvider, youtubeReplay, true, true) == null &&
                NotificationReplay.ForChange(youtubeProvider, youtubeReplay, true, false) == null,
                "YouTube unchanged and OFF do not replay");
            youtubeReplay.ProviderState.BaselinePending = true;
            Check(NotificationReplay.ForChange(youtubeProvider, youtubeReplay, false, true) == null,
                "YouTube pending baseline does not replay");
            youtubeReplay.ProviderState.BaselinePending = false;
            youtubeReplay.ProviderState.LatestContentUrl = "";
            Check(NotificationReplay.ForChange(youtubeProvider, youtubeReplay, false, true) == null,
                "YouTube incomplete latest content does not replay");
            youtubeReplay.ProviderState.LatestContentUrl = "https://www.youtube.com/shorts/WRONG000000";
            Check(NotificationReplay.ForChange(youtubeProvider, youtubeReplay, false, true) == null,
                "YouTube mismatched latest URL does not replay");
            using (var edit = new ProfileEditForm(new LanguageService("en"), registry))
            {
                var url = (TextBox)edit.Controls["newChannelUrl"];
                var menu = ((Button)edit.Controls["addChannelButton"]).ContextMenuStrip;
                Check(url.Text == "" && menu.Items.Count == 3 &&
                    menu.Items[0].Text == "CHZZK" && menu.Items[1].Text == "YouTube" &&
                    menu.Items[2].Text == "RPLAY", "channel template menu and empty initial URL");
                menu.Items[0].PerformClick();
                Check(url.Text == "https://chzzk.naver.com/live/" && url.SelectionStart == url.TextLength,
                    "CHZZK template and caret");
                menu.Items[1].PerformClick();
                Check(url.Text == "https://www.youtube.com/@" && url.SelectionStart == url.TextLength,
                    "YouTube template replaces template");
                menu.Items[2].PerformClick();
                Check(url.Text == "https://rplay.live/live/" && url.SelectionStart == url.TextLength,
                    "RPLAY template replaces template");
                url.Text = "https://www.youtube.com/@LKKVMD";
                menu.Items[0].PerformClick();
                Check(url.Text == "https://www.youtube.com/@LKKVMD", "typed URL survives template selection");
                ((TextBox)edit.Controls["profileName"]).Text = "Creator";
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(edit.Controls["saveProfileButton"], new object[] { EventArgs.Empty });
                Check(edit.Channels?.Count == 1 && edit.Channels[0].Platform == PlatformType.YouTube,
                    "saved channel platform follows URL instead of menu choice");
            }
            using (var builtIn = new ProfileEditForm(new LanguageService("ko"), registry,
                StreamerProfile.BuiltIn(new BuiltInHikiState())))
            {
                Check(!builtIn.Controls["newChannelUrl"].Enabled && !builtIn.Controls["addChannelButton"].Enabled,
                    "built-in channel add remains disabled");
                var autoChoices = builtIn.Controls.Find("autoOpenLive", true).OfType<CheckBox>().ToArray();
                Check(autoChoices.Length == 1 && autoChoices[0].Text == "방송 시작 시 자동으로 열기" &&
                    !autoChoices[0].Checked, "built-in live channel has localized AutoOpen OFF option");
            }
            var editable = new StreamerProfile { DisplayName = "Creator" };
            editable.Channels.Add(new ChannelProfile { Platform = PlatformType.Chzzk,
                ChannelId = BuiltInChannel.BuiltInId, LiveUrl = BuiltInChannel.UrlFor(BuiltInChannel.BuiltInId) });
            using (var editLive = new ProfileEditForm(new LanguageService("en"), registry, editable))
            {
                var autoChoice = editLive.Controls.Find("autoOpenLive", true).OfType<CheckBox>().Single();
                Check(autoChoice.Text == "Open stream automatically when live" && !autoChoice.Checked,
                    "user live channel has English AutoOpen OFF option");
                autoChoice.Checked = true;
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editLive.Controls["saveProfileButton"], new object[] { EventArgs.Empty });
                Check(editLive.Channels?.Count == 1 && editLive.Channels[0].AutoOpenLive,
                    "profile edit saves per-channel AutoOpen choice");
            }
        }
        Check(YouTubeClient.ChannelIdFromHtml("<link href='https://www.youtube.com/feeds/videos.xml?channel_id=UCabc123' type='application/rss+xml' rel='alternate'>") == "UCabc123", "RSS alternate discovery");
        Check(YouTubeClient.ChannelIdFromHtml("<link rel='canonical' href='https://www.youtube.com/channel/UCabc123'>") == "UCabc123",
            "canonical fallback");
        Check(YouTubeClient.ChannelIdFromHtml("<meta itemprop='channelId' content='UCabc123'>") == "UCabc123",
            "channelId meta fallback");
        Check(YouTubeClient.ChannelIdFromHtml("<script>{\"channelId\":\"UCabc123\"}</script>") == "UCabc123",
            "channelId property fallback");
        Check(YouTubeClient.ChannelIdFromHtml("<script>{\"externalId\":\"UCabc123\"}</script>") == "UCabc123",
            "explicit UC fallback");
        Check(YouTubeClient.ChannelIdFromHtml("<script>{\"externalId\":\"UCabc123\",\"browseId\":\"UCother\"}</script>") == null,
            "ambiguous UC IDs rejected");
        var xml = "<feed xmlns='http://www.w3.org/2005/Atom' xmlns:yt='http://www.youtube.com/xml/schemas/2015'><yt:channelId>abc123</yt:channelId><title>Creator</title>" +
            "<entry><yt:videoId>new</yt:videoId><yt:channelId>UCabc123</yt:channelId><title>New title</title><link rel='alternate' href='https://www.youtube.com/watch?v=new'/><published>2026-01-02T00:00:00Z</published><updated>2026-01-03T00:00:00Z</updated></entry>" +
            "<entry><yt:videoId>old</yt:videoId><yt:channelId>UCabc123</yt:channelId><title>Old title</title><link rel='alternate' href='https://www.youtube.com/watch?v=old'/><published>2026-01-01T00:00:00Z</published></entry></feed>";
        var feed = YouTubeClient.ParseFeed(xml, "UCabc123");
        Check(feed.ChannelId == "UCabc123" && feed.Entries[0].ContentId == "new" &&
            feed.Entries[0].Title == "New title", "Atom parsing with short root channel ID");
        Check(YouTubeClient.ParseFeed(xml.Replace("<yt:channelId>abc123</yt:channelId>",
            "<yt:channelId>UCabc123</yt:channelId>"), "UCabc123").ChannelId == "UCabc123",
            "Atom parsing with full root channel ID");
        Check(YouTubeProvider.NewContent(feed, null) == null && YouTubeProvider.NewContent(feed, "missing") == null,
            "baseline or expired entry does not alert");
        Check(YouTubeProvider.NewContent(feed, "old")?.ContentId == "new" &&
            YouTubeProvider.NewContent(feed, "new") == null, "new videoId only");
        var htmlVideos = HtmlTab("videos", "d9LiGMMNz50");
        var htmlShorts = HtmlTab("shorts", "TzJAR1xLNl4", "eGDGvf99JR8");
        Check(YouTubeClient.ParseHtmlTab(htmlVideos, "UCabc123", "videos")[0].ContentId == "d9LiGMMNz50",
            "HTML videos renderer ID");
        Check(YouTubeClient.ParseHtmlTab(htmlShorts, "UCabc123", "shorts")[0].Title == "Title TzJAR1xLNl4",
            "HTML Shorts renderer title and escaped initial data");
        var videosHandler = new ScriptedHandler(ScriptedHandler.Response(HttpStatusCode.OK, htmlVideos));
        using (var videosClient = new YouTubeClient(videosHandler))
        {
            var videos = videosClient.GetVideosAsync("https://www.youtube.com/@hiki", "UCabc123",
                CancellationToken.None).GetAwaiter().GetResult();
            Check(videos.Count == 1 && videos[0].ContentId == "d9LiGMMNz50" &&
                videosHandler.Requests.Count == 1 && videosHandler.Requests[0].AbsolutePath.EndsWith("/videos"),
                "Easter Egg reuses video parser and requests only videos tab");
        }
        var rejectedHtml = false;
        try { YouTubeClient.ParseHtmlTab(htmlShorts, "UCwrong", "shorts"); }
        catch (InvalidOperationException) { rejectedHtml = true; }
        Check(rejectedHtml, "HTML channel identity mismatch rejected");
        var oldOnlyFeed = xml.Replace("<entry><yt:videoId>new</yt:videoId><yt:channelId>UCabc123</yt:channelId><title>New title</title><link rel='alternate' href='https://www.youtube.com/watch?v=new'/><published>2026-01-02T00:00:00Z</published><updated>2026-01-03T00:00:00Z</updated></entry>", "")
            .Replace("<yt:videoId>old</yt:videoId>", "<yt:videoId>eGDGvf99JR8</yt:videoId>");
        var htmlScript = new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlVideos),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlShorts),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlVideos),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlShorts),
            ScriptedHandler.Response(HttpStatusCode.OK, oldOnlyFeed),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, HtmlTab("videos", "AAAAAAAAAAA", "d9LiGMMNz50")),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlShorts),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK,
                HtmlTab("videos", "AAAAAAAAAAA", "d9LiGMMNz50").Replace("Title AAAAAAAAAAA", "Retitled video")),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlShorts),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, "<broken>"),
            ScriptedHandler.Response(HttpStatusCode.OK, "<broken>"));
        using (var htmlClient = new YouTubeClient(htmlScript))
        {
            var provider = new YouTubeProvider(htmlClient);
            var profile = new ChannelProfile { Platform = PlatformType.YouTube, ChannelId = "UCabc123",
                LiveUrl = "https://www.youtube.com/@hiki", ProviderState = new ProviderState {
                    LastSeenContentId = "eGDGvf99JR8", LatestContentTitle = "Old Shorts" } };
            var alerts = new List<string>();
            Action<ChannelProfile, LiveState, ContentEntry> onUpdate = (p, previous, entry) =>
            { if (entry != null) alerts.Add(entry.ContentId); };
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.SequenceEqual(new[] { "TzJAR1xLNl4" }) &&
                profile.ProviderState.LastSeenContentId == "TzJAR1xLNl4",
                "RSS 404 falls back to Shorts and detects new ID");
            profile.ProviderState = new JavaScriptSerializer().Deserialize<ProviderState>(
                new JavaScriptSerializer().Serialize(profile.ProviderState));
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.Count == 1, "persisted HTML history prevents duplicate notification");
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.Count == 1 && profile.ProviderState.LastSeenContentId == "TzJAR1xLNl4" &&
                htmlScript.Requests.Count == 7, "RSS recovery does not replay stale ID or request HTML");
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.SequenceEqual(new[] { "TzJAR1xLNl4", "AAAAAAAAAAA" }) &&
                profile.ProviderState.LatestVideosId == "AAAAAAAAAAA",
                "new video after Shorts alerts once");
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.Count == 2 && profile.ProviderState.LatestContentTitle == "Retitled video",
                "HTML metadata edit updates title without new alert");
            provider.PollAsync(new[] { profile }, onUpdate, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts.Count == 2 && profile.ProviderState.LastSeenContentId == "AAAAAAAAAAA" &&
                profile.ProviderState.LatestContentTitle == "Retitled video",
                "malformed HTML preserves prior baseline and title");
        }
        using (var initialHtmlClient = new YouTubeClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.NotFound, "RSS unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlVideos),
            ScriptedHandler.Response(HttpStatusCode.OK, htmlShorts))))
        {
            var profile = new ChannelProfile { Platform = PlatformType.YouTube, ChannelId = "UCabc123",
                LiveUrl = "https://www.youtube.com/@hiki", ProviderState = new ProviderState { BaselinePending = true } };
            var alerts = 0;
            new YouTubeProvider(initialHtmlClient).PollAsync(new[] { profile },
                (p, previous, entry) => { if (entry != null) alerts++; }, CancellationToken.None).GetAwaiter().GetResult();
            Check(alerts == 0 && !profile.ProviderState.BaselinePending &&
                profile.ProviderState.LatestVideosId == "d9LiGMMNz50" &&
                profile.ProviderState.LatestShortsId == "TzJAR1xLNl4",
                "first HTML snapshot establishes two-tab baseline without alert");
        }
        var firstFeed = xml.Replace("<entry><yt:videoId>new</yt:videoId><yt:channelId>UCabc123</yt:channelId><title>New title</title><link rel='alternate' href='https://www.youtube.com/watch?v=new'/><published>2026-01-02T00:00:00Z</published><updated>2026-01-03T00:00:00Z</updated></entry>", "");
        var changedTitle = xml.Replace("New title", "Edited title");
        var shortsFeed = xml.Replace("</feed>",
            "<entry><yt:videoId>shorts</yt:videoId><yt:channelId>UCabc123</yt:channelId><title>Shorts title</title><link rel='alternate' href='https://www.youtube.com/shorts/shorts'/><published>2026-01-04T00:00:00Z</published></entry></feed>");
        var scripted = new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, "<link rel='canonical' href='https://www.youtube.com/channel/UCabc123'>"),
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "unavailable"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no videos page"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no shorts page"),
            ScriptedHandler.Response(HttpStatusCode.OK, firstFeed),
            ScriptedHandler.Response(HttpStatusCode.OK, xml),
            ScriptedHandler.Response(HttpStatusCode.OK, "<broken"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no videos page"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no shorts page"),
            ScriptedHandler.Response(HttpStatusCode.OK, xml),
            ScriptedHandler.Response(HttpStatusCode.OK, changedTitle),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "unavailable"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no videos page"),
            ScriptedHandler.Response(HttpStatusCode.NotFound, "no shorts page"),
            ScriptedHandler.Response(HttpStatusCode.OK, shortsFeed));
        using (var scriptedClient = new YouTubeClient(scripted))
        {
            var provider = new YouTubeProvider(scriptedClient);
            var profile = provider.CreateAsync("https://www.youtube.com/@LKKVMD", CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ChannelId == "UCabc123" && profile.ProviderState.BaselinePending &&
                profile.ProviderState.LastSeenContentId == null, "feed 503 still registers resolved channel");
            var notices = 0;
            Action<ChannelProfile, LiveState, ContentEntry> updated = (p, oldState, entry) => { if (entry != null) notices++; };
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(!profile.ProviderState.BaselinePending && profile.ProviderState.LastSeenContentId == "old" && notices == 0,
                "first recovered feed sets baseline without alert");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LastSeenContentId == "new" && notices == 1, "later new video alerts once");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LastSeenContentId == "new" && notices == 1, "parse error preserves baseline");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LastSeenContentId == "new" && notices == 1, "same videoId never duplicates alert");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LatestContentTitle == "Edited title" && notices == 1,
                "metadata edit refreshes title without alert");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LastSeenContentId == "new" &&
                profile.ProviderState.LatestContentTitle == "Edited title" && !profile.ProviderState.BaselinePending,
                "RSS 404 preserves established baseline and title");
            provider.PollAsync(new[] { profile }, updated, CancellationToken.None).GetAwaiter().GetResult();
            Check(profile.ProviderState.LastSeenContentId == "shorts" &&
                profile.ProviderState.LatestContentUrl.EndsWith("/shorts/shorts") && notices == 2,
                "recovered RSS detects Shorts by videoId");
            Check(scripted.Requests.Count == 15 && scripted.Requests.Count(u => u.AbsolutePath == "/@LKKVMD") == 1,
                "resolved handle page requested only once");
        }
        using (var missingClient = new YouTubeClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.NotFound, "missing"))))
        {
            try { missingClient.ResolveChannelIdAsync("https://www.youtube.com/@missing", CancellationToken.None).GetAwaiter().GetResult();
                Check(false, "channel 404 should fail"); }
            catch (YouTubeResolveException ex) { Check(ex.Failure == YouTubeResolveFailure.ChannelNotFound, "channel 404 is not-found error"); }
        }
        using (var networkClient = new YouTubeClient(new FaultHandler(new HttpRequestException("TLS failure"))))
        {
            try { networkClient.ResolveChannelIdAsync("https://www.youtube.com/@LKKVMD", CancellationToken.None).GetAwaiter().GetResult();
                Check(false, "TLS failure should fail"); }
            catch (YouTubeResolveException ex) { Check(ex.Failure == YouTubeResolveFailure.Network, "TLS failure is network error"); }
        }
        using (var serverErrorClient = new YouTubeClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "unavailable"))))
        {
            try { serverErrorClient.ResolveChannelIdAsync("https://www.youtube.com/@LKKVMD", CancellationToken.None).GetAwaiter().GetResult();
                Check(false, "page 503 should fail"); }
            catch (YouTubeResolveException ex) { Check(ex.Failure == YouTubeResolveFailure.Network, "page 503 is network error"); }
        }
        using (var feed404Client = new YouTubeClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.NotFound, "missing"))))
        {
            var pending = new YouTubeProvider(feed404Client).CreateAsync(
                "https://www.youtube.com/channel/UCabc123", CancellationToken.None).GetAwaiter().GetResult();
            Check(pending.ChannelId == "UCabc123" && pending.ProviderState.BaselinePending,
                "feed 404 keeps resolved channel pending");
        }
        using (var builtInClient = new YouTubeClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, "<meta itemprop='channelId' content='UCabc123'>"),
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "unavailable"))))
        {
            var builtIn = new ChannelProfile { Platform = PlatformType.YouTube,
                LiveUrl = "https://www.youtube.com/@LKKVMD", ProviderState = new ProviderState { BaselinePending = true } };
            var savedIds = new System.Collections.Generic.List<string>();
            new YouTubeProvider(builtInClient).PollAsync(new[] { builtIn },
                (p, oldState, entry) => savedIds.Add(p.ChannelId), CancellationToken.None).GetAwaiter().GetResult();
            Check(savedIds.Count == 1 && savedIds[0] == "UCabc123" &&
                builtIn.ProviderState.BaselinePending, "poll persists resolved ID before failed feed");
        }
        using (var timeoutClient = new YouTubeClient(new FaultHandler(new TaskCanceledException("timeout"))))
        {
            var pending = new YouTubeProvider(timeoutClient).CreateAsync(
                "https://www.youtube.com/channel/UCabc123", CancellationToken.None).GetAwaiter().GetResult();
            Check(pending.ChannelId == "UCabc123" && pending.ProviderState.BaselinePending,
                "feed timeout keeps resolved channel pending");
        }
        Check(LogoResources.PlatformLogo(PlatformType.YouTube).Width == 2172, "YOUTUBE original embedded");
        Check(LogoResources.PlatformLogo(PlatformType.Rplay) != LogoResources.PlatformLogo(PlatformType.Twitch), "distinct logos");
        var notifier = new NotificationService();
        var previousLogo = notifier.ChooseTestPlatform();
        for (var i = 0; i < 24; i++)
        {
            var currentLogo = notifier.ChooseTestPlatform();
            Check(currentLogo != previousLogo, "test logos do not repeat consecutively");
            previousLogo = currentLogo;
        }
        using (var alert = new NotificationForm(new ChannelProfile { Platform = PlatformType.YouTube, Name = "Creator" }, 0,
            NotificationEventType.NewContent, "ko", "Video title", "https://youtube.com/watch?v=abc"))
        {
            Check(Math.Abs(alert.Opacity - 1.0) < 0.001, "notification default opacity 100 percent");
            var logo = alert.Controls.OfType<System.Windows.Forms.PictureBox>().Single();
            Check(logo.SizeMode == System.Windows.Forms.PictureBoxSizeMode.Zoom &&
                ReferenceEquals(logo.Image, LogoResources.PlatformLogo(PlatformType.YouTube)), "YouTube alert logo zoom");
            var button = alert.Controls.OfType<System.Windows.Forms.Button>().Single();
            Check(button.Text == "영상 보러가기" && alert.ClientSize.Height - button.Bottom >= 12, "content button and margins");
        }
        foreach (var platform in new[] { PlatformType.Chzzk, PlatformType.Rplay, PlatformType.YouTube })
        {
            using (var alert = new NotificationForm(new ChannelProfile { Platform = platform, Name = "Creator" }, 0,
                notificationOpacity: 85))
                Check(Math.Abs(alert.Opacity - 0.85) < 0.001, platform + " alert opacity 85 percent");
        }
        using (var alert = new NotificationForm(new ChannelProfile { Platform = PlatformType.YouTube }, 0,
            notificationOpacity: 50))
            Check(Math.Abs(alert.Opacity - 0.50) < 0.001, "notification opacity 50 percent");
        var opacitySettings = new AppSettings { NotificationOpacity = 85,
            SoundMode = NotificationSoundMode.Silent };
        foreach (var platform in new[] { PlatformType.Chzzk, PlatformType.Rplay })
        {
            notifier.Show(new ChannelProfile { Platform = platform, Name = "Creator",
                NotificationsEnabled = true }, opacitySettings);
            var shown = Application.OpenForms.OfType<NotificationForm>().LastOrDefault();
            Check(shown != null && Math.Abs(shown.Opacity - 0.85) < 0.001,
                platform + " actual alert uses configured opacity");
            shown.Close();
        }
        notifier.ShowContent(new ChannelProfile { Platform = PlatformType.YouTube, Name = "Creator",
            NotificationsEnabled = true }, opacitySettings,
            new ContentEntry { ContentId = "TzJAR1xLNl4", Title = "Short title",
                Url = "https://www.youtube.com/shorts/TzJAR1xLNl4" });
        var youtubeShown = Application.OpenForms.OfType<NotificationForm>().LastOrDefault();
        Check(youtubeShown != null && Math.Abs(youtubeShown.Opacity - 0.85) < 0.001,
            "YouTube actual content alert uses configured opacity");
        youtubeShown.Close();
        var root = Path.Combine(Path.GetTempPath(), "hiki-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var freshRoot = Path.Combine(root, "fresh");
            Directory.CreateDirectory(freshRoot);
            var freshPath = Path.Combine(freshRoot, "settings.json");
            var freshService = new SettingsService(freshPath);
            var fresh = freshService.Load();
            Check(freshService.CanSave && File.Exists(freshPath) &&
                Directory.Exists(Path.Combine(freshRoot, "profiles")),
                "clean first run creates settings JSON and profiles directory");
            Check(fresh.Streamers.Count == 1 && fresh.Streamers[0].IsBuiltIn &&
                fresh.Streamers[0].Channels.Count == 2 && fresh.NotificationOpacity == 100 &&
                Directory.GetFiles(Path.Combine(freshRoot, "profiles"), "*.json").Length == 0,
                "clean first run starts with built-in HIKI only");
            Check(new SettingsService(freshPath).Load().Streamers.Count == 1,
                "created default settings load on restart");
            Directory.Delete(Path.Combine(freshRoot, "profiles"));
            Check(new SettingsService(freshPath).Load().Streamers.Count == 1 &&
                Directory.Exists(Path.Combine(freshRoot, "profiles")),
                "missing profiles directory is recreated for existing settings");
            File.WriteAllText(freshPath, "", Encoding.UTF8);
            var recoveredEmpty = new SettingsService(freshPath).Load();
            Check(recoveredEmpty.Streamers.Count == 1 && File.Exists(freshPath + ".broken") &&
                File.ReadAllText(freshPath + ".broken").Length == 0 &&
                new SettingsService(freshPath).Load().Streamers.Count == 1,
                "empty settings are preserved and replaced with defaults");
            File.WriteAllText(freshPath, "{broken", Encoding.UTF8);
            var recoveredBrokenService = new SettingsService(freshPath);
            var recoveredBroken = recoveredBrokenService.Load();
            Check(recoveredBrokenService.CanSave && recoveredBroken.Streamers.Count == 1 &&
                File.Exists(freshPath + ".broken") &&
                Directory.GetFiles(freshRoot).Count(file => Path.GetFileName(file).StartsWith(
                    "settings.json.broken.", StringComparison.Ordinal)) == 1 &&
                new SettingsService(freshPath).Load().Streamers.Count == 1,
                "corrupt settings preserve a second backup and app recovers");
            File.WriteAllText(freshPath, "{\"SettingsSchemaVersion\":999}", Encoding.UTF8);
            var futureService = new SettingsService(freshPath);
            var future = futureService.Load();
            Check(!futureService.CanSave && future.Streamers.Count == 1 &&
                File.ReadAllText(freshPath).Contains("999"),
                "newer schema is preserved without overwrite");
            var langs = Path.Combine(root, "lang"); Directory.CreateDirectory(langs);
            File.WriteAllText(Path.Combine(langs, "en.ini"), "[Language]\nName=English\nCode=en\n[General]\nSettings=Settings\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(langs, "ko.ini"), "[Language]\nName=한국어\nCode=ko\n[General]\nSettings=설정\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(langs, "ja.ini"), "[Language]\nName=日本語\nCode=ja\n[General]\nSettings=設定\n", Encoding.UTF8);
            var language = new LanguageService("ja", langs);
            Check(language.Packs.Count == 3 && language.Language == "ja" && language.Get("General", "Settings") == "設定",
                "dynamic Japanese language pack discovery");
            Check(new LanguageService("ja", langs).Language == "ja", "Japanese selection restored after restart");
            using (var languageForm = new SettingsForm(new AppSettings { Language = "ja", SoundMode = NotificationSoundMode.Silent }, language,
                () => { }, new NotificationService()))
            {
                var choice = (ComboBox)languageForm.Controls["languageChoice"];
                Check(choice.Items.Count == 3 && ((LanguagePack)choice.SelectedItem).Code == "ja" &&
                    choice.Items.Cast<LanguagePack>().Any(pack => pack.Name == "日本語"),
                    "settings language menu uses discovered metadata");
                var slider = (TrackBar)languageForm.Controls["notificationOpacity"];
                var value = (Label)languageForm.Controls["notificationOpacityValue"];
                Check(slider.Minimum == 50 && slider.Maximum == 100 && slider.Value == 100 && value.Text == "100%",
                    "settings opacity range and default display");
                slider.Value = 85;
                Check(value.Text == "85%", "settings opacity percentage updates immediately");
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(languageForm.Controls["testAlertButton"], new object[] { EventArgs.Empty });
                var preview = Application.OpenForms.OfType<NotificationForm>().LastOrDefault();
                Check(preview != null && Math.Abs(preview.Opacity - 0.85) < 0.001,
                    "unsaved settings opacity applies to test alert");
                preview.Close();
            }
            File.AppendAllText(Path.Combine(langs, "en.ini"), "Fallback=English fallback\n", Encoding.UTF8);
            language.Rescan();
            Check(language.Get("General", "Fallback") == "English fallback", "missing key falls back to English");
            File.WriteAllText(Path.Combine(langs, "ja.ini"), "[Language]\nName=日本語 (Japanese)\nCode=ja\n[General]\nSettings=設定\n", Encoding.UTF8);
            language.Rescan();
            Check(language.Language == "ja" && language.Packs.Any(pack => pack.Code == "ja" && pack.Name == "日本語 (Japanese)"),
                "language display name changes without changing saved code");
            File.WriteAllText(Path.Combine(langs, "duplicate.ini"), "[Language]\nName=Fake\nCode=ja\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(langs, "invalid.ini"), "[Language]\nCode=xx\n", Encoding.UTF8);
            language.Rescan();
            Check(language.Packs.Count == 3 && language.Language == "ja" && language.Get("General", "Settings") == "設定",
                "duplicate and invalid packs ignored deterministically");
            File.Delete(Path.Combine(langs, "duplicate.ini"));
            File.Delete(Path.Combine(langs, "ja.ini")); language.Rescan();
            Check(language.Language == "en" && language.Get("General", "Settings") == "Settings",
                "removed selected pack falls back to English");
            var settingsPath = Path.Combine(root, "settings.json");
            var legacy = new { BuiltInNotificationsEnabled = false, NotificationsEnabled = true, Language = "ko",
                Profiles = new object[] {
                    new { ChannelId = id, IsBuiltIn = true, NotificationsEnabled = false },
                    new { ChannelId = "0123456789abcdef0123456789abcdef", Platform = 0,
                        LiveUrl = BuiltInChannel.UrlFor("0123456789abcdef0123456789abcdef"), Name = "Creator", NotificationsEnabled = true },
                    new { ChannelId = rplay.CreatorOid, CreatorOid = rplay.CreatorOid, Platform = 1,
                        LiveUrl = rplayUrl, Name = "RPLAY Creator", NotificationsEnabled = false } } };
            File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(legacy), Encoding.UTF8);
            var service = new SettingsService(settingsPath);
            var loaded = service.Load();
            Check(loaded.NotificationOpacity == 100, "legacy settings without opacity default to 100");
            Check(loaded.Streamers.SelectMany(streamer => streamer.Channels).All(channel => !channel.AutoOpenLive),
                "legacy channels without AutoOpenLive default to OFF");
            Check(loaded.Streamers.Count == 3 && loaded.Profiles.Count == 4, "legacy migration");
            Check(loaded.Streamers[0].Memo == "귀염냥이" && loaded.Streamers[0].DisplayName == "Hikimori Neko",
                "built-in default memo and fixed name");
            Check(loaded.Streamers[0].IsBuiltIn && loaded.Streamers[0].Channels.Count == 2 &&
                !loaded.Streamers[0].Channels[0].NotificationsEnabled, "built-in state migrated");
            Check(File.Exists(settingsPath + ".migrated.bak"), "legacy backup");
            Check(Directory.GetFiles(Path.Combine(root, "profiles"), "*.json").Length == 2, "one JSON per user streamer");
            var again = service.Load();
            again.NotificationOpacity = 85; service.Save(again);
            Check(service.Load().NotificationOpacity == 85 && File.ReadAllText(settingsPath).Contains("\"NotificationOpacity\":85"),
                "notification opacity persists as integer percentage");
            var settingsJson = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsPath));
            settingsJson["NotificationOpacity"] = 0;
            File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(settingsJson), Encoding.UTF8);
            Check(service.Load().NotificationOpacity == 50, "invalid opacity zero clamps to 50");
            settingsJson["NotificationOpacity"] = 150;
            File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(settingsJson), Encoding.UTF8);
            Check(service.Load().NotificationOpacity == 100, "opacity above 100 clamps to 100");
            service.Save(again);
            again.Language = "ja"; service.Save(again);
            Check(service.Load().Language == "ja", "selected language code persists without ko/en clamping");
            Check(again.Streamers.Count == 3 && !again.Streamers[2].Channels[0].NotificationsEnabled &&
                again.Streamers[2].Channels[0].CreatorOid == rplay.CreatorOid,
                "no duplicate migration, RPLAY identity and alert retained");
            again.Streamers[0].Memo = "";
            again.Streamers[0].Channels[0].AutoOpenLive = true;
            again.Streamers[1].Channels[0].AutoOpenLive = true;
            again.Streamers[0].Channels[1].ChannelId = "UCabc123";
            again.Streamers[0].Channels[1].NotificationsEnabled = false;
            again.Streamers[0].Channels[1].ProviderState.LastSeenContentId = "new";
            again.Streamers[0].Channels[1].ProviderState.BaselinePending = true;
            again.Streamers[0].Channels[1].ProviderState.LatestShortsId = "new";
            again.Streamers[0].Channels[1].ProviderState.RecentContentIds.Add("old");
            again.Streamers[1].Memo = "Talk";
            var second = again.Streamers[1];
            var secondId = second.Id;
            second.DisplayName = "Renamed";
            second.Channels.Add(new ChannelProfile { StreamerProfileId = secondId, Platform = PlatformType.YouTube,
                ChannelId = "UCabc123", LiveUrl = "https://youtube.com/channel/UCabc123",
                ProviderState = new ProviderState { LastSeenContentId = "new", BaselinePending = true,
                    LatestVideosId = "new", RecentContentIds = new List<string> { "old", "new" } } });
            service.Save(again);
            var roundtrip = service.Load();
            Check(roundtrip.Streamers[0].Channels[0].AutoOpenLive && roundtrip.Streamers[1].Channels[0].AutoOpenLive,
                "built-in and user channel AutoOpenLive persist across restart");
            Check(roundtrip.Streamers[0].Memo == "" && roundtrip.Streamers[1].Memo == "Talk" &&
                roundtrip.Streamers[1].DisplayName == "Renamed", "memo and name persistence");
            Check(roundtrip.Streamers[0].Channels[1].ChannelId == "UCabc123" &&
                roundtrip.Streamers[0].Channels[1].ProviderState.LastSeenContentId == "new" &&
                !roundtrip.Streamers[0].Channels[1].ProviderState.BaselinePending &&
                !roundtrip.Streamers[0].Channels[1].NotificationsEnabled,
                "built-in YouTube state persists and established baseline is not pending");
            Check(roundtrip.Streamers[0].Channels[1].ProviderState.LatestShortsId == "new" &&
                roundtrip.Streamers[0].Channels[1].ProviderState.RecentContentIds.Contains("old"),
                "built-in HTML fallback history persists");
            Check(roundtrip.Streamers[1].Channels.Count == 2 &&
                roundtrip.Streamers[1].Channels[1].ProviderState.LastSeenContentId == "new" &&
                !roundtrip.Streamers[1].Channels[1].ProviderState.BaselinePending,
                "multiple channels normalize established baseline");
            Check(roundtrip.Streamers[1].Channels[1].ProviderState.LatestVideosId == "new" &&
                roundtrip.Streamers[1].Channels[1].ProviderState.RecentContentIds.SequenceEqual(new[] { "old", "new" }),
                "user channel HTML fallback history persists");
            Check(File.Exists(Path.Combine(root, "profiles", secondId + ".json")), "stable GUID filename");
            var corrupt = Path.Combine(root, "profiles", Guid.NewGuid().ToString("D") + ".json");
            File.WriteAllText(corrupt, "{broken");
            var partial = service.Load();
            Check(partial.Streamers.Count == 3 && service.LoadErrors.Count == 1 && File.Exists(corrupt),
                "corrupt JSON isolated and retained");
            service.Save(partial);
            Check(File.Exists(corrupt), "save leaves corrupt file untouched");
        }
        finally { Directory.Delete(root, true); }
        TwitchChecks.Run(Check);
        CimeChecks.Run(Check);
        Console.WriteLine("PASS " + checks);
    }

    private static string HtmlTab(string tab, params string[] ids)
    {
        var items = ids.Select(id => tab == "videos" ?
            "{\"richItemRenderer\":{\"content\":{\"lockupViewModel\":{\"contentType\":\"LOCKUP_CONTENT_TYPE_VIDEO\",\"contentId\":\"" + id +
            "\",\"metadata\":{\"lockupMetadataViewModel\":{\"title\":{\"content\":\"Title " + id +
            "\"}}},\"rendererContext\":{\"commandContext\":{\"onTap\":{\"innertubeCommand\":{\"commandMetadata\":{\"webCommandMetadata\":{\"url\":\"/watch?v=" + id + "\"}}}}}}}}}}" :
            "{\"richItemRenderer\":{\"content\":{\"shortsLockupViewModel\":{\"entityId\":\"shorts-shelf-item-" + id +
            "\",\"overlayMetadata\":{\"primaryText\":{\"content\":\"Title " + id +
            "\"}},\"onTap\":{\"innertubeCommand\":{\"reelWatchEndpoint\":{\"videoId\":\"" + id + "\"}}}}}}}");
        var json = "{\"contents\":{\"twoColumnBrowseResultsRenderer\":{\"tabs\":[{\"tabRenderer\":{\"selected\":true,\"content\":{\"richGridRenderer\":{\"contents\":[" +
            string.Join(",", items) + "]}}}}]}}}";
        if (tab == "shorts") json = json.Replace("\"", @"\x22").Replace("{", @"\x7b").Replace("}", @"\x7d");
        return "<link rel='alternate' type='application/rss+xml' href='https://www.youtube.com/feeds/videos.xml?channel_id=UCabc123'>" +
            "<script>var ytInitialData = " + (tab == "shorts" ? "'" + json + "'" : json) + ";</script>";
    }
}

internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses;
    public readonly System.Collections.Generic.List<Uri> Requests = new System.Collections.Generic.List<Uri>();
    public ScriptedHandler(params HttpResponseMessage[] responses)
    { this.responses = new Queue<HttpResponseMessage>(responses); }
    public static HttpResponseMessage Response(HttpStatusCode code, string body) =>
        new HttpResponseMessage(code) { Content = new StringContent(body) };
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests.Add(request.RequestUri);
        var response = responses.Dequeue();
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}

internal sealed class FaultHandler : HttpMessageHandler
{
    private readonly Exception error;
    public FaultHandler(Exception error) { this.error = error; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromException<HttpResponseMessage>(error);
}
