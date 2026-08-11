# Arcade Bike Physics Pro — Franklin Game Integration

Tài liệu này mô tả source Arcade Bike Physics Pro (ABP) đang có trong project,
cấu trúc prefab chuẩn và lớp tích hợp dành riêng cho Franklin Game.

> Source of truth của cấu hình hiện tại là `Bike_01_Sport.prefab`. Các bike khác
> phải giữ cùng cấu trúc và mapping component; chỉ mesh/model được phép khác.

## 1. Tổng quan

ABP là bộ điều khiển xe máy dùng `Rigidbody`, raycast bánh xe và lực tùy chỉnh.
Package không dùng `WheelCollider` để tạo chuyển động. Controller chính xử lý:

- dò ground tại bánh trước/sau;
- gravity, suspension, ma sát ngang và rolling resistance;
- tăng tốc, phanh, lùi, drift, burnout và wheelie;
- xoay tay lái, nghiêng model và quay bánh xe;
- số xe, âm thanh động cơ, skid sound;
- skidmark, khói lốp và các UnityEvent khi cất cánh/hạ đất/đổi số.

Trong Franklin Game, ABP vẫn là physics engine duy nhất của bike. Các hệ thống
entry, mobile input, camera, đèn và ragdoll được nối qua lớp adapter Franklin.

## 2. Cấu trúc thư mục

```text
Arcade Bike Physics Pro/
├── Scripts/
│   ├── ArcadeBikeControllerPro.cs   # Physics/controller chính
│   ├── BikerAnimationTargets.cs     # Bộ target pose của rider ABP gốc
│   ├── CameraController.cs          # Camera Cinemachine riêng của ABP gốc
│   ├── RagdollActivator.cs          # Ragdoll kiểu dummy prefab của ABP gốc
│   ├── SkidmarkController.cs        # Sinh mesh vệt bánh xe runtime
│   └── UiButton_ABP_Pro.cs          # Pointer button đơn giản cho mobile
├── Prefabs/
│   ├── Skidmark Controller.prefab
│   └── TireSmoke.prefab
├── Materials/
│   ├── Skidmark Material.mat
│   ├── Skidmark Shader.shader
│   ├── ParticleSmokeMaterial.mat
│   └── zero Friction.physicMaterial
├── Textures/
│   ├── ParticleSmokeTexture.png
│   └── SkidmarksTexture.psd
└── Audios/
    ├── motocross-engine.wav
    ├── skid loop 1.wav
    └── Car Gear switch 3.wav
```

Các prefab bike đã tích hợp nằm ngoài package:

```text
Assets/Model/DQP_MotorBikePack_URP14/Generated/Prefabs/Bikes/
```

## 3. Kiến trúc trong Franklin Game

```mermaid
flowchart LR
    subgraph Input["Input và nhân vật"]
        HUD["FranklinMobileHud"]
        ENTRY["BikeEntry"]
        KEYBOARD["Keyboard - tùy chọn"]
    end

    subgraph Adapter["Lớp tích hợp Franklin"]
        DRIVER["FranklinArcadeBikeDriver"]
        CAMERA["FranklinBikeCameraManager"]
        RAGDOLL["FranklinArcadeBikeRagdoll"]
        LIGHTS["VehicleLights"]
        FLARE["FranklinBikeBrakeReverseFlare"]
        IMPACT["FranklinBikeImpactAudio"]
        HEALTH["FranklinBikeHealth"]
        DAMAGE_FX["FranklinBikeDamageEffects"]
        DEFORM["FranklinBikeDeformation"]
        DESTROY["FranklinBikeDestruction"]
    end

    subgraph ABP["Arcade Bike Physics Pro"]
        CONTROLLER["ArcadeBikeControllerPro"]
        INPUT["BikeInput"]
        PHYSICS["Gravity + Suspension + Friction + Drive"]
        VISUAL["Steering + Lean + Wheelie + Wheels"]
        FX["Audio + Skidmark + Tire Smoke"]
    end

    HUD --> DRIVER
    KEYBOARD --> DRIVER
    ENTRY -->|"enable / exit / crash release"| DRIVER
    ENTRY --> CAMERA
    DRIVER -->|"provideInput mỗi frame"| INPUT
    INPUT --> CONTROLLER
    CONTROLLER --> PHYSICS
    CONTROLLER --> VISUAL
    CONTROLLER --> FX
    DRIVER --> LIGHTS
    CONTROLLER --> FLARE
    CONTROLLER --> IMPACT
    IMPACT -->|"accepted impact"| HEALTH
    IMPACT -->|"accepted contact"| DEFORM
    HEALTH --> TRAITS["GC2 health-attribute-id"]
    HEALTH -->|"0 HP lock"| DRIVER
    HEALTH --> DAMAGE_FX
    DAMAGE_FX -->|"32% smoke / 14% fire / 0 HP"| DESTROY
    DRIVER <--> RAGDOLL
    CAMERA --> MAIN["GC2 Main Camera Shot"]
    PHYSICS --> RB["Bike Rigidbody"]
    RB --> GROUND["Drivable Ground"]
```

