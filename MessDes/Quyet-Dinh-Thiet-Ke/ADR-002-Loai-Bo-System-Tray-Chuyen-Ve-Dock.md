---
title: "ADR-002: Loại Bỏ Tính Năng Khay Hệ Thống (Tray), Giữ Chuẩn Dock Taskbar"
tags:
  - adr
  - quyet-dinh
  - ui-ux
  - taskbar
status: Accepted
date: 2026-10-19
---

# ADR-002: Loại Bỏ Tính Năng Khay Hệ Thống (Tray), Giữ Chuẩn Dock Taskbar

## 1. Trạng Thái (Status)
**Đã phê duyệt (Accepted)**

---

## 2. Bối Cảnh (Context)
* Trong bản dựng đầu tiên, ứng dụng được thiết kế tính năng thu nhỏ xuống **Khay hệ thống (System Tray)** cạnh đồng hồ Windows khi người dùng bấm nút Minimize `[—]` hoặc nút Close `[✕]`.
* Tuy nhiên, trong thực tế trải nghiệm của người dùng:
  1. Người dùng cảm thấy phiền phức khi bấm thu nhỏ thì ứng dụng lại biến mất khỏi thanh Taskbar/Dock, buộc phải vào khay mũi tên góc dưới để tìm lại icon.
  2. Người dùng muốn ứng dụng hoạt động theo thói quen trực quan của hệ điều hành Windows: Bấm thu nhỏ thì nằm dưới thanh Taskbar, click vào là mở lại ngay; bấm đóng là tắt hẳn.

---

## 3. Quyết Định (Decision)
* **Gỡ bỏ hoàn toàn module `TrayIconService`:** Không tạo icon chạy ngầm trong khay hệ thống, không hiển thị balloon tip.
* **Quy chuẩn lại hành vi các nút:**
  * **Nút Minimize `[—]`:** Thu nhỏ cửa sổ trực tiếp xuống thanh Taskbar (Dock) của Windows (`WindowState = WindowState.Minimized`).
  * **Nút Close `[✕]`:** Thoát và đóng hoàn toàn ứng dụng (`Application.Current.Shutdown()`).

---

## 4. Hệ Quả (Consequences)

### Tích cực:
* Trải nghiệm sử dụng trực quan, tự nhiên, đúng chuẩn các phần mềm desktop hiện đại.
* Không có tiến trình chạy ngầm âm thầm gây tốn tài nguyên khi người dùng đã có ý định tắt ứng dụng.
* Thanh Taskbar luôn hiển thị rõ ràng icon Messenger, dễ dàng bấm chuyển đổi cửa sổ.

### Hạn chế:
* Khi đóng ứng dụng bằng `[✕]`, ứng dụng sẽ tắt hoàn toàn nên sẽ không nhận được thông báo tin nhắn ngầm cho đến khi được mở lại. Đây là hành vi mong muốn của người dùng.
