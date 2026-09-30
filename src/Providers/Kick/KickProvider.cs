using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed class KickProvider : IPlatformProvider
    {
        private readonly KickClient client;
        private ProviderMetadata metadata;
        public KickProvider() : this(new KickClient()) { }
        internal KickProvider(KickClient client) { this.client = client; }
        // Codes 0-5 are retained by existing providers. No Contracts enum change required.
        public PlatformType Platform => (PlatformType)6;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.LiveTitle |
            PlatformCapabilities.ViewerCount | PlatformCapabilities.ChannelPage | PlatformCapabilities.AutoOpen;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(60);
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            using (var stream = typeof(KickProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.Kick.png"))
            using (var memory = new MemoryStream())
            {
                if (stream == null) throw new InvalidDataException("Missing Kick logo");
                stream.CopyTo(memory);
                return new ProviderMetadata { Id = "kick", Platform = Platform, DisplayName = "Kick",
                    UrlTemplate = "https://kick.com/", Capabilities = Capabilities, PollInterval = PollInterval,
                    Order = 6, PollOrder = 6, PollIndependently = true, LogoPng = memory.ToArray() };
            }
        }
        public bool CanHandleUrl(string url) => KickClient.ParseSlug(url) != null;
        public void NormalizeStoredProfile(ChannelProfile profile)
        {
            var slug = KickClient.ParseSlug(profile.LiveUrl);
            if (slug == null || !string.Equals(slug, profile.ChannelId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid Kick channel");
            profile.ChannelId = slug; profile.LiveUrl = KickClient.UrlFor(slug);
        }
        public void PrepareForSave(ChannelProfile profile) { }
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile) => profile.State == LiveState.Live
            ? new ReplayableNotification { Type = NotificationEventType.LiveStarted } : null;
        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            var slug = KickClient.ParseSlug(url);
            if (slug == null) throw new ArgumentException("Invalid Kick URL");
            var status = await client.GetStatusAsync(slug, token).ConfigureAwait(false);
            if (status.State == LiveState.Unknown) throw new InvalidOperationException("Kick lookup failed");
            var profile = new ChannelProfile { Platform = Platform, ChannelId = slug, LiveUrl = KickClient.UrlFor(slug), Name = status.Name ?? slug };
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
                var status = await client.GetStatusAsync(profile.ChannelId, token).ConfigureAwait(false);
                profile.ProviderState = profile.ProviderState ?? new ProviderState();
                Apply(profile, status);
                if (status.State == LiveState.Live)
                {
                    if (string.IsNullOrEmpty(status.SessionId) || status.SessionId == profile.ProviderState.LastLiveSessionId)
                        previous = LiveState.Live;
                    profile.ProviderState.LastLiveSessionId = status.SessionId;
                }
                // Match the existing CIME contract: notify/open only on confirmed Offline -> new Live.
                // Startup and Unknown recovery remain silent; errors never clear the last session.
                updated(profile, previous, null);
            }
        }
        private static void Apply(ChannelProfile profile, KickStatus status)
        {
            profile.State = status.State;
            profile.ViewerCount = status.State == LiveState.Live ? status.Viewers : null;
            if (status.State == LiveState.Unknown) return;
            profile.Name = status.Name ?? profile.Name;
            if (status.State != LiveState.Live) return;
            profile.LiveTitle = status.Title; profile.LiveSessionId = status.SessionId; profile.LiveStartedAt = status.StartedAt;
        }
        public void Dispose() { client.Dispose(); }
    }
}
