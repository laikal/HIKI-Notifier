using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class RplayProvider
    {
        
        public RplayProvider() : this(new RplayClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(RplayProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.Rplay.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "rplay", Platform = Platform, DisplayName = "RPLAY",
                UrlTemplate = "https://rplay.live/live/", Capabilities = Capabilities | PlatformCapabilities.AutoOpen, PollInterval = PollInterval,
                PollIndependently = false, PersistAfterPoll = false, Order = 2, PollOrder = 1, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { profile.CreatorOid = profile.ChannelId;
            if (!string.Equals(RplayClient.ParseLiveUrl(profile.LiveUrl), profile.CreatorOid, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid RPLAY channel"); }
        public void PrepareForSave(ChannelProfile profile)
        {  }
        public void Dispose() { client?.Dispose(); }
    }
}
