using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class ChzzkProvider : IPlatformProvider
    {
        private readonly ChzzkClient client;
        internal ChzzkProvider(ChzzkClient client) { this.client = client; }
        public PlatformType Platform => PlatformType.Chzzk;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.ViewerCount |
            PlatformCapabilities.LiveTitle | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(30);
        public bool CanHandleUrl(string url) => ChzzkClient.ParseLiveUrl(url) != null;
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile) =>
            profile.State == LiveState.Live ? new ReplayableNotification { Type = NotificationEventType.LiveStarted } : null;
        public Task<ChannelProfile> CreateAsync(string url, CancellationToken token) => client.GetProfileAsync(ChzzkClient.ParseLiveUrl(url), token);
        public async Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token)
        {
            foreach (var channel in channels)
            {
                token.ThrowIfCancellationRequested();
                var previous = channel.State;
                await client.RefreshAsync(channel, token).ConfigureAwait(false);
                updated(channel, previous, null);
            }
        }
    }

}
