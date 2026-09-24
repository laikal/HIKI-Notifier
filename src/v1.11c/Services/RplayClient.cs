using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class RplayLiveInfo
    {
        public string CreatorNickname { get; set; }
        public string Title { get; set; }
        public int? ViewerCount { get; set; }
        public string ThumbnailUrl { get; set; }
    }

    internal sealed class RplayClient : IDisposable
    {
        private const string LiveListUrl = "https://api.rplay.live/live/livestreams";
        private readonly HttpClient http;

        public RplayClient(HttpMessageHandler handler = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HIKI-Notifier/" + AppInfo.Version);
        }

        public static string ParseLiveUrl(string input)
        {
            Uri uri;
            if (!Uri.TryCreate(input == null ? "" : input.Trim(), UriKind.Absolute, out uri) ||
                uri.Scheme != "https" || uri.Host != "rplay.live") return null;
            var match = Regex.Match(uri.AbsolutePath, @"^/live/([0-9a-fA-F]{24})/?$");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        public static string PlaceholderName(string creatorOid) => "RPLAY " + creatorOid.Substring(0, 8);

        public static ChannelProfile CreateProfile(string liveUrl)
        {
            var oid = ParseLiveUrl(liveUrl);
            if (oid == null) throw new ArgumentException("Invalid RPLAY URL", nameof(liveUrl));
            return new ChannelProfile { Platform = PlatformType.Rplay, ChannelId = oid, CreatorOid = oid,
                LiveUrl = liveUrl.Trim(), Name = PlaceholderName(oid), NotificationsEnabled = true };
        }

        public async Task<Dictionary<string, RplayLiveInfo>> GetLiveListAsync(CancellationToken token)
        {
            using (var response = await http.GetAsync(LiveListUrl, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return ParseLiveList(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }

        internal static Dictionary<string, RplayLiveInfo> ParseLiveList(string payload)
        {
            var root = new JavaScriptSerializer().DeserializeObject(payload);
            var items = LiveItems(root);
            var live = new Dictionary<string, RplayLiveInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var fields = item as Dictionary<string, object>;
                if (fields == null) throw new InvalidOperationException("Invalid RPLAY live item");
                var oid = Str(fields, "creatorOid");
                if (string.IsNullOrWhiteSpace(oid)) throw new InvalidOperationException("Missing RPLAY creatorOid");
                object viewers;
                live[oid] = new RplayLiveInfo {
                    CreatorNickname = Str(fields, "creatorNickname"), Title = Str(fields, "title"),
                    ThumbnailUrl = Str(fields, "thumbnailUrl"),
                    ViewerCount = fields.TryGetValue("viewerCount", out viewers) && viewers != null
                        ? (int?)Convert.ToInt32(viewers) : null
                };
            }
            return live;
        }

        private static object[] LiveItems(object root)
        {
            var items = root as object[];
            if (items != null) return items;
            var fields = root as Dictionary<string, object>;
            if (fields != null)
            {
                foreach (var key in new[] { "data", "content", "livestreams", "items", "list" })
                {
                    object value;
                    if (fields.TryGetValue(key, out value)) return LiveItems(value);
                }
            }
            throw new InvalidOperationException("Invalid RPLAY live list");
        }

        private static string Str(Dictionary<string, object> fields, string key)
        { object value; return fields.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null; }

        public static void Apply(ChannelProfile profile, RplayLiveInfo info)
        {
            if (info == null)
            {
                profile.State = LiveState.Offline; profile.LiveTitle = null; profile.ViewerCount = null;
                return;
            }
            profile.State = LiveState.Live;
            profile.LiveTitle = info.Title;
            profile.ViewerCount = info.ViewerCount;
            if (!string.IsNullOrWhiteSpace(info.CreatorNickname) &&
                (string.IsNullOrWhiteSpace(profile.Name) || profile.Name == PlaceholderName(profile.CreatorOid)))
                profile.Name = info.CreatorNickname;
        }

        public static void MarkUnknown(ChannelProfile profile)
        { profile.State = LiveState.Unknown; profile.LiveTitle = null; profile.ViewerCount = null; }

        public void Dispose() { http.Dispose(); }
    }
}
