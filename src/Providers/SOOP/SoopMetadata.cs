using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class SoopProvider
    {
        private readonly IDisposable ownedClient;
        public SoopProvider() : this(new SoopClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(SoopProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.SOOP.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "soop", Platform = Platform, DisplayName = "SOOP",
                UrlTemplate = "https://play.sooplive.co.kr/", Capabilities = Capabilities | PlatformCapabilities.AutoOpen, PollInterval = PollInterval,
                PollIndependently = true, PersistAfterPoll = false, Order = 4, PollOrder = 4, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { var id = SoopClient.ParseChannelId(profile.LiveUrl);
            if (id == null || !string.Equals(id, profile.ChannelId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid SOOP channel");
            profile.ChannelId = id; profile.LiveUrl = SoopClient.UrlFor(id); }
        public void PrepareForSave(ChannelProfile profile)
        {  }
        public void Dispose() { ownedClient?.Dispose(); }
    }
}
