using System;

namespace HikiNotifier.Models
{
    internal sealed class ChannelProfile
    {
        public const string BuiltInId = "688b22118a21cd70b53ad8b1d024b5d2";
        public string ChannelId { get; set; }
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

        public static ChannelProfile BuiltIn(bool enabled) => new ChannelProfile {
            ChannelId = BuiltInId, LiveUrl = "https://chzzk.naver.com/live/" + BuiltInId,
            Name = "Hikimori Neko", IsBuiltIn = true, NotificationsEnabled = enabled
        };
        public static string UrlFor(string id) => "https://chzzk.naver.com/live/" + id;
    }
}