### Quyết định tích hợp quan trọng

- Mobile UI dùng `FranklinMobileHud`, không dùng HUD riêng của package.
- Tất cả bike dùng một `FranklinBikeCameraManager` dưới Player và GC2 Main
  Camera Shot. Không tạo camera riêng cho từng bike.
- `bikeReferences.cameraController` để `null` trên bike đã tích hợp.
- Ragdoll dùng `FranklinArcadeBikeRagdoll` trực tiếp trên Rigidbody thật của
  bike. Không dùng dummy-bike flow của `RagdollActivator` gốc.
- `bikeReferences.ragdollActivator` để `null` trên bike đã tích hợp.
- Player và bike bỏ va chạm với nhau trong toàn bộ quá trình enter/ngồi/exit.
- Bike nằm ngã chỉ được park sau khi adapter xác nhận bề mặt trái/phải đã ổn
  định gần song song với ground.

## 4. Cấu trúc prefab bike chuẩn

Cây dưới đây là cấu trúc chức năng của `Bike_01_Sport.prefab`. Tên mesh con sẽ
khác giữa các model, nhưng các node ABP/Franklin phải giữ cùng vai trò.

```text
Bike Root
├── CenterOfGravity
├── AudioSource-Engine
├── AudioSource-Drifting
├── AudioSource-Collision
├── Triggers-Control
├── Triggers_Enter/Exit
├── ABP Gear Shift
└── ABP Rotator
    └── ABP Wheelie
        └── ABP Lean
            └── ABP Bike Model
                ├── ABP Rear Wheel Parent
                │   └── RearWheelTarget
                │       ├── Rear Rim Mesh
                │       └── Rear Tire Mesh
                ├── ABP Steering Parent
                │   └── ABP Steering
                │       ├── ABP Front Wheel Parent
                │       │   └── FrontWheelTarget
                │       │       ├── Front Rim Mesh
                │       │       └── Front Tire Mesh
                │       └── ABP Steering Meshes
                │           └── Handlebar
                │               ├── LeftHand
                │               └── RightHand
                ├── ABP Collider
                ├── Franklin Rear Brake Flare
                └── BikeBody
                    ├── Body render meshes
                    │   └── Franklin Body Mesh Collider
                    ├── Parent                         # Seat target
                    ├── LeftFoot                       # Left footpeg
                    ├── RightFoot                      # Right footpeg
                    ├── GroundLeftFoot                 # Chân trái chống đất
                    ├── Bike Entry Standing Point
                    ├── Bike Entry Standing Point Mirrored
                    ├── Fallen Bike Body Grip Left
                    ├── Fallen Bike Body Grip Right
                    ├── Franklin Ragdoll Front Wheel Collider
                    ├── Franklin Ragdoll Rear Wheel Collider
                    ├── Bike Damage Effects
                    │   ├── Weak Health Smoke
                    │   ├── Critical Warning Fire
                    │   ├── Destroyed Fire
                    │   ├── Explosion Burst
                    │   ├── Occupant Burn Fire
                    │   └── Loop Audio
                    └── Spot Light(s)
```

### Mapping `BikeReferences`

