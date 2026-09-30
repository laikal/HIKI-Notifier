using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.UI;

internal static class V120Checks
{
    internal static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "hiki-v120-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var cleanPath = Path.Combine(root, "clean", "settings.json");
            var cleanService = new SettingsService(cleanPath);
            var clean = cleanService.Load();
            check(cleanService.CanSave && clean.Streamers.Count == 1 && clean.Streamers[0].IsBuiltIn &&
                clean.Streamers[0].Channels.Count == 2 && File.Exists(cleanPath) &&
                File.Exists(Path.Combine(root, "clean", "Profiles", "hiki-builtin", "profile.ini")),
                "empty portable folder initializes global settings and built-in profile");
            var cleanGlobal = File.ReadAllText(cleanPath);
            check(!cleanGlobal.Contains("Hikimori Neko") && !cleanGlobal.Contains("Streamers"),
                "clean settings JSON excludes profile data");
            File.WriteAllText(cleanPath, "{broken");
            var recoveredService = new SettingsService(cleanPath);
            var recovered = recoveredService.Load();
            check(recoveredService.CanSave && recovered.Streamers.Single().IsBuiltIn &&
                File.Exists(cleanPath + ".broken") && File.Exists(cleanPath),
                "corrupt global settings backed up and profiles remain loadable");
            var settingsPath = Path.Combine(root, "settings.json");
            var id = Guid.NewGuid().ToString("D");
            var profile = new StreamerProfile { Id = id, DisplayName = "Migrated creator", Memo = "keep memo" };
            profile.Channels.Add(new ChannelProfile { Id = Guid.NewGuid().ToString("D"),
                StreamerProfileId = id, Platform = PlatformType.Chzzk, ChannelId = "1234567890abcdef1234567890abcdef",
                LiveUrl = "https://chzzk.naver.com/live/1234567890abcdef1234567890abcdef", Name = "Migrated creator",
                NotificationsEnabled = false, AutoOpenLive = true, LiveSessionId = "session-1" });
            var serializer = new JavaScriptSerializer();
            var oldProfiles = Path.Combine(root, "profiles"); Directory.CreateDirectory(oldProfiles);
            File.WriteAllText(Path.Combine(oldProfiles, id + ".json"), serializer.Serialize(profile));
            var legacy = new AppSettings();
            File.WriteAllText(settingsPath, serializer.Serialize(legacy));
            var service = new SettingsService(settingsPath);
            var loaded = service.Load();
            check(service.CanSave && service.LoadErrors.Count == 0, "legacy migration succeeds");
            check(loaded.Streamers.Count == 2 && loaded.Streamers.Any(s => s.Id == id), "profile ID/count retained");
            var migrated = loaded.Streamers.Single(s => s.Id == id);
            check(migrated.Memo == "keep memo" && migrated.Channels.Count == 1 &&
                !migrated.Channels[0].NotificationsEnabled && migrated.Channels[0].AutoOpenLive &&
                migrated.Channels[0].LiveSessionId == "session-1", "channel options and session retained");
            check(File.Exists(settingsPath + ".pre-1.20C.bak") &&
                File.Exists(Path.Combine(root, "Profiles", id, "profile.ini")) &&
                File.Exists(Path.Combine(root, "Profiles", StreamerProfile.BuiltInProfileId, "profile.ini")),
                "backup and individual INIs written");
            var global = File.ReadAllText(settingsPath);
            check(!global.Contains("Migrated creator") && !global.Contains("Streamers") && !global.Contains("Profiles"),
                "settings JSON contains no profile data");
            var secondService = new SettingsService(settingsPath);
            var second = secondService.Load();
            check(second.Streamers.Count == 2 && second.Streamers.Single(s => s.Id == id).Channels.Count == 1,
                "restart does not duplicate profiles");
            var created = new StreamerProfile { DisplayName = "New creator" };
            created.Channels.Add(new ChannelProfile { Platform = PlatformType.Chzzk,
                ChannelId = "abcdef1234567890abcdef1234567890",
                LiveUrl = "https://chzzk.naver.com/live/abcdef1234567890abcdef1234567890",
                Name = "New creator", StreamerProfileId = created.Id });
            second.Streamers.Add(created); service.Save(second);
            check(new SettingsService(settingsPath).Load().Streamers.Any(s => s.Id == created.Id),
                "new profile saves and reloads");
            second.Streamers.Remove(created); service.DeleteStreamer(created.Id); service.Save(second);
            check(!new SettingsService(settingsPath).Load().Streamers.Any(s => s.Id == created.Id),
                "user profile deletes without reappearing");

