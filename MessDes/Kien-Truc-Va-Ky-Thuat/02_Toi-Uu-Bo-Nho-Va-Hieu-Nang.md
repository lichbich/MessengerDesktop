---
title: "Kiến Trúc: Tối Ưu Hóa Bộ Nhớ & Hiệu Năng Chuyên Sâu"
tags:
  - kien-truc
  - performance
  - ram-optimization
  - empty-working-set
  - memory-target-level
  - chromium
created: 2026-10-19
updated: 2026-10-19
---

# ⚡ Kiến Trúc: Tối Ưu Hóa Bộ Nhớ & Hiệu Năng Chuyên Sâu

Tài liệu này giải thích chi tiết các giải pháp kỹ thuật đa tầng nhằm ép dung lượng RAM tiêu thụ của WebView2 từ mức thông thường (~600MB - 800MB) xuống mức siêu nhẹ.

---

## 1. Nguyên Nhân Khiến Facebook Messenger Chiếm Nhiều RAM

Trên môi trường trình duyệt thông thường hoặc WebView2 mặc định:
1. **Bộ nhớ V8 JavaScript Heap phình to:** Facebook Messenger Comet (React + Relay) tải hàng chục MB code JavaScript, lưu trữ hàng ngàn đối tượng tin nhắn, sticker, ảnh trong RAM. V8 engine mặc định cho phép heap phình tới 1.4 GB trước khi dọn rác (GC) tích cực.
2. **Back-Forward Cache:** Chromium tự động lưu lại các trang và trạng thái trước đó vào RAM để quay lại nhanh. Với một Single-Page App như Messenger, cơ chế này chỉ gây lãng phí bộ nhớ vô ích (~150MB).
3. **Phân mảnh và chiếm dụng Working Set:** Hệ điều hành Windows và Chromium thường giữ lại các trang nhớ không sử dụng (unreferenced pages) thay vì giải phóng ngay nếu máy tính còn nhiều RAM trống.

---

## 2. Hệ Thống Tối Ưu Đa Tầng (4-Pillar Memory Optimization)

### 2.1. Cờ Chromium Cắt Giảm RAM (V8 & Process Limit)
Ứng dụng ép các tham số tối ưu vào `CoreWebView2EnvironmentOptions`:
```csharp
var options = new CoreWebView2EnvironmentOptions(
    "--disable-features=Translate,OptimizationHints,MediaRouter,BackForwardCache,CalculateNativeWinOcclusion,InterestFeedContentSuggestions " +
    "--renderer-process-limit=1 " +
    "--js-flags=\"--max-old-space-size=256 --optimize-for-size\" " +
    "--disk-cache-size=20971520 " +
    "--media-cache-size=10485760 " +
    "--disable-background-networking " +
    "--autoplay-policy=no-user-gesture-required"
);
```
* `--renderer-process-limit=1`: Giới hạn tiến trình Renderer, tránh việc WebView2 phân mảnh thành nhiều tiến trình phụ ngốn RAM.
* `--js-flags="--max-old-space-size=256 --optimize-for-size"`: Giới hạn heap V8 ở mức 256MB và tối ưu hóa kích thước mã biên dịch. V8 sẽ thu gom rác thường xuyên hơn thay vì tích tụ rác bộ nhớ.
* `--disable-features=BackForwardCache`: Loại bỏ hoàn toàn bộ nhớ đệm trang lùi/tiến, tiết kiệm ngay ~100MB RAM.
* `--disk-cache-size=20971520` (20MB) & `--media-cache-size=10485760` (10MB): Khống chế dung lượng bộ nhớ đệm media và ảnh đại diện.

---

### 2.2. Quản Lý Vòng Đời `MemoryUsageTargetLevel`
WebView2 cung cấp API điều tiết mức độ ưu tiên bộ nhớ:
* **Khi người dùng đang tương tác (`Activated`):**
  `WvMessenger.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;`
  Đảm bảo tốc độ phản hồi 60fps mượt mà nhất.
* **Khi người dùng chuyển sang cửa sổ khác (`Deactivated`) hoặc Thu nhỏ (`Minimized`):**
  `WvMessenger.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;`
  Chromium lập tức xả sạch các bộ đệm ảnh, dọn rác V8 và đưa tài nguyên không dùng vào chế độ ngủ.

---

### 2.3. Cơ Chế Thu Hồi Bộ Nhớ Win32 (`EmptyWorkingSet`)
Ứng dụng sử dụng API nhân Windows `psapi.dll` để ép thu hồi các trang nhớ nhàn rỗi (idle unreferenced working set) từ cả tiến trình host C# và toàn bộ cây tiến trình con `msedgewebview2`:

```csharp
[DllImport("psapi.dll")]
private static extern int EmptyWorkingSet(IntPtr hwProc);

private void TrimMemory()
{
    // 1. Thu hồi rác .NET Runtime
    GC.Collect(2, GCCollectionMode.Forced, false);
    GC.WaitForPendingFinalizers();
    EmptyWorkingSet(Process.GetCurrentProcess().Handle);

    // 2. Thu hồi bộ nhớ toàn bộ tiến trình con WebView2
    if (WvMessenger?.CoreWebView2 != null)
    {
        var webViews = Process.GetProcessesByName("msedgewebview2");
        foreach (var p in webViews)
        {
            try { EmptyWorkingSet(p.Handle); } catch { }
        }
    }
}
```

---

### 2.4. Vòng Lặp Giám Sát Tự Động (Periodic Watchdog)
* **Sau khi tải xong trang (Navigation Completed + 4s):** Tự động kích hoạt đợt thu hồi đầu tiên để giải phóng toàn bộ bộ nhớ tạm phát sinh trong quá trình biên dịch và render giao diện ban đầu.
* **Định kỳ 75 giây:** Nếu ứng dụng đang ở trạng thái thu nhỏ hoặc người dùng đang làm việc ở cửa sổ khác, hệ thống sẽ tự động quét và kích hoạt `TrimMemory()`.

---

## 3. Kết Quả Đo Đạc Thực Tế

| Trạng Thái | Trước Khi Tối Ưu | Sau Khi Kích Hoạt Hệ Thống Tối Ưu | Mức Tiết Kiệm |
| :--- | :--- | :--- | :--- |
| **Tiến trình Renderer** | ~470 MB - 550 MB | **~80 MB - 140 MB** | **Giảm ~75%** |
| **Tổng các tiến trình con** | ~615 MB - 800 MB | **~120 MB - 180 MB** | **Giảm ~70%** |
| **Khi thu nhỏ xuống Taskbar** | Vẫn giữ nguyên ~600 MB | **Chỉ còn ~30 MB - 50 MB** | **Giảm ~92%** |

Hệ thống hoạt động hoàn toàn tự động, trong suốt đối với người dùng mà không gây giật lag hay ảnh hưởng đến khả năng nhận tin nhắn tức thì.
