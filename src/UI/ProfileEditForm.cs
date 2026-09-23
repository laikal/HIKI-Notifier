using System;
using System.Drawing;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class ProfileEditForm : Form
    {
        private readonly TextBox input = new TextBox();
        private readonly CheckBox notifications = new CheckBox();
        private readonly Label hint = new Label();
        public string LiveUrl => input.Text.Trim();
        public bool NotificationsEnabled => notifications.Checked;
        public ProfileEditForm(LanguageService language, ChannelProfile profile = null)
        {
            Text = language.Get("Profiles", profile == null ? "Add" : "Edit");
            StartPosition = FormStartPosition.CenterParent; UiTheme.Apply(this);
            ClientSize = new Size(500, 210); MinimumSize = Size; MaximumSize = Size;
            Controls.Add(new Label { Text = language.Get("Profiles", "LiveUrl"), Left = 20, Top = 21, Width = 460, ForeColor = ForeColor });
            input.SetBounds(20, 51, 460, 28); input.Text = profile?.LiveUrl ?? "https://chzzk.naver.com/live/";
            input.ReadOnly = profile != null && profile.IsBuiltIn; Controls.Add(input);
            notifications.Text = language.Get("Profiles", "UseNotifications"); notifications.SetBounds(20, 91, 460, 28);
            notifications.Checked = profile == null || profile.NotificationsEnabled; Controls.Add(notifications);
            hint.SetBounds(20, 127, 460, 22); hint.ForeColor = UiTheme.Error; Controls.Add(hint);
            var ok = new ModernButton { Text = language.Get("General", "Save"), Left = 290, Top = 161, Width = 90 };
            var cancel = new ModernButton { Text = language.Get("General", "Cancel"), Left = 390, Top = 161, Width = 90, DialogResult = DialogResult.Cancel };
            UiTheme.Style(ok); UiTheme.Style(cancel);
            ok.Click += (s, e) => { if (ChzzkClient.ParseLiveUrl(input.Text) == null && RplayClient.ParseLiveUrl(input.Text) == null) hint.Text = language.Get("Profiles", "InvalidUrl"); else { DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
