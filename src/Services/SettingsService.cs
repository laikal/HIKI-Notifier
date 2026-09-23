using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class SettingsService
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly string path;
        private readonly string legacyPath;
        public SettingsService(string customPath = null)
        {
            path = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (customPath == null)
                legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HIKI Notifier", "settings.json");
        }
        public AppSettings Load()
        {
            AppSettings settings = null;
            if (legacyPath != null && !File.Exists(path) && File.Exists(legacyPath))
            {
                try { File.Copy(legacyPath, path); }
                catch (Exception) { }
            }
            var loadPath = File.Exists(path) ? path : legacyPath;
            try { if (loadPath != null && File.Exists(loadPath)) settings = json.Deserialize<AppSettings>(File.ReadAllText(loadPath, Encoding.UTF8)); }
            catch (Exception) { }
            settings = settings ?? new AppSettings();
            settings.Profiles = (settings.Profiles ?? new System.Collections.Generic.List<ChannelProfile>())
                .Where(p => p != null && !p.IsBuiltIn && p.ChannelId != ChannelProfile.BuiltInId &&
                       (p.Platform == PlatformType.Chzzk &&
                        System.Text.RegularExpressions.Regex.IsMatch(p.ChannelId ?? "", "^[0-9a-f]{32}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        p.Platform == PlatformType.Rplay &&
                        RplayClient.ParseLiveUrl(p.LiveUrl) != null &&
                        string.Equals(RplayClient.ParseLiveUrl(p.LiveUrl), p.CreatorOid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.ChannelId, p.CreatorOid, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(p => p.Platform + ":" + p.ChannelId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            foreach (var profile in settings.Profiles)
            {
                profile.IsBuiltIn = false;
                if (profile.Platform == PlatformType.Chzzk) profile.LiveUrl = ChannelProfile.UrlFor(profile.ChannelId);
                profile.State = LiveState.Unknown;
            }
            settings.Profiles.Insert(0, ChannelProfile.BuiltIn(settings.BuiltInNotificationsEnabled));
            if (settings.Language != "en") settings.Language = "ko";
            return settings;
        }
        public void Save(AppSettings settings)
        {
            settings.BuiltInNotificationsEnabled = settings.Profiles.First(p => p.IsBuiltIn).NotificationsEnabled;
            var serializable = new AppSettings { BuiltInNotificationsEnabled = settings.BuiltInNotificationsEnabled,
                NotificationsEnabled = settings.NotificationsEnabled, RunAtStartup = settings.RunAtStartup,
                Language = settings.Language, SoundMode = settings.SoundMode, WavePath = settings.WavePath,
                Profiles = settings.Profiles.Where(p => !p.IsBuiltIn).Select(p => new ChannelProfile {
                    ChannelId = p.ChannelId, Platform = p.Platform, CreatorOid = p.CreatorOid,
                    LiveUrl = p.LiveUrl, Name = p.Name, ImageUrl = p.ImageUrl,
                    NotificationsEnabled = p.NotificationsEnabled, IsBuiltIn = false }).ToList() };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, json.Serialize(serializable), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }
}
