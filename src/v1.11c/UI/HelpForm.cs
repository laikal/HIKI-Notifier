using System;
using System.Drawing;
using System.Windows.Forms;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class HelpForm : Form
    {
        public HelpForm(LanguageService language)
        {
            Text = language.Get("Help", "Title"); StartPosition = FormStartPosition.CenterParent;
            UiTheme.Apply(this);
            ClientSize = new Size(620, 490); MinimumSize = Size;
            var text = new Label { Left = 24, Top = 22, Width = 570, Height = 405, AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Segoe UI", 10), ForeColor = ForeColor, Text = language.Get("Help", "Body") };
            Controls.Add(text);
            var close = new ModernButton { Text = language.Get("General", "Close"), Left = 499, Top = 440, Width = 95,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.OK };
            UiTheme.Style(close);
            Controls.Add(close); AcceptButton = close;
        }
    }
}
