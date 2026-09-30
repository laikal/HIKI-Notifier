using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HikiNotifier.Models;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class NotificationAppearanceForm : Form
    {
        private readonly LanguageService language;
        private readonly StreamerProfile streamer;
        private readonly AppSettings settings;
        private readonly NotificationService notification;
        private readonly Label backgroundName = new Label();
        private readonly Button textButton = new ModernButton(), outlineButton = new ModernButton();
        private NotificationAppearance draft;
        internal NotificationAppearance Result { get; private set; }

        internal NotificationAppearanceForm(LanguageService language, StreamerProfile streamer, NotificationAppearance original,
            AppSettings settings, NotificationService notification)
        {
            this.language = language; this.streamer = streamer; this.settings = settings ?? new AppSettings { Language = language.Language };
            this.notification = notification ?? new NotificationService();
            draft = original?.Copy() ?? new NotificationAppearance();
            Text = language.Get("Appearance", "Title"); StartPosition = FormStartPosition.CenterParent; UiTheme.Apply(this);
            ClientSize = new Size(445, 245); MinimumSize = Size; MaximumSize = Size;
            Controls.Add(new Label { Text = language.Get("Appearance", "Background"), Left = 18, Top = 20, Width = 110 });
            backgroundName.SetBounds(130, 20, 285, 22); Controls.Add(backgroundName);
            var choose = new ModernButton { Text = language.Get("Appearance", "SelectImage"), Left = 18, Top = 50, Width = 130 };
            var remove = new ModernButton { Text = language.Get("Appearance", "RemoveImage"), Left = 160, Top = 50, Width = 130 };
            UiTheme.Style(choose); UiTheme.Style(remove); Controls.Add(choose); Controls.Add(remove);
            choose.Click += (s, e) => SelectImage(); remove.Click += (s, e) => { draft.Background = "";
                draft.BackgroundPath = null; draft.PendingBackgroundPath = null; RefreshControls(); };
            textButton.SetBounds(18, 92, 195, 32); outlineButton.SetBounds(225, 92, 195, 32);
            textButton.Text = language.Get("Appearance", "TextColor"); outlineButton.Text = language.Get("Appearance", "OutlineColor");
            UiTheme.Style(textButton); UiTheme.Style(outlineButton); Controls.Add(textButton); Controls.Add(outlineButton);
            textButton.Click += (s, e) => PickColor(true); outlineButton.Click += (s, e) => PickColor(false);
            var reset = new ModernButton { Text = language.Get("Appearance", "Reset"), Left = 225, Top = 145, Width = 194 };
            UiTheme.Style(reset); Controls.Add(reset);
            reset.Click += (s, e) => { draft = new NotificationAppearance(); RefreshControls(); };
            var preview = new ModernButton { Text = language.Get("Appearance", "Test"), Left = 18, Top = 145, Width = 194 };
            UiTheme.Style(preview); Controls.Add(preview);
            preview.Click += (s, e) => notification.ShowTest(streamer, draft, this.settings);
            var save = new ModernButton { Text = language.Get("General", "Save"), Left = 225, Top = 194, Width = 90 };
            var cancel = new ModernButton { Text = language.Get("General", "Cancel"), Left = 325, Top = 194, Width = 90,
                DialogResult = DialogResult.Cancel };
            UiTheme.Style(save); UiTheme.Style(cancel); Controls.Add(save); Controls.Add(cancel);
            save.Click += (s, e) => { Result = draft.Copy(); DialogResult = DialogResult.OK; Close(); };
            AcceptButton = save; CancelButton = cancel;
            RefreshControls();
        }
        private void SelectImage()
        {
            using (var dialog = new OpenFileDialog { Filter = "JPG or GIF (*.jpg;*.jpeg;*.gif)|*.jpg;*.jpeg;*.gif" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    HikiNotifier.Services.BackgroundImage.Validate(dialog.FileName);
                    draft.PendingBackgroundPath = dialog.FileName;
                    draft.BackgroundPath = dialog.FileName;
                    draft.Background = Path.GetFileName(dialog.FileName);
                    RefreshControls();
                }
                catch (Exception ex)
                {
                    var key = ex is InvalidDataException ? ex.Message : "InvalidImage";
                    MessageBox.Show(this, language.Get("Appearance", key), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        private void PickColor(bool text)
        {
            using (var dialog = new ColorDialog { Color = NotificationColors.Parse(text ? draft.TextColor : draft.OutlineColor,
                text ? Color.White : Color.FromArgb(32, 32, 32)), FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var color = string.Format("#{0:X2}{1:X2}{2:X2}", dialog.Color.R, dialog.Color.G, dialog.Color.B);
                if (text) draft.TextColor = color; else draft.OutlineColor = color;
                RefreshControls();
            }
        }
        private void RefreshControls()
        {
            backgroundName.Text = string.IsNullOrWhiteSpace(draft.Background) ? language.Get("Appearance", "NoImage") : draft.Background;
            textButton.BackColor = NotificationColors.Parse(draft.TextColor, Color.White);
            outlineButton.BackColor = NotificationColors.Parse(draft.OutlineColor, Color.FromArgb(32, 32, 32));
        }
    }
}
