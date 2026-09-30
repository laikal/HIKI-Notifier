using System;
using System.Drawing;
using System.IO;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal static class LogoResources
    {
        private static readonly System.Collections.Generic.Dictionary<PlatformType, Image> platformImages =
            new System.Collections.Generic.Dictionary<PlatformType, Image>();
        private static readonly Lazy<Image> japanese = new Lazy<Image>(LoadJapanese);
        private static readonly Lazy<Image> korean = new Lazy<Image>(() => Load("kr_logo"));
        private static readonly Lazy<Image> english = new Lazy<Image>(() => Load("EN_logo"));

        public static Image MainLogo(string language) =>
            language == "ko" ? korean.Value : language == "ja" ? japanese.Value : english.Value;

        private static Image LoadJapanese()
        {
            try { return Load("JP_logo"); }
            catch (Exception) { return english.Value; }
        }

        public static Image PlatformLogo(PlatformType platform)
        {
            lock (platformImages)
            {
                if (platformImages.TryGetValue(platform, out var cached)) return cached;
                try
                {
                    var bytes = PlatformRegistry.Default.ForPlatform(platform)?.Metadata.LogoPng;
                    if (bytes == null) return english.Value;
                    using (var stream = new MemoryStream(bytes))
                    using (var original = Image.FromStream(stream))
                        return platformImages[platform] = new Bitmap(original);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("[Provider logo] " + ex.GetType().Name);
                    return english.Value;
                }
            }
        }

        public static PlatformType[] TestPlatforms =>
            System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(PlatformRegistry.Default.Providers, p => p.Platform));

        private static Image Load(string name)
        {
            using (Stream stream = typeof(LogoResources).Assembly.GetManifestResourceStream(
                "HikiNotifier.Assets." + name + ".png"))
            {
                if (stream == null) throw new InvalidOperationException("Missing embedded logo: " + name);
                using (var original = Image.FromStream(stream))
                    return new Bitmap(original);
            }
        }
    }
}
