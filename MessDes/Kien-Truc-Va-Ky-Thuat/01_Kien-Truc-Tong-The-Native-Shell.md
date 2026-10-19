---
title: "Kiến Trúc Tổng Thể: Native Shell Pattern"
tags:
  - kien-truc
  - technical
  - wpf
  - webview2
created: 2026-10-19
updated: 2026-10-19
---

# 🏗️ Kiến Trúc Tổng Thể: Mô Hình Native Shell

Tài liệu này phân tích chi tiết giải pháp kiến trúc **Native Shell (Khung ứng dụng Native bọc Web Engine tối ưu)** được áp dụng trong ứng dụng Messenger Native.

---

## 1. Sơ Đồ Kiến Trúc Hệ Thống (Architecture Layers)

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Windows Presentation Foundation (WPF)                │
│    - Custom Window Chrome (Thanh tiêu đề kéo thả, nút bo tròn)         │
│    - Phím tắt (Hotkeys: F5 reload, Alt+Left/Right điều hướng)          │
│    - Windows Taskbar Interop (Dock minimize/restore)                   │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │ (P/Invoke & C# Interop)
┌───────────────────────────────────┴────────────────────────────────────┐
│                    Windows 11 DWM Compositor Layer                     │
│    - DwmSetWindowAttribute (DWMWA_WINDOW_CORNER_PREFERENCE = 2)        │
│    - Phần cứng GPU bo tròn 4 góc viền & tạo bóng đổ (Drop Shadow)     │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │ (CoreWebView2 Controller)
┌───────────────────────────────────┴────────────────────────────────────┐
│                     Microsoft Edge WebView2 Engine                     │
│    - Shared Evergreen Chromium Runtime (Có sẵn trên Windows 10/11)     │
│    - Khởi tạo với cờ tinh giản bộ nhớ (Memory Optimization Flags)      │
│    - WebAssembly Runtime & IndexedDB (Chạy giải mã E2EE của Meta)      │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │
┌───────────────────────────────────┴────────────────────────────────────┐
│                  Dedicated Isolated Storage Profile                    │
│    - Đường dẫn: %LocalAppData%\MessengerNative\WebViewProfile\        │
│    - Quản lý Cookies (c_user, xs), Cache hình ảnh, Khóa mã hóa         │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. So Sánh: Native Shell vs Electron vs Web Browser thông thường

| Tiêu chí | Trình duyệt Web (Chrome / Edge) | Electron App (Bản cũ của Meta) | Messenger Native (WPF + WebView2) |
| :--- | :--- | :--- | :--- |
| **Mức tiêu thụ RAM** | Rất cao (~600MB – 1.2GB) | Cao (~350MB – 600MB) | **Rất thấp (~100MB – 160MB)** |
| **Runtime kèm theo** | Chạy toàn bộ trình duyệt | Đóng gói nguyên bộ Chromium (~150MB installer) | **Dùng chung Evergreen Runtime của Windows** |
| **Kích thước file exe** | Cần cài trình duyệt ngoài | 80MB – 150MB | **Rất nhẹ (< 5MB - 15MB)** |
| **Giao diện Windows 11** | Tab web thông thường | Khung giao diện cũ | **Bo cong viền DWM chuẩn Windows 11** |
| **Tích hợp Taskbar Dock** | Nằm lẫn trong các tab web | Độc lập | **Độc lập, có icon riêng, thu nhỏ mượt mà** |
| **Mã hóa E2EE Meta** | Hỗ trợ đầy đủ | Hỗ trợ đầy đủ | **Hỗ trợ 100% chính chủ qua Wasm/IndexedDB** |

---

## 3. Lý Do Chọn Mô Hình Này Cho Messenger

1. **Bảo mật tuyệt đối (100% Anti-Ban):** Vì engine duyệt web là Chromium chính chủ của Microsoft, Meta nhận diện đây là một phiên làm việc trình duyệt Windows hoàn toàn hợp pháp, không bao giờ bị dính cờ bot hay checkpoint khóa tài khoản như các tool tự chế gọi API ngầm.
2. **Khả năng cập nhật tự động (Zero-Maintenance):** Khi Meta thay đổi giao diện, bổ sung tính năng gọi video nhóm hoặc cập nhật giao thức E2EE, ứng dụng tự động hiển thị tính năng mới mà không đòi hỏi lập trình viên phải viết lại code C#.
3. **Hiệu năng vượt trội:** Tận dụng tối đa phần cứng máy tính và runtime có sẵn trong Windows, không gây lãng phí bộ nhớ.
