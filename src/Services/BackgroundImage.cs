using System;
using System.Drawing;
using System.IO;

namespace HikiNotifier.Services
{
    internal static class BackgroundImage
    {
        internal const long MaxBytes = 15L * 1024 * 1024;
        internal static bool Supported(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".gif";
        }
        internal static void Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Supported(path)) throw new InvalidDataException("UnsupportedFormat");
            var file = new FileInfo(path);
            if (file.Length > MaxBytes) throw new InvalidDataException("ImageTooLarge");
            using (var stream = File.OpenRead(path))
            using (var image = Image.FromStream(stream, true, true))
            {
                if (image.Width != 480 || image.Height != 270) throw new InvalidDataException("InvalidImageDimensions");
                if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase) && image.RawFormat.Guid != System.Drawing.Imaging.ImageFormat.Gif.Guid ||
                    !Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase) && image.RawFormat.Guid != System.Drawing.Imaging.ImageFormat.Jpeg.Guid)
                    throw new InvalidDataException("UnsupportedFormat");
            }
        }
    }
}
