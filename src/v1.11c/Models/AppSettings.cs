using System.Collections.Generic;

namespace HikiNotifier.Models
{
    internal sealed class AppSettings
    {
        public List<ChannelProfile> Profiles { get; set; } = new List<ChannelProfile>();
        public List<StreamerProfile> Streamers { get; set; } = new List<StreamerProfile>();
        public int SettingsSchemaVersion { get; set; } = 2;
        public int ProfileStorageVersion { get; set; } = 1;
        public BuiltInHikiState BuiltInHikiState { get; set; } = new BuiltInHikiState();
        public bool BuiltInNotificationsEnabled { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool RunAtStartup { get; set; }
        public string Language { get; set; } = "ko";
        public int NotificationOpacity { get; set; } = 100;
        public NotificationSoundMode SoundMode { get; set; } = NotificationSoundMode.BuiltIn;
        public string WavePath { get; set; } = "";

        public static int ClampNotificationOpacity(int value) => value < 50 ? 50 : value > 100 ? 100 : value;
    }
}
