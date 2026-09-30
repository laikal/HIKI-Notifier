using System;
using System.Drawing;

namespace HikiNotifier
{
    internal static class AppInfo
    {
        public static string Version => ProductVersion.Current;
        public static string NumericVersion => ProductVersion.NumericVersion;

        private static Icon icon;
        public static Icon Icon => icon ?? (icon = System.Drawing.Icon.ExtractAssociatedIcon(typeof(AppInfo).Assembly.Location));

        public static Image LoadAboutImage()
        {
            using (var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("HikiNotifier.Assets.ABOUT.png"))
            {
                if (stream == null) throw new InvalidOperationException("Missing embedded ABOUT image");
                using (var original = Image.FromStream(stream)) return new Bitmap(original);
            }
        }
    }
}
