using System.Collections.Generic;
using HikiNotifier.Services;

internal static class TestProviders
{
    // Fixture-only injection. Production uses discovery and has no concrete Client references.
    public static PlatformRegistry Create(ChzzkClient chzzk, RplayClient rplay, YouTubeClient youtube,
        TwitchClient twitch = null, SoopClient soop = null, CimeClient cime = null)
    {
        var providers = new List<IPlatformProvider> { new ChzzkProvider(chzzk), new RplayProvider(rplay), new YouTubeProvider(youtube) };
        if (twitch != null) providers.Add(new TwitchProvider(twitch));
        if (soop != null) providers.Add(new SoopProvider(soop));
        if (cime != null) providers.Add(new CimeProvider(cime));
        return new PlatformRegistry(providers);
    }
}
