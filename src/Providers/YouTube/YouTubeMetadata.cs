using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class YouTubeProvider : IContentCatalogProvider
    {
        
        public YouTubeProvider() : this(new YouTubeClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(YouTubeProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.YOUTUBE.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "youtube", Platform = Platform, DisplayName = "YouTube",
                UrlTemplate = "https://www.youtube.com/@", Capabilities = Capabilities, PollInterval = PollInterval,
                PollIndependently = false, PersistAfterPoll = true, Order = 1, PollOrder = 2, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { if (!YouTubeClient.IsChannelUrl(profile.LiveUrl)) throw new InvalidDataException("Invalid YouTube channel");
            PrepareForSave(profile); }
        public void PrepareForSave(ChannelProfile profile)
        { profile.ProviderState = profile.ProviderState ?? new ProviderState();
            profile.ProviderState.BaselinePending = string.IsNullOrEmpty(profile.ProviderState.LastSeenContentId); }
        public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<ContentEntry>> GetRecentContentAsync(ChannelProfile channel, System.Threading.CancellationToken token)
        {
            var id = channel.ChannelId;
            if (string.IsNullOrWhiteSpace(id)) id = await client.ResolveChannelIdAsync(channel.LiveUrl, token).ConfigureAwait(false);
            return await client.GetVideosAsync(channel.LiveUrl, id, token).ConfigureAwait(false);
        }
        public void Dispose() { client?.Dispose(); }
    }
}
