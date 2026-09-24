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
        private readonly TrackBar notificationOpacity = new TrackBar();
        public SettingsForm(AppSettings settings, LanguageService language, Action apply, NotificationService notification)
        {
            language.Rescan();
            Text = language.Get("General", "Settings"); StartPosition = FormStartPosition.CenterParent; UiTheme.Apply(this);
            ClientSize = new Size(500, 475); MinimumSize = Size; MaximumSize = Size;
            notifications.Text = language.Get("Settings", "EnableNotifications"); notifications.SetBounds(20, 20, 460, 28); notifications.Checked = settings.NotificationsEnabled;
            startup.Text = language.Get("Settings", "RunAtStartup"); startup.SetBounds(20, 55, 460, 28); startup.Checked = settings.RunAtStartup;
            Controls.Add(notifications); Controls.Add(startup);
            Controls.Add(new Label { Text = language.Get("Settings", "Language"), Left = 20, Top = 96, Width = 130 });
            languageChoice.Name = "languageChoice";
            languageChoice.DropDownStyle = ComboBoxStyle.DropDownList; languageChoice.SetBounds(150, 91, 160, 28);
            foreach (var pack in language.Packs) languageChoice.Items.Add(pack);
            for (var i = 0; i < languageChoice.Items.Count; i++)
                if (((LanguagePack)languageChoice.Items[i]).Code == language.Language) languageChoice.SelectedIndex = i;
            Controls.Add(languageChoice);
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
            Controls.Add(new Label { Text = language.Get("Settings", "NotificationOpacity"), Left = 20, Top = 311, Width = 130, Height = 26 });
            notificationOpacity.Name = "notificationOpacity";
            notificationOpacity.SetBounds(150, 300, 260, 45);
            notificationOpacity.Minimum = 50; notificationOpacity.Maximum = 100;
            notificationOpacity.TickFrequency = 10; notificationOpacity.SmallChange = 1;
            notificationOpacity.LargeChange = 5;
            notificationOpacity.Value = AppSettings.ClampNotificationOpacity(settings.NotificationOpacity);
            var opacityValue = new Label { Name = "notificationOpacityValue", Left = 420, Top = 311, Width = 60,
                Height = 26, Text = notificationOpacity.Value + "%" };
            notificationOpacity.ValueChanged += (s, e) => opacityValue.Text = notificationOpacity.Value + "%";
            Controls.Add(notificationOpacity); Controls.Add(opacityValue);
            var test = new ModernButton { Text = language.Get("Settings", "TestSound"), Left = 20, Top = 365, Width = 200 };
            UiTheme.Style(test);
            test.Click += (s, e) => { var testSettings = new AppSettings { SoundMode = SelectedMode(), WavePath = wave.Text }; if (!notification.TestSound(testSettings)) MessageBox.Show(this, language.Get("Settings", "WaveError")); };
            Controls.Add(test);
            var testAlert = new ModernButton { Name = "testAlertButton", Text = language.Get("Settings", "TestAlert"), Left = 230, Top = 365, Width = 200 };
            UiTheme.Style(testAlert);
            testAlert.Click += (s, e) => notification.ShowTest(new AppSettings {
                NotificationsEnabled = true, SoundMode = SelectedMode(), WavePath = wave.Text,
                Language = language.Language, NotificationOpacity = notificationOpacity.Value });
            Controls.Add(testAlert);
            var save = new ModernButton { Text = language.Get("General", "Save"), Left = 290, Top = 425, Width = 90 };
            var cancel = new ModernButton { Text = language.Get("General", "Cancel"), Left = 390, Top = 425, Width = 90, DialogResult = DialogResult.Cancel };
            UiTheme.Style(save); UiTheme.Style(cancel);
            save.Click += (s, e) =>
            {
                try { new StartupService().SetEnabled(startup.Checked); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
                settings.RunAtStartup = startup.Checked; settings.NotificationsEnabled = notifications.Checked;
                settings.SoundMode = SelectedMode(); settings.WavePath = wave.Text.Trim();
                settings.NotificationOpacity = notificationOpacity.Value;
                settings.Language = (languageChoice.SelectedItem as LanguagePack)?.Code ?? "en";
                apply(); DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(save); Controls.Add(cancel); CancelButton = cancel;
        }
        private NotificationSoundMode SelectedMode() => custom.Checked ? NotificationSoundMode.CustomWave : silent.Checked ? NotificationSoundMode.Silent : NotificationSoundMode.BuiltIn;
    }
}
