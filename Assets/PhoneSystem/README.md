# Franklin PhoneSystem

PhoneSystem là giao diện điện thoại trong game theo phong cách GTA, được tối ưu cho thao tác cảm ứng trên thiết bị mobile. Điện thoại sử dụng một Canvas riêng, xuất hiện ở bên phải màn hình và được mở từ button Phone nằm cạnh minimap.

Vị trí mở của giao diện được lấy trực tiếp từ `Phone Device > RectTransform > Anchored Position` trong prefab. Khi Play, hệ thống không còn dùng một `Open Position` ẩn để ghi đè chỉnh sửa này; vị trí đóng được tự tính nằm ngoài mép phải màn hình.

## Tính năng hiện có

- Tự nạp prefab khi scene bắt đầu và giữ PhoneSystem khi chuyển scene.
- Tự áp dụng `Screen.safeArea` cho thiết bị có tai thỏ hoặc vùng bo góc.
- Sáu ứng dụng: Điện thoại, Tin nhắn, Ghi chú, Bản đồ, Camera selfie và Ảnh.
- Bàn phím gọi điện có hai tab riêng: `KEYPAD` và `RECENT`.
- Chuyển riêng giữa chế độ nhập số `123` và nhập chữ `ABC`; hai dạng không hiển thị đồng thời.
- Nhập chữ theo kiểu multi-tap/T9, ví dụ `ABC`, `DEF`, `GHI`.
- Lịch sử cuộc gọi được lưu trực tiếp trên thiết bị bằng `PlayerPrefs` và có button xóa.
- Tin nhắn nhiệm vụ có màn hình danh sách, màn hình đọc chi tiết, button nhận hoặc hủy nhiệm vụ.
- Khi điện thoại mở, các nút điều khiển nhân vật/xe và toàn bộ Weapon HUD được ẩn.
- Khi điện thoại đóng, các điều khiển và Weapon HUD được khôi phục; vị trí và kích thước HUD không bị thay đổi.
- Các idle variation/idle gesture phụ của `FranklinAnimationBridge` được dừng và tạm khóa khi phone mở; locomotion Animator vẫn hoạt động để animation xương cầm điện thoại không bị đóng băng.
- Player dùng Unity Humanoid IK để đưa riêng tay phải từ bên hông lên và cầm model `Phone_A1_LP`; tay trái giữ nguyên animation locomotion, không còn bị solver của PhoneSystem điều khiển.
- Model phone bám theo socket runtime trên bàn tay phải. Grip được tạo bằng Humanoid muscle thay vì xoay trực tiếp local Euler của bone, nên không phụ thuộc trục xương riêng của model Franklin.
- Mọi button trong Canvas PhoneSystem đều kích hoạt ngón cái phải thực hiện một nhịp chạm vật lý.
- Camera app lấy vị trí khởi đầu từ `PhoneInstance` trên tay phải, sau đó điều khiển một bản runtime của `Assets/Prefab/Camera Shot.prefab`. Shot giữ nguyên `ShotTypeThirdPerson` và input orbit mobile; GC2 Main Camera chỉ kế thừa output từ Camera Shot. Player quay tự nhiên bằng Facing layer và đưa điện thoại lên pose selfie riêng.
- Nút Shutter lưu frame đang xem thành PNG trong `Application.persistentDataPath/FranklinPhonePhotos` và tự nạp tối đa 9 ảnh mới nhất vào Photos app.
- Màn hình model sáng sau khi điện thoại được đưa lên, tắt ngay khi bắt đầu cất điện thoại; vật liệu gốc không bị sửa vì hiệu ứng dùng `MaterialPropertyBlock` riêng cho instance runtime.
- Đầu Player nhìn vào điện thoại bằng `RigLookTo` của Game Creator 2 với layer ưu tiên `-100`, cao hơn camera head-look hiện có.
- Hiệu ứng trượt mở/đóng sử dụng thời gian không phụ thuộc `Time.timeScale`.
- Bộ âm thanh UI đa dạng cho mở/đóng điện thoại, ứng dụng, phím nhập, gọi điện, xác nhận, lỗi và nhiệm vụ.

