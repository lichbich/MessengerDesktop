using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessengerNative.Models;

namespace MessengerNative.Services
{
    public class AuthService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MessengerNative"
        );
        private static readonly string SessionFilePath = Path.Combine(AppDataFolder, "session.bin");

        public AuthService()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        public bool HasSession()
        {
            if (!File.Exists(SessionFilePath)) return false;
            var session = LoadSession();
            return session != null && session.IsValid;
        }

        public void SaveSession(UserSession session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                File.WriteAllBytes(SessionFilePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
            }
        }

        public UserSession? LoadSession()
        {
            try
            {
                if (!File.Exists(SessionFilePath)) return null;

                var encryptedBytes = File.ReadAllBytes(SessionFilePath);
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<UserSession>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
                return null;
            }
        }

        public void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
            }
        }

        public static UserSession? ParseCookieString(string rawCookies)
        {
            if (string.IsNullOrWhiteSpace(rawCookies)) return null;

            var session = new UserSession();
            var pairs = rawCookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                var val = kv[1].Trim();

                if (key.Equals("c_user", StringComparison.OrdinalIgnoreCase))
                {
                    session.UserId = val;
                }
                else if (key.Equals("xs", StringComparison.OrdinalIgnoreCase))
                {
                    session.XsToken = val;
                }
                else if (key.Equals("datr", StringComparison.OrdinalIgnoreCase))
                {
                    session.Datr = val;
                }
                else if (key.Equals("fr", StringComparison.OrdinalIgnoreCase))
                {
                    session.Fr = val;
                }
            }

            return session.IsValid ? session : null;
        }
    }
}
