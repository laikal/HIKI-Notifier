using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class TwitchProvider
    {
        private readonly IDisposable ownedClient;
        public TwitchProvider() : this(new TwitchClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(TwitchProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.Twitch.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "twitch", Platform = Platform, DisplayName = "Twitch",
                UrlTemplate = "https://www.twitch.tv/", Capabilities = Capabilities | PlatformCapabilities.AutoOpen, PollInterval = PollInterval,
                PollIndependently = true, PersistAfterPoll = false, Order = 3, PollOrder = 3, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { var id = TwitchClient.ParseLogin(profile.LiveUrl);
            if (id == null || !string.Equals(id, profile.ChannelId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid Twitch channel");
            profile.ChannelId = id; profile.LiveUrl = TwitchClient.UrlFor(id); }
        public void PrepareForSave(ChannelProfile profile)
        {  }
        public void Dispose() { ownedClient?.Dispose(); }
    }
}