## Cài đặt nhanh

Prefab đã được cấu hình tại:

`Assets/PhoneSystem/Resources/FranklinPhoneSystem.prefab`

Không cần đặt prefab thủ công vào từng scene. Khi chạy game, `FranklinPhoneSystem` tự tải prefab từ thư mục `Resources` nếu chưa có instance nào trong scene.

Button Phone hiện có trong `CanvasPlayerControl` phát sự kiện qua `FranklinMobileHud`. PhoneSystem tự tìm HUD, đăng ký sự kiện và mở hoặc đóng giao diện khi người chơi nhấn button này.

Nếu cần tạo lại toàn bộ prefab từ mã Editor, sử dụng menu:

`Tools > Franklin Game > Install Phone System UI`

> Lưu ý: lệnh Install sẽ dựng và lưu lại prefab PhoneSystem. Chỉ chạy khi thật sự muốn tái tạo giao diện vì các chỉnh sửa thủ công trực tiếp trên prefab có thể bị ghi đè.

## Cách sử dụng trong game

### Màn hình chính

Nhấn button Phone cạnh minimap để mở điện thoại. Màn hình chính hiển thị sáu ứng dụng:

| Index | Ứng dụng | Trạng thái |
| ---: | --- | --- |
| 0 | Điện thoại | Bàn phím số/chữ, gọi giả lập và lịch sử cuộc gọi |
| 1 | Tin nhắn | Danh sách yêu cầu nhiệm vụ, nhận hoặc hủy |
| 2 | Ghi chú | Nhập nội dung bằng bàn phím T9; thao tác lưu hiện đang giả lập |
| 3 | Bản đồ | Giao diện bản đồ giả lập |
| 4 | Camera | Live selfie, quay Player về Main Camera và chụp ảnh |
| 5 | Ảnh | 9 ảnh selfie thật gần nhất, xem toàn màn hình và xóa ảnh |

Button `X` hoạt động theo màn hình hiện tại:

- Khi đang ở trong một ứng dụng, `X` chỉ đóng ứng dụng và quay về Home của điện thoại.
- Khi đang ở Home, `X` mới đóng toàn bộ điện thoại.

Hai button điều hướng đáy `HOME` và `APPS` đã được loại bỏ; toàn bộ thao tác quay lại/đóng điện thoại dùng button `X` trên header.

### Điện thoại và lịch sử cuộc gọi

- Tab `KEYPAD` chứa bàn phím và các thao tác `DEL`, `CALL`, `ABC/123`.
- Chế độ `123` nhập số điện thoại.
- Chế độ `ABC` nhập tên bằng cách nhấn lặp cùng một phím để đổi ký tự.
- Nhấn `CALL` sẽ tạo một mục trong lịch sử.
- Tab `RECENT` hiển thị tối đa 8 cuộc gọi gần nhất.
- Button xóa trong tab `RECENT` xóa cả danh sách trên giao diện và dữ liệu đã lưu.

Dữ liệu lịch sử được lưu bằng khóa:

`FranklinGame.PhoneSystem.CallHistory.v1`

### Tin nhắn nhiệm vụ

Màn hình Tin nhắn chỉ hiển thị danh sách tin. Khi chọn một tin, PhoneSystem chuyển sang nội dung nhiệm vụ với hai lựa chọn:

- `ACCEPT`: xác nhận nhiệm vụ và phát `EventMissionAccepted`.
- `CANCEL`: quay lại danh sách tin nhắn, không nhận nhiệm vụ.

Ba dữ liệu nhiệm vụ hiện tại là dữ liệu mẫu cho Lamar, Lester và Downtown Cab. Logic bắt đầu nhiệm vụ thật cần được kết nối thông qua sự kiện nhận nhiệm vụ.

