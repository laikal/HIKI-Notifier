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
            foreach (var profile in settings.Profiles)
            {
                var sub = new ToolStripMenuItem(profile.Name);
                var enabled = new ToolStripMenuItem(language.Get("Profiles", "Notifications")) { Checked = profile.NotificationsEnabled, CheckOnClick = true };
                enabled.CheckedChanged += (s, e) => changeNotification(profile, enabled.Checked);
                sub.DropDownItems.Add(enabled);
                sub.DropDownItems.Add(language.Get("General", "WatchLive"), null, (s, e) => NotificationService.Open(profile.LiveUrl));
                profiles.DropDownItems.Add(sub);
            }
            menu.Items.Add(profiles);
            menu.Items.Add(language.Get("Tray", "About"), null, (s, e) => about());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(language.Get("Tray", "Exit"), null, (s, e) => exit());
        }
        public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); }
    }
}
