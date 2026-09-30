using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class ChzzkProvider
    {
        
        public ChzzkProvider() : this(new ChzzkClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(ChzzkProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.Chzzk.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "chzzk", Platform = Platform, DisplayName = "CHZZK",
                UrlTemplate = "https://chzzk.naver.com/live/", Capabilities = Capabilities | PlatformCapabilities.AutoOpen, PollInterval = PollInterval,
                PollIndependently = false, PersistAfterPoll = false, Order = 0, PollOrder = 0, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { if (!string.Equals(ChzzkClient.ParseLiveUrl(profile.LiveUrl), profile.ChannelId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid CHZZK channel"); }
        public void PrepareForSave(ChannelProfile profile)
        {  }
        public void Dispose() { client?.Dispose(); }
    }
}
