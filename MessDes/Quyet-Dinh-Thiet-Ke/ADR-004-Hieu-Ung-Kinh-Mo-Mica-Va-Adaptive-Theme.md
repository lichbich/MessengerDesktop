---
title: "ADR-004: Khắc Phục Lỗi Xuyên Nền, Ẩn Scrollbar Và Adaptive Theme Hoàn Hảo"
tags:
  - adr
  - quyet-dinh
  - mica
  - acrylic
  - adaptive-theme
  - scrollbar
status: Accepted
date: 2026-10-19
---

# ADR-004: Khắc Phục Lỗi Xuyên Nền, Ẩn Scrollbar Và Adaptive Theme Hoàn Hảo

## 1. Trạng Thái (Status)
**Đã phê duyệt (Accepted) - Cập nhật sửa lỗi triệt để thanh cuộn thừa**

---

## 2. Bối Cảnh (Context)
1. **Lỗi thanh cuộn ngoài (Outer Scrollbar):** 
   - Ở ngoài cùng bên phải xuất hiện thanh cuộn xám dày có mũi tên tam giác `▲` và `▼` của Windows/Chromium, kéo dài ứng dụng ra cũng không mất.
   - Nguyên nhân: Facebook Messenger sử dụng cấu trúc Comet SPA với các thẻ bọc toàn màn hình (`div`, `main`) có thuộc tính `overflow-y: scroll` hoặc `auto`. Ngoài ra, nếu chỉ gán CSS vào `document.head` tại thời điểm khởi tạo (`AddScriptToExecuteOnDocumentCreatedAsync`), `document.head` có thể chưa tồn tại (`null`) dẫn đến script bị dừng giữa chừng.
2. **Lỗi xuyên nền (Wallpaper Bleed-Through Bug):** 
   - Đã xử lý bằng cách chuyển sang `GlassFrameThickness="0"` kết hợp Solid Adaptive Header theo phong cách File Explorer.
3. **Adaptive Theme:** 
   - Bắt sự kiện Win32 `WM_SETTINGCHANGE` và `WM_THEMECHANGED` để đồng bộ Dark/Light Mode.

---

## 3. Quyết Định (Decision)

### 3.1. Cơ chế 3 tầng triệt tiêu thanh cuộn ngoài (Triple-Shield Scrollbar Suppression)
1. **Tầng 1 - Root CSS & WebKit Buttons:**
   - Triệt tiêu thanh cuộn của `html` và `body` với `overflow: hidden !important; scrollbar-width: none !important;`.
   - Vô hiệu hóa hoàn toàn các nút mũi tên tam giác: `*::-webkit-scrollbar-button { display: none !important; width: 0 !important; height: 0 !important; }`.
2. **Tầng 2 - Quét và xử lý Wrapper Div toàn màn hình:**
   - Tự động duyệt các container bao phủ `>= 95%` kích thước màn hình (`offsetWidth >= window.innerWidth * 0.95 && offsetHeight >= window.innerHeight * 0.95`).
   - Nếu container toàn màn hình có `overflow-y: scroll` hoặc `auto`, lập tức ép về `overflow-y: hidden !important`.
   - **Đảm bảo an toàn:** Khung tin nhắn (chat feed) và danh sách hội thoại bên trái (chiếm bề ngang nhỏ hơn 95%) không bị ảnh hưởng, giữ nguyên cuộn mượt mà.
3. **Tầng 3 - Kích hoạt đa điểm & Vòng lặp giám sát (Continuous Monitor):**
   - Tiêm từ sớm qua `AddScriptToExecuteOnDocumentCreatedAsync`.
   - Tiêm lại tại `DOMContentLoaded` và `NavigationCompleted`.
   - Chạy kiểm tra định kỳ (600ms) để đối phó với hiện tượng React/Comet tự tạo lại DOM khi chuyển đổi giữa các cuộc trò chuyện.

### 3.2. Nền thanh tiêu đề Solid Adaptive Theme
* **Dark Theme:** `#202020`, viền `#2D2D2D`, chữ `#FFFFFF`, icon `#CCCCCC`.
* **Light Theme:** `#F3F3F3`, viền `#E5E5E5`, chữ `#1A1A1A`, icon `#444444`.
* Bo góc tự nhiên Windows 11 qua `DWMWA_WINDOW_CORNER_PREFERENCE`.

---

## 4. Hệ Quả (Consequences)
* Thanh cuộn xám thừa ngoài cùng biến mất hoàn toàn 100%, kể cả khi thay đổi kích thước cửa sổ hay chuyển đổi cuộc trò chuyện.
* Giữ nguyên vẹn thanh cuộn nội bộ của lịch sử tin nhắn và danh sách bạn bè.
* Ứng dụng chạy mượt mà, đạt trải nghiệm native cao cấp.
