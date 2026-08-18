# Web menu asset provenance

- `menu-background.webp` được chuyển đổi từ `Assets/FranklinMenuScene/Art/FranklinMenuBackground.png`.
- Ảnh PNG trong project là bản 1920×1080 của ảnh nền được tạo bằng OpenAI ImageGen cho Frank Sandbox Game. Nguồn sinh ban đầu được lưu trong lịch sử tác vụ Codex của chủ dự án; giao diện web không dùng trực tiếp file trong `.codex`.
- `JosefinSans-Regular.ttf` và `JosefinSans-Bold.ttf` đến từ GameCreator Blockout package đang có trong project. Font dùng SIL Open Font License 1.1; bản giấy phép được chép thành `Assets/StreamingAssets/FranklinWebMenu/assets/OFL.txt`.
- Mockup đính kèm chỉ được dùng làm tham chiếu bố cục. UI web là các phần tử HTML/CSS có thể tương tác, không phải ảnh mockup phẳng.

## Nền hồ sơ khách

- `Assets/StreamingAssets/FranklinWebMenu/assets/guest-background.webp` được tạo mới bằng OpenAI ImageGen built-in ngày 2026-08-17. Mockup đăng nhập chỉ được dùng làm tham chiếu về độ mờ, bảng màu và không khí; ảnh không chứa UI, chữ, logo hoặc chủ thể nhận diện được.
- Prompt cuối: `Use case: stylized-concept; Asset type: static 16:9 background for a mobile game guest-profile/login WebView screen; Image 1 is a style, palette, blur, and mood reference only; generate a completely new clean background with no interface elements; a heavily defocused rainy city street at night, made as a pre-baked cinematic bokeh background so no runtime blur is needed; fictional modern downtown street after rain, distant buildings and street lights reduced to soft bokeh shapes, wet pavement reflecting light; cinematic photorealistic game background, intentionally shallow-focus and soft throughout; wide 16:9, centered visual balance, dark unobtrusive middle area behind a large UI panel, subtle brighter bokeh near the outer left and right edges; deep black and navy, restrained cyan and violet highlights, a few muted warm amber lights; no UI, frames, panels, buttons, text, letters, numbers, logos, watermarks, readable signs, recognizable brands, people, celebrities, copyrighted characters, prominent vehicles, sharp focal objects, or high-frequency detail.`
- Nguồn sinh được giữ tại `/Users/diepquocphong/.codex/generated_images/01a00935-b915-7be3-8fd6-9be27a0550a7/exec-f1fff86f-4e11-409b-8705-1dc8384e1948.png` (1672×941). Bản runtime được chuẩn hóa 1920×1080, bỏ metadata và mã hóa WebP quality 78; SHA-256 `786f2d305c4e12af9354d160b477e12112d9ed493e3aeec5dd5518c885c961a2`, dung lượng 35.196 byte.

## Thumbnail nhiệm vụ

- `assets/missions/mission-01-strawberry-pickup.webp` đến `mission-06-carrier-signal.webp` được chuyển từ sáu ảnh PNG 1024×576 trong `Assets/FranklinMenuScene/Art/Missions/`.
- Sáu ảnh gốc được tạo riêng bằng OpenAI ImageGen; prompt và đường dẫn nguồn được ghi trong `Assets/FranklinMenuScene/Art/Missions/IMAGEGEN.md`.
- Bản dùng trong WebView được resize về 768×432, bỏ metadata và mã hóa WebP quality 76; ảnh PNG gốc vẫn được giữ nguyên làm master asset.

## Icon khóa nhiệm vụ

- `Assets/StreamingAssets/FranklinWebMenu/assets/mission-lock-icon.png` được tạo bằng OpenAI ImageGen built-in ngày 2026-08-17. Hai lượt đầu thử nền alpha nhưng công cụ xuất checkerboard RGB; bản runtime dùng lượt chỉnh cuối với nền đen sâu trùng panel để không cần mask hoặc blur ở WebView mobile.
- Prompt sinh ban đầu: `Use case: stylized-concept; Asset type: premium mobile game UI icon; a single industrial padlock icon integrated inside a compact angular hexagonal tech badge; front-facing closed padlock with a thick readable shackle, reinforced gunmetal body, one clean keyhole; polished AAA mobile-game UI asset, realistic brushed metal with crisp silhouette readable at 48 pixels; perfectly centered square icon; cool silver highlights, subtle cyan edge light lower-right and restrained purple edge light upper-left; no text, numbers, letters, logo, trademark or watermark.`
- Prompt chỉnh cuối: `Use case: precise-object-edit; replace only the light gray and white checkerboard outside the approved badge with one perfectly uniform solid near-black color #020407; preserve the exact padlock, badge, composition, metal texture and cyan/purple edge lights; no checkerboard, gradient, texture, noise, halo, scenery, shadow, text, logo or watermark.`
- Nguồn sinh cuối được giữ tại `/Users/diepquocphong/.codex/generated_images/01a00935-b915-7be3-8fd6-9be27a0550a7/exec-81d46b3b-f42c-47a8-8eb7-b6d4a2ef89e0.png` (1254×1254). Bản runtime được resize Lanczos về 256×256 và bỏ metadata; SHA-256 `adbd8e56211d35c39b7dd8ecaf9fb4bd2671ec74f1c774db1484051e39da81ad`, dung lượng 89.712 byte.

## Âm thanh menu

Chỉ các clip dưới đây được lấy từ hai thư viện âm thanh do chủ dự án cung cấp. Hai thư mục nguồn không kèm LICENSE/README, vì vậy project cần giữ riêng hóa đơn hoặc bằng chứng quyền sử dụng; danh sách này chỉ ghi nguồn, không thay thế giấy phép.

- `menu-ambient.m4a`: `Ultimate Game Music Collection v1.9.2/Ambience/Modern Dark LOOP.wav`, chuyển sang AAC-LC stereo 44,1 kHz, 96 kbps để giảm WAV 19,35 MB xuống khoảng 1,34 MB.
- `ui-hover-01/02/03.wav`: `Game Music Stingers and UI SFX 2 Pack v1.0/UI_SFX/UI/FA_Select_Button_1_2`, `_1_3`, `_1_7`.
- `ui-activate.wav`: `UI_SFX/UI/LQ_Click_Button.wav`.
- `ui-back.wav`: `UI_SFX/UI/LQ_Back_Button.wav`.
- `ui-panel.wav`: `UI_SFX/Misc/PP_UI_Swap.wav`.
- `ui-transition.wav`: `UI_SFX/Misc/PP_Whoosh_1_2.wav`.
- `ui-denied.wav`: `UI_SFX/UI/CGM3_Error_Button_02_1.wav`.
- `ui-error.wav`: `UI_SFX/UI/FA_Error_Button_1.wav`.
- `ui-notify.wav`: `UI_SFX/Stinger/LQ_Positive_Notification.wav`.

Các SFX ngắn giữ PCM WAV 44,1 kHz để phản hồi chạm nhanh và tránh tạo decoder mới theo từng lần bấm. Runtime chỉ đóng gói các file đã chọn, không sao chép toàn bộ hai thư viện nguồn.
