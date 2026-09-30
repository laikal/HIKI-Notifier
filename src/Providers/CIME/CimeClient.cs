using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class CimeStatus
    {
        public LiveState State { get; set; } = LiveState.Unknown;
        public string Name { get; set; }
        public string Title { get; set; }
        public string SessionId { get; set; }
        public int? Viewers { get; set; }
    }

    internal sealed class CimeClient : IDisposable
    {
        private readonly HttpClient http;
        public CimeClient() : this(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false }) { }
        internal CimeClient(HttpMessageHandler handler)
        {
            http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 HIKI-Notifier/" + ProductVersion.DisplayVersion);
        }
        public static string ParseHandle(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim();
            if (!Uri.TryCreate(input.Contains("://") ? input : "https://" + input, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || uri.Host != "ci.me" ||
                !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return null;
            var match = Regex.Match(Uri.UnescapeDataString(uri.AbsolutePath), @"^/@([A-Za-z0-9_-]+)(?:/live)?/?$");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }
        public static string UrlFor(string handle) => "https://ci.me/@" + handle + "/live";
        public async Task<CimeStatus> GetStatusAsync(string handle, CancellationToken token)
        {
            try
            {
                using (var response = await http.GetAsync(UrlFor(handle), token).ConfigureAwait(false))
                {
                    Debug.WriteLine("[CIME] HTTP " + (int)response.StatusCode);
                    if (!response.IsSuccessStatusCode) Trace.WriteLine("[CIME] HTTP " + (int)response.StatusCode);
                    if (!response.IsSuccessStatusCode ||
                        ParseHandle(response.RequestMessage?.RequestUri?.AbsoluteUri ?? UrlFor(handle)) != handle)
                        return new CimeStatus();
                    return ParseHtml(await response.Content.ReadAsStringAsync().ConfigureAwait(false), handle);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { Trace.WriteLine("[CIME] " + ex.GetType().Name); return new CimeStatus(); }
        }
        internal static CimeStatus ParseHtml(string html, string handle)
        {
            try
            {
                CimeStatus found = null;
                // Read only the route's bodyData.live, never sidebar/recommended channel data.
                foreach (Match script in Regex.Matches(html ?? "", @"<script\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    if (!script.Groups[1].Value.Contains("window.__RUNE_DATA__")) continue;
                    foreach (Match match in Regex.Matches(script.Groups[1].Value,
                        @"\.push\(JSON\.parse\('((?:\\.|[^'\\])*)'\)\)", RegexOptions.Singleline))
                    {
                        var serializer = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
                        var root = serializer.DeserializeObject(DecodeJsString(match.Groups[1].Value)) as Dictionary<string, object>;
                        if (Text(root, "key") != "/@[:slug]/live") continue;
                        if (!(Value(root, "args") is object[] args)) return new CimeStatus();
                        foreach (var item in args)
                        {
                            var arg = item as Dictionary<string, object>;
                            if (Text(arg, "path") != "/@[:slug]/live") continue;
                            var body = Value(arg, "bodyData") as Dictionary<string, object>;
                            var live = Value(body, "live") as Dictionary<string, object>;
                            var channel = Value(live, "channel") as Dictionary<string, object>;
                            if (found != null || !string.Equals(Text(channel, "slug"), handle, StringComparison.OrdinalIgnoreCase))
                                return new CimeStatus();
                            var state = Text(live, "state");
                            // The route's live.state is primary; channel.isLive can lag behind it.
                            if (Value(channel, "isLive") is bool isLive && isLive != (state == "ACTIVE"))
                                Debug.WriteLine("[CIME] channel.isLive differs from primary live.state");
                            if (state == "INACTIVE")
                                found = new CimeStatus { State = LiveState.Offline, Name = Text(channel, "name") };
                            else if (state == "ACTIVE")
                            {
                                var id = Text(live, "id");
                                // live.id is reused by the channel; openedAt identifies each broadcast.
                                var opened = Text(live, "openedAt");
                                if (string.IsNullOrEmpty(id) || !DateTimeOffset.TryParse(opened, CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal, out var started)) return new CimeStatus();
                                int? viewers = null;
                                if (int.TryParse(Convert.ToString(Value(live, "curViewerCnt"), CultureInfo.InvariantCulture), out var count) && count >= 0)
                                    viewers = count;
                                found = new CimeStatus { State = LiveState.Live, Name = Text(channel, "name"),
                                    Title = Text(live, "title"), Viewers = viewers,
                                    SessionId = id + ":" + started.ToUniversalTime().ToString("O") };
                            }
                            else return new CimeStatus();
                        }
                    }
                }
                return found ?? new CimeStatus();
            }
            catch (Exception ex) { Trace.WriteLine("[CIME parser] " + ex.GetType().Name); return new CimeStatus(); }
        }
        private static object Value(Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out var value) ? value : null;
        private static string Text(Dictionary<string, object> obj, string key) => Value(obj, key) as string;
        private static string DecodeJsString(string value)
        {
            var result = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '\\') { result.Append(c); continue; }
                if (++i >= value.Length) throw new FormatException();
                c = value[i];
                switch (c)
                {
                    case '\\': case '\'': case '"': case '/': result.Append(c); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'u': case 'x':
                        var length = c == 'u' ? 4 : 2;
                        result.Append((char)int.Parse(value.Substring(i + 1, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += length; break;
                    default: throw new FormatException();
                }
            }
            return result.ToString();
        }
        public void Dispose() => http.Dispose();
    }
}