| Field | Transform/component chuẩn |
|---|---|
| `Rotator` | `ABP Rotator` |
| `WheelieTransform` | `ABP Wheelie` |
| `LeanTransform` | `ABP Lean` |
| `FrontWheelParent` | `ABP Front Wheel Parent` |
| `RearWheelParent` | `ABP Rear Wheel Parent` |
| `FrontWheel` | `FrontWheelTarget` chứa cả mâm và lốp |
| `RearWheel` | `RearWheelTarget` chứa cả mâm và lốp |
| `BikeSteering` | `ABP Steering` |
| `BikeSteeringParent` | `ABP Steering Parent` |
| `SteeringMeshes` | `ABP Steering Meshes` |
| `BikeModel` | `ABP Bike Model` |
| `BodyMesh` | `BikeBody` |
| `BikeRb` | `Rigidbody` trên Bike Root |
| `collider` | `CapsuleCollider` dùng bởi controller ABP |
| `skidmarksPrefab` | `Prefabs/Skidmark Controller.prefab` |
| `tireSmokePrefab` | `Prefabs/TireSmoke.prefab` |
| `BikerAnimationTargets` | `null` trong Franklin integration |
| `cameraController` | `null` trong Franklin integration |
| `ragdollActivator` | `null` trong Franklin integration |

> `FrontWheelTarget` và `RearWheelTarget` phải chứa tối thiểu hai phần render:
> mâm và lốp. Không trỏ controller vào riêng một mesh bánh xe.

## 5. Runtime flow

```mermaid
sequenceDiagram
    participant UI as Mobile HUD / Keyboard
    participant Driver as FranklinArcadeBikeDriver
    participant ABP as ArcadeBikeControllerPro
    participant RB as Rigidbody
    participant Ground as Drivable Ground
    participant FX as Audio / Skid / Smoke

    UI->>Driver: SetVirtual...Input(bool)
    loop Mỗi Update
        Driver->>ABP: provideInput(0..1)
        ABP->>Ground: Raycast bánh trước và sau
        Ground-->>ABP: hit, normal, compression
        ABP->>ABP: steering, lean, wheelie, wheel visuals
        ABP->>FX: pitch, slip, skidmark, smoke
    end
    loop Mỗi FixedUpdate
        ABP->>RB: custom gravity
        ABP->>RB: suspension force
        ABP->>RB: lateral friction
        ABP->>RB: drive / brake / reverse
        ABP->>RB: turning / burnout
    end
```

### Thứ tự xử lý của controller

`Update()`:

1. Raycast và đặt visual bánh trước/sau lên ground.
2. Tính ground normal, trạng thái grounded và phát event takeoff/landing.
3. Căn `Rotator` theo mặt đất hoặc world-up khi ở trên không.
4. Đọc `BikeInput`, cập nhật steer, lean, wheelie và wheel animation.
5. Cập nhật engine audio, slip, skidmark, smoke và skid sound.

`FixedUpdate()`:

1. Chuyển `Rigidbody.linearVelocity` sang local bike velocity.
2. Áp custom gravity.
3. Tính và áp lực suspension.
4. Áp ma sát ngang.
5. Xử lý tăng tốc, lùi, phanh và rolling resistance.
6. Xử lý burnout/rotation và gear shift.

## 6. API gốc của Arcade Bike Physics Pro

Namespace của package:

```csharp
using ArcadeBP_Pro;
```

### `ArcadeBikeControllerPro`

File: [Scripts/ArcadeBikeControllerPro.cs](Scripts/ArcadeBikeControllerPro.cs)

#### API điều khiển

| API | Mô tả |
|---|---|
| `provideInput(accelerate, reverse, handBrake, steerLeft, steerRight, wheelie)` | Ghi sáu kênh input. Giá trị sử dụng thực tế là `0..1`. Phải cập nhật mỗi frame hoặc trả về `0` khi thả nút. |
| `StartBike()` | Cho phép accelerate/turn và bật tiếng động cơ. |
| `StopBike()` | Khóa accelerate/turn và mute tiếng động cơ. |
| `reduceSurfaceNormalDownVelocity(factor)` | Loại bớt thành phần velocity hướng vào normal của mặt đường. |
| `RotateVector(vector, axis, degree)` | Utility xoay vector quanh một trục. |

#### Trạng thái đọc runtime

