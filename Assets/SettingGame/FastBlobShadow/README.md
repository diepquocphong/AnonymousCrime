# Franklin Mobile Blob Shadow

Core blob shadow dùng chung cho Player, NPC, Car và Bike, đã rút gọn từ Fast Volumetric Blob Shadows để dùng với Unity 6 và URP 17 trên mobile.

## Thành phần được giữ lại

- `Runtime/FranklinBlobShadow.cs`: controller và API runtime.
- `Shaders/FranklinMobileBlobShadow.shader`: shader URP đọc depth texture, unlit, một pass.
- `Materials/PlayerBlobShadow.mat`: material dùng chung; mỗi instance tùy biến qua `MaterialPropertyBlock`.
- `Meshes/ShadowSphere_Mesh.fbx`: volume sphere thấp, không animation/material/collider.
- `Editor/FranklinBlobShadowInstaller.cs`: API Editor idempotent để tích hợp/cập nhật prefab.
- Mỗi prefab gameplay có đúng một `MobileBlobShadow` và một `FranklinBlobShadow` trên root.

Examples, editor tools, shader Built-in, cube variant, tài liệu/changelog cũ và file `.unitypackage` đã bị loại vì không cần ở runtime.

## Cách hoạt động

Shadow volume hỗ trợ ba footprint trong cùng shader: `Circle` cho Player/NPC,
`Rectangle` mềm cho Car và `Ellipse` cho Bike. Width (`X`) và length (`Z`) độc lập
ở vehicle; installer đo renderer bounds thật của từng prefab, loại VFX/helper rồi clamp
về khoảng hợp lý. Footprint giữ trục dài theo hướng forward của vehicle và đồng thời
nghiêng theo normal mặt đất. Ground tracking dùng `Physics.RaycastNonAlloc`; probe mặc định của
Player chạy mỗi `0.08s` thay vì mỗi frame, bỏ qua trigger và collider thuộc chính owner.
Giữa hai probe, vị trí được chiếu lên mặt phẳng ground đã cache để giảm độ trễ khi di chuyển.

Khi Player nhảy hoặc bay lên, shadow vẫn nằm trên ground nhưng tự thu nhỏ và mờ dần. Mặc định hiệu ứng bắt đầu sau `0.05m` độ cao airborne và renderer tắt hoàn toàn từ `2.5m`. Chiều cao được tính sau khi trừ pivot grounded `1m` của Player.

Shader tái tạo world position từ URP depth texture rồi tạo falloff ellipse/rectangle
trong local space. Shape được truyền bằng `MaterialPropertyBlock`, không tạo material
instance, shader keyword hoặc variant theo từng vehicle. Controller tự bật
`requiresDepthTexture` trên camera gameplay; không bật depth toàn bộ `Mobile_RPAsset`,
tránh áp chi phí này cho camera không dùng shadow.

Lưu ý: vật liệu transparent không ghi depth sẽ không nhận blob shadow.

## API runtime

Master switch dùng chung cho toàn bộ Player, NPC và vehicle:

```csharp
// Tắt toàn bộ, lưu vào PlayerPrefs cho lần chạy sau.
FranklinBlobShadow.SetGlobalEnabled(false);

// Bật lại toàn bộ.
FranklinBlobShadow.SetGlobalEnabled(true);

bool enabled = FranklinBlobShadow.GlobalEnabled;
enabled = FranklinBlobShadow.ToggleGlobalEnabled();
FranklinBlobShadow.GlobalEnabledChanged += OnBlobShadowSettingChanged;
```

Preference key là `Franklin.FastBlobShadow.Enabled`. Có thể gọi
`FranklinBlobShadow.ResetGlobalEnabled()` để xóa lựa chọn đã lưu và trở về mặc định bật.
Khi master switch tắt, tất cả renderer tắt ngay; controller không tìm camera, không
kiểm tra distance và không raycast ground. `SetVisible` bên dưới chỉ điều khiển riêng
một instance và không thể ghi đè master switch.