            var builtin = loaded.Streamers.Single(s => s.IsBuiltIn);
            var builtInChannelIds = builtin.Channels.Select(c => c.Id).ToArray();
            var jpg = Path.Combine(root, "source.jpg");
            var jpeg = Path.Combine(root, "source.jpeg");
            var gif = Path.Combine(root, "source.gif");
            using (var bitmap = new Bitmap(480, 270))
            { bitmap.Save(jpg, ImageFormat.Jpeg); bitmap.Save(jpeg, ImageFormat.Jpeg); bitmap.Save(gif, ImageFormat.Gif); }
            BackgroundImage.Validate(jpg); BackgroundImage.Validate(jpeg); BackgroundImage.Validate(gif);
            builtin.Appearance.PendingBackgroundPath = jpg;
            builtin.Appearance.TextColor = "#123456"; builtin.Appearance.OutlineColor = "#ABCDEF";
            service.Save(loaded);
            var withAppearance = new SettingsService(settingsPath).Load();
            var savedBuiltin = withAppearance.Streamers.Single(s => s.IsBuiltIn);
            check(savedBuiltin.Channels.Select(c => c.Id).SequenceEqual(builtInChannelIds) &&
                savedBuiltin.Appearance.TextColor == "#123456" && savedBuiltin.Appearance.OutlineColor == "#ABCDEF" &&
                File.Exists(savedBuiltin.Appearance.BackgroundPath), "built-in appearance survives restart with channels");
            var actualAppearance = (NotificationAppearance)typeof(NotificationService)
                .GetMethod("ForProfile", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { savedBuiltin.Channels[0], withAppearance });
            check(ReferenceEquals(actualAppearance, savedBuiltin.Appearance),
                "built-in channel alert uses its saved profile appearance");
            var firstBackground = savedBuiltin.Appearance.BackgroundPath;
            savedBuiltin.Appearance.PendingBackgroundPath = jpeg;
            service.Save(withAppearance);
            check(!File.Exists(firstBackground), "replaced background cleaned");
            savedBuiltin.Appearance.PendingBackgroundPath = gif;
            service.Save(withAppearance);
            check(savedBuiltin.Appearance.BackgroundPath.EndsWith(".gif") && File.Exists(savedBuiltin.Appearance.BackgroundPath),
                "GIF background stored inside built-in profile folder");
            savedBuiltin.Appearance = new NotificationAppearance();
            service.Save(withAppearance);
            var reset = new SettingsService(settingsPath).Load().Streamers.Single(s => s.IsBuiltIn);
            check(reset.Appearance.BackgroundPath == null && reset.Channels.Select(c => c.Id).SequenceEqual(builtInChannelIds),
                "appearance reset preserves built-in profile and channels");
            var deletionBlocked = false;
            try { service.DeleteStreamer(StreamerProfile.BuiltInProfileId); }
            catch (InvalidOperationException) { deletionBlocked = true; }
            check(deletionBlocked && File.Exists(Path.Combine(root, "Profiles", StreamerProfile.BuiltInProfileId, "profile.ini")),
                "built-in profile cannot be deleted");