| Field/property | Ý nghĩa |
|---|---|
| `localBikeVelocity` | Velocity của Rigidbody trong không gian `Rotator`, đơn vị m/s. Setter private. |
| `CurrentSteerInput` | `-1`, `0` hoặc `1`. Setter private. |
| `bikeIsGrounded` | Ít nhất một bánh đang chạm drivable ground. |
| `frontWheelIsGrounded` | Bánh trước đang grounded. |
| `rearWheelIsGrounded` | Bánh sau đang grounded. |
| `isDoingWheelie` | Controller đang xử lý wheelie. |
| `canAccelerate` | Cho phép lực tăng tốc. |
| `canTurn` | Cho phép turning. |
| `currentGear` | Số hiện tại. |
| `CurrntGearProperty` | Property nội bộ của gear change; tên typo được giữ theo package. |
| `skidmarkController` | Instance skidmark được tạo trong `Awake()`. Setter private. |

#### Nhóm cấu hình Inspector

| Nhóm | Nội dung |
|---|---|
| `BikeInput` | Accelerate, reverse, handbrake, steer trái/phải, wheelie. |
| `BikeReferences` | Toàn bộ transform, Rigidbody, collider, FX và component phụ. |
| `BikeGeometry` | Radius, width và góc đặt bánh trước/sau. |
| `BikeSuspension` | Spring, damper, ground-stick và max compression. |
| `BikeSettings` | Layer, tốc độ, acceleration, brake, steering, lean, friction, gravity, burnout, wheelie và alignment. |
| `BikeCurves` | Acceleration, reverse acceleration, steering, friction và lean theo normalized speed. |
| `BikeAudio` | Engine, gear-shift, skid sound và khoảng pitch. |
| `BikeEvents` | `OnTakeOff`, `OnGrounded`, `OnGearChange`. |

#### Events

```csharp
controller.bikeEvents.OnTakeOff.AddListener(OnBikeTakeOff);
controller.bikeEvents.OnGrounded.AddListener(OnBikeLanded);
controller.bikeEvents.OnGearChange.AddListener(OnBikeGearChanged);
```

### `SkidmarkController`

File: [Scripts/SkidmarkController.cs](Scripts/SkidmarkController.cs)

| API | Mô tả |
|---|---|
| `AddSkidMark(position, normal, opacity, lastIndex)` | Thêm một segment bằng opacity `0..1`, trả về index dùng cho segment kế tiếp. |
| `AddSkidMark(position, normal, color, lastIndex)` | Biến thể dùng `Color32`. |
| `SkidmarkWidth` | Độ rộng vệt; controller gán theo width bánh sau. |

Truyền `lastIndex = -1` để bắt đầu một vệt mới. Mesh dùng ring buffer tối đa
2.048 mark và chỉ upload khi dữ liệu thay đổi.

### `UiButton_ABP_Pro`

File: [Scripts/UiButton_ABP_Pro.cs](Scripts/UiButton_ABP_Pro.cs)

Component triển khai `IPointerDownHandler` và `IPointerUpHandler`:

| API | Mô tả |
|---|---|
| `isPressed` | `true` từ PointerDown tới PointerUp. |
| `onButtonDown` | UnityEvent khi bắt đầu nhấn. |
| `onButtonUp` | UnityEvent khi thả. |

Franklin Game dùng HUD chung nên component này chỉ cần cho UI ABP độc lập.

### `CameraController` — legacy trong Franklin

File: [Scripts/CameraController.cs](Scripts/CameraController.cs)

| API | Mô tả |
|---|---|
| `SetCameratarget(followTarget, lookAtTarget)` | Đổi Follow/LookAt của toàn bộ camera ABP. |
| `resetCameratarget()` | Khôi phục target ban đầu. |

Component còn hỗ trợ đổi nhiều Cinemachine camera, speed shake và FOV theo tốc
độ. Không gắn component này vào bike Franklin vì `Awake()` của nó tự tách camera
root khỏi parent và tạo camera riêng cho từng bike.

### `RagdollActivator` — legacy trong Franklin

File: [Scripts/RagdollActivator.cs](Scripts/RagdollActivator.cs)

| API | Mô tả |
|---|---|
| `ForceActivateRagdoll()` | Ép kích hoạt ragdoll gốc. |
| `ReEnableBike()` | Hủy dummy ragdoll và bật lại bike gốc. |
| `ResetBike()` | Gọi `ReEnableBike()` nếu đang ragdoll. |
| `setCameraTargetToRagdoll()` | Cho camera ABP bám vào hips ragdoll. |
| `resetCameratoBike()` | Khôi phục camera target. |
| `onRagdollActivated` | Event sau khi spawn dummy bike và character ragdoll. |
| `onBikeReEnabled` | Event sau khi bật lại bike gốc. |

