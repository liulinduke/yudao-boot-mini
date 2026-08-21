using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SocialMatrix.WpfHost.Services
{
    internal static class PostMediaRandomizer
    {
        private static readonly string[] Emojis = { "😊", "✨", "🌟", "💡", "🙌", "👍", "🎉", "💬" };

        public static string AppendRandomEmoji(string content)
        {
            return $"{content.TrimEnd()} {Emojis[Random.Shared.Next(Emojis.Length)]}".TrimStart();
        }

        public static string[] CreateNoisyImageCopies(IEnumerable<string> mediaPaths, out List<string> temporaryFiles)
        {
            temporaryFiles = new List<string>();
            var processedPaths = new List<string>();
            foreach (var path in mediaPaths)
            {
                if (!IsSupportedImage(path))
                {
                    processedPaths.Add(path);
                    continue;
                }

                try
                {
                    var outputPath = CreateNoisyImageCopy(path);
                    temporaryFiles.Add(outputPath);
                    processedPaths.Add(outputPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[发帖] 图片加噪点失败，使用原图: {path}; {ex.Message}");
                    processedPaths.Add(path);
                }
            }
            return processedPaths.ToArray();
        }

        public static void DeleteTemporaryFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { /* Best-effort cleanup. */ }
            }
        }

        private static bool IsSupportedImage(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateNoisyImageCopy(string sourcePath)
        {
            var decoder = BitmapDecoder.Create(new Uri(sourcePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var source = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var noise = Random.Shared.Next(-2, 3);
                pixels[i] = Clamp(pixels[i] + noise);
                pixels[i + 1] = Clamp(pixels[i + 1] + noise);
                pixels[i + 2] = Clamp(pixels[i + 2] + noise);
            }

            var bitmap = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY,
                PixelFormats.Bgra32, null, pixels, stride);
            var outputPath = Path.Combine(Path.GetTempPath(), $"social-matrix-post-{Guid.NewGuid():N}.png");
            using var stream = File.Create(outputPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            return outputPath;
        }

        private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);
    }
}
