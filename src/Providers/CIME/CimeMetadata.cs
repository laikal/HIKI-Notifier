using System;
using System.IO;
using HikiNotifier.Models;
namespace HikiNotifier.Services
{
    public sealed partial class CimeProvider
    {
        private readonly IDisposable ownedClient;
        public CimeProvider() : this(new CimeClient()) { }
        private ProviderMetadata metadata;
        public ProviderMetadata Metadata => metadata ?? (metadata = CreateMetadata());
        private ProviderMetadata CreateMetadata()
        {
            byte[] logo;
            using (var stream = typeof(CimeProvider).Assembly.GetManifestResourceStream("HikiNotifier.Assets.CIME.png"))
            using (var memory = new MemoryStream())
            { if (stream == null) throw new InvalidDataException("Missing provider logo"); stream.CopyTo(memory); logo = memory.ToArray(); }
            return new ProviderMetadata { Id = "cime", Platform = Platform, DisplayName = "CIME",
                UrlTemplate = "https://ci.me/@", Capabilities = Capabilities | PlatformCapabilities.AutoOpen, PollInterval = PollInterval,
                PollIndependently = true, PersistAfterPoll = false, Order = 5, PollOrder = 5, LogoPng = logo };
        }
        public void NormalizeStoredProfile(ChannelProfile profile)
        { var id = CimeClient.ParseHandle(profile.LiveUrl);
            if (id == null || !string.Equals(id, profile.ChannelId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid CIME channel");
            profile.ChannelId = id; profile.LiveUrl = CimeClient.UrlFor(id); }
        public void PrepareForSave(ChannelProfile profile)
        {  }
        public void Dispose() { ownedClient?.Dispose(); }
    }
}
