using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

internal static class KickChecks
{
    private static int checks;
    private static void Check(bool pass, string description)
    { if (!pass) throw new Exception("FAIL " + description); checks++; }
    private const string Offline = "{\"id\":10,\"slug\":\"example\",\"user\":{\"username\":\"Example\"},\"livestream\":null}";
    private static string Live(int id = 100, int viewers = 0) => "{\"id\":10,\"slug\":\"example\",\"user\":{\"username\":\"Example\"},\"livestream\":{\"id\":" + id +
        ",\"channel_id\":10,\"session_title\":\"Fixture broadcast\",\"created_at\":\"2026-09-26 00:00:00\",\"viewer_count\":" + viewers + "}}";
    [STAThread] private static void Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--online") { Online(); return; }
            Application.EnableVisualStyles();
            Check(KickClient.ParseSlug("https://kick.com/ExAmPlE/") == "example", "slug normalized");
            Check(KickClient.ParseSlug("https://www.kick.com/example?ref=test") == "example", "www/query supported");
            foreach (var url in new[] { "http://kick.com/example", "https://kick.com.evil/example", "https://kick.com/a/b", "https://kick.com/", "https://user@kick.com/example", "https://kick.com:444/example" })
                Check(KickClient.ParseSlug(url) == null, "reject invalid URL " + url);
            Check(KickClient.Parse(Offline, "example").State == LiveState.Offline, "explicit null livestream Offline");
            var live = KickClient.Parse(Live(), "example");
            Check(live.State == LiveState.Live && live.Title == "Fixture broadcast" && live.Viewers == 0 && live.SessionId == "id:100" && live.StartedAt.HasValue,
                "live title/viewers zero/session/start");
            Check(KickClient.Parse(Live().Replace("\"id\":100,", "\"slug\":\"stream-one\","), "example").SessionId == "slug:stream-one", "stream slug fallback identity");
            foreach (var body in new[] { "", "<html>challenge</html>", "{}", "[]", "null", "{\"id\":10,\"slug\":\"example\"}",
                Offline.Replace("example", "recommended"), Live().Replace("\"channel_id\":10", "\"channel_id\":11"),
                Offline.Replace("null", "false"), Offline.Replace("null", "{}"), Live().Replace("\"viewer_count\":0", "\"viewer_count\":-1") })
                Check(KickClient.Parse(body, "example").State == LiveState.Unknown, "malformed or wrong scope Unknown");
            foreach (var status in new[] { 403, 404, 429, 500, 302 })
            using (var client = new KickClient(new Replies(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(Offline) })))
                Check(client.GetStatusAsync("example", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown, "HTTP " + status + " Unknown");
            foreach (var error in new Exception[] { new HttpRequestException("network fixture"), new TaskCanceledException("timeout fixture") })
            using (var client = new KickClient(new Replies(error)))
                Check(client.GetStatusAsync("example", CancellationToken.None).GetAwaiter().GetResult().State == LiveState.Unknown, "network/timeout Unknown");
            using (var client = new KickClient(new Replies(new TaskCanceledException())))
            {
                var cancel = new CancellationToken(true); bool canceled = false;
                try { client.GetStatusAsync("example", cancel).GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
                Check(canceled, "shutdown cancellation propagated");
            }
            var replies = new Replies(Offline);
            using (var provider = new KickProvider(new KickClient(replies)))
            {
                var channel = provider.CreateAsync("https://www.kick.com/EXAMPLE/", CancellationToken.None).GetAwaiter().GetResult();
                Check(channel.State == LiveState.Offline && (int)channel.Platform == 6 && channel.Name == "Example" && channel.LiveUrl == "https://kick.com/example", "channel registration");
                Check(replies.Urls.Single() == "https://kick.com/api/v2/channels/example", "single designated endpoint only");
                Check(replies.UserAgent.Contains("1.17c"), "central version User-Agent");
                Check(provider.Metadata.LogoPng.Length > 0 && provider.Metadata.PollIndependently && provider.PollInterval.TotalSeconds == 60, "embedded logo and polling metadata");
                HostIntegration(channel);
            }
            Transitions();
            CommonPolling();
            Console.WriteLine("PASS " + checks + " Kick checks");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    private static Assembly Host => Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HIKI Notifier.exe"));
    private static void CommonPolling()
    {
        var handler = new Replies(Offline);
        using (var provider = new KickProvider(new KickClient(handler)))
        {
            var app = Host;
            var registryType = app.GetType("HikiNotifier.Services.PlatformRegistry");
            var registry = registryType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { new IPlatformProvider[] { provider }, null });
            var profile = new ChannelProfile { Platform = (PlatformType)6, ChannelId = "example" };
            var pollType = app.GetType("HikiNotifier.Services.PollingService");
            using (var poll = (IDisposable)Activator.CreateInstance(pollType, new object[] { registry,
                new Func<IList<ChannelProfile>>(() => new[] { profile }) }))
            {
                pollType.GetMethod("Start").Invoke(poll, null);
                ((Task)pollType.GetMethod("StopAsync").Invoke(poll, null)).GetAwaiter().GetResult();
                Check(profile.State == LiveState.Offline && handler.Urls.Count == 1, "unchanged common polling dispatches Kick once");
            }
        }
    }
    private static void Transitions()
    {
        using (var provider = new KickProvider(new KickClient(new Replies(Live(), Live(), "bad", Live(), Offline, Live(101), Live(101), "bad", Live(101)))))
        {
            var profile = new ChannelProfile { Platform = (PlatformType)6, ChannelId = "example", LiveUrl = "https://kick.com/example", AutoOpenLive = true };
            var opened = new List<string>();
            var autoType = Host.GetType("HikiNotifier.Services.AutoOpenService");
            var auto = Activator.CreateInstance(autoType, new object[] { new Action<string>(opened.Add) });
            var notify = Host.GetType("HikiNotifier.Services.PollingService").GetMethod("ShouldNotify", BindingFlags.Static | BindingFlags.NonPublic);
            int alerts = 0;
            for (int i=0;i<9;i++)
            {
                provider.PollAsync(new[] {profile}, (p, previous, entry) => {
                    if ((bool)notify.Invoke(null, new object[] {previous,p.State})) alerts++;
                    autoType.GetMethod("OnUpdated").Invoke(auto,new object[] {p,previous,provider.Capabilities});
                },CancellationToken.None).GetAwaiter().GetResult();
                if (i==2) Check(profile.State == LiveState.Unknown && profile.ProviderState.LastLiveSessionId == "id:100", "failure preserves session");
                if (i==3) Check(alerts == 0 && opened.Count == 0, "startup and Unknown same-session silent");
            }
            Check(alerts == 1 && opened.Count == 1, "one notification/AutoOpen for new Offline->Live; repeated Live silent");
            Check(provider.GetReplayableNotification(profile)?.Type == NotificationEventType.LiveStarted, "existing replay contract");
        }
    }
    private static void HostIntegration(ChannelProfile channel)
    {
        var app = Host;
        var registryType = app.GetType("HikiNotifier.Services.PlatformRegistry");
        var registry = registryType.GetProperty("Default").GetValue(null);
        var providers = ((IEnumerable<IPlatformProvider>)registryType.GetProperty("Providers").GetValue(registry)).ToArray();
        Check(providers.Length == 7 && providers.Single(p => p.Metadata.Id == "kick").Platform == (PlatformType)6, "unchanged Main auto discovers Kick");
        var existing = new[] { "chzzk", "rplay", "youtube", "twitch", "soop", "cime" };
        Check(existing.All(id => providers.Any(p => p.Metadata.Id == id)), "six existing modules still registered");
        foreach (var code in new[] { "ko", "en", "ja" })
        {
            var lang = Activator.CreateInstance(app.GetType("HikiNotifier.Services.LanguageService"), new object[] {code,null});
            var editType = app.GetType("HikiNotifier.UI.ProfileEditForm");
            using (var edit = (Form)Activator.CreateInstance(editType,new[] {lang,registry,null}))
            {
                var menu = edit.Controls.Find("addChannelButton",true).Single().ContextMenuStrip;
                Check(menu.Items.Cast<ToolStripItem>().Any(i => i.Text == "Kick"), code + " Kick auto menu");
                ((ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Kick")).PerformClick();
                var input = edit.Controls.Find("newChannelUrl",true).Single();
                Check(input.Text == "https://kick.com/", code + " Kick template");
                input.Text = channel.LiveUrl;
                Check((bool)editType.GetMethod("AddEnteredChannel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(edit,null), code + " URL row accepted");
                edit.Controls.Find("profileName",true).Single().Text = "Kick fixture";
                editType.GetMethod("SaveDraft",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(edit,null);
                var draft = ((IEnumerable)editType.GetProperty("Channels").GetValue(edit)).Cast<object>().Single();
                Check((int)(PlatformType)draft.GetType().GetProperty("Platform").GetValue(draft)==6, code + " draft platform preserved");
            }
        }
        var temporary = Path.Combine(Path.GetTempPath(), "hiki-kick-state-"+Guid.NewGuid().ToString("N"));
        var storageType = app.GetType("HikiNotifier.Services.SettingsService");
        var storage = Activator.CreateInstance(storageType,new[] {(object)Path.Combine(temporary,"settings.json"),registry});
        var settings = storageType.GetMethod("Load").Invoke(storage,null);
        var streamer = Activator.CreateInstance(app.GetType("HikiNotifier.Models.StreamerProfile"),true);
        streamer.GetType().GetProperty("DisplayName").SetValue(streamer,"Kick fixture");
        ((IList)streamer.GetType().GetProperty("Channels").GetValue(streamer)).Add(channel);
        ((IList)settings.GetType().GetProperty("Streamers").GetValue(settings)).Add(streamer);
        storageType.GetMethod("Save").Invoke(storage,new[] {settings});
        var restored = storageType.GetMethod("Load").Invoke(storage,null);
        var all = ((IEnumerable)restored.GetType().GetProperty("Profiles").GetValue(restored)).Cast<ChannelProfile>();
        Check(all.Any(p => (int)p.Platform==6 && p.LiveUrl==channel.LiveUrl), "unchanged storage round trip platform 6");
        settings.GetType().GetProperty("SoundMode").SetValue(settings,Enum.Parse(app.GetType("HikiNotifier.Models.NotificationSoundMode"),"Silent"));
        var notification = Activator.CreateInstance(app.GetType("HikiNotifier.Services.NotificationService"),true);
        notification.GetType().GetMethod("Show").Invoke(notification,new object[] {channel,settings,false});
        var alert = Application.OpenForms.Cast<Form>().Single(f=>f.GetType().Name=="NotificationForm");
        Check(alert.Controls.OfType<PictureBox>().Single().Image != null, "existing notification renders Kick logo");
        alert.Close(); alert.Dispose();
        Console.WriteLine("Isolated persistence evidence: " + temporary);
    }
    private static void Online()
    {
        using (var provider = new KickProvider())
        using (var client = new KickClient())
        foreach (var slug in new[] { "trainwreckstv", "xqc" })
        {
            var result = client.GetStatusAsync(slug,CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine(slug + " state=" + result.State + " title=" + result.Title + " viewers=" + result.Viewers + " session=" + result.SessionId + " started=" + result.StartedAt);
            if (result.State == LiveState.Unknown) { Environment.ExitCode=2; continue; }
            var profile = provider.CreateAsync("https://kick.com/"+slug,CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("Registered: " + profile.LiveUrl + " name=" + profile.Name + " state=" + profile.State);
        }
    }
    private sealed class Replies : HttpMessageHandler
    {
        private readonly Queue<object> results;
        public readonly List<string> Urls = new List<string>(); public string UserAgent;
        public Replies(params object[] results) { this.results = new Queue<object>(results); }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Urls.Add(request.RequestUri.AbsoluteUri); UserAgent=request.Headers.UserAgent.ToString();
            var result=results.Dequeue();
            if(result is Exception error) return Task.FromException<HttpResponseMessage>(error);
            return Task.FromResult(result as HttpResponseMessage ?? new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent((string)result)});
        }
    }
}
