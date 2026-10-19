---
title: "Nghiệp Vụ: Trải Nghiệm Người Dùng & Giao Diện (UX/UI)"
tags:
  - nghiep-vu
  - ui-ux
  - windows11
  - fluent-design
created: 2026-10-19
updated: 2026-10-19
---

# 🎨 Nghiệp Vụ: Trải Nghiệm Người Dùng & Thiết Kế Giao Diện (UI/UX)

Tài liệu này mô tả các tiêu chuẩn giao diện người dùng, hành vi tương tác cửa sổ và triết lý tối giản hóa của ứng dụng **Messenger Native** trên hệ điều hành Windows 11.

---

## 1. Triết Lý Thiết Kế (Design Principles)

* **Chuẩn hóa Windows 11 (Native Windows 11 Experience):** Ứng dụng phải hòa hợp hoàn hảo với ngôn ngữ thiết kế Fluent Design của Microsoft, bao gồm viền bo tròn góc mượt mà, bóng đổ tự nhiên và màu sắc Dark Theme thanh lịch.
* **Tối giản & Không gây xao nhãng (Distraction-Free):** Loại bỏ toàn bộ các nút bấm và tính năng rườm rà không đem lại giá trị sử dụng thực tế (các thanh công cụ phụ, menu che khuất màn hình). Người dùng mở app lên là để trò chuyện ngay lập tức.
* **Thao tác nhanh, mượt mà:** Khởi động tức thì, điều hướng nhanh chóng bằng phím tắt và chuột.

---

## 2. Bố Cục Cửa Sổ (Window Architecture)

Cửa sổ được thiết kế không viền cũ kỹ (`WindowStyle="None"` với `WindowChrome`) và chia thành 2 phân vùng chính:

```
┌────────────────────────────────────────────────────────────────────────┐
│ [Icon] Messenger   [◀] [▶] [🔄]          Trực tuyến        [ — ] [ ▢ ] [ ✕ ] │ <-- Top Titlebar (40px)
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│                                                                        │
│                                                                        │
│                 Khung hiển thị Messenger chính thức                     │
│               (Hòm thư, hội thoại, cuộc gọi, sticker)                  │
│                                                                        │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
  ^~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~^
                     Bo góc viền cong chuẩn Windows 11
```

---

## 3. Đặc Tả Thanh Tiêu Đề (Custom Titlebar)

Thanh tiêu đề có chiều cao **40px**, tích hợp các thành phần theo thứ tự từ trái sang phải:

| Khu vực | Thành phần | Mô tả nghiệp vụ | Phím tắt tương ứng |
| :--- | :--- | :--- | :--- |
| **Bên trái (Left)** | **Icon ứng dụng** | Logo Messenger chính chủ kích thước 22x22px, hiển thị nhận diện thương hiệu. | - |
| | **Tên app** | Chữ "Messenger" sắc nét font Segoe UI / SemiBold. | - |
| | **Nút Quay lại [◀]** | Quay về màn hình trước đó nếu người dùng vừa bấm vào link/cài đặt. | `Alt + Left` |
| | **Nút Tiến lên [▶]** | Đi tới màn hình kế tiếp nếu đã quay lại trước đó. | `Alt + Right` |
| | **Nút Nạp lại [🔄]** | Tải lại trang Messenger (rất hữu ích khi mạng chập chờn hoặc sau khi duyệt 2FA trên điện thoại). | `F5` hoặc `Ctrl + R` |
| **Ở giữa (Center)** | **Trạng thái / Tiêu đề** | Hiển thị dòng chữ *"Trực tuyến"* hoặc cập nhật động *"Messenger (N tin nhắn mới)"*. Toàn bộ vùng này là **vùng kéo thả cửa sổ (Caption Drag Region)**. | Kéo thả chuột trái |
| **Bên phải (Right)** | **Nút Thu nhỏ [—]** | Thu nhỏ cửa sổ xuống thanh Dock / Taskbar Windows. | `Win + Down` |
| | **Nút Phóng to [▢]** | Phóng to toàn màn hình hoặc khôi phục kích thước ban đầu. | Nhấp đúp chuột lên thanh tiêu đề |
| | **Nút Đóng [✕]** | Đóng hoàn toàn ứng dụng và giải phóng bộ nhớ. Khi rê chuột vào đổi sang màu đỏ đặc trưng `#E81123`. | `Alt + F4` |

---

## 4. Nghiệp Vụ Bo Góc Viền Cửa Sổ (Windows 11 Rounded Corners)

### 4.1. Vấn đề của các ứng dụng WPF truyền thống
* Các ứng dụng WPF tự vẽ khung (custom window) thông thường sẽ có 4 góc vuông sắc nhọn, trông lạc hậu và không ăn nhập với giao diện bo cong mềm mại của Windows 11.

### 4.2. Giải pháp kỹ thuật trong Messenger Native
* Kết hợp thiết lập `CornerRadius="10"` trong XAML và can thiệp sâu vào **Desktop Window Manager (DWM)** của Windows 11 thông qua API `DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND)`.
* Cơ chế này giúp toàn bộ cửa sổ (bao gồm cả lớp nội dung WebView2 bên trong) được hệ điều hành Windows 11 bo cong góc bằng phần cứng GPU, khử răng cưa và tạo đổ bóng tự nhiên tuyệt đẹp.

---

## 5. Tinh Chỉnh Thanh Cuộn (Custom Scrollbar Styling)

Mặc định, thanh cuộn của trình duyệt web thường to bản và thô kệch. Ứng dụng tự động can thiệp mã CSS tinh gọn:
* Độ rộng thanh cuộn: **8px**.
* Con trỏ cuộn (Thumb): Màu xám tối `#444950`, bo góc tròn `4px`.
* Nền rãnh cuộn (Track): Trong suốt hoàn toàn, không chiếm diện tích nội dung văn bản.
* Hiệu ứng rê chuột: Tự động sáng nhẹ `#5A5F67` tạo phản hồi trực quan mượt mà.
