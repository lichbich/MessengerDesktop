using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MessengerNative.Services
{
    public class ImageCacheService
    {
        private static readonly string CacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MessengerNative",
            "Cache"
        );

        private readonly HttpClient _httpClient;

        public ImageCacheService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!Directory.Exists(CacheFolder))
            {
                Directory.CreateDirectory(CacheFolder);
            }
        }

        public async Task<BitmapImage?> GetImageAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                var hash = GetMd5Hash(url);
                var localPath = Path.Combine(CacheFolder, $"{hash}.img");

                if (!File.Exists(localPath))
                {
                    var bytes = await _httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(localPath, bytes);
                }

                return LoadBitmapFromDisk(localPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cache/load image: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage LoadBitmapFromDisk(string filePath)
        {
            var bitmap = new BitmapImage();
            using var stream = File.OpenRead(filePath);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static string GetMd5Hash(string input)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
