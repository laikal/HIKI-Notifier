using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.UI;

internal static class TwitchChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var at = new DateTimeOffset(2026, 9, 24, 16, 50, 0, TimeSpan.Zero);
        var fixture = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "twitch-eslcs.html"));
        var noBroadcast = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "twitch-twitch.html"));
        foreach (var url in new[] { "https://www.twitch.tv/Example/?foo=bar", "www.twitch.tv/Example", "twitch.tv/example", "https://twitch.tv/example#about" })
            check(TwitchClient.ParseLogin(url) == "example", "Twitch URL normalized " + url);
        foreach (var url in new[] { "https://twitch.tv.evil/example", "https://evil@twitch.tv/example", "https://www.twitch.tv/", "https://twitch.tv/directory", "https://twitch.tv/example/videos", "https://twitch.tv:444/example", "https://twitch.tv/%2Fexample", "http://twitch.tv/example" })
            check(TwitchClient.ParseLogin(url) == null, "Twitch invalid URL rejected " + url);
        var live = TwitchClient.ParseHtml(fixture, "eslcs", at);
        check(live.ChannelExists && live.State == LiveState.Live && live.Name == "ESLCS", "captured Twitch LIVE structured data");
        check(live.Title == "LIVE: LPH Gaming vs WBT - ESL Challenger League - Season 52 EU", "Twitch title from broadcast object");
        check(live.StartedAt == DateTimeOffset.Parse("2026-09-24T12:40:19Z") && live.SessionId.StartsWith("eslcs:"), "Twitch startedAt session key");
        check(live.Viewers == null && live.Category == null, "Twitch missing optional metadata is not fabricated");
        check(TwitchClient.ParseHtml(fixture.Replace("\"description\":\"LIVE: LPH Gaming vs WBT - ESL Challenger League - Season 52 EU\",", ""), "eslcs", at).State == LiveState.Live,
            "Twitch optional title does not determine LIVE");
        var offlineCandidate = TwitchClient.ParseHtml(noBroadcast, "twitch", at);
        check(offlineCandidate.ChannelExists && offlineCandidate.LookupState == TwitchLookupState.NotLive &&
            offlineCandidate.State == LiveState.Offline, "Twitch verified profile without broadcast is NotLive");
        check(TwitchClient.ParseHtml(fixture, "another", at).State == LiveState.Unknown, "Twitch rejects other channel metadata");
        check(TwitchClient.ParseHtml(fixture.Replace("channel=eslcs", "channel=another"), "eslcs", at).State == LiveState.Unknown, "Twitch embed channel must match");
        check(TwitchClient.ParseHtml(fixture.Replace("channel=eslcs", "video=123"), "eslcs", at).State == LiveState.Unknown, "Twitch VOD cannot prove LIVE");
        check(TwitchClient.ParseHtml(fixture.Replace("\"isLiveBroadcast\":true", "\"isLiveBroadcast\":false"), "eslcs", at).State == LiveState.Offline, "Twitch bound non-live broadcast is NotLive");
        check(TwitchClient.ParseHtml(fixture.Replace("\"isLiveBroadcast\":true", "\"isLiveBroadcast\":\"true\""), "eslcs", at).State == LiveState.Unknown, "Twitch wrong marker type is UNKNOWN");
        check(TwitchClient.ParseHtml(fixture, "eslcs", at.AddHours(1)).State == LiveState.Unknown, "Twitch expired SEO metadata is UNKNOWN");
        check(TwitchClient.ParseHtml(fixture, "eslcs", at.AddDays(-1)).State == LiveState.Unknown, "Twitch future scheduled metadata is UNKNOWN");
        check(TwitchClient.ParseHtml("<script type='application/ld+json'>{bad</script>", "eslcs", at).State == LiveState.Unknown, "Twitch malformed JSON is UNKNOWN");
        check(TwitchClient.ParseHtml("<html>live offline recommended channel</html>", "eslcs", at).State == LiveState.Unknown, "Twitch UI words are not status signals");
        check(TwitchClient.ParseHtml("<html>channel not found</html>", "missing", at).ChannelExists == false, "Twitch nonexistent/shell page cannot verify existence");
        using (var client = new TwitchClient(new ScriptedHandler(ScriptedHandler.Response(HttpStatusCode.OK, fixture)), () => at))
            check(client.GetStatusAsync("eslcs", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Live, "Twitch HTTP HTML pipeline");
        foreach (var code in new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError, (HttpStatusCode)429 })
        using (var client = new TwitchClient(new ScriptedHandler(ScriptedHandler.Response(code, fixture)), () => at))
            check(client.GetStatusAsync("eslcs", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown, "Twitch HTTP failure UNKNOWN " + code);
        using (var client = new TwitchClient(new ScriptedHandler(ScriptedHandler.Response(HttpStatusCode.NotFound, "missing")), () => at))
            check(client.GetStatusAsync("missing", CancellationToken.None).GetAwaiter().GetResult().LookupState == TwitchLookupState.NotFound,
                "Twitch reliable HTTP 404 is NotFound");
        using (var client = new TwitchClient(new FaultHandler(new HttpRequestException("fixture network failure"))))
            check(client.GetStatusAsync("eslcs", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown, "Twitch network failure UNKNOWN");
        using (var client = new TwitchClient(new FaultHandler(new TaskCanceledException("timeout"))))
            check(client.GetStatusAsync("eslcs", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown, "Twitch timeout UNKNOWN");

        // State sequences complement the HTTP/parser fixtures below.
        var next = new TwitchStatus { State = LiveState.Live, SessionId = "session-1", Title = "Live", StartedAt = at, Viewers = 0 };
        var provider = new TwitchProvider((login, token) => Task.FromResult(next), () => at);
        var profile = new ChannelProfile { Platform = PlatformType.Twitch, ChannelId = "example", LiveUrl = TwitchClient.UrlFor("example"), AutoOpenLive = true };
        int alerts = 0, opens = 0;
        var auto = new AutoOpenService(url => opens++);
        Action poll = () => { provider.PollAsync(new[] { profile }, (p, previous, entry) => {
            if (PollingService.ShouldNotify(previous, p.State)) alerts++;
            auto.OnUpdated(p, previous, provider.Capabilities);
        }, CancellationToken.None).GetAwaiter().GetResult(); at = at.AddMinutes(6); };
        poll();
        check(alerts == 0 && opens == 0 && profile.ViewerCount == 0, "Twitch startup LIVE silent; real zero viewer preserved");
        next = new TwitchStatus(); poll();
        check(profile.State == LiveState.Unknown && profile.LiveSessionId == "session-1" && profile.LiveTitle == "Live", "Twitch unknown preserves session and title");
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-1" }; poll();
        check(alerts == 0 && opens == 0, "Twitch LIVE UNKNOWN same LIVE no duplicate");
        next = new TwitchStatus { State = LiveState.Offline }; poll();
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-1" }; poll();
        check(alerts == 0 && opens == 0, "Twitch same startup session after OFFLINE does not reopen");
        next = new TwitchStatus { State = LiveState.Offline }; poll();
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-2" }; poll(); poll();
        check(alerts == 1 && opens == 1, "Twitch verified OFFLINE new session alerts and opens once");
        profile.AutoOpenLive = false; poll(); profile.AutoOpenLive = true; poll();
        check(opens == 1, "Twitch AutoOpen OFF ON during LIVE does not open immediately");
        check(NotificationReplay.ForChange(provider, profile, false, true)?.Type == NotificationEventType.LiveStarted && opens == 1,
            "Twitch alerts OFF ON replays without AutoOpen");
        check(NotificationReplay.ForChange(provider, profile, true, true) == null && NotificationReplay.ForChange(provider, profile, true, false) == null,
            "Twitch unchanged/disabled alerts do not replay");
        next = new TwitchStatus { State = LiveState.Offline }; poll(); next = new TwitchStatus { State = LiveState.Live }; poll();
        check(alerts == 1 && opens == 1, "Twitch ambiguous missing session suppresses repeat");
        next = new TwitchStatus(); poll();
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-3" }; poll(); poll();
        check(alerts == 2 && opens == 2, "Twitch UNKNOWN to proven new session alerts and opens once");
        next = new TwitchStatus(); poll();
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-3" }; poll();
        check(alerts == 2 && opens == 2, "Twitch repeated UNKNOWN recovery does not rearm same session");
        next = new TwitchStatus { State = LiveState.Offline }; poll(); next = new TwitchStatus(); poll();
        check(profile.State == LiveState.Offline, "Twitch NotLive survives failed poll");
        next = new TwitchStatus { State = LiveState.Live, SessionId = "session-4" }; poll();
        check(alerts == 3 && opens == 3, "Twitch NotLive error LIVE remains armed for a new session");
        next = new TwitchStatus(); poll(); next = new TwitchStatus { State = LiveState.Live }; poll();
        check(alerts == 3 && opens == 3, "Twitch UNKNOWN recovery without session identity is silent");
        int calls = 0;
        var backoff = new TwitchProvider((login, token) => { calls++; return Task.FromResult(new TwitchStatus()); }, () => at);
        Action retry = () => backoff.PollAsync(new[] { profile }, (p, previous, entry) => { }, CancellationToken.None).GetAwaiter().GetResult();
        retry(); retry(); check(calls == 1, "Twitch retry not busy-looped");
        at = at.AddSeconds(60); retry(); at = at.AddSeconds(60); retry(); check(calls == 2, "Twitch repeated failure backoff 120 seconds");
        at = at.AddSeconds(60); retry(); at = at.AddSeconds(120); retry(); check(calls == 3, "Twitch repeated failure backoff 300 seconds");
        var isolated = new TwitchProvider((login, token) => login == "bad" ? Task.FromException<TwitchStatus>(new InvalidOperationException()) : Task.FromResult(new TwitchStatus { State = LiveState.Live }));
        int updates = 0;
        isolated.PollAsync(new[] { new ChannelProfile { ChannelId = "bad" }, new ChannelProfile { ChannelId = "good" } },
            (p, previous, entry) => updates++, CancellationToken.None).GetAwaiter().GetResult();
        check(updates == 2, "Twitch per-channel exception isolation returns control to common scheduler");
        using (var chzzk = new ChzzkClient()) using (var rplay = new RplayClient()) using (var youtube = new YouTubeClient()) using (var twitch = new TwitchClient())
        {
            var registry = TestProviders.Create(chzzk, rplay, youtube, twitch);
            check(registry.Providers.Count() == 4 && registry.ForUrl("twitch.tv/example").Platform == PlatformType.Twitch && registry.ForPlatform(PlatformType.Twitch).PollInterval.TotalSeconds == 60,
                "Twitch registered with 60s polling alongside existing providers");
            foreach (var lang in new[] { "ko", "en" }) using (var form = new ProfileEditForm(new LanguageService(lang), registry))
            {
                var menu = ((Button)form.Controls["addChannelButton"]).ContextMenuStrip;
                var input = (TextBox)form.Controls["newChannelUrl"];
                check(menu.Items.Count == 4 && menu.Items[3].Text == "Twitch", "Twitch menu " + lang);
                menu.Items[3].PerformClick();
                check(input.Text == "https://www.twitch.tv/" && input.SelectionStart == input.TextLength, "Twitch template and caret " + lang);
                input.Text = "https://www.youtube.com/@example"; menu.Items[3].PerformClick();
                check(input.Text == "https://www.youtube.com/@example" && registry.ForUrl(input.Text).Platform == PlatformType.YouTube, "Twitch menu does not force URL platform " + lang);
            }
        }
        var temp = Path.Combine(Path.GetTempPath(), "hiki-twitch-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var storage = new SettingsService(Path.Combine(temp, "settings.json"));
            var settings = storage.Load();
            var streamer = new StreamerProfile { DisplayName = "Twitch fixture" };
            streamer.Channels.Add(new ChannelProfile { Platform = PlatformType.Twitch, ChannelId = "example", LiveUrl = "twitch.tv/Example/", AutoOpenLive = true });
            settings.Streamers.Add(streamer); storage.Save(settings);
            var loaded = storage.Load().Streamers.Single(s => s.Id == streamer.Id).Channels.Single();
            check(storage.LoadErrors.Count == 0 && loaded.Platform == PlatformType.Twitch && loaded.LiveUrl == "https://www.twitch.tv/example" && loaded.AutoOpenLive && loaded.State == LiveState.Unknown,
                "Twitch profile persistence and startup UNKNOWN");
        }
        finally { Directory.Delete(temp, true); }
        HtmlTransitions(check, fixture, noBroadcast);
        SchedulerIsolation(check);
    }

    private static void HtmlTransitions(Action<bool, string> check, string liveHtml, string profileOnly)
    {
        var at = new DateTimeOffset(2026, 9, 24, 16, 50, 0, TimeSpan.Zero);
        var notLive = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "twitch-eslcs-notlive.html"));
        var vodList = "<script type='application/ld+json'>{\"@type\":\"ItemList\",\"itemListElement\":[{\"@type\":\"VideoObject\",\"publication\":{\"@type\":\"BroadcastEvent\",\"isLiveBroadcast\":true}}]}</script>";
        check(TwitchClient.ParseHtml(profileOnly + vodList, "twitch", at).State == LiveState.Offline,
            "Twitch VOD/recommendation ItemList cannot supply a current broadcast");
        check(TwitchClient.ParseHtml(notLive.Replace("ProfilePage", "ChangedProfile"), "eslcs", at).State == LiveState.Unknown,
            "Twitch changed identity schema stays UNKNOWN");
        check(TwitchClient.ParseHtml(notLive + "<script type='application/ld+json'>{\"@graph\":{}}</script>", "eslcs", at).State == LiveState.Unknown,
            "Twitch changed graph shape with valid identity stays UNKNOWN");
        check(TwitchClient.ParseHtml(notLive + "<script type='application/ld+json'>{\"@type\":\"NewBroadcastSchema\"}</script>", "eslcs", at).State == LiveState.Unknown,
            "Twitch unfamiliar structured schema never implies NotLive");
        check(TwitchClient.ParseHtml(notLive + "<script type='application/ld+json'>{bad</script>", "eslcs", at).State == LiveState.Unknown,
            "Twitch parse failure despite valid identity stays UNKNOWN");
        check(TwitchClient.ParseHtml(liveHtml.Replace("\"publication\"", "\"changedPublication\""), "eslcs", at).State == LiveState.Unknown,
            "Twitch malformed live candidate never implies NotLive");
        check(TwitchClient.ParseHtml(liveHtml + liveHtml, "eslcs", at).State == LiveState.Unknown,
            "Twitch conflicting duplicate broadcast objects stay UNKNOWN");
        check(TwitchClient.ParseHtml(liveHtml.Replace("2026-09-24T12:40:19Z", "invalid-date"), "eslcs", at).State == LiveState.Unknown,
            "Twitch malformed broadcast timestamp stays UNKNOWN");
        check(TwitchClient.ParseHtml(notLive + "<script type='application/ld+json'>{\"@type\":\"ItemList\",\"itemListElement\":[1]}</script>", "eslcs", at).State == LiveState.Unknown,
            "Twitch malformed list shape stays UNKNOWN");
        using (var client = new TwitchClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, notLive),
            ScriptedHandler.Response(HttpStatusCode.ServiceUnavailable, "unavailable"),
            ScriptedHandler.Response(HttpStatusCode.OK, liveHtml),
            ScriptedHandler.Response(HttpStatusCode.OK, notLive),
            ScriptedHandler.Response(HttpStatusCode.OK, liveHtml.Replace("2026-09-24T12:40:19Z", "2026-09-24T16:00:00Z"))), () => at))
        {
            var provider = new TwitchProvider(client.GetStatusAsync, () => at);
            var profile = new ChannelProfile { Platform = PlatformType.Twitch, ChannelId = "eslcs", LiveUrl = TwitchClient.UrlFor("eslcs"), AutoOpenLive = true };
            int alerts = 0, opens = 0;
            var auto = new AutoOpenService(url => opens++);
            Action poll = () => { provider.PollAsync(new[] { profile }, (p, previous, entry) => {
                if (PollingService.ShouldNotify(previous, p.State)) alerts++;
                auto.OnUpdated(p, previous, provider.Capabilities);
            }, CancellationToken.None).GetAwaiter().GetResult(); at = at.AddMinutes(1); };
            poll();
            check(profile.State == LiveState.Offline && alerts == 0, "Twitch HTTP normal channel HTML establishes NotLive");
            poll();
            check(profile.State == LiveState.Offline && alerts == 0, "Twitch HTTP error preserves confirmed NotLive");
            poll();
            check(profile.State == LiveState.Live && alerts == 1 && opens == 1, "Twitch HTML NotLive to LIVE notifies and opens once");
            poll(); poll();
            check(alerts == 2 && opens == 2, "Twitch HTML NotLive rearms next distinct broadcast");
        }
        var next = new TwitchStatus();
        var startup = new TwitchProvider((login, token) => Task.FromResult(next), () => at);
        var fresh = new ChannelProfile { Platform = PlatformType.Twitch };
        int startupAlerts = 0;
        Action initial = () => { startup.PollAsync(new[] { fresh }, (p, previous, entry) => {
            if (PollingService.ShouldNotify(previous, p.State)) startupAlerts++;
        }, CancellationToken.None).GetAwaiter().GetResult(); at = at.AddMinutes(6); };
        initial(); next = new TwitchStatus { State = LiveState.Live, SessionId = "first-observation" }; initial();
        check(startupAlerts == 0, "Twitch startup error then first LIVE remains silent");
    }

    private static void SchedulerIsolation(Action<bool, string> check)
    {
        var pending = new PendingTwitchHandler();
        using (var twitch = new TwitchClient(pending))
        using (var chzzk = new ChzzkClient(new ScriptedHandler(
            ScriptedHandler.Response(HttpStatusCode.OK, "{\"code\":200,\"content\":{\"status\":\"CLOSE\"}}"),
            ScriptedHandler.Response(HttpStatusCode.OK, "{\"code\":200,\"content\":{\"status\":\"CLOSE\"}}"))))
        using (var youtube = new YouTubeClient()) using (var rplay = new RplayClient())
        {
            var registry = TestProviders.Create(chzzk, rplay, youtube, twitch);
            var profiles = new[] { new ChannelProfile { Platform = PlatformType.Chzzk, ChannelId = BuiltInChannel.BuiltInId },
                new ChannelProfile { Platform = PlatformType.Twitch, ChannelId = "example" } };
            using (var polling = new PollingService(registry, () => profiles))
            {
                int chzzkUpdates = 0;
                var twice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                polling.ProfileUpdated += (p, previous, entry) => {
                    if (p.Platform == PlatformType.Chzzk && Interlocked.Increment(ref chzzkUpdates) == 2) twice.TrySetResult(true); };
                polling.Start();
                var completed = Task.WhenAny(twice.Task, Task.Delay(35000)).GetAwaiter().GetResult();
                pending.Finish.TrySetResult(ScriptedHandler.Response(HttpStatusCode.OK, "<html></html>"));
                polling.StopAsync().GetAwaiter().GetResult();
                check(completed == twice.Task, "stalled Twitch HTTP does not delay next CHZZK polling cycle");
                check(pending.Calls == 1, "Twitch batch never overlaps; StopAsync joins pending task");
            }
        }
    }

    internal static void LiveProbe()
    {
        using (var client = new TwitchClient())
        foreach (var login in new[] { "eslcs", "gaules", "twitch" })
        {
            var result = client.GetStatusAsync(login, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine(login + " source=" + result.Source + " exists=" + result.ChannelExists + " state=" + result.State +
                " title=" + result.Title + " viewers=" + result.Viewers + " started=" + result.StartedAt + " session=" + result.SessionId);
        }
    }
}

internal sealed class PendingTwitchHandler : HttpMessageHandler
{
    internal readonly TaskCompletionSource<HttpResponseMessage> Finish = new TaskCompletionSource<HttpResponseMessage>();
    internal int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    { Interlocked.Increment(ref Calls); return Finish.Task; }
}
