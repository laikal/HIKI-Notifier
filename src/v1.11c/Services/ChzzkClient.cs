using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class ChzzkClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private const string Base = "https://api.chzzk.naver.com";

        public ChzzkClient(HttpMessageHandler handler = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HIKI-Notifier/" + AppInfo.Version);
        }

        public static string ParseLiveUrl(string input)
        {
            Uri uri;
            if (!Uri.TryCreate(input == null ? "" : input.Trim(), UriKind.Absolute, out uri) ||
                uri.Scheme != "https" || uri.Host != "chzzk.naver.com") return null;
            var match = Regex.Match(uri.AbsolutePath, @"^/live/([0-9a-fA-F]{32})/?$");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        private async Task<Dictionary<string, object>> GetContentAsync(string path, CancellationToken token)
        {
            using (var response = await http.GetAsync(Base + path, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var root = json.DeserializeObject(await response.Content.ReadAsStringAsync().ConfigureAwait(false)) as Dictionary<string, object>;
                if (root == null || Convert.ToInt32(root["code"]) != 200 || !root.ContainsKey("content")) throw new InvalidOperationException("Invalid CHZZK response");
                var content = root["content"] as Dictionary<string, object>;
                if (content == null) throw new InvalidOperationException("Missing CHZZK content");
                return content;
            }
        }

        private static string Str(Dictionary<string, object> content, string key)
        { object value; return content.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null; }

        public async Task<ChannelProfile> GetProfileAsync(string id, CancellationToken token)
        {
            var content = await GetContentAsync("/service/v1/channels/" + id, token).ConfigureAwait(false);
            var name = Str(content, "channelName");
            if (string.IsNullOrWhiteSpace(name) || Str(content, "channelId") != id) throw new InvalidOperationException("Invalid channel data");
            var profile = new ChannelProfile { ChannelId = id, LiveUrl = ChannelProfile.UrlFor(id), Name = name,
                ImageUrl = Str(content, "channelImageUrl"), NotificationsEnabled = true };
            await RefreshAsync(profile, token).ConfigureAwait(false);
            return profile;
        }

        public async Task RefreshAsync(ChannelProfile profile, CancellationToken token)
        {
            try
            {
                var content = await GetContentAsync("/polling/v3/channels/" + profile.ChannelId + "/live-status", token).ConfigureAwait(false);
                var status = Str(content, "status");
                if (status == "OPEN") profile.State = LiveState.Live;
                else if (status == "CLOSE") profile.State = LiveState.Offline;
                else profile.State = LiveState.Unknown;
                profile.LiveTitle = Str(content, "liveTitle");
                profile.Category = Str(content, "liveCategoryValue");
                object viewers;
                profile.ViewerCount = content.TryGetValue("concurrentUserCount", out viewers) && viewers != null ? (int?)Convert.ToInt32(viewers) : null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { profile.State = LiveState.Unknown; profile.LiveTitle = null; profile.Category = null; profile.ViewerCount = null; }
        }

        public void Dispose() { http.Dispose(); }
    }
}
