using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.Tray;

namespace HikiNotifier.UI
{
    internal sealed class MainForm : Form
    {
        private readonly SettingsService storage = new SettingsService();
        private readonly AppSettings settings;
        private readonly LanguageService language;
        private readonly ChzzkClient client = new ChzzkClient();
        private readonly RplayClient rplay = new RplayClient();
        private readonly YouTubeClient youtube = new YouTubeClient();
        private readonly PlatformRegistry registry;
        private readonly NotificationService notification = new NotificationService();
        private readonly PollingService polling;
        private readonly TrayManager tray;
        private readonly ListView list = new ProfileListView();
        private readonly PictureBox title = new PictureBox();
        private readonly LogoEasterEgg logoEasterEgg = new LogoEasterEgg(NotificationService.Open);
        private readonly Button add = new ModernButton(), edit = new ModernButton(), delete = new ModernButton(),
            watch = new ModernButton(), options = new ModernButton(), help = new ModernButton(),
            about = new ModernButton(), exit = new ModernButton();
        private bool exiting;

        public MainForm(bool startHidden)
        {
            settings = storage.Load(); language = new LanguageService(settings.Language);
            registry = new PlatformRegistry(client, rplay, youtube);
            UiTheme.Apply(this);
            Size = new Size(880, 470); MinimumSize = Size; StartPosition = FormStartPosition.CenterScreen;
            list.SetBounds(18, 54, 828, 275); list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.ShowItemToolTips = true;
            list.BackColor = UiTheme.Surface; list.ForeColor = UiTheme.Text; list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = new Font("Segoe UI", 10F);
            list.Columns.Add("", 210); list.Columns.Add("", 110); list.Columns.Add("", 90);
            list.Columns.Add("", 100); list.Columns.Add("", 315);
            list.Columns[2].TextAlign = HorizontalAlignment.Right;
            list.Columns[3].TextAlign = HorizontalAlignment.Center;
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.MouseDoubleClick += ProfileListMouseDoubleClick;
            Controls.Add(list);
            title.SetBounds(18, 7, 150, 42); title.SizeMode = PictureBoxSizeMode.Zoom;
            title.BackColor = UiTheme.Background; Controls.Add(title);
            title.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) logoEasterEgg.RegisterClick(); };
            var buttons = new[] { add, edit, delete, watch, options, help, about, exit };
            for (var i = 0; i < buttons.Length; i++)
            { buttons[i].SetBounds(18 + (i % 4) * 208, 340 + (i / 4) * 37, 196, 31); UiTheme.Style(buttons[i]); Controls.Add(buttons[i]); }
            add.Click += async (s, e) => await AddAsync(); edit.Click += async (s, e) => await EditAsync();
            delete.Click += (s, e) => DeleteSelected(); watch.Click += (s, e) => WatchSelected();
            options.Click += (s, e) => { using (var form = new SettingsForm(settings, language, () => { SaveAndRefresh(); }, notification)) form.ShowDialog(this); };
            help.Click += (s, e) => { using (var form = new HelpForm(language)) form.ShowDialog(this); };
            about.Click += (s, e) => ShowAbout(); exit.Click += async (s, e) => await ExitAsync();
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += (s, e) => { if (!exiting) { e.Cancel = true; Hide(); } };
            tray = new TrayManager(settings, language, ShowWindow, ShowAbout, () => { var ignored = ExitAsync(); },
                (profile, enabled) => SetProfileNotifications(profile, enabled));
            polling = new PollingService(registry, () => settings.Profiles);
            polling.ProfileUpdated += OnProfileUpdated;
            RefreshText(); RefreshList(); polling.Start();
            if (storage.LoadErrors.Count > 0)
                Shown += (s, e) => MessageBox.Show(this, string.Join(Environment.NewLine, storage.LoadErrors),
                    "HIKI Notifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (startHidden) { ShowInTaskbar = false; Shown += (s, e) => Hide(); }
        }

