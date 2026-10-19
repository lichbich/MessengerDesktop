using System;

namespace MessengerNative.Models
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark"; // Dark, Light, Oled
        public bool MinimizeToTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool LaunchOnStartup { get; set; } = false;
        public bool ShowNotifications { get; set; } = true;
        public bool PlayNotificationSound { get; set; } = true;
    }
}
