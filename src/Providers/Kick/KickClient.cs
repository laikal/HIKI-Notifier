using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class KickStatus
    {
        public LiveState State { get; set; } = LiveState.Unknown;
        public string Name { get; set; }
        public string Title { get; set; }
        public int? Viewers { get; set; }
        public string SessionId { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
    }

    internal sealed class KickClient : IDisposable
    {
        private readonly HttpClient http;
        public KickClient(HttpMessageHandler handler = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler {
                UseCookies = false, AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 HIKI-Notifier/" + ProductVersion.DisplayVersion);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public static string ParseSlug(string input)
        {
            if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                (uri.Host != "kick.com" && uri.Host != "www.kick.com") || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
                return null;
            var match = Regex.Match(uri.AbsolutePath, @"^/([A-Za-z0-9_-]{1,64})/?$");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }
        public static string UrlFor(string slug) => "https://kick.com/" + slug;

        public async Task<KickStatus> GetStatusAsync(string slug, CancellationToken token)
        {
            if (ParseSlug(UrlFor(slug)) != slug) return new KickStatus();
            try
            {
                using (var response = await http.GetAsync("https://kick.com/api/v2/channels/" + slug, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    { Trace.WriteLine("[Kick] HTTP " + (int)response.StatusCode); return new KickStatus(); }
                    return Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), slug);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            { Trace.WriteLine("[Kick] request failure: " + ex.GetType().Name); return new KickStatus(); }
        }

        internal static KickStatus Parse(string json, string expectedSlug)
        {
            try
            {
                var root = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 }.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || !string.Equals(Text(root, "slug"), expectedSlug, StringComparison.OrdinalIgnoreCase) ||
                    !root.ContainsKey("livestream")) throw new FormatException("Missing target channel data");
                var channelId = Identifier(root, "id");
                if (channelId == null) throw new FormatException("Missing channel identity");
                var name = expectedSlug;
                if (root.TryGetValue("user", out var user) && user is Dictionary<string, object> userData)
                    name = Text(userData, "username") ?? name;
                if (root["livestream"] == null) return new KickStatus { State = LiveState.Offline, Name = name };
                var live = root["livestream"] as Dictionary<string, object>;
                if (live == null || live.Count == 0) throw new FormatException("Invalid livestream object");
                var boundChannel = Identifier(live, "channel_id");
                if (boundChannel != null && boundChannel != channelId) throw new FormatException("Wrong livestream channel");
                var id = Identifier(live, "id");
                var streamSlug = Text(live, "slug");
                var created = Text(live, "created_at");
                DateTimeOffset? started = null;
                if (!string.IsNullOrWhiteSpace(created) && DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)) started = timestamp;
                var title = Text(live, "session_title");
                int? viewers = null;
                if (live.TryGetValue("viewer_count", out var count) && count != null)
                {
                    if (!int.TryParse(Convert.ToString(count, CultureInfo.InvariantCulture), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var parsed) || parsed < 0) throw new FormatException("Invalid viewer count");
                    viewers = parsed;
                }
                if (id == null && streamSlug == null && created == null && title == null && !viewers.HasValue)
                    throw new FormatException("Unrecognized livestream object");
                return new KickStatus { State = LiveState.Live, Name = name, Title = title, Viewers = viewers, StartedAt = started,
                    SessionId = id != null ? "id:" + id : !string.IsNullOrWhiteSpace(streamSlug) ? "slug:" + streamSlug :
                        started.HasValue ? "start:" + started.Value.ToString("O", CultureInfo.InvariantCulture) : null };
            }
            catch (Exception ex)
            { Trace.WriteLine("[Kick] parse failure: " + ex.GetType().Name); return new KickStatus(); }
        }
        private static string Text(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return null;
            if (!(value is string)) throw new FormatException("Invalid " + key);
            return (string)value;
        }
        private static string Identifier(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return null;
            if (!long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.None,
                CultureInfo.InvariantCulture, out var id) || id <= 0) throw new FormatException("Invalid " + key);
            return id.ToString(CultureInfo.InvariantCulture);
        }
        public void Dispose() { http.Dispose(); }
    }
}
