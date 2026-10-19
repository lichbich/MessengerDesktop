using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MessengerNative.Services
{
    public class NotificationService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        private static readonly string _avatarCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MessengerNative",
            "Cache",
            "Avatars"
        );

        static NotificationService()
        {
            try
            {
                if (!Directory.Exists(_avatarCacheDir))
                {
                    Directory.CreateDirectory(_avatarCacheDir);
                }
            }
            catch { }
        }

        public static void ShowMessageNotification(string senderName, string content, string? avatarUrl = null, string? threadId = null)
        {
            // CRITICAL: Execute 100% on a background thread so the WPF UI thread never blocks or freezes
            Task.Run(async () =>
            {
                try
                {
                    var title = string.IsNullOrWhiteSpace(senderName) ? "Facebook Messenger" : senderName;
                    var body = string.IsNullOrWhiteSpace(content) ? "Bạn có tin nhắn mới." : content;

                    var builder = new ToastContentBuilder()
                        .AddText(title)
                        .AddText(body)
                        .AddAttributionText("Facebook Messenger")
                        .AddArgument("action", "activateWindow");

                    // In unpackaged Win32 desktop apps, ToastNotificationManagerCompat.CanUseHttpImages is FALSE!
                    // Passing an http/https URL directly causes the Windows Toast WinRT engine to hang/deadlock.
                    // We must cache the image locally and pass a file:// Uri.
                    string? localAvatarPath = await TryGetLocalAvatarAsync(avatarUrl);
                    if (!string.IsNullOrEmpty(localAvatarPath) && File.Exists(localAvatarPath))
                    {
                        try
                        {
                            builder.AddAppLogoOverride(new Uri(localAvatarPath), ToastGenericAppLogoCrop.Circle);
                        }
                        catch { }
                    }

                    builder.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
                    // Ultimate resilient fallback: text-only toast without logo
                    try
                    {
                        new ToastContentBuilder()
                            .AddText(string.IsNullOrWhiteSpace(senderName) ? "Facebook Messenger" : senderName)
                            .AddText(string.IsNullOrWhiteSpace(content) ? "Bạn có tin nhắn mới." : content)
                            .AddAttributionText("Facebook Messenger")
                            .AddArgument("action", "activateWindow")
                            .Show();
                    }
                    catch { }
                }
            });
        }

        private static async Task<string?> TryGetLocalAvatarAsync(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl)) return null;

            try
            {
                if (avatarUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(avatarUrl).LocalPath;
                }

                if (File.Exists(avatarUrl))
                {
                    return avatarUrl;
                }

                if (avatarUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    avatarUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // Generate consistent filename using MD5 hash of the URL
                    string hash;
                    using (var md5 = MD5.Create())
                    {
                        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(avatarUrl));
                        hash = Convert.ToHexString(hashBytes);
                    }

                    string cacheFile = Path.Combine(_avatarCacheDir, $"{hash}.jpg");
                    if (File.Exists(cacheFile) && new FileInfo(cacheFile).Length > 0)
                    {
                        return cacheFile;
                    }

                    // Fast download with 1.5s timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
                    var bytes = await _httpClient.GetByteArrayAsync(avatarUrl, cts.Token);
                    if (bytes != null && bytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(cacheFile, bytes, cts.Token);
                        return cacheFile;
                    }
                }
            }
            catch
            {
                // Silently fallback if download fails or times out
            }

            return null;
        }
    }
}