## API runtime

Namespace:

```csharp
using FranklinGame.PhoneSystem;
```

Mở, đóng hoặc chuyển trạng thái điện thoại:

```csharp
FranklinPhoneSystem.Instance?.SetOpen(true);   // Mở
FranklinPhoneSystem.Instance?.SetOpen(false);  // Đóng
FranklinPhoneSystem.Instance?.TogglePhone();   // Chuyển trạng thái
```

Mở trực tiếp một ứng dụng:

```csharp
FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
if (phone != null)
{
    phone.SetOpen(true);
    phone.OpenApp(1); // Mở Tin nhắn
}
```

Kết nối sự kiện nhận nhiệm vụ:

```csharp
private void OnEnable()
{
    FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
    if (phone != null) phone.EventMissionAccepted += OnMissionAccepted;
}

private void OnDisable()
{
    FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
    if (phone != null) phone.EventMissionAccepted -= OnMissionAccepted;
}

private void OnMissionAccepted(int missionIndex)
{
    // 0: Lamar, 1: Lester, 2: Downtown Cab
    // Kích hoạt hệ thống nhiệm vụ thật tại đây.
}
```

Các thuộc tính hữu ích:

- `FranklinPhoneSystem.Instance`: instance PhoneSystem hiện tại.
- `IsOpen`: cho biết điện thoại đang mở hay đóng.
- `IsConfigured`: kiểm tra các reference UI bắt buộc đã được gán đầy đủ.

## Model điện thoại và animation xương

Model vật lý được lấy từ:

`Assets/PhoneSystem/Phone_A1_LP/Phone_A1_LP.prefab`

Component `FranklinPhoneHandPresentation` nằm cùng GameObject với `FranklinPhoneSystem` và tự tìm Player hiện tại bằng `ShortcutPlayer`. Vì vậy không cần thêm component hoặc sửa trực tiếp `Player.prefab`.

Luồng animation runtime:

1. Khi mở PhoneSystem, model được tạo tại socket của bàn tay phải đang ở bên hông.
2. Humanoid IK đưa cánh tay phải lên tư thế cầm trước ngực trong `0.72` giây; `RightElbow` hint giữ hướng gập khuỷu tay ổn định.
3. Khi điện thoại qua ngưỡng đưa lên, mesh `ScrOn` được bật màu và emission; GC2 bắt đầu hướng đầu Player vào model.
4. Hand socket giữ model bám theo tay phải; các Humanoid muscle khép bốn ngón quanh phone và thêm idle sway rất nhẹ.
5. Mỗi lần nhấn button trên UI, ngón cái phải chuyển từ pose nghỉ sang pose chạm rồi trở lại trong một nhịp `0.22` giây.
6. Khi đóng, màn hình và Look target tắt, tay phải trở về bên hông trong `0.58` giây, sau đó model được ẩn.

Các offset tay phải, elbow hint, góc xoay, muscle grip/ngón cái, idle sway, scale model, thời gian hòa trộn và màu màn hình đều được serialize trên `FranklinPhoneHandPresentation`. Hai field `Phone Position` và `Phone Rotation` là lớp tinh chỉnh cuối theo local space của socket tay phải, dùng để dịch/xoay riêng model phone mà không đổi pose IK của tay. Có thể tinh chỉnh các giá trị này trong Inspector nếu thay model Player, nhưng không cần chỉnh `RectTransform` của giao diện điện thoại.

## Camera selfie và Photos

Khi mở Camera app, `FranklinPhoneSelfieCamera` tạo một bản runtime của `Assets/Prefab/Camera Shot.prefab`, giữ nguyên `ShotTypeThirdPerson` cùng `InputValueVector2HalfRightMobileControllerY`. PhoneSystem chỉ chuyển active shot bằng `MainCamera.Transition.ChangeToShot`; không ghi trực tiếp position/rotation lên Main Camera. Vị trí khởi đầu của Third Person orbit được giải từ lens của model phone sau Humanoid IK, vì vậy shot bắt đầu tại điện thoại nhưng người dùng vẫn kéo nửa phải màn hình để orbit quanh Player.

