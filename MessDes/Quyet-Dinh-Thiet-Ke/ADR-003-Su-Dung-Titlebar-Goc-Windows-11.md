---
title: "ADR-003: Đồng Nhất 1 Hàng Tiêu Đề Chuẩn Windows 11 File Explorer"
tags:
  - adr
  - quyet-dinh
  - ui-ux
  - windows11
  - titlebar
status: Accepted
date: 2026-10-19
---

# ADR-003: Đồng Nhất 1 Hàng Tiêu Đề Chuẩn Windows 11 File Explorer

## 1. Trạng Thái (Status)
**Đã phê duyệt (Accepted) - Cập nhật sửa lỗi đồ họa & icon gốc**

---

## 2. Bối Cảnh (Context)
* Khi thử nghiệm dùng thanh tiêu đề mặc định của hệ điều hành kèm dải công cụ phụ bên dưới, giao diện bị tách thành **2 hàng riêng biệt** làm tốn diện tích hiển thị và mất tính thẩm mỹ.
* Các nút điều khiển cửa sổ *(Minimize, Maximize, Close)* nếu vẽ thủ công bằng vector `<Path>` trong WPF sẽ dễ bị méo hình, nét vẽ không đồng đều, và khi đổi trạng thái Maximize / Restore xuất hiện lỗi chồng hình (2 hình vuông lồng nhau bị đứt đoạn).
* Nút WPF mặc định khi rê chuột vào bị hiện vệt màu xanh lơ nhạt (`#CCE8FF`), không đúng phong cách Fluent của Windows 11.

---

## 3. Quyết Định (Decision)

### 3.1. Gộp lại thành 1 HÀNG DUY NHẤT (Single Unified 40px Title Bar)
* Toàn bộ icon Messenger, tên app, bộ 3 nút điều hướng *(Back `←`, Forward `→`, Reload `↻`)*, dòng trạng thái và bộ nút điều khiển cửa sổ đều nằm chung trên 1 hàng duy nhất cao 40px.

### 3.2. Sử dụng bộ ký tự Icon chuẩn gốc của Windows 11 (Segoe Fluent Icons / MDL2 Assets)
Thay vì tự vẽ nét vector `<Path>` tùy ý, ứng dụng sử dụng trực tiếp bộ font hệ thống gốc của Microsoft:
* **Minimize:** Glyph `\uE921` (`&#xE921;` - ChromeMinimize).
* **Maximize:** Glyph `\uE922` (`&#xE922;` - ChromeMaximize).
* **Restore:** Glyph `\uE923` (`&#xE923;` - ChromeRestore) khi cửa sổ được phóng to cực đại.
* **Close:** Glyph `\uE8BB` (`&#xE8BB;` - ChromeClose).
* **Font Family:** `Segoe Fluent Icons, Segoe MDL2 Assets`.

### 3.3. Hiệu ứng Hover và tương tác chuẩn mực
* **Minimize / Maximize:** Rê chuột vào hiện nền xám mờ bo góc nhẹ (`#333333` ở Dark Mode, `#E5E5E5` ở Light Mode). Khi bấm chuột xuống hiện màu `#2A2A2A` / `#CCCCCC`.
* **Close:** Rê chuột vào hiện màu đỏ hệ thống Windows `#E81123`, biểu tượng chuyển sang màu trắng `#FFFFFF`. Bấm chuột xuống hiện màu `#C42B1C`.
* **Phóng to / Khôi phục:** Khi cửa sổ ở trạng thái Maximized, biểu tượng tự đổi sang `\uE923` và căn chỉnh lề 6px để không bị che khuất viền màn hình; khi trở về Normal tự đổi lại `\uE922`.

---

## 4. Hệ Quả (Consequences)
* Bộ icon sắc nét 100%, đồng nhất tuyệt đối với tất cả các ứng dụng mặc định của Windows 11 (File Explorer, Settings, Terminal).
* Triệt tiêu hoàn toàn lỗi vỡ hình, chồng hình khi phóng to / khôi phục cửa sổ.
* Tiết kiệm tối đa không gian chiều dọc cho nội dung hội thoại.
