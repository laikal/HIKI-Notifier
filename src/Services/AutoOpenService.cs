using System;
using System.Collections.Generic;
using System.Diagnostics;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class AutoOpenService
    {
        private sealed class OpenState
        {
            public bool HasOpened;
            public string LastSessionId;
            public int OfflineSamples;
        }

        private readonly Dictionary<string, OpenState> states = new Dictionary<string, OpenState>();
        private readonly Action<string> openUrl;

        public AutoOpenService(Action<string> openUrl)
        { this.openUrl = openUrl ?? throw new ArgumentNullException(nameof(openUrl)); }

        public void OnUpdated(ChannelProfile profile, LiveState previous, PlatformCapabilities capabilities)
        {
            if ((capabilities & PlatformCapabilities.LiveStatus) == 0) return;
            var key = profile.Id ?? profile.LiveUrl;
            if (string.IsNullOrEmpty(key)) return;
            if (!states.TryGetValue(key, out var state)) states[key] = state = new OpenState();
            if (profile.State == LiveState.Offline)
            {
                if (state.OfflineSamples < 2) state.OfflineSamples++;
                return;
            }
            if (profile.State != LiveState.Live) return;

            var sessionId = string.IsNullOrWhiteSpace(profile.LiveSessionId) ? null : profile.LiveSessionId;
            var shouldOpen = profile.AutoOpenLive && PollingService.ShouldNotify(previous, profile.State) &&
                !string.IsNullOrWhiteSpace(profile.LiveUrl) &&
                !(sessionId != null && state.HasOpened && sessionId == state.LastSessionId) &&
                !(sessionId == null && state.HasOpened && state.OfflineSamples < 2);
            state.OfflineSamples = 0;
            if (!shouldOpen) return;

            state.HasOpened = true;
            state.LastSessionId = sessionId;
            try { openUrl(profile.LiveUrl); }
            catch (Exception ex) { Debug.WriteLine("Automatic live URL launch failed: " + ex); }
        }
    }
}
