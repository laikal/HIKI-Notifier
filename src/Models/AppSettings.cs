using System.Collections.Generic;

namespace HikiNotifier.Models
{
    internal sealed class AppSettings
    {
        public List<ChannelProfile> Profiles { get; set; } = new List<ChannelProfile>();
        public bool BuiltInNotificationsEnabled { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool RunAtStartup { get; set; }
        public string Language { get; set; } = "ko";
        public NotificationSoundMode SoundMode { get; set; } = NotificationSoundMode.BuiltIn;
        public string WavePath { get; set; } = "";
    }
}