Camera Shot dùng `Lock Horizontal Framing Across Devices` với mốc mặc định `1920×1080`, `60°`. Hệ thống ghi FOV thích nghi vào chính Viewport của runtime GC2 Camera Shot: mỗi điện thoại/tablet được đổi vertical FOV theo aspect thật nhưng giữ cùng horizontal FOV thiết kế. Vì vậy các giá trị `Shoulder`, `Lift`, `Radius` đã căn Player sát mép trái ở Game View 1920×1080 sẽ giữ cùng tọa độ ngang chuẩn hóa trên màn hình 16:9, 19.5:9, 4:3 hoặc khi đổi orientation. Khi Camera app đóng, FOV trước selfie được phục hồi nếu shot gameplay cũ không có FOV riêng.

Camera RenderTexture đọc transform và FOV cuối từ Main Camera sau khi GC2 đã áp Camera Shot, clipping và shake. `Match Camera Shot Horizontal Framing` tiếp tục giữ horizontal FOV đó khi chuyển sang khung dọc của phone ảo. Viewfinder dùng khung `425×714`, neo tại `Y = -66` để giữ nguyên mép trên dưới header nhưng kéo live feed xuống hết mép dưới màn hình, phía sau vùng gesture indicator; RenderTexture `725×1218` có cùng aspect `25:42` nên không làm méo ảnh. Vì vậy Player có cùng vị trí ngang trên màn hình thiết bị và live view (ví dụ sát mép trái ở màn hình thật thì cũng sát mép trái trong phone ảo). Quyền orbit vẫn nằm ở Camera Shot đúng theo pipeline của GC2.

Khi thoát Camera app hoặc đóng phone, PhoneSystem trả lại Camera Shot đã lưu nếu nó vẫn đang sở hữu shot selfie. Nếu hệ thống xe hoặc nhiệm vụ yêu cầu một shot mới trong lúc selfie, PhoneSystem ghi nhớ shot mới đó để phục hồi đúng khi thoát Camera app.

Hai field `Phone Camera Position` và `Phone Camera Rotation` trên `FranklinPhoneSelfieCamera` chỉ tinh chỉnh lens vật lý theo local space của phone. GC2 `ShotTypeThirdPerson` không sử dụng Transform của GameObject Shot làm input vị trí; Transform đó bị tính lại mỗi frame. Vì vậy nhóm `GC2 Camera Shot - Third Person` cung cấp trực tiếp ba giá trị thật `Shoulder`, `Lift` và `Radius`, được ghi vào `ShotSystemThirdPerson` của runtime shot. Có thể chỉnh ba giá trị này ngay trong Play Mode và Camera Shot cập nhật mà không cần đóng/mở lại app. `Player Aim Offset` chỉnh điểm ngắm; `Selfie Near Clip` tránh mesh điện thoại che camera. `Selfie Field Of View` chỉ có hiệu lực khi chủ động tắt `Match Camera Shot Horizontal Framing`.

Nội dung giao diện phone dùng scale `0.92` bên trong màn hình 408×778, tạo thêm một khoảng inset nhỏ quanh Camera, Photos và các app khác thay vì chạm sát khung máy.

Player dùng Facing layer của Game Creator để liên tục xoay toàn thân về vị trí của GC2 Camera Shot đang orbit. Trong selfie, `Selfie Turn Speed Multiplier = 3` nhân `Character.Motion.AngularSpeed` lên ba lần ngay trước Character update, giúp thân Player bắt kịp orbit nhanh hơn; hệ thống vẫn theo dõi base speed mới nếu locomotion state thay đổi và phục hồi đúng base speed khi thoát. `FranklinPhoneHandPresentation` đồng thời hòa sang `Selfie Pose` và dùng `RigLookTo` để đầu/mắt nhìn thẳng vào transform của chính Camera Shot, không dùng camera phone hoặc RenderTexture làm look target. Camera Shot orbit theo world-space quanh pivot Player nên việc Player xoay người không ghi đè input orbit. Khi thoát Camera app hoặc đóng điện thoại, Facing layer, turn-speed override, RigLookTo, camera render, GC2 selfie shot và pose selfie đều được giải phóng.

