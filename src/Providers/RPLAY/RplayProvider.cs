using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class RplayProvider : IPlatformProvider
    {
        private readonly RplayClient client;
        internal RplayProvider(RplayClient client) { this.client = client; }
        public PlatformType Platform => PlatformType.Rplay;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.ViewerCount |
            PlatformCapabilities.LiveTitle | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(30);
        public bool CanHandleUrl(string url) => RplayClient.ParseLiveUrl(url) != null;
        public ReplayableNotification GetReplayableNotification(ChannelProfile profile) =>
            profile.State == LiveState.Live ? new ReplayableNotification { Type = NotificationEventType.LiveStarted } : null;
        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            var profile = RplayClient.CreateProfile(url);
            try { var live = await client.GetLiveListAsync(token).ConfigureAwait(false); RplayLiveInfo info;
                RplayClient.Apply(profile, live.TryGetValue(profile.CreatorOid, out info) ? info : null); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { Trace.WriteLine("[RPLAY] lookup failure: " + ex.GetType().Name); RplayClient.MarkUnknown(profile); }
            return profile;
        }
        public async Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token)
        {
            if (channels.Count == 0) return;
            Dictionary<string, RplayLiveInfo> live = null;
            try { live = await client.GetLiveListAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { Trace.WriteLine("[RPLAY] poll failure: " + ex.GetType().Name); }
            foreach (var channel in channels)
            {
                var previous = channel.State;
                RplayLiveInfo info;
                if (live == null) RplayClient.MarkUnknown(channel);
                else RplayClient.Apply(channel, live.TryGetValue(channel.CreatorOid, out info) ? info : null);
                updated(channel, previous, null);
            }
        }
    }

}
