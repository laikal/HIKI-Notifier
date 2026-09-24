using System;
using System.Collections.Generic;

namespace HikiNotifier.Models
{
    internal sealed class StreamerProfile
    {
        public const string BuiltInProfileId = "hiki-builtin";
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string DisplayName { get; set; }
        public string Memo { get; set; } = "";
        public List<ChannelProfile> Channels { get; set; } = new List<ChannelProfile>();
        public bool IsBuiltIn => Id == BuiltInProfileId;

        public static StreamerProfile BuiltIn(BuiltInHikiState state)
        {
            state = state ?? new BuiltInHikiState();
            var streamer = new StreamerProfile { Id = BuiltInProfileId, DisplayName = "Hikimori Neko",
                Memo = state.Memo ?? "귀염냥이" };
            var chzzk = ChannelProfile.BuiltIn(state.ChzzkAlertEnabled);
            chzzk.Id = "hiki-chzzk"; chzzk.StreamerProfileId = BuiltInProfileId;
            var youtube = new ChannelProfile { Id = "hiki-youtube", StreamerProfileId = BuiltInProfileId,
                Platform = PlatformType.YouTube, IsBuiltIn = true, Name = "Hikimori Neko",
                LiveUrl = "https://www.youtube.com/@%ED%9E%88%ED%82%A4%EB%AA%A8%EB%A6%AC%EB%84%A4%EC%BD%94",
                ChannelId = state.YouTubeChannelId, NotificationsEnabled = state.YouTubeAlertEnabled,
                ProviderState = new ProviderState { BaselinePending = string.IsNullOrEmpty(state.YouTubeLastSeenContentId),
                    LastSeenContentId = state.YouTubeLastSeenContentId,
                    LatestContentTitle = state.YouTubeLatestTitle, LatestContentUrl = state.YouTubeLatestUrl,
                    LatestVideosId = state.YouTubeLatestVideosId, LatestShortsId = state.YouTubeLatestShortsId,
                    RecentContentIds = state.YouTubeRecentContentIds ?? new List<string>() } };
            streamer.Channels.Add(chzzk); streamer.Channels.Add(youtube);
            return streamer;
        }
    }

    internal sealed class BuiltInHikiState
    {
        public string Memo { get; set; } = "귀염냥이";
        public bool ChzzkAlertEnabled { get; set; } = true;
        public bool YouTubeAlertEnabled { get; set; } = true;
        public string YouTubeChannelId { get; set; }
        public bool YouTubeBaselinePending { get; set; }
        public string YouTubeLastSeenContentId { get; set; }
        public string YouTubeLatestTitle { get; set; }
        public string YouTubeLatestUrl { get; set; }
        public string YouTubeLatestVideosId { get; set; }
        public string YouTubeLatestShortsId { get; set; }
        public List<string> YouTubeRecentContentIds { get; set; } = new List<string>();
    }
}
