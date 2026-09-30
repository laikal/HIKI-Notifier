using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class YouTubeProvider : IPlatformProvider
    {
        private readonly YouTubeClient client;
        private readonly Dictionary<string, int> failures = new Dictionary<string, int>();
        [Conditional("DEBUG")]
        private static void Log(string message) => Debug.WriteLine("[YouTube poll] " + DateTimeOffset.Now.ToString("O") + " " + message);
        internal YouTubeProvider(YouTubeClient client) { this.client = client; }
        public PlatformType Platform => PlatformType.YouTube;
        public PlatformCapabilities Capabilities => PlatformCapabilities.NewContent |
            PlatformCapabilities.LatestContentTitle | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(180);
        public bool CanHandleUrl(string url) => YouTubeClient.IsChannelUrl(url);
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile)
        {
            var state = profile.ProviderState;
            if (state == null || state.BaselinePending || string.IsNullOrWhiteSpace(state.LastSeenContentId) ||
                string.IsNullOrWhiteSpace(state.LatestContentTitle) || string.IsNullOrWhiteSpace(state.LatestContentUrl)) return null;
            if (!System.Text.RegularExpressions.Regex.IsMatch(state.LastSeenContentId, "^[A-Za-z0-9_-]{11}$")) return null;
            if (!Uri.TryCreate(state.LatestContentUrl, UriKind.Absolute, out var url) ||
                (url.Host != "youtube.com" && url.Host != "www.youtube.com" && url.Host != "youtu.be")) return null;
            var id = state.LastSeenContentId;
            if (!(url.AbsolutePath == "/shorts/" + id ||
                url.AbsolutePath == "/watch" && url.Query.Contains("v=" + id) ||
                url.Host == "youtu.be" && url.AbsolutePath == "/" + id)) return null;
            return new ReplayableNotification { Type = NotificationEventType.NewContent,
                Content = new ContentEntry { ContentId = state.LastSeenContentId,
                    Title = state.LatestContentTitle, Url = state.LatestContentUrl } };
        }
        internal static ContentEntry NewContent(YouTubeFeed feed, string previous)
        {
            var latest = feed.Entries.FirstOrDefault();
            return latest != null && !string.IsNullOrEmpty(previous) && latest.ContentId != previous &&
                feed.Entries.Any(e => e.ContentId == previous) ? latest : null;
        }
        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            try { return await CreateCoreAsync(url, token).ConfigureAwait(false); }
            catch (YouTubeResolveException ex) { throw new ProviderLookupException(
                ex.Failure == YouTubeResolveFailure.ChannelNotFound ? "YouTubeResolveFailed" : "YouTubeNetworkError", ex); }
        }
        private async Task<ChannelProfile> CreateCoreAsync(string url, CancellationToken token)
        {
            var id = await client.ResolveChannelIdAsync(url, token).ConfigureAwait(false);
            var profile = new ChannelProfile { Platform = PlatformType.YouTube, ChannelId = id, LiveUrl = url.Trim(),
                NotificationsEnabled = true, ProviderState = new ProviderState { BaselinePending = true } };
            try
            {
                var feed = await client.GetFeedAsync(id, token).ConfigureAwait(false);
                SetInitialBaseline(profile, feed);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                try
                {
                    var snapshot = await client.GetHtmlSnapshotAsync(profile.LiveUrl, id, token).ConfigureAwait(false);
                    ApplyHtml(profile, snapshot); // First successful snapshot only establishes a baseline.
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception) { /* Preserve the resolved ID and retry later. */ }
            }
            return profile;
        }

        internal static void SetInitialBaseline(ChannelProfile profile, YouTubeFeed feed)
        {
            var latest = feed.Entries.FirstOrDefault();
            var state = profile.ProviderState ?? (profile.ProviderState = new ProviderState());
            if (latest != null)
            {
                state.LastSeenContentId = latest.ContentId;
                state.LatestContentTitle = latest.Title;
                state.LatestContentUrl = latest.Url;
                state.BaselinePending = false;
                Remember(state, feed.Entries.Select(e => e.ContentId));
            }
            else state.BaselinePending = true;
            if (!profile.IsBuiltIn && !string.IsNullOrWhiteSpace(feed.ChannelName))
                profile.Name = feed.ChannelName;
            profile.LiveTitle = state.LatestContentTitle;
            profile.LatestContentUrl = state.LatestContentUrl;
        }

        private static void Remember(ProviderState state, IEnumerable<string> ids)
        {
            state.RecentContentIds = state.RecentContentIds ?? new List<string>();
            foreach (var id in ids.Reverse())
            {
                if (string.IsNullOrEmpty(id)) continue;
                state.RecentContentIds.Remove(id);
                state.RecentContentIds.Insert(0, id);
            }
            if (state.RecentContentIds.Count > 20)
                state.RecentContentIds.RemoveRange(20, state.RecentContentIds.Count - 20);
        }

        private static ContentEntry ApplyFeed(ChannelProfile channel, YouTubeFeed feed)
        {
            var state = channel.ProviderState ?? (channel.ProviderState = new ProviderState());
            var latest = feed.Entries.FirstOrDefault();
            if (string.IsNullOrEmpty(state.LastSeenContentId))
            {
                SetInitialBaseline(channel, feed);
                return null;
            }
            state.BaselinePending = false;
            ContentEntry newlyPublished = null;
            if (latest != null)
            {
                var known = new HashSet<string>(state.RecentContentIds ?? new List<string>());
                known.Add(state.LastSeenContentId);
                if (!known.Contains(latest.ContentId))
                {
                    if (feed.Entries.Any(e => known.Contains(e.ContentId))) newlyPublished = latest;
                    state.LastSeenContentId = latest.ContentId;
                    state.LatestContentTitle = latest.Title;
                    state.LatestContentUrl = latest.Url;
                }
                else if (latest.ContentId == state.LastSeenContentId)
                {
                    state.LatestContentTitle = latest.Title;
                    state.LatestContentUrl = latest.Url;
                }
                Remember(state, feed.Entries.Select(e => e.ContentId));
            }
            if (!channel.IsBuiltIn && !string.IsNullOrWhiteSpace(feed.ChannelName))
                channel.Name = feed.ChannelName;
            channel.LiveTitle = state.LatestContentTitle;
            channel.LatestContentUrl = state.LatestContentUrl;
            return newlyPublished;
        }

        private static void FindHtmlNewContent(List<ContentEntry> entries, string tabHead,
            HashSet<string> known, List<ContentEntry> found)
        {
            if (entries == null || entries.Count == 0) return;
            var anchor = !string.IsNullOrEmpty(tabHead) ? entries.FindIndex(e => e.ContentId == tabHead) : -1;
            if (anchor < 0) anchor = entries.FindIndex(e => known.Contains(e.ContentId));
            if (anchor < 0) return; // No trusted boundary: establish this tab without announcing old items.
            foreach (var entry in entries.Take(anchor).Reverse())
                if (!known.Contains(entry.ContentId) && found.All(e => e.ContentId != entry.ContentId))
                    found.Add(entry);
        }

        private static List<ContentEntry> ApplyHtml(ChannelProfile channel, YouTubeHtmlSnapshot snapshot)
        {
            var state = channel.ProviderState ?? (channel.ProviderState = new ProviderState());
            var known = new HashSet<string>(state.RecentContentIds ?? new List<string>());
            if (!string.IsNullOrEmpty(state.LastSeenContentId)) known.Add(state.LastSeenContentId);
            var found = new List<ContentEntry>();
            if (known.Count != 0)
            {
                FindHtmlNewContent(snapshot.Videos, state.LatestVideosId, known, found);
                FindHtmlNewContent(snapshot.Shorts, state.LatestShortsId, known, found);
            }
            if (snapshot.Videos?.Count > 0) state.LatestVideosId = snapshot.Videos[0].ContentId;
            if (snapshot.Shorts?.Count > 0) state.LatestShortsId = snapshot.Shorts[0].ContentId;
            if (string.IsNullOrEmpty(state.LastSeenContentId))
            {
                var baseline = snapshot.Shorts?.FirstOrDefault() ?? snapshot.Videos?.FirstOrDefault();
                if (baseline != null)
                {
                    state.LastSeenContentId = baseline.ContentId;
                    state.LatestContentTitle = baseline.Title;
                    state.LatestContentUrl = baseline.Url;
                    state.BaselinePending = false;
                }
                found.Clear();
            }
            else state.BaselinePending = false;
            if (found.Count > 0)
            {
                var latest = found[found.Count - 1];
                state.LastSeenContentId = latest.ContentId;
                state.LatestContentTitle = latest.Title;
                state.LatestContentUrl = latest.Url;
            }
            else
            {
                var current = (snapshot.Videos ?? new List<ContentEntry>()).Concat(snapshot.Shorts ?? new List<ContentEntry>())
                    .FirstOrDefault(e => e.ContentId == state.LastSeenContentId);
                if (current != null) { state.LatestContentTitle = current.Title; state.LatestContentUrl = current.Url; }
            }
            Remember(state, (snapshot.Videos ?? new List<ContentEntry>()).Take(8).Select(e => e.ContentId)
                .Concat((snapshot.Shorts ?? new List<ContentEntry>()).Take(8).Select(e => e.ContentId))
                .Concat(found.Select(e => e.ContentId)));
            channel.LiveTitle = state.LatestContentTitle;
            channel.LatestContentUrl = state.LatestContentUrl;
            return found;
        }
        public async Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token)
        {
            foreach (var channel in channels)
            {
                token.ThrowIfCancellationRequested();
                var key = channel.Id ?? channel.LiveUrl;
                var oldId = channel.ProviderState?.LastSeenContentId;
                int failed = 0;
                if (key != null) failures.TryGetValue(key, out failed);
                Log("start channel=" + channel.ChannelId + " old=" + oldId + " failures=" + failed + " interval=" + PollInterval.TotalSeconds + "s");
                try
                {
                    if (string.IsNullOrWhiteSpace(channel.ChannelId))
                    {
                        channel.ChannelId = await client.ResolveChannelIdAsync(channel.LiveUrl, token).ConfigureAwait(false);
                        var resolvedState = channel.ProviderState ?? (channel.ProviderState = new ProviderState());
                        resolvedState.BaselinePending = string.IsNullOrEmpty(resolvedState.LastSeenContentId);
                        // Persist the resolved ID even if the following feed request fails.
                        updated(channel, channel.State, null);
                    }
                    YouTubeFeed feed = null;
                    try { feed = await client.GetFeedAsync(channel.ChannelId, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex) { Log("RSS unavailable channel=" + channel.ChannelId + " error=" + ex); }
                    List<ContentEntry> newlyPublished;
                    string source, latestId;
                    if (feed != null)
                    {
                        var found = ApplyFeed(channel, feed);
                        newlyPublished = found == null ? new List<ContentEntry>() : new List<ContentEntry> { found };
                        source = "RSS"; latestId = feed.Entries.FirstOrDefault()?.ContentId;
                    }
                    else
                    {
                        var snapshot = await client.GetHtmlSnapshotAsync(channel.LiveUrl, channel.ChannelId, token)
                            .ConfigureAwait(false);
                        newlyPublished = ApplyHtml(channel, snapshot);
                        source = "HTML";
                        latestId = snapshot.Shorts?.FirstOrDefault()?.ContentId ?? snapshot.Videos?.FirstOrDefault()?.ContentId;
                    }
                    if (key != null) failures.Remove(key);
                    Log("success source=" + source + " channel=" + channel.ChannelId + " latest=" + latestId + " old=" + oldId +
                        " result=" + (newlyPublished.Count > 0 ? "new " + newlyPublished.Count : channel.ProviderState.BaselinePending ? "pending" :
                            string.IsNullOrEmpty(oldId) ? "baseline" : "unchanged") +
                        " failures=0 interval=" + PollInterval.TotalSeconds + "s reset=" + (failed > 0));
                    if (newlyPublished.Count == 0) updated(channel, channel.State, null);
                    else foreach (var entry in newlyPublished) updated(channel, channel.State, entry);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    if (key != null) failures[key] = failed + 1;
                    Log("failure channel=" + channel.ChannelId + " old=" + oldId + " failures=" +
                        (failed + 1) + " interval=" + PollInterval.TotalSeconds + "s error=" + ex);
                    // Keep the established baseline and retry at the normal interval.
                }
            }
        }
    }

}
