using System;
using System.Collections.Generic;

namespace HikiNotifier.Models
{
    internal sealed class ChannelProfile
    {
        public const string BuiltInId = "688b22118a21cd70b53ad8b1d024b5d2";
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
        public bool IsBuiltIn { get; set; }
        public LiveState State { get; set; } = LiveState.Unknown;
        public string LiveTitle { get; set; }
        public string Category { get; set; }
        public int? ViewerCount { get; set; }
        public string LatestContentUrl { get; set; }

        public static ChannelProfile BuiltIn(bool enabled) => new ChannelProfile {
            ChannelId = BuiltInId, LiveUrl = "https://chzzk.naver.com/live/" + BuiltInId,
            Name = "Hikimori Neko", IsBuiltIn = true, NotificationsEnabled = enabled
        };
        public static string UrlFor(string id) => "https://chzzk.naver.com/live/" + id;
    }

    internal sealed class ProviderState
    {
        public bool BaselinePending { get; set; }
        public string LastSeenContentId { get; set; }
        public string LatestContentTitle { get; set; }
        public string LatestContentUrl { get; set; }
        public string LatestVideosId { get; set; }
        public string LatestShortsId { get; set; }
        public List<string> RecentContentIds { get; set; } = new List<string>();
    }
}