Nhấn Shutter sẽ đọc đúng frame hiện tại từ `RenderTexture`, mã hóa PNG và lưu tại:

`Application.persistentDataPath/FranklinPhonePhotos`

Hệ thống giữ tối đa 30 file gần nhất và hiển thị 9 ảnh mới nhất trong Photos app full viền. Các ô mockup màu đã bị loại bỏ; ô thumbnail chỉ xuất hiện khi có file ảnh thật. Chạm thumbnail để xem ảnh toàn màn hình, `BACK` để quay lại và `DELETE` hai lần để xác nhận xóa ảnh đang xem. Ảnh được nạp lại từ ổ lưu trữ sau khi đổi scene hoặc khởi động game lần sau; không dùng `PlayerPrefs` để chứa dữ liệu ảnh.

Khi phone mở, `FranklinMobileHud` ẩn riêng hai button `Jog` và `Sprint`, đồng thời `FranklinAnimationBridge` chặn cả sprint từ bàn phím/auto-run nhưng vẫn cho phép đi bộ thường. `FranklinPhoneHandPresentation` giữ limb mask `ArmRight` của Game Creator trong suốt thời gian trình bày phone; Humanoid IK sở hữu chuỗi tay phải để locomotion không đánh tay ra khỏi điện thoại.

## Tích hợp với HUD

Khi mở điện thoại, PhoneSystem thực hiện các hành vi sau:

1. Hiển thị màn hình Home và cập nhật giờ, ngày, safe area.
2. Gọi `FranklinMobileHud.AcquireControlsSuppression(this)` để tạm khóa các điều khiển mobile.
3. Ẩn `Weapon Card` và `Quick Item Rail`, bao gồm armor, lựu đạn và chai xăng.

Khi đóng hoặc hủy PhoneSystem, các trạng thái trên được giải phóng và Weapon HUD hiện lại. Việc ẩn/hiện chỉ thay đổi trạng thái runtime, không ghi lại hoặc sửa `RectTransform` của HUD.

## Cấu trúc thư mục

```text
Assets/PhoneSystem/
├── Editor/
│   └── FranklinPhoneSystemInstaller.cs   # Dựng và lưu prefab UI
├── Audio/                                # Sound effect AOSP OGG và attribution Apache 2.0
├── Preview/                              # Ảnh tham khảo các màn hình
├── Resources/
│   ├── FranklinPhoneSystem.prefab        # Prefab được nạp lúc runtime
│   └── Icons/                            # Icon của sáu ứng dụng
├── Runtime/
│   ├── FranklinPhoneSystem.cs             # Điều hướng, nhập liệu và tích hợp HUD
│   ├── FranklinPhoneHandPresentation.cs   # Cầm/rút máy, pose selfie, ngón tay và GC2 Look
│   └── FranklinPhoneSelfieCamera.cs        # RenderTexture, Facing, capture PNG và Photos
└── Phone_A1_LP/                           # Model vật lý, mesh, material và animation màn hình
```

## Chỉnh sửa và mở rộng

- Chỉnh giao diện trực tiếp trong prefab nếu chỉ cần thay đổi vị trí, kích thước, màu hoặc nội dung hình ảnh.
- Giữ nguyên các reference đã serialize trên component `FranklinPhoneSystem` khi đổi hierarchy.
- Nếu thay đổi số lượng hoặc thứ tự ứng dụng, cập nhật đồng thời `m_AppScreens`, `m_AppButtons`, `APP_TITLES` và logic `OpenApp`.
- Khi bổ sung dữ liệu thật cho Bản đồ, Camera, Ảnh hoặc Ghi chú, nên giữ lớp runtime hiện tại làm bộ điều hướng và tách logic từng ứng dụng thành component riêng.
- Lịch sử cuộc gọi hiện lưu local bằng `PlayerPrefs`; nếu game có hệ thống save riêng, có thể thay phần `LoadCallHistory`, `AddCallHistory` và `ClearCallHistory` bằng save service của dự án.

