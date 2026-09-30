using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed partial class SettingsService
    {
        private ProfileStore profileStore => new ProfileStore(DataDirectory);
        public AppSettings Load() => LoadPortable();
        public void Save(AppSettings settings) => SavePortable(settings);
        public void DeleteStreamer(string id) => profileStore.Delete(id);

        private AppSettings LoadPortable()
        {
            LoadErrors.Clear(); canSave = true;
            AppSettings settings = null;
            bool migrated = false;
            string source = null;
            try
            {
                Directory.CreateDirectory(DataDirectory);
                source = File.Exists(path) ? path : !custom ? LegacySource() : null;
                if (source != null)
                {
                    var raw = File.ReadAllText(source, Encoding.UTF8);
                    var values = json.DeserializeObject(raw) as Dictionary<string, object>;
                    if (values == null) throw new InvalidDataException("Invalid settings JSON");
                    if (values.TryGetValue("SettingsSchemaVersion", out var schema) && Convert.ToInt32(schema) > 2)
                        throw new NotSupportedException("Unsupported settings schema");
                    if (values.TryGetValue("ProfileStorageVersion", out var storageVersion) && Convert.ToInt32(storageVersion) > 2)
                        throw new NotSupportedException("Unsupported profile storage schema");
                    if (values.TryGetValue("ProfileStorageVersion", out var version) && Convert.ToInt32(version) == 2 && source == path)
                    {
                        settings = json.Deserialize<AppSettings>(raw) ?? throw new InvalidDataException("Empty settings");
                    }
                    else
                    {
                        settings = ImportLegacy(source, raw);
                        migrated = true;
                    }
                }
                else settings = new AppSettings();
            }
            catch (Exception ex)
            {
                LoadErrors.Add("Settings: " + ex.Message);
                if (source != path || ex is NotSupportedException || !File.Exists(path))
                { canSave = false; return Initialize(new AppSettings()); }
                try
                {
                    var backup = path + ".broken";
                    if (File.Exists(backup)) backup += "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
                        "." + Guid.NewGuid().ToString("N");
                    File.Copy(path, backup);
                    File.Delete(path);
                    settings = new AppSettings();
                }
                catch (Exception backupError)
                { canSave = false; LoadErrors.Add("Settings backup: " + backupError.Message); return Initialize(new AppSettings()); }
            }
            settings.NotificationOpacity = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            settings.NotificationDurationSeconds = AppSettings.ClampNotificationDuration(settings.NotificationDurationSeconds);
            if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = "ko";
            if (!migrated) settings.Streamers = new List<StreamerProfile>();
            var store = profileStore;
            try
            {
                Directory.CreateDirectory(store.Root);
                foreach (var file in store.Files())
                {
                    try
                    {
                        var profile = store.Read(file);
                        foreach (var channel in profile.Channels) registry.ForPlatform(channel.Platform)?.NormalizeStoredProfile(channel);
                        settings.Streamers.RemoveAll(s => string.Equals(s.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                        settings.Streamers.Add(profile);
                    }
                    catch (Exception ex) { LoadErrors.Add(file + ": " + ex.Message); }
                }
                if (!settings.Streamers.Any(s => s.IsBuiltIn)) settings.Streamers.Insert(0, StreamerProfile.BuiltIn(settings.BuiltInHikiState));
                var builtInProfile = settings.Streamers.First(s => s.IsBuiltIn);
                settings.Streamers.Remove(builtInProfile);
                settings.Streamers.Insert(0, builtInProfile);
                settings.Profiles = settings.Streamers.SelectMany(s => s.Channels).ToList();
                if (migrated)
                {
                    // Existing INIs win on retry. All legacy JSON remains untouched until every
                    // profile has been written and read back; the global marker is the last step.
                    foreach (var streamer in settings.Streamers)
                    {
                        if (!store.Exists(streamer.Id)) store.Write(streamer, registry);
                        var verified = store.Read(Path.Combine(store.Folder(streamer.Id), "profile.ini"));
                        if (verified.Id != streamer.Id || verified.Channels.Count != streamer.Channels.Count ||
                            verified.Memo != streamer.Memo) throw new InvalidDataException("Profile migration verification failed");
                    }
                    SaveGlobal(settings);
                }
                else if (!File.Exists(path)) SavePortable(settings);
            }
            catch (Exception ex)
            {
                canSave = false; LoadErrors.Add("Profile migration/bootstrap: " + ex.Message);
            }
            return settings;
        }

        private string LegacySource()
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HIKI Notifier", "settings.json");
            if (File.Exists(local)) return local;
            var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HIKI Notifier", "settings.json");
            return File.Exists(roaming) ? roaming : null;
        }

        private AppSettings ImportLegacy(string source, string raw)
        {
            var fields = json.DeserializeObject(raw) as Dictionary<string, object>;
            var legacySettings = json.Deserialize<AppSettings>(raw) ?? new AppSettings();
            // Preserve the source before replacing an EXE-adjacent old settings.json.
            var backup = source == path ? path + ".pre-1.20C.bak" : Path.Combine(DataDirectory, "legacy-settings.pre-1.20C.bak");
            if (!File.Exists(backup)) File.Copy(source, backup);
            var loaded = new List<StreamerProfile>();
            var oldDirectory = Path.GetDirectoryName(source);
            var oldProfiles = Path.Combine(oldDirectory, "profiles");
            if (Directory.Exists(oldProfiles))
            {
                foreach (var file in Directory.GetFiles(oldProfiles, "*.json"))
                {
                    var profileJson = File.ReadAllText(file, Encoding.UTF8);
                    var streamer = json.Deserialize<StreamerProfile>(profileJson);
                    if (streamer == null || streamer.Channels == null) throw new InvalidDataException("Invalid legacy profile: " + file);
                    var profileValues = json.DeserializeObject(profileJson) as Dictionary<string, object>;
                    if (profileValues == null || !profileValues.ContainsKey("Id") || string.IsNullOrWhiteSpace(streamer.Id))
                        streamer.Id = Path.GetFileNameWithoutExtension(file);
                    if (!Guid.TryParse(streamer.Id, out _)) throw new InvalidDataException("Invalid legacy profile ID: " + file);
                    loaded.Add(streamer);
                }
            }
            var builtin = StreamerProfile.BuiltIn(legacySettings.BuiltInHikiState);
            if (fields != null && fields.TryGetValue("Profiles", out var old) && old is object[] && loaded.Count == 0)
            {
                foreach (var channel in legacySettings.Profiles ?? new List<ChannelProfile>())
                {
                    if (channel == null) continue;
                    if (channel.IsBuiltIn || channel.ChannelId == BuiltInChannel.BuiltInId)
                    {
                        builtin.Channels[0].NotificationsEnabled = channel.NotificationsEnabled;
                        builtin.Channels[0].AutoOpenLive = channel.AutoOpenLive;
                        continue;
                    }
                    var id = Guid.TryParse(channel.StreamerProfileId, out var parsed)
                        ? parsed.ToString("D") : StableLegacyId(channel);
                    var streamer = loaded.FirstOrDefault(s => s.Id == id);
                    if (streamer == null)
                    {
                        streamer = new StreamerProfile { Id = id, DisplayName = channel.Name ?? channel.ChannelId, Memo = "" };
                        loaded.Add(streamer);
                    }
                    channel.StreamerProfileId = id; streamer.Channels.Add(channel);
                }
            }
            // Very early JSON may have Streamers embedded directly.
            if (loaded.Count == 0 && legacySettings.Streamers != null)
                loaded.AddRange(legacySettings.Streamers.Where(s => s != null && !s.IsBuiltIn));
            legacySettings.Streamers = new List<StreamerProfile> { builtin };
            foreach (var streamer in loaded)
            {
                if (string.IsNullOrWhiteSpace(streamer.Id)) streamer.Id = Guid.NewGuid().ToString("D");
                if (!Guid.TryParse(streamer.Id, out _)) throw new InvalidDataException("Invalid legacy streamer ID");
                streamer.Appearance = streamer.Appearance ?? new NotificationAppearance();
                foreach (var channel in streamer.Channels)
                {
                    if (channel == null) throw new InvalidDataException("Invalid legacy channel");
                    channel.StreamerProfileId = streamer.Id;
                    channel.ProviderState = channel.ProviderState ?? new ProviderState();
                }
                legacySettings.Streamers.Add(streamer);
            }
            legacySettings.Profiles = legacySettings.Streamers.SelectMany(s => s.Channels).ToList();
            return legacySettings;
        }
        private static string StableLegacyId(ChannelProfile channel)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes("HIKI Notifier legacy:" + channel.Platform + ":" + channel.ChannelId));
                var bytes = new byte[16]; Array.Copy(hash, bytes, 16);
                return new Guid(bytes).ToString("D");
            }
        }
        private void SavePortable(AppSettings settings)
        {
            if (!canSave) throw new InvalidOperationException("Profile migration failed; existing data preserved.");
            settings.NotificationOpacity = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            settings.NotificationDurationSeconds = AppSettings.ClampNotificationDuration(settings.NotificationDurationSeconds);
            Directory.CreateDirectory(DataDirectory);
            foreach (var streamer in settings.Streamers) profileStore.Write(streamer, registry);
            SaveGlobal(settings);
        }
        private void SaveGlobal(AppSettings settings)
        {
            ProfileStore.Atomic(path, json.Serialize(new {
                SettingsSchemaVersion = 2, ProfileStorageVersion = 2,
                settings.NotificationsEnabled, settings.RunAtStartup, settings.Language,
                settings.SoundMode, settings.NotificationOpacity, settings.NotificationDurationSeconds, settings.WavePath
            }));
        }
    }
}
