using System;
using System.Diagnostics;

namespace HikiNotifier.UI
{
    internal sealed class LogoEasterEgg
    {
        private static readonly string[] urls = {
            "https://www.youtube.com/watch?v=bwY0diHkO-M",
            "https://www.youtube.com/watch?v=9bLtVQzRJj4",
            "https://www.youtube.com/watch?v=bVGsYrMm-3w",
            "https://www.youtube.com/watch?v=3q-uUu0p27M",
            "https://www.youtube.com/watch?v=PEGlTyXkwE4"
        };
        private static readonly TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
        private readonly Action<string> openUrl;
        private readonly Func<DateTimeOffset> now;
        private readonly Random random;
        private DateTimeOffset? lastClick;
        private int clickCount;

        public LogoEasterEgg(Action<string> openUrl, Func<DateTimeOffset> now = null, Random random = null)
        {
            this.openUrl = openUrl ?? throw new ArgumentNullException(nameof(openUrl));
            this.now = now ?? (() => DateTimeOffset.UtcNow);
            this.random = random ?? new Random();
        }

        public static bool IsCandidateUrl(string url) => Array.IndexOf(urls, url) >= 0;

        public void RegisterClick()
        {
            var current = now();
            if (lastClick.HasValue && (current < lastClick.Value || current - lastClick.Value >= idleTimeout))
                clickCount = 0;
            lastClick = current;
            if (++clickCount < 15) return;

            clickCount = 0;
            var url = urls[random.Next(urls.Length)];
            try { openUrl(url); }
            catch (Exception ex) { Debug.WriteLine("Logo Easter Egg URL launch failed: " + ex); }
        }
    }
}
