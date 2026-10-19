using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using MessengerNative.Services;
using WpfApplication = System.Windows.Application;

namespace MessengerNative
{
    public partial class App : WpfApplication
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        public static readonly int WM_SHOW_MESSENGER = RegisterWindowMessage("MESSENGER_NATIVE_ACTIVATE_WINDOW_98761B90");

        private static Mutex? _singleInstanceMutex;

        private readonly AuthService _authService = new();
        private readonly FacebookService _fbService = new();
        private readonly StorageService _storageService = new();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            const string mutexName = "Global\\MessengerNative_SingleInstance_Mutex_98761B90";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isOnlyInstance);

            if (!isOnlyInstance)
            {
                // Another instance of Messenger is already running!
                // Broadcast asynchronous message to bring existing window to front, then terminate immediately.
                PostMessage(HWND_BROADCAST, WM_SHOW_MESSENGER, IntPtr.Zero, IntPtr.Zero);
                Environment.Exit(0);
                return;
            }

            // Register Toast Activation Callback to bring the window to front safely
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MainWindow is MainWindow win)
                    {
                        win.BringToFront();
                    }
                }));
            };

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var mainWindow = new MainWindow(_authService, _fbService, _storageService);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                ToastNotificationManagerCompat.Uninstall();
            }
            catch { }

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}
