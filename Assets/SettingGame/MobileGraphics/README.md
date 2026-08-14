# Franklin Mobile Graphics Settings

Các component điều khiển Settings nằm tập trung trong `Assets/SettingGame/Setting.prefab`.
Màn hình vẫn nối với nút bánh răng và dùng chung Canvas/HUD hiện có, nhưng không còn
gắn component Setting lên `CanvasPlayerControl.prefab`. Panel bind lại HUD/Canvas khi
đổi scene và không polling renderer hoặc tìm object theo frame.

## Tính năng

- `CHẤT LƯỢNG`: có bốn lựa chọn Auto/Thấp/Vừa/Cao và lưu bằng `PlayerPrefs`.
  `AUTO` phân loại thiết bị bằng RAM, VRAM và số lõi CPU, sau đó tiếp tục tăng/giảm
  render scale theo FPS đo được. Ba mức thủ công vẫn dùng dynamic resolution để giữ
  FPS ổn định trong giới hạn của từng profile.
  Ba mức thủ công là preset hiệu ứng bắt buộc: `THẤP` tắt FBS, SSAO, FXAA/SMAA,
  Fast DOF và toàn bộ URP Post Processing; `VỪA` chỉ bật FBS; `CAO` bật FBS, SSAO,
  SMAA Low, Fast DOF và toàn bộ URP Post Processing trên camera gameplay. Khi vừa
  chọn `CAO`, DOF được bật mặc định nhưng nút DOF vẫn cho phép chọn `TẮT/GẦN/XA`
  và lưu lựa chọn. Các hiệu ứng khác luôn tuân theo preset; `AUTO` cho phép người
  chơi điều chỉnh từng hiệu ứng riêng.
- `FBS`: khóa tổng dùng API có sẵn
  `FranklinBlobShadow.SetGlobalEnabled(bool)`. Khi tắt, toàn bộ renderer, raycast nền,
  kiểm tra camera và kiểm tra khoảng cách của blob shadow đều dừng.
- `SSAO`: bật/tắt `ScreenSpaceAmbientOcclusion` ở cấp Renderer Feature. Khi tắt,
  URP bỏ hoàn toàn SSAO render pass thay vì render rồi đặt opacity bằng 0.
- `KHỬ RĂNG CƯA`: ba lựa chọn `TẮT / FXAA / SMAA LOW`. FXAA là mặc định và được
  khuyến nghị cho mobile; SMAA Low sắc nét hơn nhưng tốn GPU hơn. Tùy chọn được áp
  dụng cho camera gameplay hiện tại và mọi camera Base xuất hiện sau đó khi đổi qua
  Player/Car/Bike.
- `LẤY NÉT DOF`: nút tuần tự `TẮT / GẦN / XA`. Bản URP dùng hai lượt blur ở 1/4
  độ phân giải và camera depth, không cần đổi material của map. Hiệu ứng tự dùng vị
  trí Player làm mốc và chỉ làm mờ một chiều ở xa, nên Player/mặt đất quanh chân
  luôn sắc nét. `GẦN` bắt đầu mờ sau Player 15 m; `XA` bắt đầu mờ sau Player 40 m,
  phù hợp camera lái xe ngoài trời.
- Khi Settings mở, input đi bộ/lái xe được khóa bằng owner token của
  `FranklinMobileHud`; đóng màn hình sẽ chỉ trả lại token của Settings.
- FPS counter tự spawn đúng một instance vào `CanvasPlayerControl` trong scene;
  nếu canvas này chưa tồn tại, counter đợi và tự gắn vào Screen Space Canvas phù hợp.
  Counter nằm sát góc trên trái, cập nhật mỗi 0.5 giây và không raycast UI.

Màu FPS: xanh từ 55 FPS, vàng từ 30 FPS và đỏ dưới 30 FPS. Việc lấy mẫu dùng
`Time.unscaledDeltaTime`, nên counter vẫn chính xác khi game pause hoặc thay đổi
`timeScale` và không thực hiện tìm Canvas mỗi frame sau khi đã gắn thành công.

## Cấu hình SSAO mobile

Installer tạo `Mobile SSAO (Franklin Low Cost)` trong
`Assets/Settings/Mobile_Renderer.asset` với:

- half resolution (`Downsample`);
- depth reconstruction, không ép depth-normal prepass;
- 4 AO samples;
- low-cost Kawase blur;
- interleaved gradient, không cần lấy mẫu blue-noise texture;
- `Intensity 1.15`, `Direct Lighting Strength 0.35`, `Radius 0.24` để các khe,
  chân tường và tiếp điểm môi trường rõ hơn;
- mặc định runtime **TẮT** trên mobile. Renderer Feature được giữ active trong asset
  để URP không strip shader variant khi build; `FranklinMobileGraphicsSettings.Awake`
  áp trạng thái đã lưu trước frame render đầu tiên.

Editor installer tự khôi phục `m_Active` của SSAO asset khi thoát Play Mode (kể cả
khi bật Enter Play Mode Options / không reload domain), nhờ đó build sau khi test
không vô tình bị URP strip mất SSAO shader variants.

Cấu hình này chỉ chạy khi người chơi bật SSAO. FBS mặc định bật và vẫn dùng distance
culling/probe throttling hiện có.