Flow gốc sẽ instantiate dummy bike + character ragdoll rồi disable bike thật.
Franklin không dùng flow này; xem `FranklinArcadeBikeRagdoll` bên dưới.

### `BikerAnimationTargets`

File: [Scripts/BikerAnimationTargets.cs](Scripts/BikerAnimationTargets.cs)

Data component chứa target hip, spine, leg và hand cho các trạng thái idle,
normal speed, high speed, in-air và reverse. Franklin dùng `BikeEntry`, Humanoid
pose clip và hand/foot IK riêng, vì vậy reference này để `null` trên bike hiện tại.

## 7. API tích hợp Franklin

Namespace chính:

```csharp
using FranklinGame.Vehicles;
using FranklinGame.Animations;
```

### `FranklinArcadeBikeDriver`

File: [FranklinArcadeBikeDriver.cs](../../FranklinAnimations/Runtime/FranklinArcadeBikeDriver.cs)

Đây là API nên dùng cho gameplay và mobile input.

| API | Mô tả |
|---|---|
| `SetVehicleEnabled(state)` | Bật/tắt quyền điều khiển bike. |
| `SetVehicleEnabled(state, preserveMomentum)` | Bật/tắt và tùy chọn giữ momentum. |
| `SetVirtualAccelerateInput(active)` | Ga đầy. |
| `SetVirtualSlowAccelerateInput(active)` | Ga chậm theo `m_SlowThrottle`. |
| `SetVirtualBrakeReverseInput(active)` | Phanh khi đang tiến, lùi khi đã dừng/đổi hướng. |
| `SetVirtualSteerLeftInput(active)` | Lái trái. |
| `SetVirtualSteerRightInput(active)` | Lái phải. |
| `SetVirtualHandbrakeInput(active)` | Phanh tay/drift. |
| `SetVirtualWheelieInput(active)` | Giữ input bốc bánh trước. |
| `SetVirtualBurnoutInput(active)` | Gửi đồng thời accelerate + reverse để kích hoạt burnout ABP. |
| `SetDamageLocked(locked)` | Khóa toàn bộ input lái khi Bike health bằng 0 nhưng vẫn giữ exit flow an toàn. |
| `SetHandbrakeInput(active)` | Nguồn phanh tay bên ngoài HUD. |
| `SetHeadlightEnabled(active)` | Bật/tắt đèn trước qua `VehicleLights`. |
| `BeginExitStop()` | Khóa input và giảm tốc bike trước khi exit. |
| `CancelExitStop()` | Hủy trạng thái chờ dừng để exit. |
| `RequestExit()` | Yêu cầu `BikeEntry` chạy exit cho rider hiện tại. |
| `CrashDismount(fallSign, toppleAngularVelocity)` | Thả rider và chuyển bike sang physics ragdoll động. |
| `KeepCrashRagdollDynamic()` | Đảm bảo Rigidbody ragdoll không kinematic/freeze rotation. |
| `ParkGroundedRagdoll()` | Park sau khi adapter xác nhận bike nằm ổn định trên ground. |
| `ResetVehicle()` | Xóa velocity và dựng lại các transform điều khiển chính. |

Các property đọc quan trọng: `IsVehicleEnabled`, `IsDamageLocked`, `IsAirborne`,
`IsCrashCoasting`, `VehicleBody` và `SpeedMetersPerSecond`.

### `FranklinBikeHealth`

File: [FranklinBikeHealth.cs](../../FranklinAnimations/Runtime/FranklinBikeHealth.cs)

| API | Mô tả |
|---|---|
| `ApplyDamage(amount)` | Trừ GC2 `health-attribute-id`. |
| `Repair(amount)` | Hồi một lượng HP và mở khóa damage khi HP lớn hơn 0. |
| `RepairFull()` | Hồi đầy HP Bike. |
| `SetNormalizedHealth(ratio)` | Đặt HP theo tỷ lệ `0..1`. |
| `EventHealthChanged` | Event cập nhật health không cần polling. |
| `EventDestroyed` / `EventRestored` | Event một lần khi đi qua biên 0 HP. |

