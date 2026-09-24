using System;

namespace HikiNotifier.Models
{
    [Flags]
    internal enum PlatformCapabilities
    {
        None = 0, LiveStatus = 1, ViewerCount = 2, LiveTitle = 4,
        NewContent = 8, LatestContentTitle = 16, ChannelPage = 32
    }

    internal enum NotificationEventType { LiveStarted, NewContent }
}
