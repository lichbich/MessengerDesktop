using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessengerNative.Models;
using MessengerNative.Services;

namespace MessengerNative.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly FacebookService _fbService;
        private readonly StorageService _storageService;

        private List<ThreadItem> _allThreads = new();
        private Dictionary<string, List<ChatMessage>> _messagesCache = new();

        [ObservableProperty]
        private ObservableCollection<ThreadItem> _filteredThreads = new();

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _messages = new();

        [ObservableProperty]
        private ThreadItem? _selectedThread;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _messageInput = string.Empty;

        [ObservableProperty]
        private string _currentUserName = "Facebook User";

        [ObservableProperty]
        private string _currentUserAvatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isOtherTyping = false;

        [ObservableProperty]
        private bool _isSettingsOpen = false;

        [ObservableProperty]
        private AppSettings _settings;

        public event Action? RequestScrollToBottom;
        public event Action? RequestRelogin;

        public MainViewModel(AuthService authService, FacebookService fbService, StorageService storageService)
        {
            _authService = authService;
            _fbService = fbService;
            _storageService = storageService;

            _settings = _storageService.LoadSettings();

            _fbService.MessageReceived += OnMessageReceived;
            _fbService.TypingStatusChanged += OnTypingStatusChanged;
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;

            var session = _authService.LoadSession();
            if (session != null)
            {
                _fbService.SetSession(session);
                CurrentUserName = !string.IsNullOrEmpty(session.UserName) ? session.UserName : "Facebook User";
                if (!string.IsNullOrEmpty(session.UserAvatarUrl))
                {
                    CurrentUserAvatar = session.UserAvatarUrl;
                }
            }

            // Load local cache first for instant UI response (<50ms)
            var cachedThreads = _storageService.LoadThreads();
            _messagesCache = _storageService.LoadAllMessages();

            if (cachedThreads.Count > 0)
            {
                _allThreads = cachedThreads;
                ApplyFilter();
                if (FilteredThreads.Count > 0)
                {
                    SelectedThread = FilteredThreads[0];
                }
            }

            // Then fetch updated threads
            try
            {
                var freshThreads = await _fbService.FetchThreadsAsync();
                if (freshThreads.Count > 0)
                {
                    _allThreads = freshThreads;
                    _storageService.SaveThreads(_allThreads);
                    ApplyFilter();

                    if (SelectedThread == null && FilteredThreads.Count > 0)
                    {
                        SelectedThread = FilteredThreads[0];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh threads: {ex.Message}");
            }

            IsLoading = false;
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSelectedThreadChanged(ThreadItem? value)
        {
            if (value != null)
            {
                value.UnreadCount = 0;
                _ = LoadMessagesForThreadAsync(value.ThreadId);
            }
            else
            {
                Messages.Clear();
            }
        }

        private void ApplyFilter()
        {
            FilteredThreads.Clear();
            var query = SearchText?.Trim().ToLower() ?? string.Empty;

            var list = string.IsNullOrEmpty(query)
                ? _allThreads
                : _allThreads.Where(t => t.Name.ToLower().Contains(query) || t.LastMessageSnippet.ToLower().Contains(query));

            foreach (var item in list)
            {
                FilteredThreads.Add(item);
            }
        }

        private async Task LoadMessagesForThreadAsync(string threadId)
        {
            Messages.Clear();

            // Load from cache first
            if (_messagesCache.TryGetValue(threadId, out var cached) && cached.Count > 0)
            {
                foreach (var msg in cached)
                {
                    Messages.Add(msg);
                }
                RequestScrollToBottom?.Invoke();
            }

            // Fetch latest messages
            try
            {
                var freshMessages = await _fbService.FetchMessagesAsync(threadId);
                if (freshMessages.Count > 0)
                {
                    Messages.Clear();
                    foreach (var msg in freshMessages)
                    {
                        Messages.Add(msg);
                    }
                    _messagesCache[threadId] = freshMessages;
                    _storageService.SaveAllMessages(_messagesCache);
                    RequestScrollToBottom?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch messages: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (SelectedThread == null || string.IsNullOrWhiteSpace(MessageInput)) return;

            var text = MessageInput.Trim();
            MessageInput = string.Empty;

            var msg = await _fbService.SendMessageAsync(SelectedThread.ThreadId, text);
            Messages.Add(msg);

            // Update cache
            if (!_messagesCache.ContainsKey(SelectedThread.ThreadId))
            {
                _messagesCache[SelectedThread.ThreadId] = new List<ChatMessage>();
            }
            _messagesCache[SelectedThread.ThreadId].Add(msg);
            _storageService.SaveAllMessages(_messagesCache);

            // Update thread snippet
            SelectedThread.LastMessageSnippet = $"Bạn: {text}";
            SelectedThread.LastMessageTimestamp = DateTime.Now;
            _storageService.SaveThreads(_allThreads);

            RequestScrollToBottom?.Invoke();
        }

        [RelayCommand]
        public async Task SendQuickLikeAsync()
        {
            if (SelectedThread == null) return;
            MessageInput = "👍";
            await SendMessageAsync();
        }

        [RelayCommand]
        public void ToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
        }

        [RelayCommand]
        public void SaveSettings()
        {
            _storageService.SaveSettings(Settings);
            IsSettingsOpen = false;
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await InitializeAsync();
        }

        [RelayCommand]
        public void Logout()
        {
            var result = MessageBox.Show(
                "Bạn có chắc chắn muốn đăng xuất khỏi Facebook Messenger?",
                "Xác nhận đăng xuất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _authService.ClearSession();
                RequestRelogin?.Invoke();
            }
        }

        private void OnMessageReceived(ChatMessage message)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Update cache
                if (!_messagesCache.ContainsKey(message.ThreadId))
                {
                    _messagesCache[message.ThreadId] = new List<ChatMessage>();
                }
                _messagesCache[message.ThreadId].Add(message);
                _storageService.SaveAllMessages(_messagesCache);

                // Update thread
                var targetThread = _allThreads.FirstOrDefault(t => t.ThreadId == message.ThreadId);
                if (targetThread != null)
                {
                    targetThread.LastMessageSnippet = message.Content;
                    targetThread.LastMessageTimestamp = message.Timestamp;

                    if (SelectedThread?.ThreadId == message.ThreadId)
                    {
                        Messages.Add(message);
                        RequestScrollToBottom?.Invoke();
                    }
                    else
                    {
                        targetThread.UnreadCount++;
                    }

                    _storageService.SaveThreads(_allThreads);
                }

                // Show Windows Toast notification if enabled
                if (Settings.ShowNotifications)
                {
                    NotificationService.ShowMessageNotification(
                        message.SenderName,
                        message.Content,
                        message.SenderAvatarUrl,
                        message.ThreadId
                    );
                }
            });
        }

        private void OnTypingStatusChanged(string threadId, bool isTyping)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (SelectedThread?.ThreadId == threadId)
                {
                    IsOtherTyping = isTyping;
                }
            });
        }
    }
}
