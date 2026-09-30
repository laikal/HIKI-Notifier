using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class TwitchProvider : IPlatformProvider
    {
        private sealed class Observations
        {
            public bool SawLive;
            public LiveState LastConfirmed = LiveState.Unknown;
            public readonly HashSet<string> Sessions = new HashSet<string>();
            public readonly Queue<string> SessionOrder = new Queue<string>();
            public int Failures;
            public DateTimeOffset RetryAt;
        }
        private readonly Func<string, CancellationToken, Task<TwitchStatus>> fetch;
        private readonly Func<DateTimeOffset> now;
        private readonly Dictionary<string, Observations> observations = new Dictionary<string, Observations>();
        internal TwitchProvider(TwitchClient client) : this(client.GetStatusAsync) { ownedClient = client; }
        internal TwitchProvider(Func<string, CancellationToken, Task<TwitchStatus>> fetch, Func<DateTimeOffset> now = null)
        { this.fetch = fetch; this.now = now ?? (() => DateTimeOffset.UtcNow); }
        public PlatformType Platform => PlatformType.Twitch;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.LiveTitle |
            PlatformCapabilities.ViewerCount | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(60);
        public bool CanHandleUrl(string url) => TwitchClient.ParseLogin(url) != null;
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile) =>
            profile.State == LiveState.Live ? new ReplayableNotification { Type = NotificationEventType.LiveStarted } : null;

        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            var login = TwitchClient.ParseLogin(url);
            if (login == null) throw new ArgumentException("Invalid Twitch channel URL");
            var status = await fetch(login, token).ConfigureAwait(false);
            if (status == null || status.LookupState == TwitchLookupState.NotFound)
                throw new InvalidOperationException("Twitch channel was not found");
            if (!status.ChannelExists) throw new InvalidOperationException("Twitch channel could not be verified");
            var profile = new ChannelProfile { Platform = PlatformType.Twitch, ChannelId = login,
                LiveUrl = TwitchClient.UrlFor(login), Name = status.Name ?? login };
            Apply(profile, status);
            return profile;
        }

        public async Task PollAsync(IList<ChannelProfile> channels,
            Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token)
        {
            foreach (var profile in channels)
            {
                token.ThrowIfCancellationRequested();
                var key = profile.Id ?? profile.LiveUrl;
                if (key == null) continue;
                if (!observations.TryGetValue(key, out var seen))
                    observations[key] = seen = new Observations { LastConfirmed = profile.State };
                if (now() < seen.RetryAt) continue;
                var previous = profile.State;
                // Remember a LIVE result obtained during registration as well as polling.
                if (previous == LiveState.Live) Remember(seen, profile.LiveSessionId);
                TwitchStatus status;
                try { status = await fetch(profile.ChannelId, token).ConfigureAwait(false) ?? new TwitchStatus(); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception) { status = new TwitchStatus(); }
                Apply(profile, status);
                if (status.State == LiveState.Unknown)
                {
                    // A failed poll does not discard a previously confirmed NotLive result or rearm a live session.
                    if (seen.LastConfirmed == LiveState.Offline) profile.State = LiveState.Offline;
                    seen.Failures = Math.Min(3, seen.Failures + 1);
                    seen.RetryAt = now().AddSeconds(seen.Failures == 1 ? 60 : seen.Failures == 2 ? 120 : 300);
                }
                else { seen.Failures = 0; seen.RetryAt = now().Add(PollInterval); }
                if (profile.State == LiveState.Live)
                {
                    // Map Twitch's confirmed transition to the existing notification/AutoOpen contract.
                    // Recovery from an error may prove a new session without an intervening NotLive sample.
                    if (previous == LiveState.Unknown && (seen.LastConfirmed == LiveState.Offline ||
                        (seen.LastConfirmed == LiveState.Live && seen.Sessions.Count > 0 &&
                         !string.IsNullOrEmpty(profile.LiveSessionId) && !seen.Sessions.Contains(profile.LiveSessionId))))
                        previous = LiveState.Offline;
                    if (previous == LiveState.Offline && seen.SawLive &&
                        (string.IsNullOrEmpty(profile.LiveSessionId) || seen.Sessions.Contains(profile.LiveSessionId)))
                        previous = LiveState.Unknown;
                    Remember(seen, profile.LiveSessionId);
                }
                if (status.State != LiveState.Unknown) seen.LastConfirmed = status.State;
                updated(profile, previous, null);
            }
        }

        private static void Remember(Observations seen, string session)
        {
            seen.SawLive = true;
            if (string.IsNullOrEmpty(session) || !seen.Sessions.Add(session)) return;
            seen.SessionOrder.Enqueue(session);
            if (seen.SessionOrder.Count > 20) seen.Sessions.Remove(seen.SessionOrder.Dequeue());
        }
        private static void Apply(ChannelProfile profile, TwitchStatus status)
        {
            profile.State = status.State;
            profile.ViewerCount = status.State == LiveState.Live ? status.Viewers : null;
            if (status.State == LiveState.Live)
            {
                profile.LiveTitle = status.Title;
                profile.Category = status.Category;
                profile.LiveSessionId = status.SessionId;
                profile.LiveStartedAt = status.StartedAt;
            }
            // Failure never clears the session/baseline or masquerades as an offline result.
        }
    }
}
