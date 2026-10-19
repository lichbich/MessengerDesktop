---
title: "Kiến Trúc: Tích Hợp Windows 11 DWM & Bo Góc Viền"
tags:
  - kien-truc
  - windows11
  - dwm
  - p-invoke
  - icons
created: 2026-10-19
updated: 2026-10-19
---

# 🪟 Kiến Trúc: Tích Hợp Windows 11 DWM & Bo Góc Viền

Tài liệu này trình bày giải pháp kỹ thuật kết hợp giữa WPF XAML và Desktop Window Manager (DWM) của hệ điều hành Windows 11 để mang lại hiệu ứng bo cong viền và đổ bóng chuẩn native.

---

## 1. Cơ Chế Hoạt Động Của DWM Trên Windows 11

Từ phiên bản Windows 11 (Build 22000 trở đi), Microsoft giới thiệu tính năng bo cong các góc cửa sổ ở cấp độ hệ thống (OS-level compositor). Tuy nhiên, đối với các ứng dụng WPF tự vẽ khung (custom chrome), Windows mặc định sẽ không tự động bo góc trừ khi ứng dụng khai báo cờ `DWMWA_WINDOW_CORNER_PREFERENCE`.

---

## 2. Mã Nguồn P/Invoke Can Thiệp DWM API

Trong [MainWindow.xaml.cs](file:///c:/Users/trong/Downloads/Code/Mess/MessengerNative/MainWindow.xaml.cs):

```csharp
[DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern int DwmSetWindowAttribute(
    IntPtr hwnd, 
    int attribute, 
    ref int pvAttribute, 
    int cbAttribute
);

// Hằng số được định nghĩa trong Windows 11 SDK
private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

// Các chế độ bo góc:
// 1 = DWMWCP_DONOTROUND (Góc vuông)
// 2 = DWMWCP_ROUND (Bo cong tròn tiêu chuẩn Windows 11)
// 3 = DWMWCP_ROUNDSMALL (Bo cong góc nhỏ)
private const int DWMWCP_ROUND = 2;
```

### 2.1. Kích hoạt khi cửa sổ khởi tạo
```csharp
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    ApplyWindows11RoundedCorners();
}

private void ApplyWindows11RoundedCorners()
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
        Debug.WriteLine($"DWM Corner error: {ex.Message}");
    }
}
```

---

## 3. Cấu Hình `WindowChrome` Trong XAML

Để DWM có thể vẽ viền và đổ bóng tự nhiên bao quanh cửa sổ, thuộc tính `GlassFrameThickness` **bắt buộc phải là `1`** (thay vì `0`):

```xml
<WindowChrome.WindowChrome>
    <WindowChrome CaptionHeight="40" 
                  ResizeBorderThickness="6" 
                  CornerRadius="10" 
                  GlassFrameThickness="1" 
                  UseAeroCaptionButtons="False"/>
</WindowChrome.WindowChrome>
```

Đồng thời, lớp Border bao quanh ngoài cùng của giao diện được thiết lập:
```xml
<Border Background="#18191A" 
        BorderBrush="#393A3B" 
        BorderThickness="1" 
        CornerRadius="10" 
        ClipToBounds="True">
```
* `CornerRadius="10"`: Đồng bộ độ cong của nội dung bên trong khớp với độ cong của DWM bên ngoài.
* `ClipToBounds="True"`: Đảm bảo nền của WebView2 không bị chồi góc vuông ra khỏi viền bo tròn.

---

## 4. Xây Dựng Bộ Icon Đa Độ Phân Giải (Multi-Resolution ICO)

Để ứng dụng hiển thị sắc nét ở mọi kích thước trên Windows:
* File icon: `MessengerNative/Assets/messenger.ico`
* Hỗ trợ 5 kích thước chuẩn: **16x16, 32x32, 48x48, 64x64, 256x256 px**.
* Định dạng màu: 32-bit ARGB với kênh Alpha khử răng cưa.
* Tích hợp vào project thông qua thuộc tính:
  ```xml
  <ApplicationIcon>Assets\messenger.ico</ApplicationIcon>
  ```
  giúp icon tự động xuất hiện trên file `.exe` ngoài màn hình và thanh Taskbar / Dock.
