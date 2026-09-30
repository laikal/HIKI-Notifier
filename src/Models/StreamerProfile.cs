using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace HikiNotifier.Models
{
    internal static class BuiltInChannel
    {
        public const string BuiltInId = "688b22118a21cd70b53ad8b1d024b5d2";
        public static ChannelProfile BuiltIn(bool enabled) => new ChannelProfile {
            ChannelId = BuiltInId, LiveUrl = "https://chzzk.naver.com/live/" + BuiltInId,
            Name = "Hikimori Neko", IsBuiltIn = true, NotificationsEnabled = enabled
        };
        public static string UrlFor(string id) => "https://chzzk.naver.com/live/" + id;
    }

    internal sealed class StreamerProfile
    {
        public const string BuiltInProfileId = "hiki-builtin";
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string DisplayName { get; set; }
        public string Memo { get; set; } = "";
        public List<ChannelProfile> Channels { get; set; } = new List<ChannelProfile>();
        public NotificationAppearance Appearance { get; set; } = new NotificationAppearance();
        public bool IsBuiltIn => Id == BuiltInProfileId;

        public static StreamerProfile BuiltIn(BuiltInHikiState state)
        {
            state = state ?? new BuiltInHikiState();
            var streamer = new StreamerProfile { Id = BuiltInProfileId, DisplayName = "Hikimori Neko",
                Memo = state.Memo ?? "귀염냥이" };
            var chzzk = BuiltInChannel.BuiltIn(state.ChzzkAlertEnabled);
            chzzk.AutoOpenLive = state.ChzzkAutoOpenLive;
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

    internal sealed class NotificationAppearance
    {
        public string Background { get; set; } = "";
        public string TextColor { get; set; } = "#FFFFFF";
        public string OutlineColor { get; set; } = "#202020";
        [ScriptIgnore] public string BackgroundPath { get; set; }
        [ScriptIgnore] public string PendingBackgroundPath { get; set; }

        public NotificationAppearance Copy() => new NotificationAppearance {
            Background = Background, BackgroundPath = BackgroundPath,
            TextColor = TextColor, OutlineColor = OutlineColor,
            PendingBackgroundPath = PendingBackgroundPath
        };
    }

    internal sealed class BuiltInHikiState
    {
        public string Memo { get; set; } = "귀염냥이";
        public bool ChzzkAlertEnabled { get; set; } = true;
        public bool ChzzkAutoOpenLive { get; set; }
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
