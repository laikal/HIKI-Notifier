using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal static class NotificationReplay
    {
        public static ReplayableNotification ForChange(IPlatformProvider provider, ChannelProfile profile,
            bool oldEnabled, bool newEnabled)
        {
            if (oldEnabled || !newEnabled) return null;
            try { return provider?.GetReplayableNotification(profile); }
            catch (Exception ex) { Trace.WriteLine("[Provider replay] " + ex.GetType().Name); return null; }
        }
    }

}