## Phụ thuộc

- Unity UI (`Canvas`, `CanvasScaler`, `GraphicRaycaster`, `Button`, `Text`).
- `FranklinMobileHud` để nhận thao tác từ button Phone và khóa điều khiển mobile.
- `FranklinPlayerStatusHud` để ẩn/hiện nhóm Weapon HUD.
- Game Creator 2 `Character`, Humanoid Animator và `RigLookTo` để điều khiển xương và hướng nhìn.
- Font Game Creator Blockout chỉ được installer sử dụng khi dựng lại prefab; prefab hiện tại đã chứa các reference cần thiết.

## Âm thanh và giấy phép

Các hiệu ứng trong `Assets/PhoneSystem/Audio` là âm thanh UI điện thoại thật từ **Android Open Source Project**: `Unlock`, `Lock`, `Effect_Tick`, `KeypressStandard`, `KeypressDelete`, `KeypressReturn`, `KeypressInvalid` và `InCallNotification`. Chúng thay thế bộ âm thanh arcade trước đây và được giữ dưới giấy phép Apache 2.0.

- Nguồn: [AOSP platform/frameworks/base – UI sound effects](https://android.googlesource.com/platform/frameworks/base/+/fd228a3/data/sounds/effects/ogg/)
- Giấy phép: [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
- Bản attribution đi kèm: `Assets/PhoneSystem/Audio/LICENSE.txt`

PhoneSystem dùng `AudioSource` 2D, không phát tự động khi khởi tạo và vẫn phát khi `AudioListener.pause` đang bật. Âm lượng mặc định là `0.7`, có thể chỉnh bằng `Sound Volume` trên component `FranklinPhoneSystem` mà không cần thay đổi từng file âm thanh.

## Xử lý lỗi thường gặp

### Nhấn button Phone nhưng giao diện không mở

- Kiểm tra `FranklinPhoneSystem.prefab` còn nằm đúng trong `Assets/PhoneSystem/Resources`.
- Kiểm tra scene có `FranklinMobileHud` hoặc `CanvasPlayerControl` đã được liên kết đúng.
- Kiểm tra button cạnh minimap đang phát action `Phone`.

### Giao diện mở nhưng không nhận thao tác

- Kiểm tra scene có `EventSystem`.
- Kiểm tra Canvas PhoneSystem có `GraphicRaycaster`.
- Kiểm tra các reference button trên component `FranklinPhoneSystem` không bị mất.

### Lịch sử cuộc gọi không còn dữ liệu

- Kiểm tra game có gọi `PlayerPrefs.DeleteAll()` hay không.
- Kiểm tra khóa `FranklinGame.PhoneSystem.CallHistory.v1` không bị xóa bởi hệ thống save/reset khác.

### Weapon HUD không hiện lại

- Đóng điện thoại bằng `SetOpen(false)` thay vì chỉ tắt riêng GameObject giao diện.
- Kiểm tra scene có một instance `FranklinPlayerStatusHud` hợp lệ.

### Player không đưa tay hoặc không nhìn vào điện thoại

- Kiểm tra Player đang dùng Animator Humanoid và đã được nhận diện bởi `ShortcutPlayer`.
- Kiểm tra `Phone_A1_LP.prefab` vẫn được gán vào `FranklinPhoneHandPresentation` trên prefab PhoneSystem.
- Kiểm tra Animator Controller của Player vẫn bật `IK Pass` và Avatar có mapping `Right Hand`/`Right Thumb` hợp lệ.
