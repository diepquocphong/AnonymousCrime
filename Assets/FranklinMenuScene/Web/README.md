# Frank Sandbox Game Web Menu

Màn đầu tiên dùng HTML + CSS + JavaScript thuần để giảm kích thước, RAM và CPU trong WebView mobile. Không có framework, request mạng, video, canvas loop, realtime blur hoặc animation chạy vô hạn.

## Kích thước và responsive

- Design canvas chuẩn: **1920×1080**.
- Một nền trong design stage scale cùng UI để giữ đúng bố cục; lớp nền phụ full-bleed chỉ lấp letterbox ở 20:9/4:3.
- `env(safe-area-inset-*)` kết hợp fallback từ Unity `Screen.safeArea`, nên notch/home indicator vẫn được xử lý trên Android WebView cũ.
- Landscape 16:9 khớp design canvas; 20:9 và 4:3 giữ nguyên tỷ lệ, chỉ thay khoảng trống an toàn.
- Portrait mobile hiện hướng dẫn xoay ngang vì game được cấu hình landscape.
- Visual button luôn đúng 100 design px. Hit area trong suốt tự mở từ 116–230 design px, nhắm tới 48 CSS px trên mobile landscape mà không làm sai mockup 1920×1080. Layout compact tăng riêng cỡ chữ; ultra-compact ẩn footer/subtitle; mức micro dưới 400×300 ưu tiên nút và ẩn logo để không chồng lấn.

## Runtime web

Các file được Unity chép nguyên trạng từ:

`Assets/StreamingAssets/FranklinWebMenu/`

Nền đã chuyển từ PNG 2 MB thành WebP 1920×1080 khoảng 111 KB. Cùng một URL ảnh được dùng cho stage và lớp lấp letterbox để giữ nét trên màn hình DPI cao mà không yêu cầu hai ảnh decode đồng thời. Hai font được load local, kèm giấy phép `OFL.txt`, và không cần Internet.

Sáu thumbnail nhiệm vụ được đóng gói riêng dưới `assets/missions/` ở 768×432 WebP. Ảnh chỉ decode khi màn chọn nhiệm vụ mở; các card bị khóa không tạo thêm hiệu ứng render liên tục.

## Âm thanh menu

- `menu-audio.js` quản lý một luồng ambient AAC và các SFX WAV được preload; không tạo Audio mới theo từng lần click.
- Nhạc bắt đầu sau tương tác tin cậy đầu tiên để tương thích chính sách autoplay của Android WebView/WKWebView.
- Hover/focus xoay vòng ba biến thể; click, panel, offline/lỗi, thông báo, transition và back dùng cue riêng.
- Nút `ÂM THANH` lưu trạng thái bằng Unity PlayerPrefs; localStorage chỉ là fallback khi mở HTML độc lập.
- Khi app mất focus, pause, chuyển portrait hoặc WebView bị ẩn, nhạc và SFX đều dừng. Khi busy, ambient được duck thay vì chạy thêm loading loop.
- CSP chỉ cho media local trong bundle; `connect-src 'none'` vẫn được giữ nguyên.

Tổng bundle âm thanh khoảng 1,9 MB: ambient AAC khoảng 1,34 MB và các SFX PCM khoảng 0,54 MB. Nguồn chi tiết nằm trong `ASSET_PROVENANCE.md`.

## Bridge

`FranklinWebMenuBridge.cs` nhận JSON từ WebView bằng `OnWebMessage(string)` và chỉ cho phép các action:

- `ready`
- `continue`
- `new-game`
- `mission-open`
- `settings`
- `settings-quality`, `settings-aa`, `settings-dof`, `settings-fbs`, `settings-ssao`
- `settings-audio-enabled`, `settings-audio-master`, `settings-audio-music`, `settings-audio-sfx`
- `settings-language`, `settings-complete`
- `mission-select`, `mission-play`
- `guest-create`, `guest-login`
- `quit`
- `profile`
- `sync`
- `audio-toggle`