Damage chỉ nhận từ `FranklinBikeImpactAudio.EventImpactAccepted`, vì vậy dùng
chung phân loại light/heavy, cooldown và pooled impact FX; không chạy thêm một
`OnCollisionEnter` thứ hai. Impact nặng đồng thời trừ `4–18 HP` của GC2 Player
đang ngồi trên Bike. Ở 0 HP, Bike bị khóa ga/lái và không hiện khả năng
enter mới. Thanh máu Bike dùng chung `CanvasPlayerControl`, chỉ hiện khi Player đang lái Bike
và cập nhật trực tiếp từ `EventHealthChanged` (không poll mỗi frame).
Tốc độ Bike cũng dùng UI mobile chung và cùng style với Car: số lớn kèm `km/h`, cập nhật
mỗi `0.1s` và bám theo vị trí world-space bên trái Bike bằng chuyển động mượt.
Trong `FranklinMobileHud > Bike Speed UI`, có thể đổi font, cỡ số, cỡ chữ `km/h`, font
style, màu chữ/bóng, kích thước vùng chữ và vị trí. `Follow Bike` dùng world offset như
Car; tắt tùy chọn này để dùng `Fixed Screen Position`. Các giá trị đổi ngay trong Play Mode.

### Damage VFX, destruction và deformation

`FranklinBikeDamageEffects` dùng cùng ngưỡng với Car: smoke ở `<=32%`, warning
fire ở `<=14%`, và khi HP bằng 0 chờ `1.35s` rồi mới phát `Explosion11` đúng một
lần. Smoke/fire/audio chỉ đổi trạng thái khi nhận `EventHealthChanged`; gió hạt
chỉ cập nhật tối đa `8 Hz` khi VFX đang hiện. Các ParticleSystem đã được author
sẵn dưới `BikeBody/Bike Damage Effects`, không instantiate tại thời điểm nổ.

`FranklinBikeDestruction` ghi nhớ rider ngay lúc HP về 0, kể cả khi crash-ragdoll
đã văng rider khỏi yên trước khi vụ nổ chạy. Explosion đặt Rigidbody Bike về
dynamic, bỏ freeze rotation/kinematic, giữ bánh gắn với xe, áp lực ngã, làm sẫm
renderer bằng `MaterialPropertyBlock`, đưa Player Traits `hp` về 0 và gắn fire
4 giây. Sau explosion, wreck là terminal và API Repair chỉ còn tác dụng nếu gọi
trong cửa sổ cảnh báo trước nổ. Luồng này không đổi camera: Bike vẫn dùng GC2
Main Camera Shot.

`FranklinBikeDeformation` nhận chính `EventImpactContactAccepted` đã qua cooldown
và dùng kernel Edy giống Car, nhưng profile nhỏ hơn cho thân Bike: radius `0.38m`,
displacement tối đa `0.12m`, fracture `0.018m`, tối đa 10 dents. Chỉ render mesh
thân được clone/deform ở runtime; mâm, lốp, glass và MeshCollider vật lý giữ nguyên.
API sửa hình là `ResetDeformation()`.

### `BikeEntry`

File: [BikeEntry.cs](../Vehicle%20Integration/Scripts/GeneralVehicles/BikeEntry.cs)

| API | Mô tả |
|---|---|
| `RequestEnter(character)` | Bắt đầu approach/enter hoặc recovery nếu bike đang nằm. |
| `RequestExit(character)` | Giảm tốc nếu cần rồi chạy exit bên trái. |
| `ReleaseForCrash(character)` | Tách rider khỏi seat để chuyển sang crash ragdoll. |
| `ApplyRiderHandIK()` | Áp hand IK tại thời điểm yêu cầu. |
| `SetLiveRiderPosePreview(enabled)` | Bật/tắt preview pose memory-only trong Editor/Play. |
| `SeatedCharacter` | Character đang ngồi trên bike. |
| `IsTransitioning` | Đang enter hoặc exit. |
| `IsUsingMirroredEntry` | Enter hiện tại đang dùng phía mirror. |

Target rider chính gồm `entryParent`, hai standing point, hai hand grip,
`LeftFoot`, `RightFoot`, `GroundLeftFoot` và hai fallen-bike body grip.

### `FranklinArcadeBikeRagdoll`

File: [FranklinArcadeBikeRagdoll.cs](../../FranklinAnimations/Runtime/FranklinArcadeBikeRagdoll.cs)

