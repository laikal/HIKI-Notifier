using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed partial class SoopProvider : IPlatformProvider
    {
        private readonly Func<string, CancellationToken, Task<SoopStatus>> fetch;
        internal SoopProvider(SoopClient client) : this(client.GetStatusAsync) { ownedClient = client; }
        internal SoopProvider(Func<string, CancellationToken, Task<SoopStatus>> fetch) { this.fetch = fetch; }
        public PlatformType Platform => PlatformType.Soop;
        public PlatformCapabilities Capabilities => PlatformCapabilities.LiveStatus | PlatformCapabilities.LiveTitle | PlatformCapabilities.ChannelPage;
        public TimeSpan PollInterval => TimeSpan.FromSeconds(60);
        public bool CanHandleUrl(string url) => SoopClient.ParseChannelId(url) != null;
        public ReplayableNotification GetReplayableNotification(ChannelProfile p) => p.State == LiveState.Live ? new ReplayableNotification { Type=NotificationEventType.LiveStarted } : null;
        public async Task<ChannelProfile> CreateAsync(string url, CancellationToken token)
        {
            var id=SoopClient.ParseChannelId(url); if(id==null) throw new ArgumentException("Invalid SOOP channel");
            var status=await fetch(id,token).ConfigureAwait(false);
            var p=new ChannelProfile { Platform=PlatformType.Soop, ChannelId=id, LiveUrl=SoopClient.UrlFor(id), Name=status.Name ?? id };
            Apply(p,status); if(status.State==LiveState.Live) p.ProviderState.LastLiveSessionId=status.SessionId;
            return p;
        }
        public async Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile,LiveState,ContentEntry> updated, CancellationToken token)
        {
            foreach(var p in channels)
            {
                var previous=p.State; SoopStatus s;
                try { s=await fetch(p.ChannelId,token).ConfigureAwait(false) ?? new SoopStatus(); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch { s=new SoopStatus(); }
                Apply(p,s);
                if(s.State==LiveState.Live)
                {
                    var seen=p.ProviderState.LastLiveSessionId;
                    if(!string.Equals(seen,s.SessionId,StringComparison.Ordinal))
                    { previous=LiveState.Offline; p.ProviderState.LastLiveSessionId=s.SessionId; }
                    else if(previous!=LiveState.Live) previous=LiveState.Unknown;
                }
                updated(p,previous,null);
            }
        }
        private static void Apply(ChannelProfile p, SoopStatus s)
        {
            p.State=s.State;
            if(s.State==LiveState.Live) { p.LiveSessionId=s.SessionId; p.LiveTitle=s.Title; if(!string.IsNullOrWhiteSpace(s.Name)) p.Name=s.Name; }
        }
    }
}
