using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MessengerNative.Services;
using Path = System.IO.Path;

namespace MessengerNative
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        private const int SW_RESTORE = 9;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_THEMECHANGED = 0x031A;

        private readonly AuthService _authService;
        private readonly StorageService _storageService;
        private readonly System.Windows.Threading.DispatcherTimer _memOptimizerTimer;
        private int _lastNotifiedUnreadCount = 0;
        private DateTime _lastNotificationSentTime = DateTime.MinValue;

        public MainWindow(AuthService authService, FacebookService fbService, StorageService storageService)
        {
            InitializeComponent();

            _authService = authService;
            _storageService = storageService;

            Loaded += async (s, e) =>
            {
                ApplyDwmAttributes();
                UpdateAdaptiveTheme();
                await InitializeWebViewAsync();
            };

            StateChanged += MainWindow_StateChanged;

            // When user focuses into the app: reset notification count, clear taskbar flash & restore high responsiveness
            Activated += (s, e) =>
            {
                _lastNotifiedUnreadCount = 0;
                FlashTaskbarAlert(false);
                if (WvMessenger?.CoreWebView2 != null)
                {
                    WvMessenger.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                }
            };

            // When user switches away or minimizes: Drop memory usage to Low and purge working set immediately
            Deactivated += (s, e) =>
            {
                if (WvMessenger?.CoreWebView2 != null)
                {
                    WvMessenger.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                }
                TrimMemory();
            };

            // Periodic memory watchdog: Trims idle unreferenced memory pages every 75 seconds
            _memOptimizerTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(75)
            };
            _memOptimizerTimer.Tick += (s, e) =>
            {
                if (WindowState == WindowState.Minimized || !IsActive)
                {
                    TrimMemory();
                }
            };
            _memOptimizerTimer.Start();

            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle)
                {
                    Dispatcher.InvokeAsync(UpdateAdaptiveTheme);
                }
            };

            Closed += (s, e) =>
            {
                _memOptimizerTimer.Stop();
                Application.Current.Shutdown();
            };

            KeyDown += MainWindow_KeyDown;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(WndProc);
            }

            ApplyDwmAttributes();
            UpdateAdaptiveTheme();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETTINGCHANGE || msg == WM_THEMECHANGED)
            {
                Dispatcher.InvokeAsync(UpdateAdaptiveTheme);
            }
            else if (msg == App.WM_SHOW_MESSENGER)
            {
                handled = true;
                Dispatcher.BeginInvoke(new Action(BringToFront));
            }
            return IntPtr.Zero;
        }

        private void ApplyDwmAttributes()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int preference = DWMWCP_ROUND;
                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DWM attributes error: {ex.Message}");
            }
        }

        private static bool IsWindowsDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var appVal = key.GetValue("AppsUseLightTheme");
                    if (appVal is int appInt) return appInt == 0;

                    var sysVal = key.GetValue("SystemUsesLightTheme");
                    if (sysVal is int sysInt) return sysInt == 0;
                }
            }
            catch { }
            return false;
        }

        private void UpdateAdaptiveTheme()
        {
            try
            {
                bool isDark = IsWindowsDarkTheme();
                var hwnd = new WindowInteropHelper(this).Handle;

                if (hwnd != IntPtr.Zero)
                {
                    int darkMode = isDark ? 1 : 0;
                    if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
                    {
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                    }
                }

                if (isDark)
                {
                    // Dark theme matching Windows 11 File Explorer dark mode
                    var bgBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
                    var borderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));

                    Background = bgBrush;
                    OuterBorder.Background = bgBrush;
                    OuterBorder.BorderBrush = borderBrush;
                    TitleBarBorder.Background = bgBrush;
                    TitleBarBorder.BorderBrush = borderBrush;

                    TxtAppTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                    SepDivider.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
                    TxtWindowTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8D, 0x91));

                    TxtNavBack.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                    TxtNavForward.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                    TxtNavReload.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                    TxtMinIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                    TxtMaxIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                    TxtCloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

                    Resources["CaptionButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                    Resources["CaptionButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
                    Resources["NavButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                    Resources["NavButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

                    BorderLoadingSplash.Background = bgBrush;
                    TxtLoadingTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                    TxtLoadingSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8D, 0x91));
                }
                else
                {
                    // Light theme matching Windows 11 File Explorer light mode
                    var bgBrush = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
                    var borderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));

                    Background = bgBrush;
                    OuterBorder.Background = bgBrush;
                    OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xD5, 0xD5));
                    TitleBarBorder.Background = bgBrush;
                    TitleBarBorder.BorderBrush = borderBrush;

                    TxtAppTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    SepDivider.Background = new SolidColorBrush(Color.FromRgb(0xD5, 0xD5, 0xD5));
                    TxtWindowTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x6B));

                    TxtNavBack.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                    TxtNavForward.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                    TxtNavReload.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));

                    TxtMinIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    TxtMaxIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    TxtCloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

                    Resources["CaptionButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                    Resources["CaptionButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                    Resources["NavButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                    Resources["NavButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                    BorderLoadingSplash.Background = bgBrush;
                    TxtLoadingTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    TxtLoadingSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x6B));
                }

                if (WvMessenger?.CoreWebView2 != null)
                {
                    WvMessenger.CoreWebView2.Profile.PreferredColorScheme =
                        isDark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
                    _ = SyncWebThemeAsync(isDark);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Adaptive theme error: {ex.Message}");
            }
        }

        private async Task SyncWebThemeAsync(bool isDark)
        {
            try
            {
                if (WvMessenger?.CoreWebView2 == null) return;

                var js = $@"
                    (function() {{
                        const isDark = {(isDark ? "true" : "false")};
                        if (isDark) {{
                            document.documentElement.classList.add('__fb-dark-mode');
                            if (document.body) document.body.classList.add('__fb-dark-mode');
                            document.documentElement.style.setProperty('color-scheme', 'dark');
                        }} else {{
                            document.documentElement.classList.remove('__fb-dark-mode');
                            if (document.body) document.body.classList.remove('__fb-dark-mode');
                            document.documentElement.style.setProperty('color-scheme', 'light');
                        }}
                    }})();
                ";
                await WvMessenger.ExecuteScriptAsync(js);
            }
            catch { }
        }

        private const string OuterScrollbarSuppressionScript = @"
            (function() {
                function suppressOuterScroll() {
                    try {
                        // 1. Force overflow hidden on html and body
                        if (document.documentElement) {
                            document.documentElement.style.setProperty('overflow', 'hidden', 'important');
                            document.documentElement.style.setProperty('scrollbar-width', 'none', 'important');
                            document.documentElement.style.setProperty('-ms-overflow-style', 'none', 'important');
                        }
                        if (document.body) {
                            document.body.style.setProperty('overflow', 'hidden', 'important');
                            document.body.style.setProperty('scrollbar-width', 'none', 'important');
                            document.body.style.setProperty('-ms-overflow-style', 'none', 'important');
                        }

                        // 2. Ensure Master CSS is attached
                        const target = document.head || document.documentElement || document.body;
                        if (target) {
                            const styleId = 'native-prevent-outer-scroll';
                            let style = document.getElementById(styleId);
                            if (!style) {
                                style = document.createElement('style');
                                style.id = styleId;
                                style.textContent = `
                                    html, body {
                                        overflow: hidden !important;
                                        margin: 0 !important;
                                        padding: 0 !important;
                                        width: 100vw !important;
                                        height: 100vh !important;
                                        max-width: 100vw !important;
                                        max-height: 100vh !important;
                                        scrollbar-width: none !important;
                                        -ms-overflow-style: none !important;
                                    }
                                    html::-webkit-scrollbar, body::-webkit-scrollbar {
                                        display: none !important;
                                        width: 0 !important;
                                        height: 0 !important;
                                    }
                                    *::-webkit-scrollbar-button {
                                        display: none !important;
                                        width: 0 !important;
                                        height: 0 !important;
                                    }
                                    *::-webkit-scrollbar-corner {
                                        display: none !important;
                                    }
                                `;
                                target.appendChild(style);
                            }
                        }

                        // 3. Scan for any full-window wrapper div/main causing outer vertical scrollbar
                        const viewportW = window.innerWidth;
                        const viewportH = window.innerHeight;
                        if (viewportW > 0 && viewportH > 0) {
                            const candidateElements = document.querySelectorAll('div, main, section');
                            for (let i = 0; i < candidateElements.length; i++) {
                                const el = candidateElements[i];
                                if (el.offsetWidth >= viewportW * 0.95 && el.offsetHeight >= viewportH * 0.95) {
                                    const style = window.getComputedStyle(el);
                                    if (style.overflowY === 'scroll' || style.overflowY === 'auto') {
                                        el.style.setProperty('overflow-y', 'hidden', 'important');
                                        el.style.setProperty('scrollbar-width', 'none', 'important');
                                    }
                                }
                            }
                        }
                    } catch (e) {
                        console.error('Outer scroll suppression error:', e);
                    }
                }

                suppressOuterScroll();

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', suppressOuterScroll);
                }
                window.addEventListener('load', suppressOuterScroll);

                if (!window.__nativeOuterScrollSuppressionActive) {
                    window.__nativeOuterScrollSuppressionActive = true;
                    setInterval(suppressOuterScroll, 600);
                }
            })();
        ";

        private async Task InjectOuterScrollbarSuppressionAsync()
        {
            try
            {
                if (WvMessenger?.CoreWebView2 != null)
                {
                    await WvMessenger.ExecuteScriptAsync(OuterScrollbarSuppressionScript);
                }
            }
            catch { }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MessengerNative",
                    "WebViewProfile"
                );

                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }

                // Optimized Chromium arguments to cap V8 heap, prevent background bloat, and limit process count
                var options = new CoreWebView2EnvironmentOptions(
                    "--disable-features=Translate,OptimizationHints,MediaRouter,BackForwardCache,CalculateNativeWinOcclusion,InterestFeedContentSuggestions " +
                    "--renderer-process-limit=1 " +
                    "--js-flags=\"--max-old-space-size=256 --optimize-for-size\" " +
                    "--disk-cache-size=20971520 " +
                    "--media-cache-size=10485760 " +
                    "--disable-background-networking " +
                    "--autoplay-policy=no-user-gesture-required"
                );

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await WvMessenger.EnsureCoreWebView2Async(env);

                WvMessenger.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WvMessenger.CoreWebView2.Settings.AreDevToolsEnabled = true;

                bool isDark = IsWindowsDarkTheme();
                WvMessenger.CoreWebView2.Profile.PreferredColorScheme =
                    isDark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;

                // INJECT PRE-RENDER SCRIPT: Suppresses outer scrollbar on all documents before parsing
                await WvMessenger.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(OuterScrollbarSuppressionScript);

                // Auto-allow notifications, microphone, camera, and autoplay for audio/video calls
                WvMessenger.CoreWebView2.PermissionRequested += (s, args) =>
                {
                    if (args.PermissionKind == CoreWebView2PermissionKind.Notifications ||
                        args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                        args.PermissionKind == CoreWebView2PermissionKind.Autoplay)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                        args.SavesInProfile = true;
                    }
                };

                // Pre-grant permissions for Messenger & Facebook domains
                try
                {
                    var domains = new[] { "https://www.messenger.com", "https://www.facebook.com", "https://web.facebook.com" };
                    foreach (var domain in domains)
                    {
                        await WvMessenger.CoreWebView2.Profile.SetPermissionStateAsync(
                            CoreWebView2PermissionKind.Microphone, domain, CoreWebView2PermissionState.Allow);
                        await WvMessenger.CoreWebView2.Profile.SetPermissionStateAsync(
                            CoreWebView2PermissionKind.Camera, domain, CoreWebView2PermissionState.Allow);
                        await WvMessenger.CoreWebView2.Profile.SetPermissionStateAsync(
                            CoreWebView2PermissionKind.Notifications, domain, CoreWebView2PermissionState.Allow);
                        await WvMessenger.CoreWebView2.Profile.SetPermissionStateAsync(
                            CoreWebView2PermissionKind.Autoplay, domain, CoreWebView2PermissionState.Allow);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to set pre-granted permissions: {ex.Message}");
                }

                // Handle new window requests: Allow internal call popups, open external links in browser
                WvMessenger.CoreWebView2.NewWindowRequested += (s, args) =>
                {
                    var uri = args.Uri ?? string.Empty;

                    // Internal Messenger/Facebook URLs and script popups (about:blank, blob:, data:, call rooms)
                    bool isInternal = string.IsNullOrEmpty(uri) ||
                                      uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                                      uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
                                      uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                                      uri.Contains("messenger.com", StringComparison.OrdinalIgnoreCase) ||
                                      uri.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) ||
                                      uri.Contains("fb.com", StringComparison.OrdinalIgnoreCase);

                    if (isInternal)
                    {
                        // Let WebView2 spawn the call popup window natively with shared WebRTC & session
                        args.Handled = false;
                        return;
                    }

                    // Genuine external URLs (YouTube, external websites) open in user's default browser
                    args.Handled = true;
                    try
                    {
                        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) &&
                            (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps))
                        {
                            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to open external link: {ex.Message}");
                    }
                };

                // Re-enforce scrollbar suppression when DOM content is loaded
                WvMessenger.CoreWebView2.DOMContentLoaded += async (s, args) =>
                {
                    await InjectOuterScrollbarSuppressionAsync();
                };

                // Track page navigation completion
                WvMessenger.NavigationCompleted += async (s, args) =>
                {
                    BorderLoadingSplash.Visibility = Visibility.Collapsed;
                    TxtWindowTitle.Text = "Trực tuyến";
                    Title = "Messenger";
                    await InjectOuterScrollbarSuppressionAsync();
                    await SyncWebThemeAsync(IsWindowsDarkTheme());

                    // Wait 4 seconds for page render to settle, then purge startup compilation memory
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(4000);
                        await Dispatcher.InvokeAsync(TrimMemory);
                    });
                };

                // Intercept Web Push Notifications from Messenger (sender name, snippet, avatar)
                WvMessenger.CoreWebView2.NotificationReceived += (s, args) =>
                {
                    try
                    {
                        args.Handled = true; // Handle natively via Windows Toast notification
                        var notif = args.Notification;
                        notif.ReportShown();

                        // Flash taskbar button orange/red if window is in background or minimized
                        if (!IsActive || WindowState == WindowState.Minimized)
                        {
                            Dispatcher.BeginInvoke(new Action(() => FlashTaskbarAlert(true)));
                        }

                        // Enforce at least 5s cooldown
                        if ((DateTime.UtcNow - _lastNotificationSentTime).TotalSeconds < 5.0) return;

                        _lastNotificationSentTime = DateTime.UtcNow;

                        var senderName = notif.Title ?? "Facebook Messenger";
                        var content = notif.Body ?? "Bạn có tin nhắn mới.";
                        var avatarUrl = notif.IconUri;

                        NotificationService.ShowMessageNotification(senderName, content, avatarUrl);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"NotificationReceived error: {ex.Message}");
                    }
                };

                // Listen for unread messages via DocumentTitle as an intelligent fallback
                WvMessenger.CoreWebView2.DocumentTitleChanged += async (s, args) =>
                {
                    try
                    {
                        var title = WvMessenger.CoreWebView2.DocumentTitle ?? string.Empty;
                        var match = Regex.Match(title, @"\((\d+)\)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                        {
                            Title = $"Messenger ({count} tin nhắn mới)";
                            TxtWindowTitle.Text = $"Messenger ({count} tin nhắn mới)";

                            // Flash taskbar button orange/red if window is in background or minimized
                            if (!IsActive || WindowState == WindowState.Minimized)
                            {
                                FlashTaskbarAlert(true);
                            }

                            // CRITICAL FIX: Only notify if count strictly increased and cooldown of 6s has passed
                            if (count > _lastNotifiedUnreadCount && (DateTime.UtcNow - _lastNotificationSentTime).TotalSeconds >= 6.0)
                            {
                                _lastNotifiedUnreadCount = count;
                                _lastNotificationSentTime = DateTime.UtcNow;
                                await FetchAndShowRichNotificationAsync(count, title);
                            }
                        }
                        else
                        {
                            Title = "Messenger";
                            TxtWindowTitle.Text = "Trực tuyến";

                            // CRITICAL FIX: DO NOT reset _lastNotifiedUnreadCount here if the window is NOT active!
                            // Facebook alternates the document title between "(1) Đoạn chat" and "Đoạn chat" every 1-2s.
                            // Resetting here caused an infinite notification spam loop!
                            if (IsActive)
                            {
                                _lastNotifiedUnreadCount = 0;
                                FlashTaskbarAlert(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DocumentTitleChanged error: {ex.Message}");
                    }
                };

                // Navigate to Messenger
                WvMessenger.Source = new Uri("https://www.messenger.com/");
            }
            catch (Exception ex)
            {
                BorderLoadingSplash.Visibility = Visibility.Collapsed;
                MessageBox.Show(
                    $"Lỗi khởi động WebView2: {ex.Message}\nVui lòng đảm bảo máy tính đã cài đặt Microsoft Edge WebView2 Runtime.",
                    "Lỗi khởi động",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Maximized)
                {
                    OuterBorder.Margin = new Thickness(6);
                    OuterBorder.BorderThickness = new Thickness(0);
                    TxtMaxIcon.Text = "\uE923"; // Windows 11 ChromeRestore glyph
                    BtnMaximize.ToolTip = "Khôi phục";
                }
                else
                {
                    OuterBorder.Margin = new Thickness(0);
                    OuterBorder.BorderThickness = new Thickness(1);
                    TxtMaxIcon.Text = "\uE922"; // Windows 11 ChromeMaximize glyph
                    BtnMaximize.ToolTip = "Phóng to";
                }

                if (WindowState == WindowState.Minimized)
                {
                    if (WvMessenger?.CoreWebView2 != null)
                    {
                        WvMessenger.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    }
                    TrimMemory();
                }
            }
            catch { }
        }

        /// <summary>
        /// Aggressively purges unreferenced memory pages from the WPF host and all WebView2 child processes.
        /// </summary>
        private void TrimMemory()
        {
            try
            {
                // 1. Force .NET Garbage Collection & trim host working set
                GC.Collect(2, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                try
                {
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                }
                catch { }

                // 2. Trim WebView2 browser process and its renderer child processes
                if (WvMessenger?.CoreWebView2 != null)
                {
                    uint browserPid = WvMessenger.CoreWebView2.BrowserProcessId;
                    if (browserPid > 0)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)browserPid);
                            EmptyWorkingSet(proc.Handle);
                        }
                        catch { }
                    }

                    // Trim all msedgewebview2 processes
                    var webViews = Process.GetProcessesByName("msedgewebview2");
                    foreach (var p in webViews)
                    {
                        try
                        {
                            EmptyWorkingSet(p.Handle);
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Trim memory error: {ex.Message}");
            }
        }

        private void BtnNavBack_Click(object sender, RoutedEventArgs e)
        {
            if (WvMessenger?.CanGoBack == true) WvMessenger.GoBack();
        }

        private void BtnNavForward_Click(object sender, RoutedEventArgs e)
        {
            if (WvMessenger?.CanGoForward == true) WvMessenger.GoForward();
        }

        private void BtnNavReload_Click(object sender, RoutedEventArgs e)
        {
            WvMessenger?.Reload();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                WvMessenger?.Reload();
            }
        }

        public void BringToFront()
        {
            try
            {
                FlashTaskbarAlert(false);

                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                Show();
                Activate();
                Focus();

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                    SwitchToThisWindow(hwnd, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BringToFront error: {ex.Message}");
            }
        }

        public void FlashTaskbarAlert(bool start = true)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                var fi = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = hwnd,
                    dwFlags = start ? (FLASHW_ALL | FLASHW_TIMERNOFG) : FLASHW_STOP,
                    uCount = start ? uint.MaxValue : 0,
                    dwTimeout = 0
                };

                FlashWindowEx(ref fi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FlashTaskbarAlert error: {ex.Message}");
            }
        }

        private async Task FetchAndShowRichNotificationAsync(int unreadCount, string rawTitle)
        {
            try
            {
                if (WvMessenger?.CoreWebView2 == null) return;

                // 1. Try extracting sender from title if title is "(1) Park Công Viên" or "(1) Park Công Viên đã gửi..."
                string? titleSender = null;
                var titleMatch = Regex.Match(rawTitle, @"\((\d+)\)\s*(.+)");
                if (titleMatch.Success)
                {
                    var cand = titleMatch.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(cand) &&
                        !cand.Equals("Messenger", StringComparison.OrdinalIgnoreCase) &&
                        !cand.Equals("Facebook", StringComparison.OrdinalIgnoreCase) &&
                        !cand.Equals("Đoạn chat", StringComparison.OrdinalIgnoreCase) &&
                        !cand.Equals("Chats", StringComparison.OrdinalIgnoreCase))
                    {
                        titleSender = cand;
                    }
                }

                // 2. Query DOM for the latest conversation row in the sidebar
                const string extractJs = @"
                    (function() {
                        try {
                            const rows = Array.from(document.querySelectorAll('[role=""row""], [role=""listitem""], a[href*=""/t/""], a[role=""link""]'));
                            for (const row of rows) {
                                const text = (row.innerText || '').trim();
                                if (!text || text.length < 3) continue;

                                let avatarUrl = null;
                                const img = row.querySelector('img');
                                if (img && img.src && img.src.startsWith('http')) {
                                    avatarUrl = img.src;
                                } else {
                                    const svgImg = row.querySelector('image');
                                    if (svgImg) {
                                        avatarUrl = svgImg.getAttribute('xlink:href') || svgImg.getAttribute('href');
                                    }
                                }

                                const lines = text.split('\n').map(s => s.trim()).filter(Boolean);
                                if (lines.length >= 2) {
                                    let name = lines[0];
                                    let snippet = lines[1];

                                    if (name.length < 2 && lines.length >= 3) {
                                        name = lines[1];
                                        snippet = lines[2];
                                    }

                                    if (lines.length >= 3 && (snippet.includes('phút') || snippet.includes('giờ') || snippet.includes('min') || snippet.includes('hr') || snippet.includes('·'))) {
                                        snippet = lines[2];
                                    }

                                    return JSON.stringify({
                                        found: true,
                                        name: name,
                                        snippet: snippet,
                                        avatarUrl: avatarUrl
                                    });
                                }
                            }
                        } catch (e) {
                            return JSON.stringify({ found: false, error: e.toString() });
                        }
                        return JSON.stringify({ found: false });
                    })();
                ";

                var scriptTask = WvMessenger.ExecuteScriptAsync(extractJs);
                var completedTask = await Task.WhenAny(scriptTask, Task.Delay(1000));
                string? jsonResult = null;
                if (completedTask == scriptTask)
                {
                    jsonResult = await scriptTask;
                }

                if (!string.IsNullOrEmpty(jsonResult) && jsonResult != "null")
                {
                    string unescaped = JsonSerializer.Deserialize<string>(jsonResult) ?? jsonResult;
                    using var doc = JsonDocument.Parse(unescaped);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("found", out var foundProp) && foundProp.GetBoolean())
                    {
                        string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        string snippet = root.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                        string? avatarUrl = root.TryGetProperty("avatarUrl", out var a) ? a.GetString() : null;

                        var finalName = !string.IsNullOrWhiteSpace(name) ? name : (titleSender ?? "Facebook Messenger");
                        var finalSnippet = !string.IsNullOrWhiteSpace(snippet) ? snippet : (unreadCount == 1 ? "Bạn có 1 tin nhắn mới." : $"Bạn có {unreadCount} tin nhắn chưa đọc.");

                        NotificationService.ShowMessageNotification(finalName, finalSnippet, avatarUrl);
                        return;
                    }
                }

                // If DOM scraping didn't find specific row, use titleSender if available
                if (!string.IsNullOrWhiteSpace(titleSender))
                {
                    NotificationService.ShowMessageNotification(titleSender, "Bạn có tin nhắn mới.", null);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FetchAndShowRichNotificationAsync error: {ex.Message}");
            }

            // Fallback
            NotificationService.ShowMessageNotification(
                "Facebook Messenger",
                unreadCount == 1 ? "Bạn có 1 tin nhắn mới." : $"Bạn có {unreadCount} tin nhắn chưa đọc.",
                null
            );
        }
    }
}