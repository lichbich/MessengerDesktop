using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MessengerNative.Models
{
    public partial class ChatMessage : ObservableObject
    {
        [ObservableProperty]
        private string _messageId = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _threadId = string.Empty;

        [ObservableProperty]
        private string _senderId = string.Empty;

        [ObservableProperty]
        private string _senderName = string.Empty;

        [ObservableProperty]
        private string _senderAvatarUrl = string.Empty;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private DateTime _timestamp = DateTime.Now;

        [ObservableProperty]
        private bool _isFromMe = false;

        [ObservableProperty]
        private string _status = "Sent"; // Sending, Sent, Delivered, Read

        [ObservableProperty]
        private string? _attachmentUrl;

        [ObservableProperty]
        private bool _isImage = false;

        [ObservableProperty]
        private string? _reaction;

        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentUrl);
        public bool HasReaction => !string.IsNullOrEmpty(Reaction);

        public string DisplayTime => Timestamp.ToString("HH:mm");
    }
}
