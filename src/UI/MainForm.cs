using System;
using System.Drawing;
using System.Linq;
using System.Threading;
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
        private readonly NotificationService notification;
        private readonly PollingService polling;
        private readonly TrayManager tray;
        private readonly ListView list = new ProfileListView();
        private readonly PictureBox title = new PictureBox();
        private readonly Button add = new ModernButton(), edit = new ModernButton(), delete = new ModernButton(), watch = new ModernButton(), options = new ModernButton(), help = new ModernButton(), about = new ModernButton(), exit = new ModernButton();
        private bool exiting;
        public MainForm(bool startHidden)
        {
            settings = storage.Load(); language = new LanguageService(settings.Language);
            notification = new NotificationService();
            UiTheme.Apply(this);
            Size = new Size(790, 455); MinimumSize = Size; StartPosition = FormStartPosition.CenterScreen;
            list.SetBounds(18, 54, 738, 265); list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.BackColor = UiTheme.Surface; list.ForeColor = UiTheme.Text; list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = new Font("Segoe UI", 10F);
            list.Columns.Add("", 210); list.Columns.Add("", 105); list.Columns.Add("", 90); list.Columns.Add("", 100); list.Columns.Add("", 225);
            list.Columns[1].TextAlign = HorizontalAlignment.Left;
            list.Columns[2].TextAlign = HorizontalAlignment.Right;
            list.Columns[3].TextAlign = HorizontalAlignment.Center;
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.MouseDoubleClick += ProfileListMouseDoubleClick;
            Controls.Add(list);
            title.SetBounds(18, 7, 150, 42); title.SizeMode = PictureBoxSizeMode.Zoom;
            title.BackColor = UiTheme.Background;
            Controls.Add(title);
            var buttons = new[] { add, edit, delete, watch, options, help, about, exit };
            for (var i = 0; i < buttons.Length; i++)
            { buttons[i].SetBounds(18 + (i % 4) * 188, 329 + (i / 4) * 37, 176, 31); UiTheme.Style(buttons[i]); Controls.Add(buttons[i]); }
            add.Click += async (s, e) => await AddAsync(); edit.Click += async (s, e) => await EditAsync();
            delete.Click += (s, e) => DeleteSelected(); watch.Click += (s, e) => WatchSelected();
            options.Click += (s, e) => { using (var form = new SettingsForm(settings, language, SaveAndRefresh, notification)) form.ShowDialog(this); };
            help.Click += (s, e) => { using (var form = new HelpForm(language)) form.ShowDialog(this); };
            about.Click += (s, e) => ShowAbout(); exit.Click += async (s, e) => await ExitAsync();
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += (s, e) => { if (!exiting) { e.Cancel = true; Hide(); } };
            tray = new TrayManager(settings, language, ShowWindow, ShowAbout, () => { var ignored = ExitAsync(); },
                (profile, enabled) => SetProfileNotifications(profile, enabled));
            polling = new PollingService(client, rplay, () => settings.Profiles);
            polling.StateChanged += OnStateChanged;
            RefreshText(); RefreshList(); polling.Start();
            if (startHidden) { ShowInTaskbar = false; Shown += (s, e) => Hide(); }
        }
        private void OnStateChanged(ChannelProfile profile, LiveState previous)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => {
                if (IsDisposed || !settings.Profiles.Contains(profile)) return;
                RefreshList();
                if (PollingService.ShouldNotify(previous, profile.State))
                    notification.Show(profile, settings);
            })); } catch (InvalidOperationException) { }
        }
        private ChannelProfile Selected => list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag as ChannelProfile : null;
        private void ProfileListMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = list.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;
            var profile = hit.Item.Tag as ChannelProfile;
            if (profile == null) return;
            var column = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (column == 3)
                SetProfileNotifications(profile, !profile.NotificationsEnabled);
            else if (column == 0 || column == 1 || column == 2 || column == 4)
                NotificationService.Open(profile.LiveUrl);
        }
        private void RefreshText()
        {
            title.Image = LogoResources.MainLogo(language.Language);
            Text = language.Get("General", "AppName") + " v" + AppInfo.Version;
            list.Columns[0].Text = language.Get("General", "Channel"); list.Columns[1].Text = language.Get("General", "State");
            list.Columns[2].Text = language.Get("General", "Viewers");
            list.Columns[3].Text = language.Get("Profiles", "Notifications"); list.Columns[4].Text = language.Get("General", "Title");
            add.Text = language.Get("Profiles", "Add"); edit.Text = language.Get("Profiles", "Edit"); delete.Text = language.Get("Profiles", "Delete");
            watch.Text = language.Get("General", "WatchLive"); options.Text = language.Get("General", "Settings"); help.Text = language.Get("Help", "Title");
            about.Text = language.Get("Tray", "About"); exit.Text = language.Get("Tray", "Exit"); tray.Rebuild();
        }
        private void RefreshList()
        {
            var selectedId = Selected?.ChannelId;
            list.BeginUpdate(); list.Items.Clear();
            foreach (var p in settings.Profiles)
            {
                var state = p.State == LiveState.Live ? "LIVE" : p.State == LiveState.Offline ? language.Get("General", "Offline") : language.Get("General", "Unknown");
                var viewers = p.State == LiveState.Live && p.ViewerCount.HasValue
                    ? p.ViewerCount.Value.ToString("N0") + (language.Language == "ko" ? "명" : "") : "-";
                var row = new ListViewItem(new[] { p.Name, state, viewers, p.NotificationsEnabled ? "ON" : "OFF", p.LiveTitle ?? "" }) { Tag = p };
                row.ForeColor = UiTheme.Text;
                row.UseItemStyleForSubItems = false;
                row.SubItems[1].ForeColor = p.State == LiveState.Live ? UiTheme.Live : UiTheme.Muted;
                list.Items.Add(row); if (p.ChannelId == selectedId) row.Selected = true;
            }
            list.EndUpdate(); UpdateButtons(); tray.Rebuild();
        }
        private void UpdateButtons() { edit.Enabled = watch.Enabled = Selected != null; delete.Enabled = Selected != null && !Selected.IsBuiltIn; }
        private async System.Threading.Tasks.Task AddAsync()
        {
            using (var form = new ProfileEditForm(language))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                var rplayOid = RplayClient.ParseLiveUrl(form.LiveUrl);
                var platform = rplayOid == null ? PlatformType.Chzzk : PlatformType.Rplay;
                var id = rplayOid ?? ChzzkClient.ParseLiveUrl(form.LiveUrl);
                if (settings.Profiles.Any(p => p.Platform == platform && p.ChannelId == id)) { MessageBox.Show(this, language.Get("Profiles", "Duplicate")); return; }
                try { var profile = await GetProfileAsync(form.LiveUrl); profile.NotificationsEnabled = form.NotificationsEnabled; settings.Profiles.Add(profile); SaveAndRefresh(); }
                catch (Exception) { MessageBox.Show(this, language.Get("Profiles", "FetchError")); }
            }
        }
        private async System.Threading.Tasks.Task EditAsync()
        {
            var current = Selected; if (current == null) return;
            using (var form = new ProfileEditForm(language, current))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                if (current.IsBuiltIn)
                {
                    SetProfileNotifications(current, form.NotificationsEnabled);
                    return;
                }
                var rplayOid = RplayClient.ParseLiveUrl(form.LiveUrl);
                var platform = rplayOid == null ? PlatformType.Chzzk : PlatformType.Rplay;
                var id = rplayOid ?? ChzzkClient.ParseLiveUrl(form.LiveUrl);
                if (settings.Profiles.Any(p => p != current && p.Platform == platform && p.ChannelId == id)) { MessageBox.Show(this, language.Get("Profiles", "Duplicate")); return; }
                if (platform == current.Platform && id == current.ChannelId)
                {
                    if (platform == PlatformType.Rplay) current.LiveUrl = form.LiveUrl;
                    SetProfileNotifications(current, form.NotificationsEnabled);
                    if (platform == PlatformType.Rplay) SaveAndRefresh();
                    return;
                }
                try {
                    var replacement = await GetProfileAsync(form.LiveUrl);
                    replacement.NotificationsEnabled = current.NotificationsEnabled;
                    settings.Profiles[settings.Profiles.IndexOf(current)] = replacement;
                    SetProfileNotifications(replacement, form.NotificationsEnabled, true);
                }
                catch (Exception) { MessageBox.Show(this, language.Get("Profiles", "FetchError")); }
            }
        }
        private async System.Threading.Tasks.Task<ChannelProfile> GetProfileAsync(string liveUrl)
        {
            if (RplayClient.ParseLiveUrl(liveUrl) == null)
                return await client.GetProfileAsync(ChzzkClient.ParseLiveUrl(liveUrl), CancellationToken.None);
            var profile = RplayClient.CreateProfile(liveUrl);
            try {
                var live = await rplay.GetLiveListAsync(CancellationToken.None);
                RplayLiveInfo info;
                RplayClient.Apply(profile, live.TryGetValue(profile.CreatorOid, out info) ? info : null);
            }
            catch (Exception) { RplayClient.MarkUnknown(profile); }
            return profile;
        }
        private void DeleteSelected()
        {
            var profile = Selected; if (profile == null || profile.IsBuiltIn) return;
            if (MessageBox.Show(this, language.Get("Profiles", "ConfirmDelete"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            settings.Profiles.Remove(profile); SaveAndRefresh();
        }
        private void WatchSelected() { if (Selected != null) NotificationService.Open(Selected.LiveUrl); }
        private void SetProfileNotifications(ChannelProfile profile, bool enabled, bool saveWhenUnchanged = false)
        {
            var wasEnabled = profile.NotificationsEnabled;
            if (wasEnabled == enabled && !saveWhenUnchanged) return;
            profile.NotificationsEnabled = enabled;
            SaveAndRefresh();
            if (!wasEnabled && enabled && profile.State == LiveState.Live)
                notification.Show(profile, settings, explicitReenable: true);
        }
        private void SaveAndRefresh()
        {
            try { storage.Save(settings); } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            language.Language = settings.Language; RefreshText(); RefreshList();
        }
        private void ShowAbout() { using (var form = new AboutForm()) form.ShowDialog(this); }
        private void ShowWindow() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        private async System.Threading.Tasks.Task ExitAsync()
        {
            if (exiting) return; exiting = true;
            await polling.StopAsync();
            try { storage.Save(settings); } catch (Exception) { }
            tray.Dispose(); polling.Dispose(); client.Dispose(); rplay.Dispose();
            Close(); Application.Exit();
        }
    }
}
