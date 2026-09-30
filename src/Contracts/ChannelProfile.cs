using System;
using System.Collections.Generic;

namespace HikiNotifier.Models
{
    public sealed class ChannelProfile
    {
        public string ChannelId { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string StreamerProfileId { get; set; }
        public string PlatformChannelId { get => ChannelId; set => ChannelId = value; }
        public string Url { get => LiveUrl; set => LiveUrl = value; }
        public bool AlertEnabled { get => NotificationsEnabled; set => NotificationsEnabled = value; }
        public ProviderState ProviderState { get; set; } = new ProviderState();
        public PlatformType Platform { get; set; }
        public string CreatorOid { get; set; }
        public string LiveUrl { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public bool AutoOpenLive { get; set; }
        public bool IsBuiltIn { get; set; }
        public LiveState State { get; set; } = LiveState.Unknown;
        public string LiveSessionId { get; set; }
        public DateTimeOffset? LiveStartedAt { get; set; }
        public string LiveTitle { get; set; }
        public string Category { get; set; }
        public int? ViewerCount { get; set; }
        public string LatestContentUrl { get; set; }


    }

    public sealed class ProviderState
    {
        public bool BaselinePending { get; set; }
        public string LastSeenContentId { get; set; }
        public string LatestContentTitle { get; set; }
        public string LatestContentUrl { get; set; }
        public string LatestVideosId { get; set; }
        public string LatestShortsId { get; set; }
        public List<string> RecentContentIds { get; set; } = new List<string>();
        public string LastLiveSessionId { get; set; }
    }
}
