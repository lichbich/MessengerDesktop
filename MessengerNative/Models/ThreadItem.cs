using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MessengerNative.Models
{
    public partial class ThreadItem : ObservableObject
    {
        [ObservableProperty]
        private string _threadId = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _avatarUrl = string.Empty;

        [ObservableProperty]
        private string _lastMessageSnippet = string.Empty;

        [ObservableProperty]
        private DateTime _lastMessageTimestamp = DateTime.Now;

        [ObservableProperty]
        private int _unreadCount = 0;

        [ObservableProperty]
        private bool _isOnline = false;

        [ObservableProperty]
        private bool _isGroup = false;

        public bool HasUnread => UnreadCount > 0;

        public string DisplayTime
        {
            get
            {
                var now = DateTime.Now;
                var span = now - LastMessageTimestamp;
                if (span.TotalMinutes < 1) return "Vừa xong";
                if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} ph";
                if (span.TotalDays < 1) return LastMessageTimestamp.ToString("HH:mm");
                if (span.TotalDays < 7) return LastMessageTimestamp.ToString("ddd");
                return LastMessageTimestamp.ToString("dd/MM");
            }
        }
    }
}
