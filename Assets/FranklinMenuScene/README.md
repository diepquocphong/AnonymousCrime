# Frank Sandbox Game — Web Menu

Giao diện uGUI cũ đã được gỡ khỏi `FranklinMenuScene.unity`. Scene menu hiện chỉ giữ một GameObject `FranklinWebMenuBridge` gồm bridge, native WebView host và runtime router; không còn Canvas, EventSystem, background PNG, Setting prefab hoặc các overlay Mission/Guest/Dialog/Loading cũ trong hierarchy.

Các script view/controller chỉ phục vụ uGUI cũ cũng đã bị xóa. Những lớp domain không phụ thuộc giao diện (mission progression/save/runtime gate) được giữ lại để web runtime và gameplay tiếp tục dùng dữ liệu thật.

## Web UI

- Runtime bundle: `Assets/StreamingAssets/FranklinWebMenu/`
- Bridge C#: `Assets/FranklinMenuScene/Web/FranklinWebMenuBridge.cs`
- Native host: `Assets/FranklinMenuScene/Web/FranklinWebMenuHost.cs`
- Runtime actions: `Assets/FranklinMenuScene/Web/FranklinWebMenuRuntime.cs`
- Kiến trúc, responsive và message contract: `Assets/FranklinMenuScene/Web/README.md`
- Editor scene bootstrap: `Assets/FranklinMenuScene/Editor/FranklinWebMenuSceneBuilder.cs`

Màn web dùng design canvas chuẩn 1920×1080, scale đồng nhất theo safe area và có lớp full-bleed cho tỷ lệ 20:9/4:3. Portrait hiển thị hướng dẫn xoay ngang.

## Phần được giữ lại

Các lớp domain như mission catalog/progression/save adapter và runtime gate vẫn nằm trong `Runtime/` để tái sử dụng khi nối các màn web tiếp theo. Ảnh nền PNG và thumbnail mission vẫn được giữ làm source asset; chúng không còn được scene menu load trực tiếp.

## Trạng thái tích hợp

Project dùng `gree/unity-webview` bản `package-nofragment`, khóa tại commit `356e9422e98af02d29771ccf622df7b785b16395` để build có thể tái lập. Host hiển thị bundle cục bộ trong:

- macOS Unity Editor: WKWebView snapshot vào Game view.
- Android: native WebView overlay, URL `file:///android_asset/FranklinWebMenu/index.html`.
- iOS: native WKWebView overlay từ `Application.streamingAssetsPath`.

Android Application Entry Point được đặt về `Activity`, là baseline tương thích plugin ổn định hơn trong khi package chưa cam kết riêng GameActivity. WebView bị hủy cùng scene trước khi gameplay chạy. Không có request CDN/trang ngoài; navigation chỉ cho phép bundle local và bridge nội bộ. Host chỉ hiện trang sau khi JavaScript gửi `ready`; nếu HTML/script lỗi, quá hạn hoặc WebView báo lỗi điều hướng thì host chuyển sang `fallback.html` cục bộ thay vì để một menu không bấm được.

Các nút đã nối click thật: Continue đọc save GameCreator; New Game và Nhiệm vụ mở màn chọn 6 nhiệm vụ khóa tuần tự; chỉ nút Chơi mới handoff sang gameplay; Quit hoạt động ngoài iOS. Danh sách nhiệm vụ là một dải cuộn ngang và tự căn tới nhiệm vụ đang chơi hoặc lựa chọn hợp lệ gần nhất khi mở. Profile mở hồ sơ khách cục bộ, còn Sync báo đúng trạng thái offline. Settings là màn web ba tab gồm đồ họa mobile, âm thanh menu và 12 ngôn ngữ. Menu có ambient nén AAC, nhiều cue SFX theo ngữ cảnh; audio tự pause khi app/WebView không còn hoạt động.

Phần objective trong GamePlay chưa được nối vào `FranklinMissionCompletionBridge`, vì vậy sáu entry hiện chia nội dung/thumbnail/progression ở menu nhưng vẫn tải cùng sandbox cho đến khi mission director phát sự kiện hoàn thành thật.

Không dùng lại builder uGUI cũ. Nếu cần dựng lại scene bootstrap tối giản:

`Tools > Franklin Game > Web Menu > Rebuild Web Bootstrap Scene`

Đã kiểm tra tĩnh compile C# và responsive DOM ngoài Unity. Theo yêu cầu của chủ dự án, chưa chạy Play Mode/Test Runner/device QA trong Unity.