            var wrongSize = Path.Combine(root, "wrong.jpg");
            using (var bitmap = new Bitmap(470, 270)) bitmap.Save(wrongSize, ImageFormat.Jpeg);
            var rejected = false;
            try { BackgroundImage.Validate(wrongSize); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "wrong background dimensions rejected");
            var invalid = Path.Combine(root, "invalid.jpg"); File.WriteAllText(invalid, "not an image");
            rejected = false;
            try { BackgroundImage.Validate(invalid); } catch (Exception) { rejected = true; }
            check(rejected, "invalid image rejected");
            var png = Path.Combine(root, "unsupported.png"); File.WriteAllText(png, "x");
            rejected = false;
            try { BackgroundImage.Validate(png); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "unsupported PNG rejected");
            var oversized = Path.Combine(root, "oversized.jpg");
            using (var stream = File.Create(oversized)) stream.SetLength(BackgroundImage.MaxBytes + 1);
            rejected = false;
            try { BackgroundImage.Validate(oversized); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "background exceeding 15 MB rejected");
            check(AppSettings.ClampNotificationDuration(0) == 3 &&
                AppSettings.ClampNotificationDuration(9999) == 30 &&
                new AppSettings().NotificationDurationSeconds == 12, "duration clamps and defaults");

            var url = "https://chzzk.naver.com/live/1234567890abcdef1234567890abcdef";
            var opened = new List<string>();
            using (var alert = new NotificationForm(profile.Channels[0], 0, test: false,
                appearance: new NotificationAppearance(), openUrl: opened.Add))
            {
                check(alert.ClientSize == new Size(480, 270) && alert.FormBorderStyle == FormBorderStyle.None,
                    "borderless 480x270 notification");
                var text = alert.Controls.OfType<OutlinedText>().OrderBy(c => c.Top).ToArray();
                check(text.Length == 2 && text[0].Font.SizeInPoints == 15f &&
                    text[1].Font.SizeInPoints == 11.5f && text[1].MaxLines == 2 &&
                    text[0].Bottom < text[1].Top && text[1].Bottom < alert.ClientSize.Height,
                    "larger notification text fits separate title and body areas");
                Click(alert.Controls[0]); Click(alert);
                check(opened.SequenceEqual(new[] { url }), "child click opens once");
            }
            opened.Clear();
            using (var alert = new NotificationForm(profile.Channels[0], 0, test: true, openUrl: opened.Add))
            {
                check(alert.Controls.OfType<OutlinedText>().Select(c => c.Font.SizeInPoints)
                    .SequenceEqual(new[] { 15f, 11.5f }), "test notification uses the same text rendering");
                Click(alert.Controls[0]); check(opened.Count == 0, "test notification never opens browser");
            }
            var longText = new ChannelProfile { Platform = PlatformType.Chzzk,
                Name = new string('W', 220), LiveTitle = new string('W', 440),
                LiveUrl = url };
            using (var alert = new NotificationForm(longText, 0))
            using (var bitmap = new Bitmap(480, 270))
            {
                alert.DrawToBitmap(bitmap, new Rectangle(0, 0, 480, 270));
                check(alert.Controls.OfType<OutlinedText>().All(c => c.Right <= 480 && c.Bottom <= 270),
                    "long title and body render within notification bounds");
            }
            var animated = Environment.GetEnvironmentVariable("HIKI_V120_GIF");
            if (!string.IsNullOrEmpty(animated))
            {
                using (var alert = new NotificationForm(profile.Channels[0], 0,
                    appearance: new NotificationAppearance { BackgroundPath = animated }))
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var count = (int)typeof(NotificationForm).GetField("frameCount", flags).GetValue(alert);
                    var timer = (Timer)typeof(NotificationForm).GetField("frameTimer", flags).GetValue(alert);
                    var tick = typeof(Timer).GetMethod("OnTick", flags);
                    check(count > 1 && tick != null, "animated GIF frames loaded");
                    for (var i = 0; i < count; i++) tick.Invoke(timer, new object[] { EventArgs.Empty });
                    check((int)typeof(NotificationForm).GetField("frameIndex", flags).GetValue(alert) == 0,
                        "GIF frames wrap independently of loop metadata");
                }
                using (File.Open(animated, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                check(true, "GIF resources released on dispose");
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Click(Control control)
    {
        typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) });
    }
}
