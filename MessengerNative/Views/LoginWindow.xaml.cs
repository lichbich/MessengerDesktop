using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MessengerNative.Models;
using MessengerNative.Services;

namespace MessengerNative.Views
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;
        private readonly DispatcherTimer _cookieCheckTimer;
        private bool _isLoggingIn = false;

        public UserSession? AuthenticatedSession { get; private set; }

        public LoginWindow(AuthService authService)
        {
            InitializeComponent();
            _authService = authService;

            _cookieCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _cookieCheckTimer.Tick += async (s, e) => await CheckCookiesAsync();

            Loaded += LoginWindow_Loaded;
            Closed += LoginWindow_Closed;
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Đang khởi tạo trình duyệt đăng nhập...";
                await WvLogin.EnsureCoreWebView2Async();

                TxtStatus.Text = "Vui lòng nhập tài khoản Facebook hoặc quét mã QR...";
                WvLogin.NavigationCompleted += (s, args) =>
                {
                    _cookieCheckTimer.Start();
                };
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Lỗi khởi tạo: {ex.Message}. Vui lòng chuyển sang dán Cookie thủ công.";
            }
        }

        private async Task CheckCookiesAsync()
        {
            if (_isLoggingIn || WvLogin.CoreWebView2 == null) return;

            try
            {
                // Check if current page is in 2FA checkpoint
                var currentUrl = WvLogin.Source?.ToString() ?? string.Empty;
                if (currentUrl.Contains("checkpoint", StringComparison.OrdinalIgnoreCase) || 
                    currentUrl.Contains("two_step", StringComparison.OrdinalIgnoreCase))
                {
                    TxtStatus.Text = "Đang chờ 2FA... Khi đã bấm đồng ý trên điện thoại, hãy bấm nút '✅ Tôi đã duyệt trên điện thoại' ở góc trên.";
                }

                // Query all cookies across all facebook / messenger domains
                var allCookies = new System.Collections.Generic.List<Microsoft.Web.WebView2.Core.CoreWebView2Cookie>();

                try
                {
                    var c1 = await WvLogin.CoreWebView2.CookieManager.GetCookiesAsync("https://www.facebook.com");
                    if (c1 != null) allCookies.AddRange(c1);
                }
                catch { }

                try
                {
                    var c2 = await WvLogin.CoreWebView2.CookieManager.GetCookiesAsync("https://m.facebook.com");
                    if (c2 != null) allCookies.AddRange(c2);
                }
                catch { }

                try
                {
                    var c3 = await WvLogin.CoreWebView2.CookieManager.GetCookiesAsync("https://www.messenger.com");
                    if (c3 != null) allCookies.AddRange(c3);
                }
                catch { }

                try
                {
                    var c4 = await WvLogin.CoreWebView2.CookieManager.GetCookiesAsync(currentUrl);
                    if (c4 != null) allCookies.AddRange(c4);
                }
                catch { }

                var cUser = allCookies.FirstOrDefault(c => c.Name.Equals("c_user", StringComparison.OrdinalIgnoreCase));
                var xs = allCookies.FirstOrDefault(c => c.Name.Equals("xs", StringComparison.OrdinalIgnoreCase));

                if (cUser != null && xs != null && !string.IsNullOrEmpty(cUser.Value) && !string.IsNullOrEmpty(xs.Value))
                {
                    _isLoggingIn = true;
                    _cookieCheckTimer.Stop();

                    BorderSuccessOverlay.Visibility = Visibility.Visible;
                    TxtStatus.Text = "Đã xác thực tài khoản! Đang lưu phiên bảo mật...";

                    var datr = allCookies.FirstOrDefault(c => c.Name.Equals("datr", StringComparison.OrdinalIgnoreCase));
                    var fr = allCookies.FirstOrDefault(c => c.Name.Equals("fr", StringComparison.OrdinalIgnoreCase));

                    var session = new UserSession
                    {
                        UserId = cUser.Value,
                        XsToken = xs.Value,
                        Datr = datr?.Value ?? string.Empty,
                        Fr = fr?.Value ?? string.Empty,
                        LastLoginTime = DateTime.Now
                    };

                    // Extract fb_dtsg token if possible
                    try
                    {
                        var dtsgScript = "(function() { return document.querySelector('input[name=\"fb_dtsg\"]')?.value || window.require?.('DTSGInitialData')?.token || ''; })();";
                        var dtsgResult = await WvLogin.ExecuteScriptAsync(dtsgScript);
                        if (!string.IsNullOrEmpty(dtsgResult) && dtsgResult != "null" && dtsgResult != "\"\"")
                        {
                            session.FbDtsg = dtsgResult.Trim('"');
                        }
                    }
                    catch
                    {
                        // Ignore extraction failure
                    }

                    _authService.SaveSession(session);
                    AuthenticatedSession = session;

                    await Task.Delay(1200);

                    // Dispose WebView2 immediately to free memory
                    DisposeWebView();

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cookie check error: {ex.Message}");
            }
        }

        private async void BtnApprovedOnPhone_Click(object sender, RoutedEventArgs e)
        {
            if (WvLogin.CoreWebView2 == null) return;

            TxtStatus.Text = "Đang kiểm tra kết quả duyệt từ điện thoại và chuyển tiếp...";

            // Reload or navigate to facebook.com to let server set session cookie
            try
            {
                var url = WvLogin.Source?.ToString() ?? "";
                if (url.Contains("checkpoint") || url.Contains("login"))
                {
                    WvLogin.CoreWebView2.Navigate("https://www.facebook.com/");
                }
                else
                {
                    WvLogin.Reload();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }

            await Task.Delay(2000);
            await CheckCookiesAsync();
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Đang tải lại trang...";
                WvLogin?.Reload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reload error: {ex.Message}");
            }
        }

        private void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            if (GridWebView.Visibility == Visibility.Visible)
            {
                GridWebView.Visibility = Visibility.Collapsed;
                GridManualCookie.Visibility = Visibility.Visible;
                BtnSwitchMode.Content = "Đăng nhập bằng WebView";
                _cookieCheckTimer.Stop();
            }
            else
            {
                GridWebView.Visibility = Visibility.Visible;
                GridManualCookie.Visibility = Visibility.Collapsed;
                BtnSwitchMode.Content = "Dán Cookie thủ công";
                _cookieCheckTimer.Start();
            }
        }

        private void BtnBackToWeb_Click(object sender, RoutedEventArgs e)
        {
            GridWebView.Visibility = Visibility.Visible;
            GridManualCookie.Visibility = Visibility.Collapsed;
            BtnSwitchMode.Content = "Dán Cookie thủ công";
            _cookieCheckTimer.Start();
        }

        private void BtnConfirmCookie_Click(object sender, RoutedEventArgs e)
        {
            TxtCookieError.Visibility = Visibility.Collapsed;
            var raw = TxtManualCookie.Text?.Trim();

            var session = AuthService.ParseCookieString(raw ?? string.Empty);
            if (session == null || !session.IsValid)
            {
                TxtCookieError.Text = "Chuỗi Cookie không hợp lệ! Vui lòng đảm bảo có chứa c_user=... và xs=...";
                TxtCookieError.Visibility = Visibility.Visible;
                return;
            }

            _authService.SaveSession(session);
            AuthenticatedSession = session;

            DisposeWebView();
            DialogResult = true;
            Close();
        }

        private void DisposeWebView()
        {
            try
            {
                _cookieCheckTimer.Stop();
                WvLogin?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dispose webview: {ex.Message}");
            }
        }

        private void LoginWindow_Closed(object? sender, EventArgs e)
        {
            DisposeWebView();
        }
    }
}
