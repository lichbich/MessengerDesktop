using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MessengerNative.Services
{
    public class TrayIconService : IDisposable
    {
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 100;

        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        private readonly Window _window;
        private readonly HwndSource _hwndSource;
        private bool _isCreated = false;

        public event Action? DoubleClicked;
        public event Action? RightClicked;

        public TrayIconService(Window window, string tooltip = "Messenger Native")
        {
            _window = window;
            var wih = new WindowInteropHelper(window);
            wih.EnsureHandle();

            _hwndSource = HwndSource.FromHwnd(wih.Handle)!;
            _hwndSource.AddHook(WndProc);

            CreateTrayIcon(wih.Handle, tooltip);
        }

        private void CreateTrayIcon(IntPtr hWnd, string tooltip)
        {
            var iconHandle = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION default icon

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = iconHandle,
                szTip = tooltip
            };

            _isCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                var lparamInt = lParam.ToInt32();
                if (lparamInt == WM_LBUTTONDBLCLK)
                {
                    DoubleClicked?.Invoke();
                    handled = true;
                }
                else if (lparamInt == WM_RBUTTONUP)
                {
                    RightClicked?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isCreated)
            {
                var wih = new WindowInteropHelper(_window);
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = wih.Handle,
                    uID = 1
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                _isCreated = false;
            }
        }
    }
}
