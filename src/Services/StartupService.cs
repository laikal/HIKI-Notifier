using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HikiNotifier.Services
{
    internal sealed class StartupService
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "HIKI Notifier";
        public bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key != null && key.GetValue(ValueName) != null;
        }
        public void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --startup");
                else key.DeleteValue(ValueName, false);
            }
        }
    }
}
