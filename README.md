# Messenger Native for Windows (C# / WPF / .NET 8)

Ứng dụng Facebook Messenger thuần Native cho Windows, giải quyết bài toán Meta gỡ bỏ ứng dụng desktop và thay thế webview nặng nề bằng một ứng dụng XAML mượt mà, **chỉ tiêu tốn ~40MB - 60MB RAM**.

---

## 🌟 Điểm nổi bật & Tính năng chính

1. **Kiến trúc Pure Native (Không chạy nền Chromium / Electron):**
   - Không ngốn hàng trăm MB RAM như trình duyệt web hay WebView thông thường.
   - Giao diện được vẽ bằng XAML / WPF với khả năng ảo hóa phần tử (`VirtualizingStackPanel`), cuộn 10.000 tin nhắn mà không bị lag.

2. **Cơ chế Đăng nhập 1 lần (One-Time Disposable Web Dialog):**
   - Mở cửa sổ WebView2 để người dùng đăng nhập Facebook / 2FA / Passkey.
   - Ngay sau khi phát hiện phiên đăng nhập thành công (`c_user`, `xs`, `fb_dtsg`), ứng dụng mã hóa phiên bằng **Windows DPAPI** (`ProtectedData.Protect`), lưu an toàn vào máy.
   - **Tự động HỦY và giải phóng 100% tiến trình WebView2**, giải phóng RAM ngay lập tức.
   - Hỗ trợ thêm chế độ **Dán Cookie thủ công** cho lập trình viên.

3. **Giao diện Windows 11 Fluent Dark / Modern Aesthetic:**
   - Cửa sổ không viền hiện đại (Custom Chrome), kéo thả mượt mà.
   - Cột hội thoại bên trái: Tìm kiếm tức thì, avatar bo tròn kèm chấm xanh online, huy hiệu tin nhắn chưa đọc.
   - Khung chat bên phải: Header cuộc trò chuyện, bong bóng tin nhắn (gửi/nhận), trạng thái đang soạn tin (typing indicator), phím tắt `Enter` gửi tin, `Shift + Enter` xuống dòng.
   - Nút gửi Like nhanh 👍.

4. **Tích hợp sâu vào Hệ điều hành Windows:**
   - **System Tray (Khay hệ thống):** Nút đóng `[X]` hoặc thu nhỏ `[-]` sẽ ẩn ứng dụng xuống khay hệ thống bên cạnh đồng hồ, click đúp chuột để mở lại.
   - **Thông báo Windows Toast:** Bắn thông báo chuẩn Windows Action Center khi có tin nhắn mới đến.
   - **Menu chuột phải ở khay hệ thống:** Mở nhanh, Làm mới (F5), Thoát hẳn.

5. **Lưu trữ Cục bộ & Khởi động tức thì (Offline-First):**
   - Lưu trữ danh sách chat và lịch sử tin nhắn trong `%LocalAppData%\MessengerNative`.
   - Mở ứng dụng lên hiển thị tin nhắn ngay trong **0.05 giây** trước khi đồng bộ mạng.

---

## 🚀 Cách chạy ứng dụng

### Cách 1: Chạy trực tiếp qua script
Nhấp đúp chuột vào file:
```cmd
run.bat
```

### Cách 2: Chạy bằng dòng lệnh (CLI)
```powershell
cd MessengerNative
dotnet run
```

### Cách 3: Đóng gói thành 1 file .exe duy nhất (Single File Executable)
Nhấp đúp chuột vào file:
```cmd
publish_single_file.bat
```
File thực thi độc lập sẽ được xuất ra thư mục `Release/MessengerNative.exe`. Bạn có thể mang file này sang bất kỳ máy tính Windows nào để chạy mà không cần cài thêm .NET SDK.

---

## 📁 Cấu trúc Thư mục Dự án

```
Mess/
├── run.bat                          # Script chạy nhanh app
├── publish_single_file.bat          # Script đóng gói file .exe độc lập
├── README.md                        # Hướng dẫn chi tiết
└── MessengerNative/
    ├── MessengerNative.csproj       # Project config (.NET 8 Windows 10/11)
    ├── App.xaml / App.xaml.cs       # Luồng khởi động ứng dụng & kiểm tra phiên
    ├── MainWindow.xaml / .cs        # Giao diện chính (Khung chat, danh sách bạn bè, Titlebar, Tray)
    ├── Models/
    │   ├── UserSession.cs           # Lưu trữ token phiên (c_user, xs, fb_dtsg)
    │   ├── ThreadItem.cs            # Dữ liệu cuộc trò chuyện (Hội thoại)
    │   ├── ChatMessage.cs           # Dữ liệu tin nhắn (Text, Thời gian, Trạng thái)
    │   └── AppSettings.cs           # Cài đặt giao diện & thông báo
    ├── Services/
    │   ├── AuthService.cs           # Quản lý mã hóa phiên DPAPI
    │   ├── FacebookService.cs       # Tầng kết nối mạng, GraphQL & Real-time messaging
    │   ├── StorageService.cs        # Lưu trữ cache cục bộ
    │   ├── ImageCacheService.cs     # Tải & cache ảnh/avatar vào ổ đĩa
    │   ├── NotificationService.cs   # Bắn thông báo Windows Toast
    │   └── TrayIconService.cs       # Quản lý icon khay hệ thống (Win32 P/Invoke)
    ├── ViewModels/
    │   └── MainViewModel.cs         # MVVM Controller (Binding, Commands, Events)
    ├── Views/
    │   ├── LoginWindow.xaml / .cs   # Cửa sổ đăng nhập 1 lần (Disposable WebView2)
    └── Styles/
        └── ModernTheme.xaml         # Hệ thống màu sắc, nút bấm, scrollbar, bong bóng chat
```
