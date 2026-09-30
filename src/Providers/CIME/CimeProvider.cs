using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class CimeProvider : IPlatformProvider
    {
        private readonly Func<string, CancellationToken, Task<CimeStatus>> fetch;
        internal CimeProvider(CimeClient client) : this(client.GetStatusAsync) { ownedClient = client; }
        internal CimeProvider(Func<string, CancellationToken, Task<CimeStatus>> fetch) { this.fetch = fetch; }
        public PlatformType Platform => PlatformType.Cime;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.LiveTitle |
            PlatformCapabilities.ViewerCount | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(60);
        public bool CanHandleUrl(string url) => CimeClient.ParseHandle(url) != null;
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile) =>
            profile.State == LiveState.Live ? new ReplayableNotification { Type = NotificationEventType.LiveStarted } : null;
        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            var handle = CimeClient.ParseHandle(url);
            if (handle == null) throw new ArgumentException("Invalid CIME URL");
            var status = await fetch(handle, token).ConfigureAwait(false);
            if (status == null || status.State == LiveState.Unknown) throw new InvalidOperationException("CIME lookup failed");
            var profile = new ChannelProfile { Platform = Platform, ChannelId = handle, LiveUrl = CimeClient.UrlFor(handle),
                Name = status.Name ?? handle };
            Apply(profile, status);
            if (status.State == LiveState.Live) profile.ProviderState.LastLiveSessionId = status.SessionId;
            return profile;
        }
        public async Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token)
        {
            foreach (var profile in channels)
            {
                token.ThrowIfCancellationRequested();
                var previous = profile.State;
                CimeStatus status;
                try { status = await fetch(profile.ChannelId, token).ConfigureAwait(false) ?? new CimeStatus(); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch { status = new CimeStatus(); }
                profile.ProviderState = profile.ProviderState ?? new ProviderState();
                Apply(profile, status);
                if (status.State == LiveState.Live)
                {
                    if (string.IsNullOrEmpty(status.SessionId) || status.SessionId == profile.ProviderState.LastLiveSessionId)
                        previous = LiveState.Live;
                    profile.ProviderState.LastLiveSessionId = status.SessionId;
                }
                // Only an actual Offline -> Live may notify/open. Startup and Unknown recovery stay silent.
                updated(profile, previous, null);
            }
        }
        private static void Apply(ChannelProfile profile, CimeStatus status)
        {
            profile.State = status.State;
            profile.ViewerCount = status.State == LiveState.Live ? status.Viewers : null;
            if (status.State != LiveState.Live) return;
            profile.LiveTitle = status.Title;
            profile.LiveSessionId = status.SessionId;
        }
    }
}
