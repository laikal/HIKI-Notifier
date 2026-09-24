using System;
using System.Drawing;
using System.Windows.Forms;
using HikiNotifier.Services;

namespace HikiNotifier.UI
{
    internal sealed class AboutForm : Form
    {
        private const string GitHub = "https://github.com/laikal/HIKI-Notifier";
        private const string Chzzk = "https://chzzk.naver.com/688b22118a21cd70b53ad8b1d024b5d2";
        private const string Youtube = "https://www.youtube.com/@%ED%9E%88%ED%82%A4%EB%AA%A8%EB%A6%AC%EB%84%A4%EC%BD%94";
        public AboutForm()
        {
            var korean = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko";
            Text = korean ? "프로그램 정보" : "About";
            Size = new Size(775, 550); MinimumSize = Size; MaximumSize = Size; StartPosition = FormStartPosition.CenterParent;
            UiTheme.Apply(this);
            var image = AppInfo.LoadAboutImage();
            var picture = new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom, Left = 5, Top = 16, Width = 300, Height = 478, BackColor = UiTheme.Background };
            Controls.Add(picture); FormClosed += (s, e) => image.Dispose();
            AddLabel("HIKI Notifier", 315, 20, 425, 40, 22, FontStyle.Bold, UiTheme.Accent);
            AddLabel("Version " + AppInfo.Version, 317, 64, 420, 26, 10, FontStyle.Regular, UiTheme.Text);
            AddLabel(korean ? "Windows 64-bit 방송 알림 유틸리티" : "Windows 64-bit broadcast notification utility", 317, 94, 420, 25, 10, FontStyle.Regular, UiTheme.Text);
            AddLabel(korean ? "개발자: Eltax" : "Developer: Eltax", 317, 120, 420, 25, 10, FontStyle.Regular, UiTheme.Text);
            LinkButton("GitHub", GitHub, 317, 151, 112);
            var description = korean
                ? "이 프로그램은 Eltax가 Hikimori Neko의 방송 시작을\n보다 빠르게 확인하기 위해 제작한\nWindows용 치지직 방송 알림 팬메이드 유틸리티입니다.\n\nHikimori Neko 방송 프로파일이 기본으로 포함되어 있으며,\n사용자는 원하는 다른 치지직 채널도 자유롭게 등록하여\n방송 시작 알림을 받을 수 있습니다."
                : "This fan-made Windows utility was created by Eltax to notice\nwhen Hikimori Neko starts broadcasting.\n\nHikimori Neko is included as the default channel profile.\nYou can add other CHZZK channels and receive live alerts.";
            AddLabel(description, 317, 196, 430, 140, 9, FontStyle.Regular, UiTheme.Text);
            AddLabel(korean ? "고마운 사람" : "Special Thanks", 317, 344, 420, 24, 11, FontStyle.Bold, UiTheme.Accent);
            AddLabel("Hikimori Neko", 317, 372, 420, 23, 10, FontStyle.Bold, UiTheme.Text);
            AddLabel(korean ? "이 프로그램을 만들게 된 계기가 되어준 사람" : "The person who inspired this project.", 317, 397, 420, 24, 9, FontStyle.Regular, UiTheme.Text);
            LinkButton(korean ? "Hikimori Neko 치지직" : "Hikimori Neko CHZZK", Chzzk, 317, 429, 205);
            LinkButton("Hikimori Neko YouTube", Youtube, 317, 465, 205);
        }
        private void AddLabel(string value, int left, int top, int width, int height, float size, FontStyle style, Color color)
        { Controls.Add(new Label { Text = value, Left = left, Top = top, Width = width, Height = height, Font = new Font("Segoe UI", size, style), ForeColor = color }); }
        private void LinkButton(string label, string url, int left, int top, int width)
        { var button = new ModernButton { Text = label, Left = left, Top = top, Width = width, Height = 29 }; UiTheme.Style(button); button.Click += (s, e) => NotificationService.Open(url); Controls.Add(button); }
    }
}