        private void OnProfileUpdated(ChannelProfile profile, LiveState previous, YouTubeEntry entry)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => {
                if (IsDisposed || !settings.Profiles.Contains(profile)) return;
                if (profile.Platform == PlatformType.YouTube && storage.CanSave) SaveAndRefresh();
                else RefreshList();
                if (entry != null) notification.ShowContent(profile, settings, entry);
                else if (PollingService.ShouldNotify(previous, profile.State)) notification.Show(profile, settings);
            })); } catch (InvalidOperationException) { }
        }

        private object Selected => list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag : null;
        private ChannelProfile SelectedChannel => Selected as ChannelProfile;
        private StreamerProfile SelectedStreamer => Selected as StreamerProfile ??
            settings.Streamers.FirstOrDefault(s => s.Id == SelectedChannel?.StreamerProfileId);

        private void ProfileListMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = list.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;
            var channel = hit.Item.Tag as ChannelProfile;
            if (channel == null) return;
            var column = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (column == 3) SetProfileNotifications(channel, !channel.NotificationsEnabled);
            else if (column == 0 || column == 1 || column == 2 || column == 4)
                NotificationService.Open(channel.LiveUrl);
        }

        private void RefreshText()
        {
            title.Image = LogoResources.MainLogo(language.Language);
            Text = language.Get("General", "AppName") + " v" + AppInfo.Version;
            list.Columns[0].Text = language.Get("General", "Platform");
            list.Columns[1].Text = language.Get("General", "State");
            list.Columns[2].Text = language.Get("General", "Viewers");
            list.Columns[3].Text = language.Get("Profiles", "Notifications");
            list.Columns[4].Text = language.Get("General", "LatestTitle");
            add.Text = language.Get("Profiles", "Add"); edit.Text = language.Get("Profiles", "Edit");
            delete.Text = language.Get("Profiles", "Delete"); watch.Text = language.Get("General", "WatchLive");
            options.Text = language.Get("General", "Settings"); help.Text = language.Get("Help", "Title");
            about.Text = language.Get("Tray", "About"); exit.Text = language.Get("Tray", "Exit"); tray.Rebuild();
        }

        private void RefreshList()
        {
            var selected = Selected;
            list.BeginUpdate(); list.Items.Clear();
            foreach (var streamer in settings.Streamers)
            {
                var header = new ListViewItem(new[] { streamer.DisplayName, "", "", "", streamer.Memo ?? "" })
                    { Tag = streamer, ToolTipText = streamer.Memo ?? "" };
                list.Items.Add(header); if (ReferenceEquals(selected, streamer)) header.Selected = true;
                foreach (var channel in streamer.Channels)
                {
                    var capabilities = registry.ForPlatform(channel.Platform)?.Capabilities ?? PlatformCapabilities.None;
                    var live = (capabilities & PlatformCapabilities.LiveStatus) != 0;
                    var state = !live ? (channel.ProviderState?.BaselinePending == true ?
                        language.Get("General", "Pending") : "-") : channel.State == LiveState.Live ? "LIVE" :
                        channel.State == LiveState.Offline ? language.Get("General", "Offline") : language.Get("General", "Unknown");
                    var viewers = live && channel.State == LiveState.Live && channel.ViewerCount.HasValue
                        ? channel.ViewerCount.Value.ToString("N0") + (language.Language == "ko" ? "명" : "") : "-";
                    var latest = (capabilities & PlatformCapabilities.NewContent) != 0 ?
                        channel.ProviderState?.LatestContentTitle : channel.LiveTitle;
                    var row = new ListViewItem(new[] { "    " + channel.Platform.ToString().ToUpperInvariant(), state, viewers,
                        channel.NotificationsEnabled ? "ON" : "OFF", latest ?? "" }) { Tag = channel, ToolTipText = latest ?? "" };
                    row.UseItemStyleForSubItems = false;
                    list.Items.Add(row); if (ReferenceEquals(selected, channel)) row.Selected = true;
                }
            }
            list.EndUpdate(); UpdateButtons(); tray.Rebuild();
        }

        private void UpdateButtons()
        {
            edit.Enabled = SelectedStreamer != null;
            delete.Enabled = SelectedStreamer != null && !SelectedStreamer.IsBuiltIn;
            watch.Enabled = SelectedChannel != null;
            watch.Text = SelectedChannel?.Platform == PlatformType.YouTube ?
                language.Get("General", "OpenChannel") : language.Get("General", "WatchLive");
        }

        private async Task AddAsync()
        {
            using (var form = new ProfileEditForm(language, registry))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var streamer = new StreamerProfile { DisplayName = form.DisplayName, Memo = form.Memo };
                    foreach (var draft in form.Channels)
                    {
                        var channel = await registry.ForPlatform(draft.Platform).CreateAsync(draft.Url, CancellationToken.None);
                        if (IsDuplicate(channel, null)) { MessageBox.Show(this, language.Get("Profiles", "Duplicate")); return; }
                        if (string.IsNullOrWhiteSpace(channel.Name)) channel.Name = streamer.DisplayName;
                        channel.StreamerProfileId = streamer.Id; channel.NotificationsEnabled = draft.AlertEnabled;
                        streamer.Channels.Add(channel);
                    }
                    settings.Streamers.Add(streamer); RebuildProfiles(); SaveAndRefresh();
                }
                catch (YouTubeResolveException ex) { ShowYouTubeResolveError(ex); }
                catch (Exception) { MessageBox.Show(this, language.Get("Profiles", "FetchError")); }
            }
        }

        private async Task EditAsync()
        {
            var current = SelectedStreamer; if (current == null) return;
            using (var form = new ProfileEditForm(language, registry, current))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var updated = new List<ChannelProfile>();
                    var reenabled = new List<ChannelProfile>();
                    foreach (var draft in form.Channels)
                    {
                        ChannelProfile channel;
                        if (draft.Original != null && draft.Url == draft.Original.LiveUrl) channel = draft.Original;
                        else
                        {
                            channel = await registry.ForPlatform(draft.Platform).CreateAsync(draft.Url, CancellationToken.None);
                            if (draft.Original != null) channel.Id = draft.Original.Id;
                        }
                        if (string.IsNullOrWhiteSpace(channel.Name)) channel.Name = form.DisplayName;
                        if (IsDuplicate(channel, current)) { MessageBox.Show(this, language.Get("Profiles", "Duplicate")); return; }
                        channel.StreamerProfileId = current.Id;
                        if (draft.Original != null && !draft.Original.NotificationsEnabled && draft.AlertEnabled)
                            reenabled.Add(channel);
                        channel.NotificationsEnabled = draft.AlertEnabled;
                        updated.Add(channel);
                    }
                    current.DisplayName = current.IsBuiltIn ? "Hikimori Neko" : form.DisplayName;
                    current.Memo = form.Memo;
                    current.Channels = updated;
                    RebuildProfiles();
                    if (SaveAndRefresh()) foreach (var channel in reenabled) ReplayEnabledNotification(channel, false, true);
                }
                catch (YouTubeResolveException ex) { ShowYouTubeResolveError(ex); }
                catch (Exception) { MessageBox.Show(this, language.Get("Profiles", "FetchError")); }
            }
        }

        private bool IsDuplicate(ChannelProfile channel, StreamerProfile editing) => settings.Profiles.Any(p =>
            p.StreamerProfileId != editing?.Id && p.Platform == channel.Platform &&
            string.Equals(p.ChannelId, channel.ChannelId, StringComparison.OrdinalIgnoreCase));
        private void ShowYouTubeResolveError(YouTubeResolveException error)
        {
            MessageBox.Show(this, language.Get("Profiles", error.Failure == YouTubeResolveFailure.ChannelNotFound ?
                "YouTubeResolveFailed" : "YouTubeNetworkError"));
        }
        private void RebuildProfiles() { settings.Profiles = settings.Streamers.SelectMany(s => s.Channels).ToList(); }

        private void DeleteSelected()
        {
            var streamer = SelectedStreamer; if (streamer == null || streamer.IsBuiltIn) return;
            if (MessageBox.Show(this, language.Get("Profiles", "ConfirmDelete"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var index = settings.Streamers.IndexOf(streamer);
            settings.Streamers.Remove(streamer); RebuildProfiles();
            if (SaveAndRefresh())
            {
                try { storage.DeleteStreamer(streamer.Id); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            }
            else
            {
                settings.Streamers.Insert(index, streamer); RebuildProfiles(); RefreshList();
            }
        }
        private void WatchSelected() { if (SelectedChannel != null) NotificationService.Open(SelectedChannel.LiveUrl); }
        private void SetProfileNotifications(ChannelProfile profile, bool enabled)
        {
            var wasEnabled = profile.NotificationsEnabled;
            if (wasEnabled == enabled) return;
            profile.NotificationsEnabled = enabled;
            if (SaveAndRefresh()) ReplayEnabledNotification(profile, wasEnabled, enabled);
        }
        private void ReplayEnabledNotification(ChannelProfile profile, bool oldEnabled, bool newEnabled)
        {
            var provider = registry.ForPlatform(profile.Platform);
            notification.Replay(profile, settings, NotificationReplay.ForChange(provider, profile, oldEnabled, newEnabled));
        }
        private bool SaveAndRefresh()
        {
            bool success = true;
            try { storage.Save(settings); } catch (Exception ex) { success = false; MessageBox.Show(this, ex.Message); }
            language.Language = settings.Language; RefreshText(); RefreshList();
            return success;
        }
        private void ShowAbout() { using (var form = new AboutForm()) form.ShowDialog(this); }
        private void ShowWindow() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        private async Task ExitAsync()
        {
            if (exiting) return; exiting = true;
            await polling.StopAsync();
            try { storage.Save(settings); } catch (Exception) { }
            tray.Dispose(); polling.Dispose(); client.Dispose(); rplay.Dispose(); youtube.Dispose();
            Close(); Application.Exit();
        }
    }
}
