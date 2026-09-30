using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal enum TwitchLookupState { Unknown, NotFound, NotLive, Live }

    internal sealed class TwitchStatus
    {
        public TwitchLookupState LookupState { get; set; } = TwitchLookupState.Unknown;
        public LiveState State { get; set; } = LiveState.Unknown;
        public bool ChannelExists { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public int? Viewers { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public string SessionId { get; set; }
        public string Category { get; set; }
        public string Source { get; set; } = "Public HTML";
    }

    // Offline means NotLive: a verified, normally parsed channel page has no current broadcast.
    // Anonymous GQL currently requires a Client-ID. No third-party ID or playback token is used.
    internal sealed class TwitchClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly Func<DateTimeOffset> now;
        private static readonly Regex Scripts = new Regex(
            @"<script\b[^>]*\btype\s*=\s*['""]application/ld\+json['""][^>]*>(.*?)</script\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "directory", "downloads", "settings", "subscriptions", "inventory", "wallet", "videos", "search",
          "jobs", "turbo", "login", "signup", "activate", "friends", "messages", "payments", "products", "p" };

        public TwitchClient(HttpMessageHandler handler = null, Func<DateTimeOffset> now = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler {
                AllowAutoRedirect = true, UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            http.Timeout = TimeSpan.FromSeconds(12);
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HIKINotifier/" + ProductVersion.DisplayVersion);
            this.now = now ?? (() => DateTimeOffset.UtcNow);
        }

        public static string ParseLogin(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (!text.Contains("://")) text = "https://" + text;
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                (uri.Host != "twitch.tv" && uri.Host != "www.twitch.tv")) return null;
            var login = uri.AbsolutePath.Trim('/');
            return Regex.IsMatch(login, @"\A[a-zA-Z0-9_]{1,25}\z") && !Reserved.Contains(login)
                ? login.ToLowerInvariant() : null;
        }

        public static string UrlFor(string login) => "https://www.twitch.tv/" + login;

        public async Task<TwitchStatus> GetStatusAsync(string login, CancellationToken token)
        {
            if (ParseLogin(UrlFor(login)) != login) return new TwitchStatus();
            try
            {
                using (var response = await http.GetAsync(UrlFor(login), token).ConfigureAwait(false))
                {
                    Debug.WriteLine("[Twitch HTML] " + login + " HTTP " + (int)response.StatusCode);
                    if (!response.IsSuccessStatusCode) Trace.WriteLine("[Twitch] HTTP " + (int)response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return new TwitchStatus { LookupState = TwitchLookupState.NotFound };
                    if (!response.IsSuccessStatusCode || ParseLogin(response.RequestMessage.RequestUri.AbsoluteUri) != login)
                        return new TwitchStatus();
                    return ParseHtml(await response.Content.ReadAsStringAsync().ConfigureAwait(false), login, now());
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Trace.WriteLine("[Twitch HTML] " + login + " " + ex.GetType().Name);
                return new TwitchStatus();
            }
        }

        internal static TwitchStatus ParseHtml(string html, string login, DateTimeOffset now)
        {
            var result = new TwitchStatus();
            if (string.IsNullOrEmpty(html) || html.Length > 2 * 1024 * 1024) return result;
            try
            {
                var nodes = new List<Dictionary<string, object>>();
                var json = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024, RecursionLimit = 64 };
                foreach (Match match in Scripts.Matches(html))
                {
                    var root = json.DeserializeObject(match.Groups[1].Value) as Dictionary<string, object>;
                    if (root == null) return result;
                    if (root.TryGetValue("@graph", out var graph))
                    {
                        if (!(graph is object[] array) || array.Any(n => !(n is Dictionary<string, object>))) return result;
                        nodes.AddRange(array.Cast<Dictionary<string, object>>());
                    }
                    else nodes.Add(root);
                }
                // Do not turn unrecognized/schema-changed structured data into a negative result.
                var knownTypes = new[] { "ProfilePage", "BreadcrumbList", "ItemList", "VideoObject", "Organization" };
                if (nodes.Any(n => !knownTypes.Contains(Text(n, "@type")))) return result;
                if (nodes.Count(n => Text(n, "@type") == "ProfilePage") != 1) return result;
                if (nodes.Where(n => Text(n, "@type") == "ItemList").Any(n =>
                    !n.TryGetValue("itemListElement", out var items) || !(items is object[] elements) ||
                    elements.Any(item => !(item is Dictionary<string, object>)))) return result;
                var profile = nodes.Where(n => Text(n, "@type") == "ProfilePage")
                    .Select(n => Object(n, "mainEntity"))
                    .FirstOrDefault(n => n != null && Text(n, "@type") == "Person" &&
                        ParseLogin(Text(n, "url")) == login &&
                        string.Equals(Text(n, "alternateName"), login, StringComparison.OrdinalIgnoreCase));
                if (profile == null) return result;
                result.ChannelExists = true;
                result.Name = Text(profile, "name") ?? login;
                // Do not descend into ItemList: those VideoObjects are VODs/clips, not the channel's broadcast.
                var broadcasts = nodes.Where(n => Text(n, "@type") == "VideoObject").ToArray();
                if (broadcasts.Length == 0)
                {
                    result.LookupState = TwitchLookupState.NotLive;
                    result.State = LiveState.Offline;
                    return result;
                }
                if (broadcasts.Length != 1) return result;
                var video = broadcasts[0];
                if (!IsChannelEmbed(Text(video, "embedUrl"), login)) return result;
                var publication = Object(video, "publication");
                if (publication == null || Text(publication, "@type") != "BroadcastEvent" ||
                    !publication.TryGetValue("isLiveBroadcast", out var live) || !(live is bool))
                    return result;
                if (!(bool)live)
                {
                    result.LookupState = TwitchLookupState.NotLive;
                    result.State = LiveState.Offline;
                    return result;
                }
                var startedAt = Date(Text(publication, "startDate"));
                var end = Date(Text(publication, "endDate"));
                // Expired/contradictory SEO data cannot establish a current state.
                if (startedAt > now || (end.HasValue && end.Value <= now)) return result;
                if (publication.ContainsKey("startDate") && !startedAt.HasValue) return result;
                if (publication.ContainsKey("endDate") && !end.HasValue) return result;
                result.LookupState = TwitchLookupState.Live;
                result.State = LiveState.Live;
                result.Title = Text(video, "description");
                result.StartedAt = startedAt;
                result.SessionId = startedAt.HasValue ? login + ":" + startedAt.Value.ToUniversalTime().ToString("O") : null;
                // HTML has no structured concurrent viewer count or game. Never use follower/VOD counts.
                return result;
            }
            catch (Exception ex) { Trace.WriteLine("[Twitch parser] " + ex.GetType().Name); return new TwitchStatus(); }
        }

        private static bool IsChannelEmbed(string value, string login)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.Host != "player.twitch.tv" || !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;
            var args = uri.Query.TrimStart('?').Split('&').Select(s => s.Split(new[] { '=' }, 2)).ToArray();
            return !args.Any(a => a[0] == "video") && args.Count(a => a[0] == "channel") == 1 &&
                args.Any(a => a.Length == 2 && a[0] == "channel" &&
                    string.Equals(Uri.UnescapeDataString(a[1]), login, StringComparison.OrdinalIgnoreCase));
        }

        private static DateTimeOffset? Date(string text) => DateTimeOffset.TryParse(text,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : (DateTimeOffset?)null;
        private static string Text(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out var value) ? value as string : null;
        private static Dictionary<string, object> Object(Dictionary<string, object> node, string key) =>
            node.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
        public void Dispose() => http.Dispose();
    }
}