`Setting.prefab` lưu SSAO Layer Mask mặc định gồm `Ground`, `Building`, `Wall` và
`Prop` (layer 7–10). Mask này quyết định camera gameplay nào đủ điều kiện chạy SSAO.
Do SSAO tích hợp sẵn của URP là hiệu ứng screen-space, khi một camera đủ điều kiện thì
URP vẫn đánh giá toàn bộ opaque pixel mà chính camera đó render; đây không phải
`Volume Layer Mask`.

## Khử răng cưa mobile

Theo tài liệu URP, FXAA là phương pháp ít tốn tài nguyên nhất; SMAA cho kết quả sắc
nét hơn FXAA. TAA không được đưa vào menu vì không tương thích với Dynamic Resolution
đang dùng trong game và dễ tạo ghosting ở chuyển động nhanh của xe. MSAA của Mobile
RP Asset tiếp tục giữ ở 1x/tắt để không chạy chồng hai kỹ thuật AA và không tăng
texture bandwidth trên GPU mobile.

FXAA/SMAA cần URP Post Processing resolve. Manager chỉ bật resolve này trên camera,
và nếu camera vốn không dùng Post Processing thì đặt Volume Layer Mask bằng 0. Vì
vậy bật AA không vô tình bật thêm Bloom, Vignette hoặc Tonemapping trong Global
Volume. Khi chọn `TẮT`, disable manager hoặc thoát Play Mode, cấu hình gốc của camera
được khôi phục hoàn toàn.

Không có `FindObjectsOfType` hay quét Scene theo frame. Camera hiện có chỉ được quét
khi đổi setting; camera mới được nhận qua callback `Camera.onPreCull` đúng lần render
đầu tiên rồi lưu vào cache.

## Runtime API

```csharp
using FranklinGame.Settings;

FranklinMobileGraphicsSettings.SetFbsEnabled(true);
FranklinMobileGraphicsSettings.SetSsaoEnabled(false);
FranklinMobileGraphicsSettings.SetQualityLevel(3); // 0 Low, 1 Balanced, 2 High, 3 Auto
FranklinMobileGraphicsSettings.SetAntiAliasingMode(1); // 0 Off, 1 FXAA, 2 SMAA Low
FranklinMobileGraphicsSettings.SetDepthOfFieldMode(2); // 0 Off, 1 Near, 2 Far

bool fbs = FranklinMobileGraphicsSettings.FbsEnabled;
bool ssao = FranklinMobileGraphicsSettings.SsaoEnabled;
bool ssaoPass = FranklinMobileGraphicsSettings.SsaoRenderPassActive;
int quality = FranklinMobileGraphicsSettings.QualityLevel;
int antiAliasing = FranklinMobileGraphicsSettings.AntiAliasingMode;
int depthOfField = FranklinMobileGraphicsSettings.DepthOfFieldMode;
```

Mặc định mới là `AUTO`. Dữ liệu lưu cũ ở mức `0..2` vẫn được giữ nguyên, không bị
chuyển sang Auto ngoài ý muốn. Chọn Auto gọi
`DynamicResolutionScaler.ApplyAutomaticDeviceProfile()`; trạng thái hiển thị profile
thiết bị thực tế như `LOW`, `BALANCED` hoặc `HIGH` ngay trong bảng Settings.

Các khóa lưu:

- `Franklin.FastBlobShadow.Enabled`
- `Franklin.MobileGraphics.SSAO.Enabled`
- `Franklin.MobileGraphics.Quality.Level`
- `Franklin.MobileGraphics.AntiAliasing.Mode`
- `Franklin.MobileGraphics.DepthOfField.Mode`

## Cài lại tự động

Installer tự kiểm tra sau khi Unity compile. Có thể chạy thủ công:

`Tools > Franklin Game > Install Mobile Settings + SSAO + DOF`

Installer sẽ:

1. tạo/cấu hình SSAO và Fast Mobile DOF Renderer Feature cho Mobile/PC Renderer
   nếu chưa có;
2. gắn `DynamicResolutionScaler`, `FranklinMobileGraphicsSettings` và
   `FranklinMobileSettingsPanel` tập trung trong `Assets/SettingGame/Setting.prefab`,
   đồng thời gỡ hai component Setting cũ khỏi `CanvasPlayerControl.prefab`;
3. nối feature Mobile + PC/Editor để toggle có thể kiểm thử trong Editor.

Khi đang Play Mode có thể chạy smoke test không thay đổi PlayerPrefs:

`Tools > Franklin Game > Smoke Test Mobile Settings`

Để mở panel và chụp Game View phục vụ visual QA:

`Tools > Franklin Game > Open + Capture Mobile Settings`

Ảnh được ghi vào `Temp/FranklinMobileSettingsRuntime.png` và không nằm trong build.

## Mockup

`Mockups/FranklinMobileSettingsMockup.png` là bản mockup cũ được giữ lại để tham
khảo quá trình thiết kế. Ảnh không được load trong runtime nên không tốn RAM/texture
bandwidth trên thiết bị; bố cục runtime trong `FranklinMobileSettingsPanel.cs` mới
là phiên bản hiện hành.

Giao diện runtime hiện dùng thiết kế GTA mobile mới: header tối, accent cyan, một
quality card có bốn profile, ba mức khử răng cưa và nút lấy nét DOF
`TẮT/GẦN/XA`; hai feature card FBS/SSAO cân đối và một nút `HOÀN TẤT`.
Các lựa chọn được lưu ngay khi nhấn nên không còn cặp button `ÁP DỤNG/ĐÓNG` dư thừa.
