using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class PollingService : IDisposable
    {
        private readonly PlatformRegistry registry;
        private readonly Func<IList<ChannelProfile>> profiles;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Dictionary<PlatformType, DateTime> due = new Dictionary<PlatformType, DateTime>();
        private Task loop;
        public event Action<ChannelProfile, LiveState, YouTubeEntry> ProfileUpdated;

        public PollingService(PlatformRegistry registry, Func<IList<ChannelProfile>> profiles)
        { this.registry = registry; this.profiles = profiles; }

        public void Start() { if (loop == null) loop = RunAsync(cancellation.Token); }
        internal static bool ShouldNotify(LiveState previous, LiveState current) => previous == LiveState.Offline && current == LiveState.Live;
        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var snapshot = profiles().ToArray();
                    foreach (var provider in registry.Providers)
                    {
                        DateTime next;
                        if (due.TryGetValue(provider.Platform, out next) && DateTime.UtcNow < next) continue;
                        due[provider.Platform] = DateTime.UtcNow + provider.PollInterval;
                        var channels = snapshot.Where(p => p.Platform == provider.Platform).ToArray();
                        if (channels.Length != 0)
                            await provider.PollAsync(channels, (p, previous, entry) => ProfileUpdated?.Invoke(p, previous, entry), token).ConfigureAwait(false);
                    }
                    await Task.Delay(30000, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
        public async Task StopAsync() { cancellation.Cancel(); if (loop != null) await loop.ConfigureAwait(false); }
        public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
    }
}
