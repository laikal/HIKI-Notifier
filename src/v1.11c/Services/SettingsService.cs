using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class SettingsService
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly string path;
        private readonly string profilesPath;
        private readonly bool custom;
        private bool canSave = true;
        public bool CanSave => canSave;
        public readonly List<string> LoadErrors = new List<string>();
        public string DataDirectory => Path.GetDirectoryName(path);

        public SettingsService(string customPath = null)
        {
            custom = customPath != null;
            path = customPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HIKI Notifier", "settings.json");
            profilesPath = Path.Combine(DataDirectory, "profiles");
        }

        public AppSettings Load()
        {
            LoadErrors.Clear();
            canSave = true;
            var source = FindSource();
            if (source == null) return Initialize(new AppSettings());
            AppSettings settings;
            string raw;
            try { raw = File.ReadAllText(source, Encoding.UTF8); settings = json.Deserialize<AppSettings>(raw) ?? new AppSettings(); }
            catch (Exception ex) { canSave = false; LoadErrors.Add(source + ": " + ex.Message); return Initialize(new AppSettings()); }
            Dictionary<string, object> fields;
            int storageVersion;
            try
            {
                fields = json.DeserializeObject(raw) as Dictionary<string, object>;
                storageVersion = fields != null && fields.ContainsKey("ProfileStorageVersion") ?
                    Convert.ToInt32(fields["ProfileStorageVersion"]) : 0;
                if (storageVersion > 1) throw new InvalidDataException("Unsupported profile storage version");
                if (fields != null && fields.ContainsKey("SettingsSchemaVersion") &&
                    Convert.ToInt32(fields["SettingsSchemaVersion"]) > 2)
                    throw new InvalidDataException("Unsupported settings schema version");
            }
            catch (Exception ex) { canSave = false; LoadErrors.Add(source + ": " + ex.Message); return Initialize(new AppSettings()); }
            var migrated = storageVersion == 1 && source == path;
            if (!migrated)
            {
                try
                {
                    Migrate(settings, source);
                    settings = json.Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    canSave = false;
                    LoadErrors.Add("Migration: " + ex.Message);
                    return InitializeLegacyInMemory(settings);
                }
            }
            settings = Initialize(settings);
            foreach (var file in Directory.Exists(profilesPath) ? Directory.GetFiles(profilesPath, "*.json") : new string[0])
            {
                try
                {
                    var streamer = json.Deserialize<StreamerProfile>(File.ReadAllText(file, Encoding.UTF8));
                    Guid parsed;
                    if (streamer == null || !Guid.TryParse(streamer.Id, out parsed) ||
                        !string.Equals(Path.GetFileNameWithoutExtension(file), streamer.Id, StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(streamer.DisplayName) || streamer.Channels == null)
                        throw new InvalidDataException("Invalid streamer profile");
                    foreach (var channel in streamer.Channels)
                    {
                        if (channel == null || !Enum.IsDefined(typeof(PlatformType), channel.Platform) ||
                            channel.Platform == PlatformType.Twitch || string.IsNullOrWhiteSpace(channel.LiveUrl))
                            throw new InvalidDataException("Invalid channel");
                        channel.StreamerProfileId = streamer.Id;
                        if (string.IsNullOrWhiteSpace(channel.Name)) channel.Name = streamer.DisplayName;
                        if (channel.Platform == PlatformType.Rplay)
                        {
                            channel.CreatorOid = channel.ChannelId;
                            if (!string.Equals(RplayClient.ParseLiveUrl(channel.LiveUrl), channel.CreatorOid,
                                StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid RPLAY channel");
                        }
                        if (channel.Platform == PlatformType.Chzzk &&
                            !string.Equals(ChzzkClient.ParseLiveUrl(channel.LiveUrl), channel.ChannelId,
                                StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid CHZZK channel");
                        if (channel.Platform == PlatformType.YouTube && !YouTubeClient.IsChannelUrl(channel.LiveUrl))
                            throw new InvalidDataException("Invalid YouTube channel");
                        channel.IsBuiltIn = false;
                        channel.ProviderState = channel.ProviderState ?? new ProviderState();
                        channel.ProviderState.RecentContentIds =
                            channel.ProviderState.RecentContentIds ?? new List<string>();
                        if (channel.Platform == PlatformType.YouTube)
                            channel.ProviderState.BaselinePending =
                                string.IsNullOrEmpty(channel.ProviderState.LastSeenContentId);
                        channel.State = LiveState.Unknown;
                    }
                    settings.Streamers.Add(streamer);
                }
                catch (Exception ex) { LoadErrors.Add(file + ": " + ex.Message); }
            }
            settings.Profiles = settings.Streamers.SelectMany(s => s.Channels).ToList();
            return settings;
        }

        private string FindSource()
        {
            if (File.Exists(path)) return path;
            if (custom) return null;
            var portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(portable)) return portable;
            var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HIKI Notifier", "settings.json");
            return File.Exists(roaming) ? roaming : null;
        }

        private AppSettings Initialize(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = "ko";
            settings.NotificationOpacity = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            settings.BuiltInHikiState = settings.BuiltInHikiState ?? new BuiltInHikiState();
            settings.Streamers = new List<StreamerProfile> { StreamerProfile.BuiltIn(settings.BuiltInHikiState) };
            settings.Profiles = settings.Streamers[0].Channels.ToList();
            return settings;
        }

        private AppSettings InitializeLegacyInMemory(AppSettings settings)
        {
            var old = settings.Profiles ?? new List<ChannelProfile>();
            var builtIn = old.FirstOrDefault(p => p != null && (p.IsBuiltIn || p.ChannelId == ChannelProfile.BuiltInId));
            settings.BuiltInHikiState = settings.BuiltInHikiState ?? new BuiltInHikiState();
            settings.BuiltInHikiState.ChzzkAlertEnabled = builtIn?.NotificationsEnabled ?? settings.BuiltInNotificationsEnabled;
            var initialized = Initialize(settings);
            foreach (var channel in old.Where(p => p != null && !p.IsBuiltIn && p.ChannelId != ChannelProfile.BuiltInId))
            {
                var streamer = new StreamerProfile { Id = LegacyId(channel), DisplayName = channel.Name ?? channel.ChannelId };
                channel.StreamerProfileId = streamer.Id;
                streamer.Channels.Add(channel);
                initialized.Streamers.Add(streamer);
            }
            initialized.Profiles = initialized.Streamers.SelectMany(s => s.Channels).ToList();
            return initialized;
        }

        private void Migrate(AppSettings legacy, string source)
        {
            var oldProfiles = legacy.Profiles ?? new List<ChannelProfile>();
            var legacyFields = json.DeserializeObject(File.ReadAllText(source, Encoding.UTF8)) as Dictionary<string, object>;
            var legacyItems = legacyFields != null && legacyFields.ContainsKey("Profiles") ?
                legacyFields["Profiles"] as object[] : null;
            Func<ChannelProfile, string> legacyMemo = channel =>
            {
                foreach (var item in legacyItems ?? new object[0])
                {
                    var values = item as Dictionary<string, object>;
                    object idValue, memoValue;
                    if (values != null && values.TryGetValue("ChannelId", out idValue) &&
                        string.Equals(Convert.ToString(idValue), channel.ChannelId, StringComparison.OrdinalIgnoreCase) &&
                        values.TryGetValue("Memo", out memoValue)) return Convert.ToString(memoValue);
                }
                return null;
            };
            var builtIn = oldProfiles.FirstOrDefault(p => p != null && (p.IsBuiltIn || p.ChannelId == ChannelProfile.BuiltInId));
            legacy.BuiltInHikiState = legacy.BuiltInHikiState ?? new BuiltInHikiState();
            legacy.BuiltInHikiState.ChzzkAlertEnabled = builtIn?.NotificationsEnabled ?? legacy.BuiltInNotificationsEnabled;
            if (builtIn != null && legacyMemo(builtIn) != null) legacy.BuiltInHikiState.Memo = legacyMemo(builtIn);
            legacy.Streamers = new List<StreamerProfile> { StreamerProfile.BuiltIn(legacy.BuiltInHikiState) };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var channel in oldProfiles.Where(p => p != null && !p.IsBuiltIn && p.ChannelId != ChannelProfile.BuiltInId))
            {
                if (channel.Platform != PlatformType.Chzzk && channel.Platform != PlatformType.Rplay) continue;
                if (!seen.Add(channel.Platform + ":" + channel.ChannelId)) continue;
                var streamer = new StreamerProfile { Id = LegacyId(channel), DisplayName = channel.Name ?? channel.ChannelId,
                    Memo = legacyMemo(channel) ?? "" };
                channel.StreamerProfileId = streamer.Id;
                channel.ProviderState = new ProviderState();
                if (channel.Platform == PlatformType.Chzzk) channel.LiveUrl = ChannelProfile.UrlFor(channel.ChannelId);
                streamer.Channels.Add(channel);
                legacy.Streamers.Add(streamer);
            }
            legacy.Profiles = legacy.Streamers.SelectMany(s => s.Channels).ToList();
            Directory.CreateDirectory(DataDirectory);
            var backup = string.Equals(Path.GetFullPath(source), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
                ? source + ".migrated.bak" :
                Path.Combine(DataDirectory, (string.Equals(Path.GetFullPath(Path.GetDirectoryName(source)),
                        Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), StringComparison.OrdinalIgnoreCase)
                    ? "portable" : "roaming") + "-settings.migrated.bak");
            if (!File.Exists(backup)) File.Copy(source, backup);
            Save(legacy);
        }

        private static string LegacyId(ChannelProfile channel)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("HIKI Notifier legacy:" + channel.Platform + ":" + channel.ChannelId));
                var guid = new byte[16]; Array.Copy(bytes, guid, guid.Length);
                return new Guid(guid).ToString("D");
            }
        }

        public void Save(AppSettings settings)
        {
            if (!canSave) throw new InvalidOperationException("Settings could not be loaded or migrated; existing data was preserved.");
            settings.NotificationOpacity = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(profilesPath);
            var builtIn = settings.Streamers.First(s => s.IsBuiltIn);
            var state = settings.BuiltInHikiState ?? new BuiltInHikiState();
            state.Memo = builtIn.Memo ?? "";
            state.ChzzkAlertEnabled = builtIn.Channels.First(c => c.Platform == PlatformType.Chzzk).NotificationsEnabled;
            var youtube = builtIn.Channels.First(c => c.Platform == PlatformType.YouTube);
            if (youtube.ProviderState != null)
                youtube.ProviderState.BaselinePending =
                    string.IsNullOrEmpty(youtube.ProviderState.LastSeenContentId);
            state.YouTubeAlertEnabled = youtube.NotificationsEnabled;
            state.YouTubeChannelId = youtube.ChannelId;
            state.YouTubeBaselinePending = youtube.ProviderState?.BaselinePending ?? true;
            state.YouTubeLastSeenContentId = youtube.ProviderState?.LastSeenContentId;
            state.YouTubeLatestTitle = youtube.ProviderState?.LatestContentTitle;
            state.YouTubeLatestUrl = youtube.ProviderState?.LatestContentUrl;
            state.YouTubeLatestVideosId = youtube.ProviderState?.LatestVideosId;
            state.YouTubeLatestShortsId = youtube.ProviderState?.LatestShortsId;
            state.YouTubeRecentContentIds = youtube.ProviderState?.RecentContentIds ?? new List<string>();
            settings.BuiltInHikiState = state;
            foreach (var streamer in settings.Streamers.Where(s => !s.IsBuiltIn))
            {
                Guid id;
                if (!Guid.TryParse(streamer.Id, out id)) throw new InvalidDataException("Invalid streamer ID");
                foreach (var channel in streamer.Channels.Where(c => c.Platform == PlatformType.YouTube))
                {
                    channel.ProviderState = channel.ProviderState ?? new ProviderState();
                    channel.ProviderState.BaselinePending =
                        string.IsNullOrEmpty(channel.ProviderState.LastSeenContentId);
                }
                var output = new { SchemaVersion = 1, streamer.Id, streamer.DisplayName, streamer.Memo,
                    Channels = streamer.Channels.Select(c => new { c.Id, c.StreamerProfileId, c.Platform, c.Name,
                        Url = c.LiveUrl, PlatformChannelId = c.ChannelId, AlertEnabled = c.NotificationsEnabled,
                        c.ProviderState }).ToArray() };
                WriteAtomic(Path.Combine(profilesPath, streamer.Id + ".json"), json.Serialize(output));
            }
            var root = new { SettingsSchemaVersion = 2, ProfileStorageVersion = 1,
                settings.NotificationsEnabled, settings.RunAtStartup, settings.Language, settings.SoundMode,
                settings.NotificationOpacity,
                settings.WavePath, BuiltInHikiState = state };
            WriteAtomic(path, json.Serialize(root));
        }

        public void DeleteStreamer(string id)
        {
            Guid parsed;
            if (!Guid.TryParse(id, out parsed)) throw new ArgumentException("Invalid streamer ID", nameof(id));
            var file = Path.Combine(profilesPath, id + ".json");
            if (File.Exists(file)) File.Delete(file);
        }

        private static void WriteAtomic(string destination, string content)
        {
            var temp = destination + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(destination)) File.Replace(temp, destination, null); else File.Move(temp, destination);
        }
    }
}