| API | Mô tả |
|---|---|
| `ActivateRagdoll(fallSign, angularVelocity)` | Cho bike thật ngã bằng Rigidbody động. |
| `SettleRagdoll()` | Bắt đầu/tiếp tục đánh giá điều kiện nằm ổn định. |
| `TryPrepareManualRecoveryForInteraction()` | Chuẩn bị bike để player dựng xe. |
| `TryGetManualRecoveryTarget(...)` | Tính vị trí/rotation recovery theo bên bike đang nằm. |
| `AlignManualRecoveryTargetToGround(...)` | Ép target recovery bám ground an toàn. |
| `SetManualRecoveryPose(position, rotation)` | Phối hợp dịch bike gần player trong sequence dựng xe. |
| `CompleteManualRecovery()` | Hoàn tất dựng bike và chuyển về trạng thái upright. |
| `DeactivateRagdollForDriving()` | Khôi phục physics lái xe trước khi rider điều khiển. |

Trạng thái đọc: `IsRagdoll`, `RequiresManualRecovery`, `ParkedGroundSide` và
`IsConfigured`.

### Camera và đèn

- [`FranklinBikeCameraManager`](../../FranklinAnimations/Runtime/FranklinBikeCameraManager.cs):
  `Activate(BikeEntry)` và `Deactivate()` quản lý một runtime GC2 Third Person
  Shot dùng chung dưới Player.
- [`VehicleLights`](../Vehicle%20Integration/Scripts/GeneralVehicles/VehicleLights.cs):
  `FrontLightsOn()`, `FrontLightsOff()`, `LightsOn()` và `LightsOff()`.
- [`FranklinBikeBrakeReverseFlare`](../../FranklinAnimations/Runtime/FranklinBikeBrakeReverseFlare.cs):
  đọc `BikeInput` và velocity để blend đèn đỏ khi phanh hoặc đi lùi.

## 8. Ví dụ sử dụng

### Mobile button qua Franklin driver

```csharp
using FranklinGame.Vehicles;
using UnityEngine;

public sealed class BikeButtonsExample : MonoBehaviour
{
    [SerializeField] private FranklinArcadeBikeDriver bike;

    public void AccelerateDown() => bike.SetVirtualAccelerateInput(true);
    public void AccelerateUp() => bike.SetVirtualAccelerateInput(false);

    public void BrakeDown() => bike.SetVirtualBrakeReverseInput(true);
    public void BrakeUp() => bike.SetVirtualBrakeReverseInput(false);

    public void SteerLeftDown() => bike.SetVirtualSteerLeftInput(true);
    public void SteerLeftUp() => bike.SetVirtualSteerLeftInput(false);

    public void ToggleHeadlight(bool enabled) => bike.SetHeadlightEnabled(enabled);
}
```

### Điều khiển ABP trực tiếp

Chỉ dùng khi xây scene ABP độc lập, không đi qua Franklin vehicle flow:

```csharp
using ArcadeBP_Pro;
using UnityEngine;

public sealed class DirectAbpInputExample : MonoBehaviour
{
    [SerializeField] private ArcadeBikeControllerPro controller;

    private void Update()
    {
        float accelerate = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float reverse = Input.GetKey(KeyCode.S) ? 1f : 0f;
        float handbrake = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        float left = Input.GetKey(KeyCode.A) ? 1f : 0f;
        float right = Input.GetKey(KeyCode.D) ? 1f : 0f;
        float wheelie = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;

        controller.provideInput(
            accelerate,
            reverse,
            handbrake,
            left,
            right,
            wheelie
        );
    }
}
```

## 9. Checklist khi thêm hoặc thay mesh bike

