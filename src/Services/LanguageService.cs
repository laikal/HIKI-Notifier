using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HikiNotifier.Services
{
    internal sealed class LanguageService
    {
        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(string section, string key, string fallback, StringBuilder result, uint size, string path);
        public string Language { get; set; }
        public LanguageService(string language) { Language = language == "en" ? "en" : "ko"; }
        public string Get(string section, string key)
        {
            var value = Read(Language, section, key);
            return (string.IsNullOrEmpty(value) ? (Read("en", section, key) ?? key) : value).Replace("\\n", Environment.NewLine);
        }
        private static string Read(string language, string section, string key)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang", language + ".ini");
            if (!File.Exists(path)) return null;
            var buffer = new StringBuilder(2048);
            return GetPrivateProfileString(section, key, "", buffer, (uint)buffer.Capacity, path) == 0 ? null : buffer.ToString();
        }
    }
}
