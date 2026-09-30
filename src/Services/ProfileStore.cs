using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    // The only authoritative store for streamer and channel data after migration.
    internal sealed class ProfileStore
    {
        private readonly string root;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        internal string Root => root;
        internal ProfileStore(string applicationDirectory) { root = Path.Combine(applicationDirectory, "Profiles"); }
        internal string Folder(string id)
        {
            if (id != StreamerProfile.BuiltInProfileId && !Guid.TryParse(id, out _))
                throw new InvalidDataException("Invalid profile ID");
            return Path.Combine(root, id);
        }
        internal bool Exists(string id) => File.Exists(Path.Combine(Folder(id), "profile.ini"));
        internal IEnumerable<string> Files() => Directory.Exists(root)
            ? Directory.GetFiles(root, "profile.ini", SearchOption.AllDirectories)
                .Where(file => Path.GetDirectoryName(Path.GetDirectoryName(file)).TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            : Enumerable.Empty<string>();

        internal StreamerProfile Read(string file)
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> current = null;
            foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                { current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); sections.Add(line.Substring(1, line.Length - 2), current); continue; }
                var equal = line.IndexOf('=');
                if (current == null || equal < 1) throw new InvalidDataException("Invalid profile.ini line");
                current.Add(line.Substring(0, equal), Uri.UnescapeDataString(line.Substring(equal + 1)));
            }
            if (!sections.TryGetValue("Profile", out var header)) throw new InvalidDataException("Missing Profile section");
            var folderId = Path.GetFileName(Path.GetDirectoryName(file));
            var id = Get(header, "Id") ?? folderId;
            if (!string.Equals(id, folderId, StringComparison.OrdinalIgnoreCase) ||
                (id != StreamerProfile.BuiltInProfileId && !Guid.TryParse(id, out _))) throw new InvalidDataException("Profile ID mismatch");
            var profile = new StreamerProfile { Id = id, DisplayName = Get(header, "Name"), Memo = Get(header, "Memo") ?? "",
                Channels = new List<ChannelProfile>() };
            if (string.IsNullOrWhiteSpace(profile.DisplayName)) throw new InvalidDataException("Missing profile name");
            if (sections.TryGetValue("NotificationAppearance", out var look))
            {
                profile.Appearance.Background = Get(look, "Background") ?? "";
                profile.Appearance.TextColor = Get(look, "TextColor") ?? "#FFFFFF";
                profile.Appearance.OutlineColor = Get(look, "OutlineColor") ?? "#202020";
                profile.Appearance.BackgroundPath = BackgroundFile(profile);
            }
            var count = int.Parse(Get(header, "ChannelCount") ?? "0", CultureInfo.InvariantCulture);
            if (count < 0 || count > 1000) throw new InvalidDataException("Invalid channel count");
            for (var i = 0; i < count; i++)
            {
                if (!sections.TryGetValue("Channel." + i, out var row)) throw new InvalidDataException("Missing channel section");
                var channel = new ChannelProfile {
                    Id = Get(row, "Id") ?? Guid.NewGuid().ToString("D"),
                    StreamerProfileId = id,
                    Platform = (PlatformType)int.Parse(Get(row, "Provider") ?? "-1", CultureInfo.InvariantCulture),
                    ChannelId = Get(row, "ChannelId"), LiveUrl = Get(row, "Url"),
                    Name = Get(row, "Name"), ImageUrl = Get(row, "ImageUrl"), CreatorOid = Get(row, "CreatorOid"),
                    NotificationsEnabled = bool.Parse(Get(row, "Notifications") ?? "True"),
                    AutoOpenLive = bool.Parse(Get(row, "AutoOpen") ?? "False"),
                    IsBuiltIn = profile.IsBuiltIn,
                    LiveSessionId = Get(row, "LiveSessionId"), LiveTitle = Get(row, "LiveTitle"),
                    Category = Get(row, "Category"), LatestContentUrl = Get(row, "LatestContentUrl")
                };
                if ((int)channel.Platform < 0 || string.IsNullOrWhiteSpace(channel.LiveUrl)) throw new InvalidDataException("Invalid channel");
                if (int.TryParse(Get(row, "ViewerCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var viewers)) channel.ViewerCount = viewers;
                if (DateTimeOffset.TryParse(Get(row, "LiveStartedAt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var started)) channel.LiveStartedAt = started;
                var state = Get(row, "ProviderState");
                channel.ProviderState = string.IsNullOrEmpty(state) ? new ProviderState() : json.Deserialize<ProviderState>(state) ?? new ProviderState();
                channel.ProviderState.RecentContentIds = channel.ProviderState.RecentContentIds ?? new List<string>();
                channel.State = LiveState.Unknown; // Re-evaluate live status after restart.
                profile.Channels.Add(channel);
            }
            return profile;
        }

        internal void Write(StreamerProfile profile, PlatformRegistry registry = null)
        {
            if (string.IsNullOrEmpty(profile.Id)) profile.Id = Guid.NewGuid().ToString("D");
            var directory = Folder(profile.Id);
            Directory.CreateDirectory(directory);
            profile.Appearance = profile.Appearance ?? new NotificationAppearance();
            var appearance = profile.Appearance;
            string previous = appearance.Background;
            string staged = null;
            if (!string.IsNullOrWhiteSpace(appearance.PendingBackgroundPath))
            {
                BackgroundImage.Validate(appearance.PendingBackgroundPath);
                var ext = Path.GetExtension(appearance.PendingBackgroundPath).ToLowerInvariant();
                staged = "notification-" + Guid.NewGuid().ToString("N") + ext;
                File.Copy(appearance.PendingBackgroundPath, Path.Combine(directory, staged));
                appearance.Background = staged;
            }
            var lines = new List<string>();
            Section(lines, "Profile"); Add(lines, "Id", profile.Id); Add(lines, "Name", profile.DisplayName);
            Add(lines, "Memo", profile.Memo); Add(lines, "ChannelCount", profile.Channels.Count.ToString(CultureInfo.InvariantCulture));
            Section(lines, "NotificationAppearance"); Add(lines, "Background", appearance.Background);
            Add(lines, "TextColor", appearance.TextColor); Add(lines, "OutlineColor", appearance.OutlineColor);
            for (int i = 0; i < profile.Channels.Count; i++)
            {
                var c = profile.Channels[i];
                registry?.ForPlatform(c.Platform)?.PrepareForSave(c);
                Section(lines, "Channel." + i); Add(lines, "Id", c.Id); Add(lines, "Provider", ((int)c.Platform).ToString(CultureInfo.InvariantCulture));
                Add(lines, "ChannelId", c.ChannelId); Add(lines, "Url", c.LiveUrl); Add(lines, "Name", c.Name);
                Add(lines, "ImageUrl", c.ImageUrl); Add(lines, "CreatorOid", c.CreatorOid);
                Add(lines, "Notifications", c.NotificationsEnabled.ToString()); Add(lines, "AutoOpen", c.AutoOpenLive.ToString());
                Add(lines, "LiveSessionId", c.LiveSessionId); Add(lines, "LiveStartedAt", c.LiveStartedAt?.ToString("O", CultureInfo.InvariantCulture));
                Add(lines, "LiveTitle", c.LiveTitle); Add(lines, "Category", c.Category);
                Add(lines, "ViewerCount", c.ViewerCount?.ToString(CultureInfo.InvariantCulture));
                Add(lines, "LatestContentUrl", c.LatestContentUrl);
                Add(lines, "ProviderState", json.Serialize(c.ProviderState ?? new ProviderState()));
            }
            var destination = Path.Combine(directory, "profile.ini");
            try
            {
                Atomic(destination, string.Join(Environment.NewLine, lines) + Environment.NewLine);
                appearance.BackgroundPath = BackgroundFile(profile);
                appearance.PendingBackgroundPath = null;
                foreach (var old in Directory.GetFiles(directory, "notification*"))
                {
                    if (!SafeBackgroundName(Path.GetFileName(old)) ||
                        string.Equals(Path.GetFileName(old), appearance.Background, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            catch
            {
                if (staged != null) { appearance.Background = previous; File.Delete(Path.Combine(directory, staged)); }
                throw;
            }
        }
        internal void Delete(string id)
        {
            if (id == StreamerProfile.BuiltInProfileId)
                throw new InvalidOperationException("The built-in profile cannot be deleted.");
            var folder = Folder(id);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        internal string BackgroundFile(StreamerProfile profile)
        {
            var name = profile.Appearance?.Background;
            if (!SafeBackgroundName(name)) return null;
            var file = Path.Combine(Folder(profile.Id), name);
            return File.Exists(file) ? file : null;
        }
        private static bool SafeBackgroundName(string name) => !string.IsNullOrWhiteSpace(name) &&
            name == Path.GetFileName(name) && !name.Contains("..") && BackgroundImage.Supported(name);
        private static string Get(Dictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : null;
        private static void Section(List<string> lines, string name) { lines.Add(""); lines.Add("[" + name + "]"); }
        private static void Add(List<string> lines, string key, string value) => lines.Add(key + "=" + Uri.EscapeDataString(value ?? ""));
        internal static void Atomic(string destination, string content)
        {
            var temp = destination + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(destination)) File.Replace(temp, destination, null); else File.Move(temp, destination);
        }
    }
}
