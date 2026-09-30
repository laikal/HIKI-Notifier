using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class SoundService
    {
        private const string DefaultResource = "HikiNotifier.Assets.DefaultNotification.wav";

        public bool Play(AppSettings settings)
        {
            if (settings.SoundMode == NotificationSoundMode.Silent) return true;
            if (settings.SoundMode == NotificationSoundMode.BuiltIn)
            {
                var stream = typeof(SoundService).Assembly.GetManifestResourceStream(DefaultResource);
                if (stream == null) return false;
                try
                {
                    Task.Run(() => {
                        try { using (stream) using (var player = new SoundPlayer(stream)) player.PlaySync(); }
                        catch (Exception) { }
                    });
                    return true;
                }
                catch (Exception) { stream.Dispose(); return false; }
            }
            if (settings.SoundMode != NotificationSoundMode.CustomWave) return false;
            try
            {
                if (string.IsNullOrWhiteSpace(settings.WavePath) || !File.Exists(settings.WavePath)) return false;
                using (var player = new SoundPlayer(settings.WavePath)) { player.Load(); }
                var path = settings.WavePath;
                Task.Run(() => { try { using (var player = new SoundPlayer(path)) { player.PlaySync(); } } catch (Exception) { } });
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