1. Lấy `Bike_01_Sport.prefab` làm template chức năng.
2. Giữ nguyên `ABP Rotator > ABP Wheelie > ABP Lean > ABP Bike Model`.
3. Gán đúng front/rear wheel parent và wheel target.
4. Mỗi wheel target phải chứa cả mesh mâm và mesh lốp.
5. Cập nhật radius/width theo bounds của toàn bộ bánh, không theo riêng mâm.
6. Giữ `BikeBody` làm parent của body render, seat/grip/foot/recovery target.
7. Gán Rigidbody root, ABP CapsuleCollider, skidmark và tire-smoke prefab.
8. Kiểm tra `drivableLayerMask` không chứa layer Player/Ignore Raycast cần loại.
9. Giữ camera/ragdoll reference gốc của ABP là `null` trong Franklin integration.
10. Gắn `FranklinArcadeBikeDriver`, `BikeEntry`, `FranklinArcadeBikeRagdoll`,
    `FranklinBikeHealth`, `FranklinBikeDamageEffects`, `FranklinBikeDestruction`,
    `FranklinBikeDeformation`, `VehicleLights`, impact audio và brake/reverse
    flare như Bike 01.
11. Copy rider targets và pose từ Bike 01 nếu bike mới thuộc cùng rider-fit profile.
12. Không thêm `WheelCollider`; ABP dò ground bằng raycast.

## 10. Lưu ý kỹ thuật

- Source hiện dùng API Unity 6 như `Rigidbody.linearVelocity` và namespace
  `Unity.Cinemachine`.
- `ArcadeBikeControllerPro.Start()` tắt built-in gravity của Rigidbody;
  `bikeSettings.gravity` là gravity chính khi controller hoạt động.
- `gearSpeeds` không được rỗng vì engine pitch truy cập phần tử theo gear.
- `bikeInput`, các nhóm setting, audio source và references phải khác `null`.
- `drivableLayerMask` quyết định cả wheel placement, grounded và suspension.
- `bikeIsGrounded` là OR của hai bánh; logic cần cả hai bánh phải kiểm tra riêng
  `frontWheelIsGrounded` và `rearWheelIsGrounded`.
- Burnout bắt đầu quay khi cả hai bánh grounded và bike đã chậm dưới `1 m/s`.
  Tâm quay là contact point bánh trước, tốc độ mặc định `36°/s`; lái trái/phải
  chọn chiều quay, không nhập lái thì mặc định quay theo chiều kim đồng hồ.
  `burnoutLeanAngle` mặc định `8°` làm model nghiêng nhẹ theo hướng xoay.
- Khi tốc độ dưới `3 km/h`, `BikeEntry` blend chân trái tới `GroundLeftFoot`;
  từ ngưỡng này trở lên chân trở lại footpeg.
- Tên API `provideInput`, `resetCameratarget` và `CurrntGearProperty` giữ nguyên
  casing/typo của package để tránh phá serialized code hoặc integration hiện có.
- Stock `CameraController` tự detach khỏi parent trong `Awake()`. Không bật nó
  đồng thời với `FranklinBikeCameraManager`.
- Stock `RagdollActivator` disable bike thật và spawn dummy. Không bật nó đồng
  thời với `FranklinArcadeBikeRagdoll`.
- Thay đổi hierarchy/reference nên được thực hiện trên prefab asset; tránh chỉnh
  riêng scene instance khiến các bike không còn đồng bộ với Bike 01.

## 11. Source liên quan

- [ArcadeBikeControllerPro.cs](Scripts/ArcadeBikeControllerPro.cs)
- [ArcadeBikePackIntegrator.cs](../../FranklinAnimations/Editor/ArcadeBikePackIntegrator.cs)
- [FranklinArcadeBikeDriver.cs](../../FranklinAnimations/Runtime/FranklinArcadeBikeDriver.cs)
- [FranklinBikeHealth.cs](../../FranklinAnimations/Runtime/FranklinBikeHealth.cs)
- [FranklinBikeDamageEffects.cs](../../FranklinAnimations/Runtime/FranklinBikeDamageEffects.cs)
- [FranklinBikeDestruction.cs](../../FranklinAnimations/Runtime/FranklinBikeDestruction.cs)
- [FranklinBikeDeformation.cs](../../FranklinAnimations/Runtime/FranklinBikeDeformation.cs)
- [FranklinArcadeBikeRagdoll.cs](../../FranklinAnimations/Runtime/FranklinArcadeBikeRagdoll.cs)
- [FranklinBikeCameraManager.cs](../../FranklinAnimations/Runtime/FranklinBikeCameraManager.cs)
- [BikeEntry.cs](../Vehicle%20Integration/Scripts/GeneralVehicles/BikeEntry.cs)
- [VehicleLights.cs](../Vehicle%20Integration/Scripts/GeneralVehicles/VehicleLights.cs)
