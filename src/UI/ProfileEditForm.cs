using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class ChannelDraft
    {
        public ChannelProfile Original { get; set; }
        public PlatformType Platform { get; set; }
        public string Url { get; set; }
        public bool AlertEnabled { get; set; }
        public bool AutoOpenLive { get; set; }
    }

    internal sealed class ProfileEditForm : Form
    {
        private readonly LanguageService language;
        private readonly PlatformRegistry registry;
        private readonly StreamerProfile streamer;
        private readonly TextBox name = new TextBox(), memo = new TextBox(), newUrl = new TextBox();
        private readonly FlowLayoutPanel channelRows = new FlowLayoutPanel();
        private readonly Label hint = new Label();
        private readonly ContextMenuStrip platformMenu = new ContextMenuStrip();
        private readonly List<Tuple<ChannelProfile, PlatformType, TextBox, CheckBox, CheckBox, Panel>> rows =
            new List<Tuple<ChannelProfile, PlatformType, TextBox, CheckBox, CheckBox, Panel>>();
        public string DisplayName => name.Text.Trim();
        public string Memo => memo.Text;
        public NotificationAppearance Appearance { get; private set; }
        public List<ChannelDraft> Channels { get; private set; }

        public ProfileEditForm(LanguageService language, PlatformRegistry registry, StreamerProfile streamer = null,
            AppSettings settings = null, NotificationService notification = null)
        {
            this.language = language; this.registry = registry; this.streamer = streamer;
            Appearance = streamer?.Appearance?.Copy() ?? new NotificationAppearance();
            Text = language.Get("Profiles", streamer == null ? "Add" : "Edit");
            StartPosition = FormStartPosition.CenterParent; UiTheme.Apply(this);
            ClientSize = new Size(590, 455); MinimumSize = Size; MaximumSize = Size;
            Controls.Add(new Label { Text = language.Get("Profiles", "Name"), Left = 20, Top = 18, Width = 110 });
            name.SetBounds(140, 15, 425, 28); name.Name = "profileName"; name.Text = streamer?.DisplayName ?? "";
            name.ReadOnly = streamer?.IsBuiltIn == true; Controls.Add(name);
            Controls.Add(new Label { Text = language.Get("Profiles", "Memo"), Left = 20, Top = 54, Width = 110 });
            memo.SetBounds(140, 52, 425, 52); memo.Multiline = true; memo.Text = streamer?.Memo ?? ""; Controls.Add(memo);
            Controls.Add(new Label { Text = language.Get("Profiles", "ConnectedChannels"), Left = 20, Top = 119, Width = 540 });
            channelRows.SetBounds(20, 142, 545, 191); channelRows.FlowDirection = FlowDirection.TopDown;
            channelRows.WrapContents = false; channelRows.AutoScroll = true; channelRows.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(channelRows);
            foreach (var channel in (IEnumerable<ChannelProfile>)streamer?.Channels ?? Enumerable.Empty<ChannelProfile>())
                AddRow(channel.Platform, channel.LiveUrl, channel.NotificationsEnabled, channel.AutoOpenLive, channel);
            newUrl.SetBounds(20, 345, 425, 28); newUrl.Name = "newChannelUrl";
            newUrl.Enabled = streamer?.IsBuiltIn != true;
            newUrl.KeyDown += (s, e) => { if (e.KeyCode != Keys.Enter) return;
                e.Handled = true; e.SuppressKeyPress = true; AddEnteredChannel(); };
            Controls.Add(newUrl);
            var addChannel = new ModernButton { Name = "addChannelButton",
                Text = language.Get("Profiles", "AddChannel") + " ▾", Left = 455, Top = 344, Width = 110 };
            UiTheme.Style(addChannel);
            addChannel.Enabled = streamer?.IsBuiltIn != true;
            foreach (var provider in registry.Providers)
                AddPlatformMenuItem(provider.Metadata.DisplayName, provider.Metadata.UrlTemplate);
            platformMenu.Closed += (s, e) =>
            {
                if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked || !newUrl.CanFocus) return;
                newUrl.Focus();
                newUrl.SelectionStart = newUrl.TextLength;
                newUrl.SelectionLength = 0;
            };
            addChannel.ContextMenuStrip = platformMenu;
            addChannel.Click += (s, e) => platformMenu.Show(addChannel, new Point(0, addChannel.Height));
            Disposed += (s, e) => platformMenu.Dispose();
            Controls.Add(addChannel);
            hint.SetBounds(20, 378, 350, 22); ClearHint(); Controls.Add(hint);
            var customize = new ModernButton { Name = "notificationAppearanceButton",
                Text = language.Get("Appearance", "Title") + "...", Left = 385, Top = 376, Width = 180 };
            UiTheme.Style(customize); Controls.Add(customize);
            customize.Click += (s, e) =>
            {
                var previewProfile = streamer ?? new StreamerProfile { DisplayName = DisplayName, Memo = Memo };
                if (streamer == null)
                    foreach (var row in rows) previewProfile.Channels.Add(new ChannelProfile { Platform = row.Item2 });
                using (var form = new NotificationAppearanceForm(language, previewProfile, Appearance, settings, notification))
                    if (form.ShowDialog(this) == DialogResult.OK) Appearance = form.Result;
            };
            var ok = new ModernButton { Name = "saveProfileButton", Text = language.Get("General", "Save"), Left = 375, Top = 410, Width = 90 };
            var cancel = new ModernButton { Text = language.Get("General", "Cancel"), Left = 475, Top = 410, Width = 90,
                DialogResult = DialogResult.Cancel };
            UiTheme.Style(ok); UiTheme.Style(cancel);
            ok.Click += (s, e) => SaveDraft();
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }

        private void AddPlatformMenuItem(string name, string template)
        {
            var item = platformMenu.Items.Add(name);
            item.Click += (s, e) =>
            {
                var current = newUrl.Text.Trim();
                if (current.Length == 0 || IsTemplate(current)) newUrl.Text = template;
                newUrl.Focus();
                newUrl.SelectionStart = newUrl.TextLength;
                newUrl.SelectionLength = 0;
            };
        }

        private bool IsTemplate(string url) => registry.Providers.Any(p => p.Metadata.UrlTemplate == url);

        private void ClearHint()
        {
            hint.ForeColor = UiTheme.Muted;
            hint.Text = language.Get("Profiles", "PressEnterToAddChannel");
        }

        private void ShowError(string key)
        {
            hint.ForeColor = UiTheme.Error;
            hint.Text = language.Get("Profiles", key);
        }

        private bool AddEnteredChannel()
        {
            var provider = registry.ForUrl(newUrl.Text);
            if (provider == null) { ShowError("InvalidUrl"); return false; }
            if (rows.Any(row => row.Item2 == provider.Platform)) { ShowError("DuplicatePlatform"); return false; }
            AddRow(provider.Platform, newUrl.Text.Trim(), true, false, null);
            newUrl.Clear(); ClearHint();
            return true;
        }

        private void AddRow(PlatformType platform, string url, bool enabled, bool autoOpenLive, ChannelProfile original)
        {
            var supportsLive = (registry.Capabilities(platform) & PlatformCapabilities.AutoOpen) != 0;
            var panel = new Panel { Width = 515, Height = supportsLive ? 79 : 55 };
            panel.Controls.Add(new Label { Text = registry.DisplayName(platform), Left = 3, Top = 5, Width = 85 });
            var address = new TextBox { Left = 93, Top = 2, Width = 420, Text = url, ReadOnly = streamer?.IsBuiltIn == true };
            panel.Controls.Add(address);
            var alert = new CheckBox { Left = 93, Top = 30, Width = 320, Height = 22,
                Text = (registry.Capabilities(platform) & PlatformCapabilities.NewContent) != 0 ? language.Get("Profiles", "UseNewContentNotifications") :
                    language.Get("Profiles", "UseNotifications"), Checked = enabled };
            panel.Controls.Add(alert);
            var autoOpen = new CheckBox { Name = "autoOpenLive", Left = 93, Top = 53, Width = 350, Height = 22,
                Text = language.Get("Profiles", "AutoOpenLive"),
                Checked = (supportsLive || registry.ForPlatform(platform) == null) && autoOpenLive };
            if (supportsLive) panel.Controls.Add(autoOpen);
            if (streamer?.IsBuiltIn != true)
            {
                var remove = new LinkLabel { Left = 446, Top = supportsLive ? 55 : 32, Width = 65,
                    Text = language.Get("Profiles", "RemoveChannel") };
                remove.Click += (s, e) => { channelRows.Controls.Remove(panel); rows.RemoveAll(row => row.Item6 == panel); panel.Dispose(); };
                panel.Controls.Add(remove);
            }
            rows.Add(Tuple.Create(original, platform, address, alert, autoOpen, panel));
            channelRows.Controls.Add(panel);
        }

        private void SaveDraft()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) { ShowError("NameRequired"); return; }
            if (!string.IsNullOrWhiteSpace(newUrl.Text))
            {
                // The add dialog's first URL can be entered without pressing Add Channel.
                if (!AddEnteredChannel()) return;
            }
            if (rows.Count == 0) { ShowError("ChannelRequired"); return; }
            var drafts = new List<ChannelDraft>();
            foreach (var row in rows)
            {
                if (!(row.Item1 != null && row.Item3.Text.Trim() == row.Item1.LiveUrl) &&
                    registry.ForUrl(row.Item3.Text)?.Platform != row.Item2)
                { ShowError("InvalidUrl"); return; }
                drafts.Add(new ChannelDraft { Original = row.Item1, Platform = row.Item2,
                    Url = row.Item3.Text.Trim(), AlertEnabled = row.Item4.Checked,
                    AutoOpenLive = row.Item5.Checked });
            }
            Channels = drafts;
            DialogResult = DialogResult.OK; Close();
        }
    }
}
