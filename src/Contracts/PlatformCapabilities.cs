using System;

namespace HikiNotifier.Models
{
    [Flags]
    public enum PlatformCapabilities
    {
        None = 0, LiveStatus = 1, ViewerCount = 2, LiveTitle = 4,
        NewContent = 8, LatestContentTitle = 16, ChannelPage = 32, AutoOpen = 64
    }

    public enum NotificationEventType { LiveStarted, NewContent }
}
