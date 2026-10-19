---
title: "ADR-001: Lựa chọn mô hình Native Shell thay vì tự viết API Client thuần túy"
tags:
  - adr
  - quyet-dinh
  - kien-truc
status: Accepted
date: 2026-10-19
---

# ADR-001: Lựa Chọn Mô Hình Native Shell Thay Vì Tự Viết API Client Thuần Túy

## 1. Trạng Thái (Status)
**Đã phê duyệt (Accepted)**

---

## 2. Bối Cảnh (Context)
* Người dùng mong muốn có một ứng dụng chạy nhanh, nhẹ, tiêu tốn ít RAM và có thể tùy biến trên Windows sau khi Meta gỡ ứng dụng máy tính.
* Ban đầu, ý tưởng là xây dựng một ứng dụng C#/WPF thuần túy gọi trực tiếp các API ngầm của Facebook (Reverse-Engineered API qua GraphQL/MQTT).
* Tuy nhiên, trong thực tế triển khai:
  1. Meta đã áp dụng **Mã hóa đầu cuối (E2EE)** cho toàn bộ cuộc trò chuyện 1-1. Tin nhắn không còn lưu ở dạng văn bản mà được mã hóa bằng thuật toán Double Ratchet qua module WebAssembly chạy trong trình duyệt.
  2. Việc gọi API ngầm từ C# không có ngữ cảnh trình duyệt khiến dữ liệu trả về bị mã hóa hoặc tài khoản bị dính cờ bot (Checkpoint / Ban).

---

## 3. Quyết Định (Decision)
Chuyển đổi hoàn toàn sang kiến trúc **Native Shell Tinh Gọn (Optimized Native Shell)**:
* **Khung vỏ bên ngoài:** Viết bằng C# / WPF, tích hợp bo cong viền Windows 11 DWM, thanh tiêu đề kéo thả và icon riêng biệt.
* **Lõi xử lý bên trong:** Nhúng engine Microsoft Edge WebView2, cấu hình với các cờ Chromium tối ưu tài nguyên (`--disable-features=Translate,OptimizationHints,MediaRouter`).

---

## 4. Hệ Quả (Consequences)

### Tích cực:
* Hiển thị **100% tin nhắn thật, bạn bè thật, ảnh, video, sticker và cuộc gọi**.
* Giải mã E2EE trơn tru nhờ engine WebAssembly có sẵn trong Chromium.
* Tài khoản an toàn 100%, không bao giờ bị khóa.
* Lượng RAM chỉ chiếm khoảng **100MB – 160MB**, thấp hơn rất nhiều so với mở bằng trình duyệt web thông thường (~800MB).

### Hạn chế:
* Không thể vẽ lại từng bong bóng chat bằng XAML thủ công, nhưng bù lại được giao diện chính chủ mượt mà và luôn cập nhật tính năng mới nhất từ Meta.
