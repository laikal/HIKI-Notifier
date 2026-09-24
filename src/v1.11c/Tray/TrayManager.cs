using System;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.Tray
{
    internal sealed class TrayManager : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly Action open, about, exit;
        private readonly Action<ChannelProfile, bool> changeNotification;
        private readonly AppSettings settings;
        private readonly LanguageService language;
        public TrayManager(AppSettings settings, LanguageService language, Action open, Action about, Action exit, Action<ChannelProfile, bool> changeNotification)
        {
            this.settings = settings; this.language = language; this.open = open; this.about = about; this.exit = exit; this.changeNotification = changeNotification;
            icon = new NotifyIcon { Icon = AppInfo.Icon, Text = "HIKI Notifier", Visible = true, ContextMenuStrip = menu };
            icon.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left) open(); };
            Rebuild();
        }
        public void Rebuild()
        {
            menu.Items.Clear();
            menu.Items.Add(language.Get("Tray", "Open"), null, (s, e) => open());
            var profiles = new ToolStripMenuItem(language.Get("Tray", "Profiles"));
            foreach (var streamer in settings.Streamers)
            {
                var streamerMenu = new ToolStripMenuItem(streamer.DisplayName);
                foreach (var channel in streamer.Channels)
                {
                    var channelMenu = new ToolStripMenuItem(channel.Platform.ToString().ToUpperInvariant());
                    var enabled = new ToolStripMenuItem(channel.Platform == PlatformType.YouTube ?
                        language.Get("Profiles", "UseNewContentNotifications") : language.Get("Profiles", "UseNotifications"))
                        { Checked = channel.NotificationsEnabled, CheckOnClick = true };
                    enabled.CheckedChanged += (s, e) => changeNotification(channel, enabled.Checked);
                    channelMenu.DropDownItems.Add(enabled);
                    channelMenu.DropDownItems.Add(channel.Platform == PlatformType.YouTube ?
                        language.Get("General", "OpenChannel") : language.Get("General", "WatchLive"),
                        null, (s, e) => NotificationService.Open(channel.LiveUrl));
                    streamerMenu.DropDownItems.Add(channelMenu);
                }
                profiles.DropDownItems.Add(streamerMenu);
            }
            menu.Items.Add(profiles);
            menu.Items.Add(language.Get("Tray", "About"), null, (s, e) => about());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(language.Get("Tray", "Exit"), null, (s, e) => exit());
        }
        public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); }
    }
}
