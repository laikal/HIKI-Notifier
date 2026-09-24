using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Web.Script.Serialization;

namespace HikiNotifier.Services
{
    internal sealed class YouTubeEntry
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset Updated { get; set; }
    }

    internal sealed class YouTubeFeed
    {
        public string ChannelId { get; set; }
        public string ChannelName { get; set; }
        public List<YouTubeEntry> Entries { get; set; } = new List<YouTubeEntry>();
    }

    internal sealed class YouTubeHtmlSnapshot
    {
        // Null means the tab could not be read; an empty list is a valid empty tab.
        public List<YouTubeEntry> Videos { get; set; }
        public List<YouTubeEntry> Shorts { get; set; }
        internal List<Exception> Errors { get; } = new List<Exception>();
    }

    internal enum YouTubeResolveFailure { ChannelNotFound, Network }

    internal sealed class YouTubeResolveException : Exception
    {
        public YouTubeResolveFailure Failure { get; }
        public YouTubeResolveException(YouTubeResolveFailure failure, Exception inner = null)
            : base(failure == YouTubeResolveFailure.ChannelNotFound ?
                "Unable to resolve the YouTube channel." : "Unable to connect to YouTube.", inner)
        { Failure = failure; }
    }

    internal sealed class YouTubeClient : IDisposable
    {
        private readonly HttpClient http;
        public YouTubeClient(HttpMessageHandler handler = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }) { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        }

        [Conditional("DEBUG")]
        private static void Log(string step, string value) => Debug.WriteLine("[YouTube] " + step + ": " + value);

        public static bool IsChannelUrl(string input)
        {
            Uri uri;
            if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                (uri.Host != "www.youtube.com" && uri.Host != "youtube.com")) return false;
            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            return Regex.IsMatch(path, @"^/(channel/UC[A-Za-z0-9_-]+|@[\p{L}\p{N}_.-]+|c/[\p{L}\p{N}_.-]+|user/[\p{L}\p{N}_.-]+)/?$", RegexOptions.IgnoreCase);
        }

        public static string DirectChannelId(string input)
        {
            if (!IsChannelUrl(input)) return null;
            var path = new Uri(input.Trim()).AbsolutePath;
            var match = Regex.Match(path, @"^/channel/(UC[A-Za-z0-9_-]+)/?$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        public async Task<string> ResolveChannelIdAsync(string url, CancellationToken token)
        {
            if (!IsChannelUrl(url)) throw new ArgumentException("Invalid YouTube channel URL", nameof(url));
            var direct = DirectChannelId(url);
            if (direct != null) { Log("channel ID", "direct URL " + direct); return direct; }
            Log("channel page request", url);
            try
            {
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
                {
                    Log("channel page HTTP", ((int)response.StatusCode) + " final URL=" + response.RequestMessage?.RequestUri);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new YouTubeResolveException(YouTubeResolveFailure.ChannelNotFound);
                    if (!response.IsSuccessStatusCode)
                        throw new YouTubeResolveException(YouTubeResolveFailure.Network);
                    var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log("channel page body", "received");
                    string strategy;
                    var id = ChannelIdFromHtml(html, out strategy);
                    Log("channel ID extraction", id == null ? "failed" : strategy + " " + id);
                    if (id == null) throw new YouTubeResolveException(YouTubeResolveFailure.ChannelNotFound);
                    return id;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (YouTubeResolveException) { throw; }
            catch (HttpRequestException ex) { Log("channel page transport", ex.ToString()); throw new YouTubeResolveException(YouTubeResolveFailure.Network, ex); }
            catch (TaskCanceledException ex) { Log("channel page timeout", ex.ToString()); throw new YouTubeResolveException(YouTubeResolveFailure.Network, ex); }
            catch (Exception ex) { Log("channel page failure", ex.ToString()); throw new YouTubeResolveException(YouTubeResolveFailure.Network, ex); }
        }

        internal static string ChannelIdFromHtml(string html)
        { string strategy; return ChannelIdFromHtml(html, out strategy); }

        internal static string ChannelIdFromHtml(string html, out string strategy)
        {
            strategy = null;
            // 1. The channel's advertised public feed.
            foreach (Match link in Regex.Matches(html ?? "", @"<link\b[^>]*>", RegexOptions.IgnoreCase))
            {
                var attributes = Attributes(link.Value);
                string rel, type, href;
                if (!attributes.TryGetValue("rel", out rel) || !attributes.TryGetValue("type", out type) ||
                    !attributes.TryGetValue("href", out href) || !rel.Equals("alternate", StringComparison.OrdinalIgnoreCase) ||
                    !type.Equals("application/rss+xml", StringComparison.OrdinalIgnoreCase)) continue;
                Uri feed;
                if (!Uri.TryCreate(new Uri("https://www.youtube.com"), href, out feed) || feed.Scheme != "https" ||
                    (feed.Host != "www.youtube.com" && feed.Host != "youtube.com") ||
                    feed.AbsolutePath != "/feeds/videos.xml") continue;
                var match = Regex.Match(feed.Query, @"(?:^|[?&])channel_id=(UC[A-Za-z0-9_-]+)(?:&|$)");
                if (match.Success) { strategy = "RSS alternate"; return match.Groups[1].Value; }
            }
            // 2. A canonical channel URL, independent of attribute order.
            foreach (Match link in Regex.Matches(html ?? "", @"<link\b[^>]*>", RegexOptions.IgnoreCase))
            {
                var attributes = Attributes(link.Value);
                string rel, href;
                if (attributes.TryGetValue("rel", out rel) && attributes.TryGetValue("href", out href) &&
                    rel.Equals("canonical", StringComparison.OrdinalIgnoreCase))
                {
                    var id = DirectChannelId(href);
                    if (id != null) { strategy = "canonical URL"; return id; }
                }
            }
            // 3. Explicit channelId metadata or a named property.
            foreach (Match meta in Regex.Matches(html ?? "", @"<meta\b[^>]*>", RegexOptions.IgnoreCase))
            {
                var attributes = Attributes(meta.Value);
                string content;
                if (!attributes.TryGetValue("content", out content) || !ValidChannelId(content)) continue;
                foreach (var keyName in new[] { "itemprop", "property", "name" })
                {
                    string key;
                    if (attributes.TryGetValue(keyName, out key) &&
                        key.EndsWith("channelId", StringComparison.OrdinalIgnoreCase))
                    { strategy = "channelId meta"; return content; }
                }
            }
            var properties = Regex.Matches(html ?? "", @"[""']channelId[""']\s*:\s*[""'](UC[A-Za-z0-9_-]+)[""']",
                RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (properties.Length == 1) { strategy = "channelId property"; return properties[0]; }
            // 4. Only accept a unique explicitly named UC identifier. Never infer from arbitrary UC text.
            var candidates = Regex.Matches(html ?? "", @"[""'](?:externalId|browseId)[""']\s*:\s*[""'](UC[A-Za-z0-9_-]+)[""']",
                RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
            if (candidates.Length == 1) { strategy = "unique explicit UC identifier"; return candidates[0]; }
            return null;
        }

        private static bool ValidChannelId(string value) => !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(value, @"^UC[A-Za-z0-9_-]+$");

        private static Dictionary<string, string> Attributes(string tag)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attr in Regex.Matches(tag, @"([\w:-]+)\s*=\s*(['""])(.*?)\2", RegexOptions.Singleline))
                attributes[attr.Groups[1].Value] = WebUtility.HtmlDecode(attr.Groups[3].Value);
            return attributes;
        }

        public async Task<YouTubeFeed> GetFeedAsync(string channelId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(channelId) || !Regex.IsMatch(channelId, @"^UC[A-Za-z0-9_-]+$"))
                throw new ArgumentException("Invalid YouTube channel ID", nameof(channelId));
            var url = "https://www.youtube.com/feeds/videos.xml?channel_id=" + Uri.EscapeDataString(channelId);
            Log("feed URL", url);
            try
            {
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
                {
                    Log("feed HTTP", ((int)response.StatusCode) + " final URL=" + response.RequestMessage?.RequestUri);
                    response.EnsureSuccessStatusCode();
                    var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    YouTubeFeed feed;
                    try { feed = ParseFeed(xml, channelId); }
                    catch (Exception ex) { Log("feed XML parse", "failed " + ex.ToString()); throw; }
                    Log("feed XML parse", "success entries=" + feed.Entries.Count);
                    return feed;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { Log("feed failure", ex.ToString()); throw; }
        }

        internal static YouTubeFeed ParseFeed(string xml, string expectedId)
        {
            var doc = XDocument.Parse(xml, LoadOptions.None);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
            if (doc.Root?.Name != atom + "feed") throw new InvalidOperationException("Invalid YouTube feed");
            var feedId = (string)doc.Root.Element(yt + "channelId");
            // YouTube's feed root can omit the UC prefix even though entry channel IDs include it.
            if (feedId != expectedId && feedId != expectedId.Substring(2))
                throw new InvalidOperationException("YouTube channel ID mismatch");
            var feed = new YouTubeFeed { ChannelId = expectedId, ChannelName = (string)doc.Root.Element(atom + "title") };
            foreach (var entry in doc.Root.Elements(atom + "entry"))
            {
                var id = (string)entry.Element(yt + "videoId");
                if (string.IsNullOrWhiteSpace(id) || (string)entry.Element(yt + "channelId") != expectedId)
                    throw new InvalidOperationException("Invalid YouTube feed entry");
                var link = entry.Elements(atom + "link").FirstOrDefault(e => (string)e.Attribute("rel") == "alternate");
                Uri uri;
                var href = (string)link?.Attribute("href");
                if (!Uri.TryCreate(href, UriKind.Absolute, out uri) ||
                    (uri.Host != "www.youtube.com" && uri.Host != "youtube.com"))
                    href = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id);
                DateTimeOffset published, updated;
                DateTimeOffset.TryParse((string)entry.Element(atom + "published"), out published);
                DateTimeOffset.TryParse((string)entry.Element(atom + "updated"), out updated);
                feed.Entries.Add(new YouTubeEntry { VideoId = id, Title = (string)entry.Element(atom + "title"),
                    Url = href, Published = published, Updated = updated });
            }
            feed.Entries = feed.Entries.OrderByDescending(e => e.Published).ToList();
            return feed;
        }

        public async Task<YouTubeHtmlSnapshot> GetHtmlSnapshotAsync(string channelUrl, string channelId,
            CancellationToken token)
        {
            if (!IsChannelUrl(channelUrl) || !ValidChannelId(channelId))
                throw new ArgumentException("Invalid YouTube channel");
            var snapshot = new YouTubeHtmlSnapshot();
            foreach (var tab in new[] { "videos", "shorts" })
            {
                var url = channelUrl.Trim().TrimEnd('/') + "/" + tab;
                try
                {
                    using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, token)
                        .ConfigureAwait(false))
                    {
                        Log("HTML " + tab + " HTTP", ((int)response.StatusCode) + " " + response.RequestMessage?.RequestUri);
                        response.EnsureSuccessStatusCode();
                        var entries = ParseHtmlTab(await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                            channelId, tab);
                        if (tab == "videos") snapshot.Videos = entries; else snapshot.Shorts = entries;
                        Log("HTML " + tab + " parse", "success entries=" + entries.Count);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { snapshot.Errors.Add(ex); Log("HTML " + tab + " failure", ex.ToString()); }
            }
            if (snapshot.Videos == null && snapshot.Shorts == null)
                throw new AggregateException("Both YouTube channel tabs are unavailable", snapshot.Errors);
            return snapshot;
        }

        internal static List<YouTubeEntry> ParseHtmlTab(string html, string expectedId, string tab)
        {
            if (tab != "videos" && tab != "shorts") throw new ArgumentException("Invalid tab", nameof(tab));
            if (ChannelIdFromHtml(html) != expectedId)
                throw new InvalidOperationException("YouTube HTML channel ID mismatch");
            var json = InitialDataJson(html);
            var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 200 };
            var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            var container = Dict(root, "contents");
            var contents = Dict(container, "singleColumnBrowseResultsRenderer") ??
                Dict(container, "twoColumnBrowseResultsRenderer");
            var tabs = Value(contents, "tabs") as object[];
            if (tabs == null) throw new InvalidOperationException("YouTube tabs missing");
            Dictionary<string, object> grid = null;
            foreach (var item in tabs)
            {
                var renderer = Dict(item as Dictionary<string, object>, "tabRenderer");
                if (Value(renderer, "selected") is bool selected && selected)
                {
                    grid = Dict(Dict(renderer, "content"), "richGridRenderer");
                    break;
                }
            }
            var items = Value(grid, "contents") as object[];
            if (items == null) throw new InvalidOperationException("YouTube selected grid missing");
            var entries = new List<YouTubeEntry>();
            foreach (var item in items)
            {
                var content = Dict(Dict(item as Dictionary<string, object>, "richItemRenderer"), "content");
                if (content == null) continue; // Continuation items are not content.
                string id, title;
                if (tab == "videos")
                {
                    var video = Dict(content, "compactVideoRenderer");
                    if (video != null)
                    {
                        id = Str(video, "videoId");
                        var runs = Value(Dict(video, "title"), "runs") as object[];
                        title = runs == null ? null : Str(runs.FirstOrDefault() as Dictionary<string, object>, "text");
                    }
                    else
                    {
                        var lockup = Dict(content, "lockupViewModel");
                        if (lockup == null || Str(lockup, "contentType") != "LOCKUP_CONTENT_TYPE_VIDEO")
                            throw new InvalidOperationException("YouTube video renderer changed");
                        id = Str(lockup, "contentId");
                        title = Str(Dict(Dict(Dict(lockup, "metadata"), "lockupMetadataViewModel"),
                            "title"), "content");
                        var command = Dict(Dict(Dict(lockup, "rendererContext"), "commandContext"), "onTap");
                        var url = Str(Dict(Dict(Dict(command, "innertubeCommand"), "commandMetadata"),
                            "webCommandMetadata"), "url");
                        if (url == null || !url.StartsWith("/watch?v=" + id, StringComparison.Ordinal))
                            throw new InvalidOperationException("YouTube video URL mismatch");
                    }
                }
                else
                {
                    var shorts = Dict(content, "shortsLockupViewModel");
                    if (shorts == null) throw new InvalidOperationException("YouTube Shorts renderer changed");
                    id = Str(Dict(Dict(Dict(shorts, "onTap"), "innertubeCommand"), "reelWatchEndpoint"), "videoId");
                    title = Str(Dict(Dict(shorts, "overlayMetadata"), "primaryText"), "content");
                }
                if (!Regex.IsMatch(id ?? "", @"^[A-Za-z0-9_-]{11}$") || string.IsNullOrWhiteSpace(title))
                    throw new InvalidOperationException("YouTube content identity missing");
                entries.Add(new YouTubeEntry { VideoId = id, Title = title,
                    Url = tab == "shorts" ? "https://www.youtube.com/shorts/" + id :
                        "https://www.youtube.com/watch?v=" + id });
            }
            return entries;
        }

        private static string InitialDataJson(string html)
        {
            var marker = Regex.Match(html ?? "", @"var\s+ytInitialData\s*=");
            if (!marker.Success) throw new InvalidOperationException("YouTube initial data missing");
            var start = marker.Index + marker.Length;
            while (start < html.Length && char.IsWhiteSpace(html[start])) start++;
            if (start >= html.Length || (html[start] != '{' && html[start] != '\''))
                throw new InvalidOperationException("Unsupported YouTube initial data format");
            if (html[start] == '\'')
            {
                var end = start + 1;
                for (; end < html.Length; end++)
                {
                    if (html[end] == '\\') { end++; continue; }
                    if (html[end] == '\'') break;
                }
                if (end >= html.Length) throw new InvalidOperationException("Incomplete YouTube initial data");
                return Regex.Unescape(html.Substring(start + 1, end - start - 1));
            }
            var depth = 0;
            var inString = false;
            for (var end = start; end < html.Length; end++)
            {
                var current = html[end];
                if (inString)
                {
                    if (current == '\\') { end++; continue; }
                    if (current == '"') inString = false;
                }
                else if (current == '"') inString = true;
                else if (current == '{') depth++;
                else if (current == '}' && --depth == 0)
                    return html.Substring(start, end - start + 1);
            }
            throw new InvalidOperationException("Incomplete YouTube initial data");
        }

        private static object Value(Dictionary<string, object> fields, string key)
        { object value; return fields != null && fields.TryGetValue(key, out value) ? value : null; }
        private static Dictionary<string, object> Dict(Dictionary<string, object> fields, string key) =>
            Value(fields, key) as Dictionary<string, object>;
        private static string Str(Dictionary<string, object> fields, string key) => Value(fields, key) as string;

        public void Dispose() { http.Dispose(); }
    }
}
