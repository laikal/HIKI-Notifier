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
    internal sealed partial class SettingsService
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly string path;
        private readonly string profilesPath;
        private readonly bool custom;
        private readonly PlatformRegistry registry;
        private bool canSave = true;
        public bool CanSave => canSave;
        public readonly List<string> LoadErrors = new List<string>();
        public string DataDirectory => Path.GetDirectoryName(path);

        public SettingsService(string customPath = null, PlatformRegistry registry = null)
        {
            this.registry = registry ?? PlatformRegistry.Default;
            custom = customPath != null;
            path = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            profilesPath = Path.Combine(DataDirectory, "profiles");
        }

        private AppSettings LoadLegacy()
        {
            LoadErrors.Clear();
            canSave = true;
            var source = FindSource();
            if (source == null) return CreateDefaults(null);
            AppSettings settings;
            string raw;
            try
            {
                raw = File.ReadAllText(source, Encoding.UTF8);
                settings = json.Deserialize<AppSettings>(raw);
                if (settings == null) throw new InvalidDataException("Empty settings JSON");
            }
            catch (Exception) { return CreateDefaults(source); }
            Dictionary<string, object> fields;
            int storageVersion;
            bool unsupportedVersion = false;
            try
            {
                fields = json.DeserializeObject(raw) as Dictionary<string, object>;
                if (fields == null) throw new InvalidDataException("Invalid settings JSON root");
                storageVersion = fields != null && fields.ContainsKey("ProfileStorageVersion") ?
                    Convert.ToInt32(fields["ProfileStorageVersion"]) : 0;
                if (storageVersion > 1) { unsupportedVersion = true; throw new InvalidDataException("Unsupported profile storage version"); }
                if (fields != null && fields.ContainsKey("SettingsSchemaVersion") &&
                    Convert.ToInt32(fields["SettingsSchemaVersion"]) > 2)
                { unsupportedVersion = true; throw new InvalidDataException("Unsupported settings schema version"); }
            }
            catch (Exception ex)
            {
                if (!unsupportedVersion) return CreateDefaults(source);
                canSave = false; LoadErrors.Add(source + ": " + ex.Message); return Initialize(new AppSettings());
            }
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
            string[] profileFiles;
            try { Directory.CreateDirectory(profilesPath); profileFiles = Directory.GetFiles(profilesPath, "*.json"); }
            catch (Exception ex)
            {
                canSave = false; LoadErrors.Add("Profiles: " + ex.Message); return settings;
            }
            foreach (var file in profileFiles)
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
                        if (channel == null || (int)channel.Platform < 0 ||
                            string.IsNullOrWhiteSpace(channel.LiveUrl))
                            throw new InvalidDataException("Invalid channel");
                        channel.StreamerProfileId = streamer.Id;
                        if (string.IsNullOrWhiteSpace(channel.Name)) channel.Name = streamer.DisplayName;
                        channel.IsBuiltIn = false;
                        channel.ProviderState = channel.ProviderState ?? new ProviderState();
                        channel.ProviderState.RecentContentIds = channel.ProviderState.RecentContentIds ?? new List<string>();
                        registry.ForPlatform(channel.Platform)?.NormalizeStoredProfile(channel);
                        channel.State = LiveState.Unknown;
                    }
                    settings.Streamers.Add(streamer);
                }
                catch (Exception ex) { LoadErrors.Add(file + ": " + ex.Message); }
            }
            settings.Profiles = settings.Streamers.SelectMany(s => s.Channels).ToList();
            return settings;
        }

        private AppSettings CreateDefaults(string damagedSource)
        {
            var defaults = Initialize(new AppSettings());
            try
            {
                if (damagedSource != null && File.Exists(damagedSource))
                {
                    Directory.CreateDirectory(DataDirectory);
                    var backup = Path.Combine(DataDirectory, "settings.json.broken");
                    if (File.Exists(backup))
                        backup += "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N");
                    File.Copy(damagedSource, backup);
                    LoadErrors.Add("Damaged settings preserved: " + backup);
                }
                SaveLegacy(defaults);
            }
            catch (Exception ex)
            {
                canSave = false;
                LoadErrors.Add("Settings initialization: " + ex.Message);
            }
            return defaults;
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
            var builtIn = old.FirstOrDefault(p => p != null && (p.IsBuiltIn || p.ChannelId == BuiltInChannel.BuiltInId));
            settings.BuiltInHikiState = settings.BuiltInHikiState ?? new BuiltInHikiState();
            settings.BuiltInHikiState.ChzzkAlertEnabled = builtIn?.NotificationsEnabled ?? settings.BuiltInNotificationsEnabled;
            var initialized = Initialize(settings);
            foreach (var channel in old.Where(p => p != null && !p.IsBuiltIn && p.ChannelId != BuiltInChannel.BuiltInId))
            {
                var streamer = new StreamerProfile { Id = Guid.TryParse(channel.StreamerProfileId, out var retainedId) ? retainedId.ToString("D") : LegacyId(channel), DisplayName = channel.Name ?? channel.ChannelId };
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
            var builtIn = oldProfiles.FirstOrDefault(p => p != null && (p.IsBuiltIn || p.ChannelId == BuiltInChannel.BuiltInId));
            legacy.BuiltInHikiState = legacy.BuiltInHikiState ?? new BuiltInHikiState();
            legacy.BuiltInHikiState.ChzzkAlertEnabled = builtIn?.NotificationsEnabled ?? legacy.BuiltInNotificationsEnabled;
            if (builtIn != null && legacyMemo(builtIn) != null) legacy.BuiltInHikiState.Memo = legacyMemo(builtIn);
            legacy.Streamers = new List<StreamerProfile> { StreamerProfile.BuiltIn(legacy.BuiltInHikiState) };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var channel in oldProfiles.Where(p => p != null && !p.IsBuiltIn && p.ChannelId != BuiltInChannel.BuiltInId))
            {
                if (!seen.Add(channel.Platform + ":" + channel.ChannelId)) continue;
                var streamer = new StreamerProfile { Id = Guid.TryParse(channel.StreamerProfileId, out var retainedId) ? retainedId.ToString("D") : LegacyId(channel), DisplayName = channel.Name ?? channel.ChannelId,
                    Memo = legacyMemo(channel) ?? "" };
                channel.StreamerProfileId = streamer.Id;
                if (channel.Platform == PlatformType.Chzzk) channel.LiveUrl = BuiltInChannel.UrlFor(channel.ChannelId);
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
            SaveLegacy(legacy);
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

        private void SaveLegacy(AppSettings settings)
        {
            if (!canSave) throw new InvalidOperationException("Settings could not be loaded or migrated; existing data was preserved.");
            settings.NotificationOpacity = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(profilesPath);
            var builtIn = settings.Streamers.First(s => s.IsBuiltIn);
            var state = settings.BuiltInHikiState ?? new BuiltInHikiState();
            state.Memo = builtIn.Memo ?? "";
            state.ChzzkAlertEnabled = builtIn.Channels.First(c => c.Platform == PlatformType.Chzzk).NotificationsEnabled;
            state.ChzzkAutoOpenLive = builtIn.Channels.First(c => c.Platform == PlatformType.Chzzk).AutoOpenLive;
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
                foreach (var channel in streamer.Channels)
                    registry.ForPlatform(channel.Platform)?.PrepareForSave(channel);
                var output = new { SchemaVersion = 1, streamer.Id, streamer.DisplayName, streamer.Memo,
                    Channels = streamer.Channels.Select(c => new { c.Id, c.StreamerProfileId, c.Platform, c.Name,
                        Url = c.LiveUrl, PlatformChannelId = c.ChannelId, AlertEnabled = c.NotificationsEnabled,
                        c.AutoOpenLive,
                        c.ProviderState }).ToArray() };
                WriteAtomic(Path.Combine(profilesPath, streamer.Id + ".json"), json.Serialize(output));
            }
            var root = new { SettingsSchemaVersion = 2, ProfileStorageVersion = 1,
                settings.NotificationsEnabled, settings.RunAtStartup, settings.Language, settings.SoundMode,
                settings.NotificationOpacity,
                settings.WavePath, BuiltInHikiState = state };
            WriteAtomic(path, json.Serialize(root));
        }

        private void DeleteStreamerLegacy(string id)
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
