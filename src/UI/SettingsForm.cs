using System;
using System.Drawing;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class SettingsForm : Form
    {
        private readonly CheckBox notifications = new CheckBox(), startup = new CheckBox();
        private readonly RadioButton defaultSound = new RadioButton(), custom = new RadioButton(), silent = new RadioButton();
        private readonly TextBox wave = new TextBox();
        private readonly ComboBox languageChoice = new ComboBox();
        public SettingsForm(AppSettings settings, LanguageService language, Action apply, NotificationService notification)
        {
            Text = language.Get("General", "Settings"); StartPosition = FormStartPosition.CenterParent; UiTheme.Apply(this);
            ClientSize = new Size(500, 400); MinimumSize = Size; MaximumSize = Size;
            notifications.Text = language.Get("Settings", "EnableNotifications"); notifications.SetBounds(20, 20, 460, 28); notifications.Checked = settings.NotificationsEnabled;
            startup.Text = language.Get("Settings", "RunAtStartup"); startup.SetBounds(20, 55, 460, 28); startup.Checked = settings.RunAtStartup;
            Controls.Add(notifications); Controls.Add(startup);
            Controls.Add(new Label { Text = language.Get("Settings", "Language"), Left = 20, Top = 96, Width = 130 });
            languageChoice.DropDownStyle = ComboBoxStyle.DropDownList; languageChoice.SetBounds(150, 91, 160, 28);
            languageChoice.Items.AddRange(new object[] { "한국어", "English" }); languageChoice.SelectedIndex = settings.Language == "en" ? 1 : 0; Controls.Add(languageChoice);
            Controls.Add(new Label { Text = language.Get("Settings", "NotificationSound"), Left = 20, Top = 137, Width = 460 });
            defaultSound.Text = language.Get("Settings", "DefaultSound"); defaultSound.SetBounds(20, 164, 460, 25);
            custom.Text = language.Get("Settings", "CustomWave"); custom.SetBounds(20, 194, 460, 25);
            silent.Text = language.Get("Settings", "Silent"); silent.SetBounds(20, 267, 460, 25);
            defaultSound.Checked = settings.SoundMode == NotificationSoundMode.BuiltIn;
            custom.Checked = settings.SoundMode == NotificationSoundMode.CustomWave;
            silent.Checked = settings.SoundMode == NotificationSoundMode.Silent;
            wave.SetBounds(38, 225, 340, 28); wave.Text = settings.WavePath;
            var browse = new ModernButton { Text = language.Get("Settings", "Browse"), Left = 390, Top = 223, Width = 90 };
            UiTheme.Style(browse);
            browse.Click += (s, e) => { using (var dialog = new OpenFileDialog { Filter = "WAV files (*.wav)|*.wav" }) if (dialog.ShowDialog(this) == DialogResult.OK) { wave.Text = dialog.FileName; custom.Checked = true; } };
            Controls.Add(defaultSound); Controls.Add(custom); Controls.Add(silent); Controls.Add(wave); Controls.Add(browse);
            var test = new ModernButton { Text = language.Get("Settings", "TestSound"), Left = 20, Top = 308, Width = 200 };
            UiTheme.Style(test);
            test.Click += (s, e) => { var testSettings = new AppSettings { SoundMode = SelectedMode(), WavePath = wave.Text }; if (!notification.TestSound(testSettings)) MessageBox.Show(this, language.Get("Settings", "WaveError")); };
            Controls.Add(test);
            var testAlert = new ModernButton { Text = language.Get("Settings", "TestAlert"), Left = 230, Top = 308, Width = 200 };
            UiTheme.Style(testAlert);
            testAlert.Click += (s, e) => notification.Show(new ChannelProfile {
                ChannelId = ChannelProfile.BuiltInId, LiveUrl = ChannelProfile.UrlFor(ChannelProfile.BuiltInId),
                Name = "Hikimori Neko", LiveTitle = language.Get("Settings", "TestAlert"),
                NotificationsEnabled = true, State = LiveState.Live
            }, new AppSettings { NotificationsEnabled = true, SoundMode = SelectedMode(), WavePath = wave.Text });
            Controls.Add(testAlert);
            var save = new ModernButton { Text = language.Get("General", "Save"), Left = 290, Top = 350, Width = 90 };
            var cancel = new ModernButton { Text = language.Get("General", "Cancel"), Left = 390, Top = 350, Width = 90, DialogResult = DialogResult.Cancel };
            UiTheme.Style(save); UiTheme.Style(cancel);
            save.Click += (s, e) =>
            {
                try { new StartupService().SetEnabled(startup.Checked); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
                settings.RunAtStartup = startup.Checked; settings.NotificationsEnabled = notifications.Checked;
                settings.SoundMode = SelectedMode(); settings.WavePath = wave.Text.Trim(); settings.Language = languageChoice.SelectedIndex == 1 ? "en" : "ko";
                apply(); DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(save); Controls.Add(cancel); CancelButton = cancel;
        }
        private NotificationSoundMode SelectedMode() => custom.Checked ? NotificationSoundMode.CustomWave : silent.Checked ? NotificationSoundMode.Silent : NotificationSoundMode.BuiltIn;
    }
}
