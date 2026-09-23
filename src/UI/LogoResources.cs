using System;
using System.Drawing;
using System.IO;
using HikiNotifier.Models;

namespace HikiNotifier.UI
{
    internal static class LogoResources
    {
        private static readonly Lazy<Image> chzzk = new Lazy<Image>(() => Load("Chzzk"));
        private static readonly Lazy<Image> rplay = new Lazy<Image>(() => Load("Rplay"));
        private static readonly Lazy<Image> twitch = new Lazy<Image>(() => Load("Twitch"));
        private static readonly Lazy<Image> korean = new Lazy<Image>(() => Load("kr_logo"));
        private static readonly Lazy<Image> english = new Lazy<Image>(() => Load("EN_logo"));

        public static Image MainLogo(string language) => language == "en" ? english.Value : korean.Value;

        public static Image PlatformLogo(PlatformType platform)
        {
            if (platform == PlatformType.Rplay) return rplay.Value;
            if (platform == PlatformType.Twitch) return twitch.Value;
            return chzzk.Value;
        }

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
