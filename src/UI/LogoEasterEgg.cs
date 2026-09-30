using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HikiNotifier.Services;

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
        private readonly Func<Task<IReadOnlyList<ContentEntry>>> getVideos;
        private readonly Func<DateTimeOffset> now;
        private readonly Random random;
        private DateTimeOffset? lastClick;
        private int clickCount;
        private bool fetching;

        public LogoEasterEgg(Action<string> openUrl, Func<Task<IReadOnlyList<ContentEntry>>> getVideos = null,
            Func<DateTimeOffset> now = null, Random random = null)
        {
            this.openUrl = openUrl ?? throw new ArgumentNullException(nameof(openUrl));
            this.getVideos = getVideos;
            this.now = now ?? (() => DateTimeOffset.UtcNow);
            this.random = random ?? new Random();
        }

        public static bool IsCandidateUrl(string url) => Array.IndexOf(urls, url) >= 0;

        public async Task RegisterClickAsync()
        {
            if (fetching) return;
            var current = now();
            if (lastClick.HasValue && (current < lastClick.Value || current - lastClick.Value >= idleTimeout))
                clickCount = 0;
            lastClick = current;
            if (++clickCount < 15) return;

            clickCount = 0;
            fetching = true;
            try
            {
                string[] candidates = null;
                try
                {
                    var videos = getVideos == null ? null : await getVideos().ConfigureAwait(false);
                    candidates = videos?.Where(video => video != null &&
                        Regex.IsMatch(video.ContentId ?? "", "^[A-Za-z0-9_-]{11}$") &&
                        video.Url == "https://www.youtube.com/watch?v=" + video.ContentId)
                        .Select(video => video.Url).Distinct().ToArray();
                }
                catch (Exception ex) { Debug.WriteLine("Logo Easter Egg video fetch failed: " + ex); }
                var choices = candidates != null && candidates.Length > 0 ? candidates : urls;
                try { openUrl(choices[random.Next(choices.Length)]); }
                catch (Exception ex) { Debug.WriteLine("Logo Easter Egg URL launch failed: " + ex); }
            }
            finally { fetching = false; }
        }
    }
}