Native host inject `window.FranklinUnity.postMessage` qua `window.Unity.call` của gree/unity-webview. JavaScript vẫn giữ fallback iOS/WebGL cho việc tái sử dụng bundle, nhưng runtime mobile hiện dùng cùng một contract JSON qua gree.

Bridge khóa action ngoài allow-list, chống double-tap ngắn, chuyển callback về Unity main thread và phát `StateJsonChanged` để host đẩy `BuildStateJson()` lại trang. `TIẾP TỤC` mặc định bị khóa tới khi Unity xác nhận có save; `THOÁT` tự ẩn trên iOS.

## Native host

`FranklinWebMenuHost.cs` tạo đúng một WebView fullscreen và load file local:

- Android: `file:///android_asset/FranklinWebMenu/index.html`
- iOS/macOS Editor: file URI dựng từ `Application.streamingAssetsPath`

Package được pin trong `Packages/manifest.json`:

`https://github.com/gree/unity-webview.git?path=/dist/package-nofragment#356e9422e98af02d29771ccf622df7b785b16395`

`SetURLPattern` chặn navigation ngoài đúng thư mục local; CSP chặn network, frame, object và form. Android dùng `Activity` entry point làm baseline tương thích. Host giữ WebView ẩn cho tới khi trang local tải xong **và** JavaScript gửi `ready`; timeout/lỗi navigation chuyển sang `fallback.html`. Khi scene menu bị unload, host ẩn/destroy native WebView để giải phóng browser/background trước gameplay.

## Luồng giao diện

- Continue: chỉ bật khi GameCreator xác nhận có save, sau đó gọi `LoadLatest()`.
- New Game: mở màn chọn nhiệm vụ web; không tải GamePlay ngay.
- Nhiệm vụ: nút riêng trên menu chính mở cùng màn chọn nhiệm vụ và refresh tiến độ authoritative từ Unity.
- Mission: hiển thị sáu nhiệm vụ trong `FranklinMissionCatalog`, tiến độ thật `0/6` đến `6/6`, thumbnail, detail và trạng thái khóa tuần tự. `mission-select` chỉ đổi lựa chọn; `mission-play` mới xác thực ID, gọi `BeginMission()` và restart scene GamePlay.
- Chỉ gameplay gọi `FranklinMissionProgress.CompleteActiveMission()` mới tăng tiến độ và mở khóa màn kế tiếp; click/select/play không tự mở khóa. Component attachable `FranklinMissionCompletionBridge` đã được cung cấp, nhưng GamePlay hiện chưa nối nó vào mission director/sự kiện hoàn thành thật nên sáu lựa chọn vẫn vào cùng sandbox cho tới khi phần objective được tích hợp.
- Profile: mở màn hồ sơ khách cục bộ. Người chơi có thể nhập tên, chọn một trong ba avatar, tạo/cập nhật hồ sơ và đăng nhập lại bằng hồ sơ đã lưu trên thiết bị.
- Guest không phải tài khoản online: ID cục bộ, tên, avatar và thời gian chơi gần nhất được lưu bằng PlayerPrefs. Việc cập nhật hồ sơ luôn giữ nguyên identity/save hiện có vì GameCreator save và tiến độ mission chưa được namespaced theo nhiều tài khoản.
- Không có email, mật khẩu hoặc nút cloud giả; giao diện cảnh báo rõ gỡ ứng dụng/xóa dữ liệu có thể làm mất tiến trình.
- Sync: thông báo offline, không giả lập cloud.
- Quit: đóng app/thoát Play Mode; bị ẩn trên iOS.
- Settings: ba tab Đồ họa, Âm thanh và Ngôn ngữ. Đồ họa lưu profile mobile; âm thanh tách tổng/nhạc menu/SFX giao diện; ngôn ngữ hỗ trợ Việt, Anh, Đức, Pháp, Nhật, Hàn, Trung giản thể, Trung phồn thể, Nga, Bồ Đào Nha (Brazil), Tây Ban Nha (Mỹ Latinh) và Hà Lan. Unity PlayerPrefs giữ state authoritative.

Không chạy Play Mode hoặc Test Runner tự động. Chủ dự án kiểm tra Game view và thiết bị thật.