```csharp
using FranklinGame.Rendering;

FranklinBlobShadow shadow;
if (FranklinBlobShadow.TryGet(playerComponent, out shadow))
{
    shadow.SetVisible(true);
    shadow.SetOpacity(0.55f);
    shadow.SetColor(Color.black);
    shadow.SetPower(1.7f);
    shadow.SetDiameter(1f);
    shadow.SetAirborneResponse(1f, 0.05f, 2.5f, 0.25f);
    shadow.SetGroundTracking(true);
    shadow.RefreshGround();
}
```

API footprint cho vehicle:

```csharp
shadow.SetEllipse(0.9f, 2.2f);      // Bike: width, length
shadow.SetRectangle(1.9f, 4.2f);   // Car: width, length
shadow.SetSize(2.0f, 4.4f);        // Giữ shape hiện tại
shadow.SetFootprintShape(FranklinBlobShadow.FootprintShape.Rectangle);
```

Camera tạo động có thể gán rõ ràng:

```csharp
shadow.SetRenderCamera(gameplayCamera);
// Hoặc chỉ cấu hình depth cho một camera:
FranklinBlobShadow.EnsureCameraDepth(gameplayCamera);
```

Distance culling có thể chỉnh runtime mà không tìm lại component:

```csharp
shadow.SetDistanceCulling(40f, 0.25f);
```

Khi ở ngoài khoảng cách, renderer tắt và controller ngừng raycast ground. Khoảng cách chỉ kiểm tra mỗi `0.25s`, được stagger theo instance để nhiều NPC/vehicle không cùng dồn việc vào một frame.

## Prefab đã tích hợp

| Loại | Footprint | Kích thước W × L | Opacity | Culling | Ground probe |
|---|---|---:|---:|---:|---:|
| Player | Circle | 1.00 × 1.00 m | 0.58 | 40 m | 0.08 s |
| NPC | Circle | 1.00 × 1.00 m | 0.52 | 28 m | 0.10 s |
| Car | Rectangle mềm | 1.91 × 4.23 m | 0.68 | 45 m | 0.10 s |
| Bike 01–10 | Ellipse theo mesh | 0.72–1.01 × 2.11–2.23 m | 0.64 | 40 m | 0.10 s |

API Editor `FranklinGame.Rendering.Editor.FranklinBlobShadowInstaller.InstallAll()` có thể gọi lại từ installer tổng hoặc validation pipeline. Hàm này chỉ tạo/cập nhật child `MobileBlobShadow` và component `FranklinBlobShadow`; không thay cấu hình physics/gameplay khác.

## Tuning cho mobile

- `SetGlobalEnabled(false)`: tắt toàn bộ draw và update của hệ thống; nên nối trực tiếp
  với toggle Blob Shadow trong menu Graphics.
- `Probe Interval`: tăng lên `0.10-0.15` nếu có nhiều character; Player mặc định là `0.08`.
- `Max Visible Distance`: giảm để bỏ cả draw và ground probe ở khoảng cách xa; `0` là không giới hạn.
- `Distance Check Interval`: mặc định `0.25s`; không kiểm tra khoảng cách mỗi frame.
- `Ground Layers`: nên chỉ chọn layer môi trường để giảm physics query. Controller vẫn lọc collider con của owner.
- `Grounded Pivot Height`: Player hiện dùng pivot giữa capsule cao 2m nên giá trị mặc định là `1`.
- `Airborne Fade Start / Hide Height`: chỉnh thời điểm shadow bắt đầu nhỏ/mờ và biến mất hoàn toàn.
- `Volume Size Y`: giữ thấp (`0.5-0.8`) để hạn chế shadow dính lên tường/vật thể gần chân.
- Không gọi `renderer.material`; API hiện tại giữ một shared material và không tạo material instance lúc chạy.

## Nguồn

Mesh volume bắt nguồn từ asset Fast Volumetric Blob Shadows 1.4.0 của Pixel Tea. Phần controller và shader URP trong thư mục này được viết riêng cho Franklin Game.
