using System;

namespace MessengerNative.Models
{
    public class UserSession
    {
        public string UserId { get; set; } = string.Empty;
        public string XsToken { get; set; } = string.Empty;
        public string Datr { get; set; } = string.Empty;
        public string Fr { get; set; } = string.Empty;
        public string FbDtsg { get; set; } = string.Empty;
        public string UserName { get; set; } = "Facebook User";
        public string UserAvatarUrl { get; set; } = string.Empty;
        public DateTime LastLoginTime { get; set; } = DateTime.Now;
        public bool IsValid => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(XsToken);

        public string ToCookieHeader()
        {
            var parts = new System.Collections.Generic.List<string>
            {
                $"c_user={UserId}",
                $"xs={XsToken}"
            };
            if (!string.IsNullOrEmpty(Datr)) parts.Add($"datr={Datr}");
            if (!string.IsNullOrEmpty(Fr)) parts.Add($"fr={Fr}");
            return string.Join("; ", parts);
        }
    }
}
