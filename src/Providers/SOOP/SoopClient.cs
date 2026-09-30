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
    internal sealed class SoopStatus
    {
        public LiveState State { get; set; } = LiveState.Unknown;
        public string SessionId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
    }

    internal sealed class SoopClient : IDisposable
    {
        private readonly HttpClient http;
        public SoopClient()
        {
            var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
            http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 HIKI-Notifier/" + ProductVersion.DisplayVersion);
        }
        internal static string ParseChannelId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (Regex.IsMatch(value, "^[A-Za-z0-9_]{2,32}$")) return value.ToLowerInvariant();
            if (!Uri.TryCreate(value.Contains("://") ? value : "https://" + value, UriKind.Absolute, out var uri)) return null;
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            if (host != "play.sooplive.co.kr" && host != "play.sooplive.com" && host != "play.afreecatv.com") return null;
            var id = uri.AbsolutePath.Trim('/').Split('/')[0];
            return Regex.IsMatch(id ?? "", "^[A-Za-z0-9_]{2,32}$") ? id.ToLowerInvariant() : null;
        }
        internal static string UrlFor(string id) => "https://play.sooplive.co.kr/" + id;
        public async Task<SoopStatus> GetStatusAsync(string id, CancellationToken token)
        {
            try
            {
                using (var form = new FormUrlEncodedContent(new Dictionary<string,string> {
                    ["bid"]=id, ["bno"]="null", ["type"]="live", ["pwd"]="", ["player_type"]="html5",
                    ["stream_type"]="common", ["mode"]="landing", ["from_api"]="0" }))
                using (var response = await http.PostAsync("https://live.sooplive.co.kr/afreeca/player_live_api.php?bjid=" + Uri.EscapeDataString(id), form, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) { System.Diagnostics.Trace.WriteLine("[SOOP] HTTP " + (int)response.StatusCode); return new SoopStatus(); }
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return Parse(json);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("[SOOP] request/parse failure: " + ex.GetType().Name); return new SoopStatus(); }
        }
        internal static SoopStatus Parse(string json)
        {
            try
            {
                var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
                var channel = root?["CHANNEL"] as Dictionary<string,object>;
                if (channel == null || !channel.ContainsKey("RESULT")) return new SoopStatus();
                var result = Convert.ToString(channel["RESULT"]);
                if (result == "0") return new SoopStatus { State = LiveState.Offline };
                if (result != "1") return new SoopStatus();
                var bno = channel.ContainsKey("BNO") ? Convert.ToString(channel["BNO"]) : null;
                long broadcastNumber;
                if (!long.TryParse(bno, out broadcastNumber) || broadcastNumber <= 0) return new SoopStatus();
                return new SoopStatus { State=LiveState.Live, SessionId=bno,
                    Title=channel.ContainsKey("TITLE") ? Convert.ToString(channel["TITLE"]) : null,
                    Name=channel.ContainsKey("BJNICK") ? Convert.ToString(channel["BJNICK"]) : null };
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("[SOOP] request/parse failure: " + ex.GetType().Name); return new SoopStatus(); }
        }
        public void Dispose() { http.Dispose(); }
    }
}
