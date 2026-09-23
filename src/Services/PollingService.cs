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
        private readonly ChzzkClient client;
        private readonly RplayClient rplay;
        private readonly Func<IList<ChannelProfile>> profiles;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Task loop;
        public event Action<ChannelProfile, LiveState> StateChanged;

        public PollingService(ChzzkClient client, RplayClient rplay, Func<IList<ChannelProfile>> profiles)
        { this.client = client; this.rplay = rplay; this.profiles = profiles; }

        public void Start() { if (loop == null) loop = RunAsync(cancellation.Token); }
        internal static bool ShouldNotify(LiveState previous, LiveState current) => previous == LiveState.Offline && current == LiveState.Live;
        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var snapshot = profiles().ToArray();
                    foreach (var profile in snapshot.Where(p => p.Platform == PlatformType.Chzzk))
                    {
                        token.ThrowIfCancellationRequested();
                        var previous = profile.State;
                        await client.RefreshAsync(profile, token).ConfigureAwait(false);
                        StateChanged?.Invoke(profile, previous);
                    }
                    var rplayProfiles = snapshot.Where(p => p.Platform == PlatformType.Rplay).ToArray();
                    if (rplayProfiles.Length != 0)
                    {
                        Dictionary<string, RplayLiveInfo> live = null;
                        try { live = await rplay.GetLiveListAsync(token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception) { }
                        foreach (var profile in rplayProfiles)
                        {
                            var previous = profile.State;
                            RplayLiveInfo info;
                            if (live == null) RplayClient.MarkUnknown(profile);
                            else RplayClient.Apply(profile, live.TryGetValue(profile.CreatorOid, out info) ? info : null);
                            StateChanged?.Invoke(profile, previous);
                        }
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
