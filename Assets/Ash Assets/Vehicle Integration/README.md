# Franklin Game — Vehicle Integration

Đây là module tập trung cho hạ tầng vehicle của Franklin Game: code dùng chung,
Car, Bike, Hover vehicle, nội dung shared và package bên thứ ba. Asset được phân
loại theo **domain trước, loại file sau** để dễ tìm nhưng không trộn code tự viết
với vendor package. Mỗi asset luôn được di chuyển cùng `.meta`, vì vậy GUID và
reference từ prefab/scene vẫn được giữ nguyên.

## Cấu trúc

```text
Vehicle Integration/
├── Core/
│   ├── Data/
│   │   ├── Input/           # Input Actions dùng chung
│   │   ├── Stats/           # GC2 health/fuel/car/bike stats
│   │   └── Variables/       # GC2 global vehicle variables
│   ├── Runtime/
│   │   ├── Conditions/      # Điều kiện Visual Scripting
│   │   ├── Input/           # Mobile/Input System bridge
│   │   ├── Instructions/    # Instruction dùng chung
│   │   ├── Vehicle/         # Contract và component vehicle dùng chung
│   │   └── Legacy/          # Script cũ còn phải giữ để tương thích
│   └── Editor/
│       ├── Input/           # Property drawer
│       └── Styles/          # USS dùng chung cho custom inspector
├── Vehicles/
│   ├── Car/
│   │   ├── Runtime/         # Enter/exit, Sim-Cade adapter, health, fuel, damage
│   │   ├── Editor/          # Installer, validator và Scene preview
│   │   ├── Prefabs/         # Car.prefab, UI prefab và VFX prefab
│   │   ├── Models/          # Car.FBX
│   │   ├── Materials/       # Vehicle materials và VFX materials
│   │   ├── Textures/        # UI generated/source và VFX textures
│   │   ├── Animations/      # EntryExit, Carjacking và clip Generated
│   │   ├── Audio/           # Door/impact/damage SFX và Radio
│   │   ├── VFX/             # VFX Graph, subgraph và Hovl subset
│   │   └── Legacy/          # PhysicsCarController cũ, không gắn lên Car chính
│   ├── Bike/
│   │   ├── Animations/      # Enter/exit/idle Bike
│   │   ├── Prefabs/         # Empty-Motorbike template
│   │   ├── Textures/UI/     # Icon editor riêng của Bike
│   │   └── Legacy/          # PhysicsBikeController cũ
│   └── Hover/
│       ├── Runtime/         # Hoverbike/hoverboard/hovercar controller
│       └── Editor/          # Custom editor Hover
├── Shared/
│   ├── Audio/               # Mixer, engine, bump và drift dùng lại
│   ├── Materials/           # Material không thuộc riêng một vehicle
│   ├── Textures/            # Texture utility dùng chung
│   ├── Shaders/             # Shader/Shader Graph dùng chung
│   └── UI/Sprites/          # Sprite editor/UI dùng chung
├── Stations/
│   ├── Fuel/Runtime/        # Marker trigger, hold button và giao dịch xăng
│   └── Garage/Runtime/      # Sửa Car/Bike, reset deformation và đổ giới hạn 25%
├── ThirdParty/
│   └── Sim-Cade Vehicle Physics/
│                              # Vendor package nguyên bản, không trộn custom code
└── README.md
```

### Chỉ mục nhanh theo loại asset

| Cần tìm | Đường dẫn chuẩn |
|---|---|
| Prefab Car chính | [`Vehicles/Car/Prefabs/Car.prefab`](Vehicles/Car/Prefabs/Car.prefab) |
| Prefab UI Car | [`Vehicles/Car/Prefabs/UI/`](Vehicles/Car/Prefabs/UI/) |
| Prefab VFX Car | [`Vehicles/Car/Prefabs/VFX/`](Vehicles/Car/Prefabs/VFX/) |
| Model Car | [`Vehicles/Car/Models/`](Vehicles/Car/Models/) |
| Material thân xe | [`Vehicles/Car/Materials/Vehicle/`](Vehicles/Car/Materials/Vehicle/) |
| Material đèn dùng chung | [`Shared/Materials/VehicleLights/`](Shared/Materials/VehicleLights/) |
| Material VFX | [`Vehicles/Car/Materials/VFX/`](Vehicles/Car/Materials/VFX/) |
| Texture UI runtime | [`Vehicles/Car/Textures/UI/Generated/`](Vehicles/Car/Textures/UI/Generated/) |
| File nguồn UI | [`Vehicles/Car/Textures/UI/Source/`](Vehicles/Car/Textures/UI/Source/) |
| Texture VFX | [`Vehicles/Car/Textures/VFX/`](Vehicles/Car/Textures/VFX/) |
| Animation Car | [`Vehicles/Car/Animations/`](Vehicles/Car/Animations/) |
| Audio Car | [`Vehicles/Car/Audio/`](Vehicles/Car/Audio/) |
| Graph và subgraph VFX | [`Vehicles/Car/VFX/Graphs/`](Vehicles/Car/VFX/Graphs/) |
| Stats/Input/Variables | [`Core/Data/`](Core/Data/) |
| Asset dùng chung | [`Shared/`](Shared/) |
| Logic trạm xăng | [`Stations/Fuel/Runtime/`](Stations/Fuel/Runtime/) |
| Logic garage Car/Bike | [`Stations/Garage/Runtime/`](Stations/Garage/Runtime/) |
| Sim-Cade nguyên bản | [`ThirdParty/Sim-Cade Vehicle Physics/`](ThirdParty/Sim-Cade%20Vehicle%20Physics/) |

### Quy ước phân loại

- `Runtime` không chứa `UnityEditor`; mọi custom inspector/installer nằm trong `Editor`.
- `Prefabs`, `Models`, `Materials`, `Textures`, `Animations` và `Audio` là các
  điểm tìm duy nhất trong từng vehicle domain; không đặt texture lẫn trong
  `Materials` hoặc prefab lẫn trong `UI`.
- Asset chỉ dùng cho một vehicle nằm dưới `Vehicles/<Type>`; asset dùng lại cho
  nhiều loại nằm dưới `Core` hoặc `Shared`.
- `ThirdParty` giữ nguyên cấu trúc vendor để dễ audit license và nâng cấp package.
- File tạo bằng ImageGen nằm trong `Textures/UI/Generated`; PSD/file nguồn nằm
  trong `Textures/UI/Source`.

### Dependency nằm ngoài `Vehicles/Car/`

Các thành phần dưới đây có liên quan tới luồng Car nhưng còn được Bike, Player
hoặc GC2 dùng chung, nên không được đưa vào domain Car:

- `../Arcade Bike Physics Pro/Scripts/Integration/Rider/CharacterIKSetter.cs`
- `Core/Runtime/Vehicle/IRvrVehicleDriveController.cs`
- `Core/Runtime/Instructions/InstructionEnterVehicle.cs`
- `Core/Runtime/Instructions/InstructionExitVehicle.cs`
- `Core/Runtime/Instructions/InstructionBikePassenger.cs`
- `Core/Runtime/Vehicle/VehicleLights.cs`
- `Core/Data/Input/InputSystem_RT.inputactions`
- `Core/Data/Variables/Global Variable - Vehicles.asset`
- `Assets/FranklinAnimations/Runtime/FranklinVehicleInteractionManager.cs`
- `Assets/FranklinAnimations/Runtime/FranklinMobileHud.cs`
- `Assets/Prefab/Player.prefab`, `Assets/Prefab/NPC.prefab`
- Game Creator 2, Input System và Cinemachine packages

`FranklinBikeImpactInstaller` vẫn tái sử dụng hai clip va chạm và pooled `Collision Spark` của Car/Sim-Cade; đường dẫn Editor đã được cập nhật sang cấu trúc mới.

## Đường dẫn script

### Car runtime

| Script | Đường dẫn | Trách nhiệm |
|---|---|---|
| `CarEntry` | [`Vehicles/Car/Runtime/CarEntry.cs`](Vehicles/Car/Runtime/CarEntry.cs) | Chủ sở hữu enter/exit, bốn cửa/ghế, door animation, mirror entry và speed-aware bailout. |
| `SimcadeCarDriver` | [`Vehicles/Car/Runtime/SimcadeCarDriver.cs`](Vehicles/Car/Runtime/SimcadeCarDriver.cs) | Adapter input, camera, mobile control, speed và presentation cho Sim-Cade. |
| `SimcadeCarBrakeLights` | [`Vehicles/Car/Runtime/SimcadeCarBrakeLights.cs`](Vehicles/Car/Runtime/SimcadeCarBrakeLights.cs) | Điều khiển đèn hậu/phanh/số lùi từ input và vận tốc thật, theo kiến trúc Bike nhưng dùng hai mesh đèn hậu sẵn có của Car. |
| `SimcadeCarHorn` | [`Vehicles/Car/Runtime/SimcadeCarHorn.cs`](Vehicles/Car/Runtime/SimcadeCarHorn.cs) | Còi Car dạng hold, dùng một AudioSource 3D mono và tự ngủ hoàn toàn khi im lặng. |
| `SimcadeCarDashboard` | [`Vehicles/Car/Runtime/SimcadeCarDashboard.cs`](Vehicles/Car/Runtime/SimcadeCarDashboard.cs) | Một Canvas HUD dùng chung cho mọi instance Car; tự bind driver/health/fuel/radio của Car đang lái. |
| `SimcadeCarHealth` | [`Vehicles/Car/Runtime/SimcadeCarHealth.cs`](Vehicles/Car/Runtime/SimcadeCarHealth.cs) | Nối impact đã phân loại với GC2 `health-attribute-id`; API damage/repair. |
| `SimcadeCarFuel` | [`Vehicles/Car/Runtime/SimcadeCarFuel.cs`](Vehicles/Car/Runtime/SimcadeCarFuel.cs) | Nối GC2 `fuel-attribute-id`, hao xăng theo garanti/ga/tốc độ và khóa ga/engine khi cạn. |
| `SimcadeCarDamageEffects` | [`Vehicles/Car/Runtime/SimcadeCarDamageEffects.cs`](Vehicles/Car/Runtime/SimcadeCarDamageEffects.cs) | Event-driven khói dưới 32% máu, explosion một lần và lửa khi xe hết máu. |
| `SimcadeCarDestruction` | [`Vehicles/Car/Runtime/SimcadeCarDestruction.cs`](Vehicles/Car/Runtime/SimcadeCarDestruction.cs) | Phá hủy terminal: cháy đen riêng instance, văng bốn bánh, cưỡng chế Player ragdoll/cháy/Traits về 0 và khóa UI/điều khiển. |
| `SimcadeCarParticleWind` | [`Vehicles/Car/Runtime/SimcadeCarParticleWind.cs`](Vehicles/Car/Runtime/SimcadeCarParticleWind.cs) | Lực gió world-space cho smoke/fire, cộng airflow ngược vận tốc xe và chỉ cập nhật 8 Hz khi VFX hoạt động. |
| `SimcadeDetachedWheelCleanup` | [`Vehicles/Car/Runtime/SimcadeDetachedWheelCleanup.cs`](Vehicles/Car/Runtime/SimcadeDetachedWheelCleanup.cs) | Vòng đời debris bánh: văng, nằm phẳng theo ground, khóa physics 5 giây, chìm và tự dọn. |
| `SimcadeCarImpactAudio` | [`Vehicles/Car/Runtime/SimcadeCarImpactAudio.cs`](Vehicles/Car/Runtime/SimcadeCarImpactAudio.cs) | Phân loại va chạm, audio, pooled spark/debris và phát event impact dùng chung. |
| `SimcadeCarMetalDebris` | [`Vehicles/Car/Runtime/SimcadeCarMetalDebris.cs`](Vehicles/Car/Runtime/SimcadeCarMetalDebris.cs) | Tái sử dụng event impact để bắn mảnh kim loại lớn hơn Bike bằng một ParticleSystem pool tối ưu mobile. |
| `SimcadeCarDoorDamage` | [`Vehicles/Car/Runtime/SimcadeCarDoorDamage.cs`](Vehicles/Car/Runtime/SimcadeCarDoorDamage.cs) | Tông mạnh tại từng cửa nhả chốt thành bản lề vật lý; kính đi cùng đúng cửa, còn cú cực mạnh hoặc va chạm trực tiếp tiếp theo mới làm cửa rời. |
| `SimcadeCarDeformation` | [`Vehicles/Car/Runtime/SimcadeCarDeformation.cs`](Vehicles/Car/Runtime/SimcadeCarDeformation.cs) | Adapter event Sim-Cade gọi thuật toán deformation Edy cho các render mesh gần contact, đồng thời giữ sai lệch lái nhỏ. |
| `EdysVehicleMeshDeformation` | [`Vehicles/Car/Runtime/EdysVehicleMeshDeformation.cs`](Vehicles/Car/Runtime/EdysVehicleMeshDeformation.cs) | Phần duy nhất port từ `VehicleDamage.cs` của Edy's 5.5.3: falloff theo bán kính, impact velocity, fracture và giới hạn displacement. |
| `SimcadeCarjacking` | [`Vehicles/Car/Runtime/SimcadeCarjacking.cs`](Vehicles/Car/Runtime/SimcadeCarjacking.cs) | Kéo NPC cửa trái và nhánh ghế phụ đẩy NPC sang trái. |
| `SeatedSkeletonPoseGuard` | [`Vehicles/Car/Runtime/SeatedSkeletonPoseGuard.cs`](Vehicles/Car/Runtime/SeatedSkeletonPoseGuard.cs) | Giữ pelvis/spine/head đúng ghế trong camera handoff và bailout. |
| `ConditionCarEnabled` | [`Vehicles/Car/Runtime/ConditionCarEnabled.cs`](Vehicles/Car/Runtime/ConditionCarEnabled.cs) | Điều kiện Visual Scripting kiểm tra trạng thái Car. |

### Car editor

| Script | Đường dẫn | Trách nhiệm |
|---|---|---|
| `SimcadeCarInstaller` | [`Vehicles/Car/Editor/SimcadeCarInstaller.cs`](Vehicles/Car/Editor/SimcadeCarInstaller.cs) | Gắn và xác thực Sim-Cade stack, camera, mobile UI, audio/VFX. |
| `SimcadeCarDashboardInstaller` | [`Vehicles/Car/Editor/SimcadeCarDashboardInstaller.cs`](Vehicles/Car/Editor/SimcadeCarDashboardInstaller.cs) | Gắn HUD ImageGen, health, ba station CC0, radio tuning và validator mobile. |
| `SimcadeCarDamageEffectsInstaller` | [`Vehicles/Car/Editor/SimcadeCarDamageEffectsInstaller.cs`](Vehicles/Car/Editor/SimcadeCarDamageEffectsInstaller.cs) | Author/validate CPU particles và 3D explosion audio trực tiếp trong Car prefab. |
| `SimcadeCarDeformationInstaller` | [`Vehicles/Car/Editor/SimcadeCarDeformationInstaller.cs`](Vehicles/Car/Editor/SimcadeCarDeformationInstaller.cs) | Gắn 10 visible exterior meshes, profile Edy Sport Coupe và xác thực không có dependency EVP ngoài deformation kernel. |
| `SimcadeCarjackingInstaller` | [`Vehicles/Car/Editor/SimcadeCarjackingInstaller.cs`](Vehicles/Car/Editor/SimcadeCarjackingInstaller.cs) | Cấu hình animation, GC2 NPC, landing point và passenger push. |
| `SimcadeCarEntrySceneTool` | [`Vehicles/Car/Editor/SimcadeCarEntrySceneTool.cs`](Vehicles/Car/Editor/SimcadeCarEntrySceneTool.cs) | Preview/chỉnh anchor, cửa, IK và animation trực tiếp trong Scene view. |
| `CarEntryEditor` | [`Vehicles/Car/Editor/CarEntryEditor.cs`](Vehicles/Car/Editor/CarEntryEditor.cs) | Custom Inspector cho `CarEntry`. |
| `VehicleCarAssetConsolidator` | [`Vehicles/Car/Editor/VehicleCarAssetConsolidator.cs`](Vehicles/Car/Editor/VehicleCarAssetConsolidator.cs) | Migration giữ GUID và validator tổng hợp. |

### Script dùng chung ngoài `Car/`

| Script | Đường dẫn | Trách nhiệm |
|---|---|---|
| `IRvrVehicleDriveController` | [`Core/Runtime/Vehicle/IRvrVehicleDriveController.cs`](Core/Runtime/Vehicle/IRvrVehicleDriveController.cs) | Contract điều khiển dùng chung Car/Bike/vehicle interaction. |
| `CharacterIKSetter` | [`../Arcade Bike Physics Pro/Scripts/Integration/Rider/CharacterIKSetter.cs`](../Arcade%20Bike%20Physics%20Pro/Scripts/Integration/Rider/CharacterIKSetter.cs) | Cầu nối IK humanoid dùng chung. |
| `FranklinVehicleInteractionManager` | [`../../FranklinAnimations/Runtime/FranklinVehicleInteractionManager.cs`](../../FranklinAnimations/Runtime/FranklinVehicleInteractionManager.cs) | Phát hiện vehicle gần Player và gọi API enter/exit. |
| `FranklinMobileHud` | [`../../FranklinAnimations/Runtime/FranklinMobileHud.cs`](../../FranklinAnimations/Runtime/FranklinMobileHud.cs) | HUD điều khiển Player/Car/Bike dùng chung; không chứa logic riêng của Car dashboard. |
| `FranklinRagdollGroundGuard` | [`../../FranklinAnimations/Runtime/FranklinRagdollGroundGuard.cs`](../../FranklinAnimations/Runtime/FranklinRagdollGroundGuard.cs) | Gia cố CCD/solver cho xương ragdoll GC2 và đưa pelvis trở lại trên static ground nếu solver thực sự xuyên mặt nền. |
| `FranklinBikeImpactInstaller` | [`../Arcade Bike Physics Pro/Editor/Installation/FranklinBikeImpactInstaller.cs`](../Arcade%20Bike%20Physics%20Pro/Editor/Installation/FranklinBikeImpactInstaller.cs) | Bike tái sử dụng impact SFX/FX nhưng không phụ thuộc runtime dashboard Car. |
| `FranklinBikeHealth` | [`../Arcade Bike Physics Pro/Scripts/Integration/Damage/FranklinBikeHealth.cs`](../Arcade%20Bike%20Physics%20Pro/Scripts/Integration/Damage/FranklinBikeHealth.cs) | Nối impact Bike đã qua phân loại/cooldown với GC2 `health-attribute-id`; API damage/repair và khóa ga/lái ở 0 HP. |
| `FranklinFuelStation` | [`Stations/Fuel/Runtime/FranklinFuelStation.cs`](Stations/Fuel/Runtime/FranklinFuelStation.cs) | Tạo trigger tại GC2 Marker, hiển thị nút hold mobile và thực hiện giao dịch xăng cho Car/Bike. |
| `IFranklinFuelTank` | [`Stations/Fuel/Runtime/IFranklinFuelTank.cs`](Stations/Fuel/Runtime/IFranklinFuelTank.cs) | Contract nhiên liệu dùng chung để trạm xăng không phụ thuộc controller Car hoặc Bike. |
| `FranklinGarageService` | [`Stations/Garage/Runtime/FranklinGarageService.cs`](Stations/Garage/Runtime/FranklinGarageService.cs) | Dịch vụ chung tại bốn bay: sửa đầy health, reset deformation, gắn lại cửa Car và cho Car/Bike mua tối đa 25% dung tích bình mỗi lượt ghé. |
| `FranklinGarageZone` | [`Stations/Garage/Runtime/FranklinGarageZone.cs`](Stations/Garage/Runtime/FranklinGarageZone.cs) | Trigger adapter nhẹ được gắn vào các GC2 Marker của garage. |
| `FranklinPlayerStatusHud` | [`../../FranklinAnimations/Runtime/FranklinPlayerStatusHud.cs`](../../FranklinAnimations/Runtime/FranklinPlayerStatusHud.cs) | Wallet API và HUD tiền dùng chung; thay đổi tiền phát event ngay lập tức. |

## Sơ đồ thành phần

```mermaid
flowchart LR
    Input["Input / Mobile HUD"] --> Interaction["FranklinVehicleInteractionManager"]
    Interaction --> Entry["CarEntry"]
    Entry --> Driver["SimcadeCarDriver"]
    Entry --> Jack["SimcadeCarjacking"]
    Entry --> Pose["SeatedSkeletonPoseGuard"]
    Driver --> Simcade["Sim-Cade runtime"]
    Driver --> Camera["Sim-Cade chase camera"]
    Driver --> Mobile["Sim-Cade mobile controls"]
    Driver --> Dashboard["SimcadeCarDashboard"]
    Driver --> Fuel["SimcadeCarFuel"]
    Driver --> Lights["SimcadeCarBrakeLights"]
    Driver --> Horn["SimcadeCarHorn"]
    Horn --> HornAudio["Loop 3D mono 22.05 kHz"]
    Lights --> SharedLights["VehicleLights + MaterialPropertyBlock"]
    SharedLights --> Headlights["2 headlight meshes + 2 spotlights"]
    SharedLights --> RearLights["2 tail / brake meshes"]
    Lights --> BrakeFlares["2 occluded SRP brake / reverse flares"]
    Fuel --> FuelTraits["GC2 fuel-attribute-id"]
    Fuel --> FuelGauge["Curved vertical fuel gauge"]
    FuelGauge --> Dashboard
    Impact["SimcadeCarImpactAudio"] --> Audio["Light / heavy impact SFX"]
    Impact --> FX["Pooled spark + debris FX"]
    Impact --> Health["SimcadeCarHealth"]
    Impact --> Dent["Edy radius-based render-mesh dent"]
    Impact --> DoorDamage["Nearest-door latch / hinge damage"]
    DoorDamage --> LooseDoor["Free physical hinge"]
    DoorDamage --> DetachedDoor["Detached door debris"]
    DoorDamage --> Entry
    Dent --> SteeringDamage["Persistent small steering bias"]
    SteeringDamage --> Driver
    Health --> Traits["GC2 health-attribute-id"]
    Health --> HealthGauge["Larger mirrored vertical health gauge"]
    HealthGauge --> Dashboard
    Health --> DamageFX["SimcadeCarDamageEffects"]
    DamageFX --> WeakSmoke["Smoke <= 32%"]
    DamageFX --> CriticalFire["Warning fire <= 14%"]
    CriticalFire --> Wind["World wind + vehicle airflow at 8 Hz"]
    WeakSmoke --> Wind
    DamageFX --> Delay["Guaranteed warning 1.35s at 0%"]
    Delay --> Explosion["One-shot explosion"]
    DamageFX --> Fire["Destroyed fire"]
    Fire --> Wind
    DamageFX --> Destruction["SimcadeCarDestruction"]
    Destruction --> Charred["Black wreck via MaterialPropertyBlock"]
    Destruction --> Wheels["4 detached wheel rigidbodies"]
    Wheels --> WheelCleanup["Flat settle -> physics off 5s -> sink"]
    Destruction --> Eject["Player ejection + permanent ragdoll"]
    Eject --> PlayerTraits["Player Traits hp = 0"]
    Eject --> Burn["Prebuilt Fire3 for 4 seconds"]
    Destruction --> HideUI["Car dashboard + all mobile controls hidden"]
    Destruction --> WreckCleanup["After 15s: off-camera + Player far"]
    Driver --> Speed["SpeedKph at 10 Hz + world-follow target"]
    Speed --> Dashboard
    Dashboard --> Radio["3 CC0 stations + tuning static"]
    Entry --> Door["Door animation + 3D SFX"]
    Entry --> Clips["Entry / exit / mirrored clips"]
    BikeImpact["FranklinBikeImpactAudio"] --> BikeHealth["FranklinBikeHealth"]
    BikeImpact --> BikeDent["FranklinBikeDeformation: body render mesh only"]
    BikeHealth --> BikeTraits["GC2 health-attribute-id"]
    BikeImpact --> BikePlayerDamage["Heavy impact: seated Player hp -4..18"]
    BikeTraits --> BikeLock["0 HP: khóa ga/lái và chặn enter"]
    BikeHealth --> BikeDamageFX["Smoke 32% / Fire 14% / warning 1.35s"]
    BikeDamageFX --> BikeExplosion["Explosion11 một lần"]
    BikeExplosion --> BikeWreck["Dynamic fallen wreck + charred mesh"]
    BikeWreck --> BikePlayerZero["Captured Player hp = 0 + burn fire"]
    BikeImpact --> BikeCrash["Heavy impact: rider + bike ragdoll"]
    Garage["FranklinGarageService"] --> Wallet["Money Wallet"]
    Garage --> Health
    Garage --> DoorDamage
    Garage --> BikeHealth
    Garage --> Dent
    Garage --> BikeDent
    Garage --> Fuel
    Garage --> BikeFuel["FranklinBikeFuel"]
```

Quyền sở hữu luồng vào/ra nằm trên `CarEntry` của prefab Car. Player chỉ gọi API và cung cấp `Character`; Player không tự teleport hoặc tự điều khiển cửa.

## API runtime chính

### `CarEntry`

| API | Ý nghĩa |
|---|---|
| `bool RequestEnter(Character character)` | Yêu cầu vào cửa/ghế gần nhất. Car tự phân giải cửa, tình trạng ghế và carjacking. |
| `bool RequestEnter(Character character, CarEntrySideMode side)` | Yêu cầu cửa cụ thể: `DriverDoor`, `PassengerDoor`, `RearLeftDoor`, `RearRightDoor` hoặc `Automatic`. |
| `bool CanRequestEnter(Character character, CarEntrySideMode side)` | Kiểm tra trước mà không thay đổi state. |
| `CarEntrySideMode ResolveEntrySide(...)` | Trả về cửa/ghế thực tế sau khi xét khoảng cách và khả dụng. |
| `bool RequestExit(Character character)` | Ra xe theo tốc độ: dừng tự nhiên hoặc bailout trên 50 km/h; bailout nhanh trừ GC2 Player Traits `hp` theo vận tốc. |
| `bool IsCharacterSeated(Character character)` | Character có đang ở một trong bốn ghế hay không. |
| `bool IsRearPassenger(Character character)` | Character có đang ngồi ghế sau hay không. |
| `bool SeatOccupantInstant(Character character)` | API hệ thống để đặt NPC lái xe; tắt vật lý NPC khi ngồi. Không dùng cho tương tác Player bình thường. |
| `Task<bool> ForceEjectForDestructionAsync(...)` | API nội bộ của hệ thống nổ: nhả ghế không chạy exit animation, phục hồi physics rồi bật ragdoll không tự đứng dậy. |
| `Task<bool> MoveCharacterToEntryStandingPointAsync(Character character)` | Cho GC2 điều khiển Player đi tới điểm mở cửa; không teleport. |
| `Task<bool> EnterThroughOpenDoorAsync(...)` | Tiếp tục vào xe khi cửa đã được mở bởi carjacking; tránh mở/đóng cửa hai lần. |

Trạng thái đọc nhanh:

- `IsTransitioning`: đang vào hoặc ra xe.
- `IsUsingMirroredEntry`: đang dùng clip mirror cửa bên phải.
- `IsPassengerCarjackingSeatOccupied`: Player đang ở ghế phụ trong nhánh đẩy NPC.

### `SimcadeCarDriver`

| API | Ý nghĩa |
|---|---|
| `SetVehicleEnabled(bool state)` | Bật/tắt điều khiển xe, camera và presentation. |
| `SetVehicleEnabled(bool state, bool preserveMomentum)` | Tắt điều khiển nhưng tùy chọn giữ động lượng cho bailout. |
| `SetPassengerPresentation(bool active)` | Giữ camera/UI phù hợp khi Player là hành khách. |
| `SetDestroyed()` | Khóa vĩnh viễn input/controller/audio/dashboard của wreck; camera chỉ tắt ngay nếu không có destruction hold. |
| `RequestExit()` | Chuyển yêu cầu thoát từ UI/input về `CarEntry`. |
| `SetVirtualAccelerateInput(bool)` | Hold ga nhanh trên mobile. |
| `SetVirtualSlowAccelerateInput(bool)` | Hold ga chậm; taper lực kéo tự nhiên và giới hạn ở `60 km/h`, không giả lập bằng phanh. |
| `SetVirtualBrakeReverseInput(bool)` | Hold phanh/lùi. |
| `SetVirtualSteerLeftInput(bool)` / `SetVirtualSteerRightInput(bool)` | Điều khiển lái mobile. |
| `SetVirtualHandbrakeInput(bool)` | Phanh tay mobile. |
| `SetHeadlightEnabled(bool)` | Bật/tắt đồng thời đèn pha, spotlight và đèn hậu chạy đêm. |
| `ToggleHeadlights()` | Đảo trạng thái đèn; dùng được từ GC2 Instruction hoặc UI chung. |
| `SetHornPressed(bool)` | Nhấn/thả còi Car; HUD gọi `true` ở PointerDown và `false` ở PointerUp/PointerExit. |
| `BeginExitStop()` / `CancelExitStop()` | Hãm tốc có kiểm soát cho nhánh exit dưới ngưỡng. |
| `BeginBailoutCameraHold()` / `EndBailoutCameraHold()` | Giữ camera xe thêm 2 giây khi nhảy khỏi xe. |
| `BeginDestructionCameraHold()` | Giữ camera Sim-Cade đang active tại wreck; không tự bật camera cho Car rỗng/off-screen. |
| `ReturnDestructionCameraToPlayer(...)` | Đợi explosion burst, lerp target tới hips Player ragdoll rồi trả quyền cho GC2 camera. |
| `KeepEngineRunningAfterBailout()` | Giữ tiếng động cơ và trạng thái máy khi Player nhảy khỏi xe. |
| `ResetVehicle()` | Đưa xe về tư thế an toàn. |

Thuộc tính đọc: `IsVehicleEnabled`, `IsPassengerPresentationActive`, `SpeedMetersPerSecond`, `SpeedKph`.

### Đèn pha, đèn hậu và đèn phanh

Car tái sử dụng [`VehicleLights`](Core/Runtime/Vehicle/VehicleLights.cs) giống
`Bike_01`, nhưng có thêm
[`SimcadeCarBrakeLights`](Vehicles/Car/Runtime/SimcadeCarBrakeLights.cs) để đọc
input đã được `SimcadeCarDriver` sample và vận tốc dọc của chính Rigidbody:

- `SetHeadlightEnabled(true)` bật hai mesh đèn trước, hai spotlight và mức đèn
  hậu chạy đêm `0.32x`.
- Profile pha Car dùng cường độ `45`, range `42m`, outer cone `68°` và inner
  cone `48°`: vùng sáng rộng/dài hơn nhưng vẫn tắt realtime shadow trên cả hai
  spotlight để giữ ngân sách GPU mobile.
- Nút headlight dạng toggle vốn dùng cho Bike trong `FranklinMobileHud` được
  dùng chung khi `m_ActiveDriver` là Car; không sinh thêm button hoặc texture.
- Trục phanh/lùi, phanh tay hoặc nhánh dừng xe trước khi exit làm hai đèn hậu
  chuyển mượt tới mức đỏ `1.8x`; khi nhả phanh fade chậm hơn để không chớp gắt.
- Hai `LensFlareComponentSRP` dùng data riêng
  [`Vehicles/Car/VFX/CarBrakeOpticalFlare.asset`](Vehicles/Car/VFX/CarBrakeOpticalFlare.asset)
  và texture ImageGen 512×512
  [`Car_Brake_OpticalFlare.png`](Vehicles/Car/Textures/VFX/Car_Brake_OpticalFlare.png).
  Anchor được khóa vào đúng `Renderer.localBounds.center` của từng mesh đèn ở
  `LateUpdate`, vì visual body có thể roll độc lập với Rigidbody khi đánh lái.
  Điểm occlusion được đẩy `0.2m` về phía camera thay vì đẩy anchor ra sau xe,
  nên flare không còn parallax/trượt khỏi tâm đèn. Flare có lõi oval đỏ-trắng
  dày, không variation/distortion, scale cố định, fade cùng brake blend,
  intensity tối đa `1.1`, attenuation `70m`, occlusion `4` sample; không thêm
  point light hay realtime shadow.
- Khi xe đang lùi thật, đèn cảnh báo sau vẫn sáng kể cả input vừa được nhả.
- `VehicleLights` ghi `_GlowColor` bằng `MaterialPropertyBlock`, không gọi
  `Renderer.material`, không clone material cho từng Car. Không tạo GameObject
  lúc runtime, không raycast và không thêm realtime shadow; mỗi frame chỉ đọc input/velocity,
  còn renderer chỉ được ghi khi blend hoặc trạng thái pha thực sự thay đổi.

API đọc thêm trên `SimcadeCarDriver`: `HeadlightsEnabled`,
`SignedAccelerationInput`, `IsHandbrakeRequested`, `IsStoppingForExit`. Trạng
thái runtime trên `SimcadeCarBrakeLights`: `IsIlluminated`, `CurrentBlend`.

### Còi Car mobile

[`SimcadeCarHorn`](Vehicles/Car/Runtime/SimcadeCarHorn.cs) được Car quản lý và
HUD chỉ gọi API `SimcadeCarDriver.SetHornPressed(bool)`. Nút
[`vehicle-control-horn.png`](../../UI/FranklinMobile/Resources/FranklinMobileUI/vehicle-control-horn.png)
được tạo bằng built-in ImageGen, tách chroma-key thành PNG alpha 512² và chỉ
hiện khi Player thực sự lái Car. Nhấn giữ phát
[`car_horn_loop.wav`](Vehicles/Car/Audio/SFX/car_horn_loop.wav); thả nút,
PointerExit, exit Car, disable hoặc destruction đều dừng còi.

Clip là PCM mono 22.05 kHz dài một giây và loop liền mạch. AudioSource đặt gần
đầu xe, dùng 3D logarithmic, min/max distance `3m/55m`, Doppler `0.2`; volume
fade ngắn để không click. Component bị disable trong toàn bộ thời gian im lặng,
không `Update`, allocation, tìm component hoặc tạo AudioClip runtime trên mobile.

### `SimcadeCarjacking`

| API | Ý nghĩa |
|---|---|
| `bool CanCarjack(Character attacker)` | Kiểm tra có NPC lái và luồng không bận. |
| `bool TryBeginCarjack(Character attacker, ...)` | Bắt đầu kéo NPC qua cửa lái. |
| `bool TryEnterOccupiedCar(Character attacker, CarEntrySideMode side)` | Car tự chọn nhánh kéo bên trái hoặc vào ghế phụ rồi đẩy NPC sang cửa trái. |

### `SimcadeCarImpactAudio`

Component tự phân loại va chạm nhẹ/nặng theo impulse, điều chỉnh âm lượng/pitch so với động cơ và phát VFX tia lửa–mảnh vụn bằng pool. `EventImpactAccepted(bool isHeavy, float severity)` được phát một lần sau cooldown để `SimcadeCarHealth` dùng lại kết quả. `EventImpactContactAccepted(Collision, bool, float)` chuyển cùng kết quả và contact point cho deformation ngay trong callback, không tính collision lần hai. API `Configure(...)` chỉ dành cho installer/authoring.

### `SimcadeCarMetalDebris`

Component nghe trực tiếp `EventImpactContactAccepted` của `SimcadeCarImpactAudio`, vì vậy không tạo thêm callback collision hoặc tính severity lần hai. Mảnh vụn chỉ xuất hiện khi vận tốc tương đối hoặc vận tốc Car lớn hơn `60 km/h`; mọi va chạm ở hoặc dưới ngưỡng này, kể cả va chạm được phân loại nặng, đều không phát mảnh. Mỗi burst có `9–16` mảnh, size `0.21675–0.39525`, đã giảm thêm 15% từ profile Car trước đó. Runtime chỉ tạo một ParticleSystem pool tối đa `24` particle cho mỗi Car; billboard không collision, trail, noise, shadow, light probe hay motion vector và dùng chung atlas `MetalDebris.mat` với Bike để giữ chi phí mobile thấp.

### `SimcadeCarDoorDamage`

Component nhận lại chính `EventImpactContactAccepted`, chỉ tìm cửa trong bán kính `0.8m` quanh contact sau một cú va chạm nặng đã được chấp nhận. Severity mặc định `8` làm cửa nhả chốt, mở và dao động tự do bằng `HingeJoint`; cú va chạm đầu phải đạt mức thảm khốc `22` mới làm cửa rời ngay. Với cửa đã lỏng, impact Car tiếp theo phải đạt `16.5`, hoặc chính cánh cửa phải nhận một contact mới có normal speed `13m/s` và impulse/mass `8.5m/s` sau khoảng bảo vệ `0.45s`. Break force/torque bản lề được nâng lên `14500/12000`, tránh việc cửa vừa mở đã tự rơi do chạm nền hay do chính contact vừa nhả chốt.

Bốn mesh kính `FLWin`, `FRWin`, `RLWin`, `RRWin` được bind một lần vào pivot cửa tương ứng trước khi cache renderer/collider. Vì vậy mở cửa, cửa lỏng và cửa rời đều mang kính đi cùng; không tạo Rigidbody, collider hay script riêng cho kính. Installer tổng cũng lưu bốn reference này cho các Car prefab cấu hình lại sau này.

`RepairAllDoors()` trả tất cả cửa lỏng/rời về đúng parent và local pose đã cache
khi `Awake`, khóa physics ngay rồi dọn `HingeJoint`, Rigidbody, BoxCollider và
helper tạm. `RepairDoor(side)` phục hồi riêng một cửa; `HasDamagedDoors` và
`DamagedDoorCount` cho garage/save system kiểm tra mà không tìm GameObject.

`CarEntry.CanAnimateDoor(side)` trả về `false` cho cửa đã lỏng hoặc rời; door rotation, âm thanh đóng/mở và IK tay nắm được bỏ qua. `CarEntry.IsDoorMissing(side)` cho biết riêng trạng thái cửa đã mất. Entry/exit và carjacking vẫn dùng đúng standing/step/seat anchor, vì vậy chỉ bỏ thao tác cửa chứ không bỏ căn ghế hoặc di chuyển GC2.

### `SimcadeCarDeformation`

Car có đúng một collider thân xe: `BoxCollider` ở root, size `(1.7317466, 1.2402761, 4.327468)`, center `(-0.43742472, -0.08479142, 0.027270794)`. Không có `MeshCollider`, `SphereCollider` hoặc `CapsuleCollider` trong prefab. Collider primitive này chỉ giải quyết physics của Rigidbody; mesh bị móp là render mesh readable lấy từ `Vehicles/Car/Models/Car.FBX`, không phải collider mesh.

Lý do không đổi sang MeshCollider: tài liệu Unity 6 xếp convex MeshCollider tốn CPU hơn primitive, còn non-convex MeshCollider không được gắn lên non-kinematic Rigidbody; mỗi lần thay đổi mesh collider còn có nguy cơ runtime cooking spike. Xem [Collider types and performance](https://docs.unity3d.com/kr/current/Manual/physics-optimization-cpu-collider-types.html) và [Mesh Collider cooking optimization](https://docs.unity3d.com/jp/current/Manual/physics-optimization-cpu-mesh-cooking-options.html).

Thuật toán cũ đã được thay bằng phần render-mesh deformation của `Assets/EVP5/Scripts/VehicleDamage.cs` trong Edy's Vehicle Physics 5.5.3 do chủ project cung cấp. Chỉ công thức `DeformMesh` được port: contact velocity nhân `0.02`, falloff tuyến tính trong bán kính, fracture ngẫu nhiên nhẹ, clamp tổng displacement, sau đó luôn `RecalculateNormals()` và `RecalculateBounds()`. Không import `EVP.VehicleController`, wheel/node damage, input, repair hotkey, camera, UI hay controller nào khác của package.

Profile lấy từ prefab `Sport Coupe` của Edy: minimum relative velocity `2.5 m/s`, multiplier `1`, radius `0.5m`, max displacement `0.2m`, max vertex fracture `0.03m`. Mỗi impact xử lý tất cả render panel có hình học nằm trong bán kính thay vì bắt nhiều mesh chồng nhau cạnh tranh một target. Danh sách gồm `FrontBumper`, `RearBumper`, hai `FrontFender`, cửa lái active `DoorFL (1)`, ba cửa còn lại, `RearSanoughDoor` và `MainBody`; `DoorFL` inactive không được dùng. Mesh chỉ clone khi lần đầu thực sự tham gia deformation, vertex buffer được tái sử dụng và tối đa 12 impact được giữ trên một Car.

Va chạm lệch trái/phải từ severity `4.5` trở lên cộng một steering bias nhỏ theo phía hư hỏng. Mỗi lần tối đa `0.055`, tổng được `SimcadeCarDriver` clamp ở `±0.16`; người chơi vẫn counter-steer được. API sửa chữa là `ResetDeformation(bool resetSteering = true)`, `RepairDamageSteering(float amount)` và `ResetDamageSteering()`.

### `SimcadeCarHealth`

| API | Ý nghĩa |
|---|---|
| `ApplyDamage(float amount)` | Trừ máu qua GC2 Runtime Attribute và tự clamp. |
| `Repair(float amount)` | Hồi một lượng máu. |
| `RepairFull()` | Hồi đầy máu Car. |
| `SetNormalizedHealth(float ratio)` | Đặt máu theo tỷ lệ `0..1`, phù hợp save/load hoặc garage. |
| `EventHealthChanged(current, maximum)` | Event cho UI; không poll health mỗi frame. |

Thuộc tính đọc: `CurrentHealth`, `MaximumHealth`, `HealthRatio`, `IsDestroyed`. Impact nhẹ gây `0.35–2.25` damage, impact nặng gây `4.5–14` damage theo severity (giảm khoảng 35% so với profile ban đầu); chỉ impact đã vượt ngưỡng/cooldown của `SimcadeCarImpactAudio` mới được tính. Phân loại âm thanh, VFX va chạm và deformation vẫn dùng severity gốc nên không bị làm yếu theo lượng máu trừ.

### `SimcadeCarFuel`

| API | Ý nghĩa |
|---|---|
| `Consume(float amount)` | Trừ trực tiếp GC2 `fuel-attribute-id` và tự clamp. |
| `Refuel(float amount)` / `RefuelFull()` | Thêm xăng hoặc đổ đầy bình. |
| `SetNormalizedFuel(float ratio)` | Đặt nhiên liệu theo tỷ lệ `0..1`, dùng cho save/load hoặc trạm xăng. |
| `SetEngineActive(bool)` | API nội bộ từ Driver để chỉ chạy tiêu hao khi động cơ thực sự hoạt động. |
| `EventFuelChanged(current, maximum)` | Cập nhật thanh xăng theo event, không poll GC2 mỗi frame. |

Mỗi instance Car mới khởi tạo `Starting Fuel = 100` đúng một lần; GC2 `Fuel` Stat cũng có base `100` và `FuelAtr` có start percent `1`. Disable/enable component không tự đổ đầy lại. Profile mặc định tiêu hao `0.02` đơn vị/giây ở garanti, cộng tối đa `0.1` theo mức ga và `0.03` theo tốc độ so với mốc `120 km/h`. Coroutine chỉ tồn tại khi engine được yêu cầu chạy và tick mỗi `0.25s`; Car đỗ/tắt máy không có công việc fuel theo frame. Khi cạn xăng, `SimcadeCarDriver` khóa ga/lùi và dừng engine audio nhưng vẫn giữ phanh, lái và quán tính. Nếu `Refuel` trong lúc Player vẫn ngồi, engine và khả năng tăng tốc được khôi phục tự động.

### Trạm xăng và Money Wallet API

`fuel_station_mobile.prefab` có bốn GC2 Marker. `FranklinFuelStation` giữ nguyên
Marker và tạo `BoxCollider` trigger nhẹ tại runtime. Khi Car/Bike do Player điều
khiển đi vào vùng, nút ImageGen “hold to refuel” xuất hiện trên Canvas mobile.
Xe phải chậm hơn `3 km/h`; giữ nút sẽ bơm `5` đơn vị/giây theo tick `0.2s`.
Giá mặc định `$5` cho mỗi đơn vị, nên mỗi tick xăng tăng `1` và tiền giảm `$5`.
Nút bám sát đáy màn hình và hiển thị chi phí còn thiếu để đầy bình; con số này
giảm theo lượng xăng vừa nhận. `FranklinPlayerStatusHud` giữ balance thật nhưng
tween số tiền hiển thị bằng unscaled time, còn fuel gauge của Car/Bike SmoothDamp
về giá trị mới để thanh xăng tăng liên tục thay vì nhảy theo từng tick.
Rời marker, thả/ngắt pointer, xe chạy, đầy bình hoặc hết tiền đều dừng giao dịch.
Thanh xăng trong prompt đã được nâng thành progress bar `8px` sắc nét, cập nhật
theo phần trăm bình và hiển thị thời gian còn lại tính từ lượng thiếu chia cho
`Fuel Units Per Second`; bình càng cạn thì thời gian hold càng lâu. Trong lúc
prompt hiện, toàn bộ control lái mobile được tạm ẩn và input đang giữ được nhả.
Nút `×` đóng prompt và phục hồi control cho tới lần rời/vào marker tiếp theo;
khi đang giữ để bơm, nút đóng bị khóa nên giao dịch không bị ngắt bởi touch thứ
hai. Suppression theo owner nên không ghi đè khóa death/destruction.
Prompt chỉ xuất hiện sau khi xe đã xuống dưới `3 km/h`, vì vậy Player vẫn giữ
đầy đủ nút phanh/lái để căn xe trong marker trước khi control được tạm ẩn.

Project chưa có GC2 `Currency` asset hoặc `Bag Wealth` được cấu hình làm nguồn
tiền. Vì HUD trước đây chỉ giữ số demo `m_Money`, nó đã được nâng thành Wallet
API dùng chung thay vì tạo hai nguồn tiền độc lập:

| API | Ý nghĩa |
|---|---|
| `CurrentMoney` | Số tiền hiện tại đang hiển thị trên HUD. |
| `CanAfford(int amount)` | Kiểm tra khả năng thanh toán mà không thay đổi tiền. |
| `TrySpendMoney(int amount)` | Trừ tiền nguyên tử nếu đủ; trả `false` nếu thiếu. |
| `AddMoney(int amount)` / `SetMoney(int amount)` | Cộng hoặc đặt tiền an toàn, không cho số âm/tràn `int`. |
| `EventMoneyChanged(int current)` | Event realtime cho UI/save system. |

`IFranklinFuelTank.TryRefuel(float)` trả lại đúng lượng xăng thực nhận. Trạm chỉ
tính tiền theo lượng này, vì vậy bình gần đầy không bị tính dư. Sau này nếu chuyển
nguồn tiền sang GC2 Inventory, chỉ cần adapter Wallet gọi `Bag.Wealth`; logic
marker, UI hold và fuel tank không cần thay đổi.

### Garage sửa xe và đổ giới hạn 25%

`auto_bay_garage_mobile.prefab` dùng bốn GC2 Marker làm bốn vùng service. Component
`FranklinGarageService` tạo trigger tại runtime, tự tìm đúng Car/Bike do Player
đang điều khiển và chỉ bật UI khi xe nằm trong bay. Xe phải dừng dưới `3 km/h`.
Panel mobile nằm giữa sát đáy màn hình và có hai action rõ ràng:

- **Sửa toàn bộ**: tính giá từ phí cơ bản `$75` cộng `$12` cho mỗi HP bị thiếu,
  mở một tiến trình sửa bắt buộc chờ, hồi health dần rồi reset
  deformation/sai lệch lái và phục hồi toàn bộ cửa Car khi hoàn tất. Car đầy
  máu nhưng còn cửa lỏng/mất vẫn được nhận sửa với phí cơ bản. Wreck terminal đã nổ không được hồi
  sinh bằng garage.
- **Đổ thêm tối đa 25%**: mỗi lượt xe ở trong garage chỉ được mua thêm tối đa
  `MaximumFuel × 0.25`, không phải ép bình về mức 25%. Bình gần đầy chỉ nhận
  phần còn thiếu. Rời hẳn garage rồi quay lại mới mở một lượt 25% mới.

Fuel và tiền dùng giao dịch chính xác theo lượng `TryRefuel` thực nhận; thiếu
tiền thì mua được lượng tương ứng, không âm Wallet. Giá mặc định vẫn là `$5`
cho mỗi đơn vị. `FranklinPlayerStatusHud` tween số tiền giảm dần và fuel gauge
Car/Bike tiếp tục tăng mượt qua event hiện có. Khi panel garage hiện, prompt
radio/refuel khác cùng toàn bộ control lái Car/Bike được tạm ẩn để UI mobile
không chồng nhau và không truyền input lái ngoài ý muốn. Nút `×` góc phải đóng
panel và phục hồi control ngay; panel chỉ tự mở lại sau khi xe rời rồi vào garage
lần nữa. Suppression được quản lý theo owner nên đóng garage không thể bật lại
control đang bị hệ thống death/destruction khóa.

Thời gian sửa được tính từ tỷ lệ health bị thiếu theo đường cong lũy thừa: mặc
định hỏng rất nhẹ mất khoảng `2.5s`, gần hỏng hoàn toàn mất tới `12s`. Hai mốc
`Min Repair Duration` và `Max Repair Duration` chỉnh được trong Inspector.
Thanh progress xanh da trời cập nhật mỗi frame; trong lúc chạy, nút đóng, sửa,
đổ xăng và toàn bộ control lái đều bị khóa nên Player buộc phải chờ. Xe ở mức
critical được ổn định lên `16%` trước rồi health tăng tuyến tính để warning fire
không biến thành vụ nổ terminal giữa một giao dịch sửa đã thanh toán.

Hai icon `garage-repair-icon.png` và `garage-fuel-25-icon.png` được tạo bằng
ImageGen, xử lý alpha trong suốt và import dạng Sprite giới hạn `512px` cho
mobile. Phần khung, chữ và divider do Unity UI dựng trực tiếp nên cạnh sắc,
không phụ thuộc một ảnh panel raster lớn.

| API | Ý nghĩa |
|---|---|
| `RequestFullRepair()` | Thực hiện một giao dịch sửa xe nếu xe dừng, chưa terminal và Wallet đủ tiền. |
| `RequestLimitedRefuel()` | Mua một lần lượng xăng còn được phép trong lượt hiện tại, tối đa 25% dung tích bình. |
| `ActiveVehicle` | `IFranklinFuelTank` của Car/Bike Player đang đặt trong bay. |
| `MaximumFuelFractionPerVisit` | Giới hạn chỉ đọc; Inspector clamp trong khoảng `0.01..0.25`. |

Luồng giao dịch:

```mermaid
flowchart LR
    Marker["4 GC2 garage markers"] --> Zone["FranklinGarageZone"]
    Zone --> Service["FranklinGarageService"]
    Service --> Guard["Player controlled + speed <= 3 km/h"]
    Guard --> Repair["Full health + reset deformation + restore Car doors"]
    Guard --> Allowance["Fuel allowance = capacity x 25% - added this visit"]
    Repair --> Wallet["FranklinPlayerStatusHud Wallet"]
    Allowance --> Tank["IFranklinFuelTank.TryRefuel"]
    Tank --> Exact["Charge exact accepted fuel"]
    Exact --> Wallet
    Service --> Mobile["Bottom mobile service panel"]
```

### `FranklinBikeHealth`

Bike dùng cùng kiến trúc event-driven của Car nhưng giữ profile riêng phù hợp khối lượng và ngưỡng impact của Arcade Bike. `FranklinBikeImpactAudio` tiếp tục là nơi duy nhất phân loại collision, áp cooldown, phát âm thanh và pooled spark; `FranklinBikeHealth` chỉ nhận `EventImpactAccepted` nên một contact không bị tính damage hai lần.

| API | Ý nghĩa |
|---|---|
| `ApplyDamage(float amount)` | Trừ trực tiếp GC2 `health-attribute-id`, tự clamp theo Runtime Attribute. |
| `Repair(float amount)` | Hồi một lượng HP và mở khóa damage khi HP lớn hơn 0. |
| `RepairFull()` | Hồi đầy HP Bike. |
| `SetNormalizedHealth(float ratio)` | Đặt HP theo tỷ lệ `0..1`, dùng cho save/load hoặc garage. |
| `EventHealthChanged(current, maximum)` | Cập nhật thanh máu Bike trong UI mobile dùng chung; không poll mỗi frame. |
| `SpeedMetersPerSecond × 3.6` | Hiển thị tốc độ Bike theo `km/h` trên UI mobile chung, cùng style với Car. |
| `EventDestroyed` / `EventRestored` | Phát đúng một lần khi đi qua biên 0 HP. |

Thuộc tính đọc: `CurrentHealth`, `MaximumHealth`, `HealthRatio`, `IsDestroyed`. Profile mặc định đã giảm: impact nhẹ gây `0.35–2` damage, impact nặng gây `4–12` damage và đạt mức tối đa ở severity `14`. Impact nặng còn trừ `4–18 HP` của GC2 Player đang ngồi. Khi HP bằng 0, `FranklinArcadeBikeDriver` giữ suspension/exit flow nhưng bỏ toàn bộ input ga, lùi, lái, wheelie và burnout; `BikeEntry` từ chối lượt enter mới. Repair chỉ mở khóa, không tự enter hoặc tự bật điều khiển.

### Bike damage VFX, destruction và deformation

`FranklinBikeDamageEffects` tái sử dụng asset Hovl và audio 3D của Car nhưng có
ngân sách nhỏ hơn: Smoke1 tối đa 22 hạt, warning Fire3 12, destroyed Fire3 18,
Player burn 10 và Explosion11 tổng tối đa 60. Ngưỡng là smoke `<=32%`, warning
fire `<=14%`. Trong critical fire, Bike tự mất `1.5%` maximum HP mỗi giây theo
tick `0.25s`; Repair vượt 14% sẽ dừng drain. Tại 0 HP luôn cảnh báo thêm `1.35s`
rồi mới nổ một lần. Critical drain chỉ trừ Bike health, không trừ Player lần hai.

`FranklinBikeDestruction` lưu rider ở thời điểm `EventDestroyed`, nên vụ nổ vẫn
trừ đúng Player dù `FranklinBikeCrashRagdoll` đã nhả rider khỏi seat. Xe thật
được bỏ kinematic/freeze rotation, giữ nguyên hai bánh, trở thành fallen wreck,
nhận lực ngã và char bằng `MaterialPropertyBlock`; Player Traits `hp` về 0 và
nhận burn fire 4 giây. Không có camera explosion riêng cho Bike.

`FranklinBikeDeformation` dùng `EventImpactContactAccepted` và kernel
`EdysVehicleMeshDeformation` của Car. Chỉ MeshFilter thân dưới `BikeBody` được
deform; wheel/tire, glass và MeshCollider không thay đổi. Profile mặc định:
minimum `2.5m/s`, radius `0.38m`, max displacement `0.12m`, fracture `0.018m`,
tối đa 10 dents và 32.000 vertices/panel. Giới hạn này bao phủ body panel DQP
lớn nhất hiện tại (30.320 vertices). `ResetDeformation()` phục hồi mesh.

### `SimcadeCarDamageEffects`

Component không có `Update`: chỉ nhận `EventHealthChanged`. Dưới hoặc bằng 32% máu, Hovl `Smoke1` được bật. Dưới hoặc bằng 14%, một `Critical Warning Fire` nhỏ bắt đầu cháy và tự rút máu theo tick `0.25s`; tốc độ được tính theo phần trăm maximum health nên từ đúng ngưỡng 14% sẽ mất khoảng `7s` để về 0. Nếu Repair đưa máu lên trên 14%, coroutine dừng mà không trừ thêm. Khi health chạm 0, smoke và warning fire luôn được giữ thêm `1.35s` rồi `Explosion11` mới chạy đúng một lần; vì vậy lửa tiếp tục báo nguy hiểm cho tới đúng lúc nổ. Sau explosion, warning/smoke tắt và `Destroyed Fire` duy trì cùng `SimcadeCarDestruction`. Wreck là trạng thái terminal: Repair không hồi sinh điều khiển hoặc arm lại explosion; muốn dùng lại phải respawn prefab Car.

Explosion audio dùng bản ghi thực tế từ một lần thử nghiệm nổ xe của `eth131`, giấy phép CC0. Bản dùng trong game được cắt khoảng lặng, downmix mono, resample `32 kHz` và giữ dynamic transient/đuôi vang tự nhiên; Unity nén Vorbis `Compressed In Memory` để phù hợp mobile. AudioSource là 3D logarithmic, không Doppler, nghe đầy ở gần trong `7m` và giảm tự nhiên đến `110m`. Nguồn và quy trình xử lý được lưu tại `Car/Audio/SFX/CarExplosionSfx_SOURCE.txt`.

Khi Weak Smoke hoạt động, một loop hơi/khí xì 3D rất nhẹ chạy ở volume `0.18`, nghe gần trong `2.5m` và tắt ở `30m`. Khi Critical Fire bắt đầu, loop lửa crackle chạy ở volume `0.5`; sau explosion cùng Destroyed Fire, volume tăng thành `0.8` và giảm tự nhiên tới `48m`. Runtime chỉ gọi `Play/Stop` lúc health đổi trạng thái, không poll âm thanh trong `Update`, không tạo AudioSource lúc chạy. Hai clip CC0 đã được xử lý mono `32 kHz`, nén Vorbis `Compressed In Memory`; nguồn được ghi tại `Car/Audio/SFX/CarDamageLoopSfx_SOURCES.txt`.

`SimcadeCarParticleWind` đặt toàn bộ smoke/fire loop ở simulation space `World` và dùng `Force over Lifetime` thay vì xoay emitter giả. Gia tốc cuối cùng là hướng gió thế giới cộng với airflow ngược vận tốc vật lý của Rigidbody Car, được clamp tối đa `6m/s²`. Không có `Update`: một coroutine duy nhất chỉ chạy khi ít nhất một loop VFX đang hiện và cập nhật tối đa `8 Hz`; khi VFX tắt coroutine dừng hoàn toàn. Trong Inspector có thể chỉnh `World Wind Direction`, `World Wind Acceleration`, `Vehicle Airflow Factor`, giới hạn lực và tần suất.

`SimcadeCarDestruction` quét renderer đúng một lần lúc nổ và ghi màu đen bằng `MaterialPropertyBlock`, vì vậy không clone material và không làm đổi màu những Car khác đang dùng chung material. Bốn wheel visual được tách thành rigidbody tạm thời, kế thừa một phần vận tốc xe và văng ngang/xoay. Khi chạm ground và giảm tốc (hoặc hết thời gian bay tối đa), `SimcadeDetachedWheelCleanup` căn trục bánh theo pháp tuyến mặt đất để lốp nằm phẳng, đặt Rigidbody kinematic, tắt gravity/collision/collider hoàn toàn, giữ nguyên 5 giây rồi chìm `0.48m` trong `0.8s` và tự dọn.

Nếu Player đang ngồi trong Car, camera Sim-Cade được giữ tại vụ nổ theo thời lượng visual burst của ParticleSystem, clamp trong `0.9–2.5s` (`Explosion11` hiện là `1.5s`). Đuôi vang 7 giây của audio không giữ camera quá lâu; audio length chỉ là fallback nếu không có particle. Sau đó camera target dùng smoothstep để lerp trong `0.75s` từ wreck tới hips của Player ragdoll, chờ thêm một `LateUpdate`, rồi mới trả camera cho GC2. Car rỗng hoặc camera xe không active không khởi tạo luồng camera này.

Xác Car luôn tồn tại ít nhất 15 giây. Sau mốc này hệ thống chỉ kiểm tra mỗi `0.5s` và chỉ ẩn toàn bộ wreck khi đồng thời thỏa hai điều kiện: bounds thân xe không nằm trong frustum của `Camera.main`, và Player cách xe ít nhất `35m`. Các giá trị này chỉnh được trong nhóm `Wreck Cleanup` của `SimcadeCarDestruction`. Khi Player đang ở bất kỳ ghế nào, Car nhả Player cạnh thân xe không qua exit animation, bật ragdoll không auto-recover, gắn `Fire3` đã author sẵn trong prefab trong 4 giây, đưa GC2 Player Traits `hp` về `0`, đồng thời ẩn cả dashboard/Car controls và Player mobile controls. Có thể mở lại HUD sau respawn bằng `FranklinMobileHud.SetControlsSuppressed(false)`.

Chỉ các asset/dependency cần thiết được lấy từ `3D Fire and Explosions v2.1`; không import toàn bộ package. Texture 2K được giới hạn còn `512px` (`Point19` là `256px`) và dùng ASTC 6x6 trên Android/iOS. Ngân sách mobile: smoke tối đa 28 hạt, critical fire 16 hạt, destroyed fire 24 hạt, Player burn 12 hạt và ba lớp explosion tổng capacity không quá 60 hạt. Tất cả ParticleSystem được author sẵn trong prefab, không `Instantiate` lúc nổ.

### `SimcadeCarDashboard`

| API | Ý nghĩa |
|---|---|
| `SetPresentationActive(bool)` | Hiện/ẩn dashboard cùng trạng thái driver/passenger Car. |
| `IsSharedHudActive` | Cho HUD vehicle dùng chung biết Car đang sở hữu Canvas; dùng để loại trừ telemetry Bike trong lúc chuyển vehicle. |
| `SetSpeedHudPosition(float left, float height, Vector2 screenOffset)` | Chỉnh điểm bám world và bù vị trí pixel của km/h bằng code. |
| `SetHealthHudPosition(float right, float height, Vector2 screenOffset)` | Chỉnh độc lập thanh máu bám bên phải thân xe và bù vị trí pixel. |
| `ToggleRadio()` / `SetRadioEnabled(bool)` | Bật hoặc tắt radio. |
| `PreviousTrack()` / `NextTrack()` | Chuyển track và bắt đầu phát. |
| `SelectTrack(int index, bool play)` | Chọn track bằng code, hỗ trợ mở rộng playlist. |

Dashboard dùng đúng một Canvas static dùng chung với sorting order `1210`, không tạo một Canvas cho từng Car. Khi đổi xe, `s_ActiveDashboard` chỉ rebind telemetry/radio sang `SimcadeCarDashboard` của xe đang lái; các Car còn lại không update HUD và event của chúng không được phép ghi lên UI. Khi Canvas Car active, `FranklinMobileHud` ưu tiên driver Car và tắt ngay speed/fuel/health cùng ba nút riêng của Bike; khi trở lại Bike, HUD Bike mới được bind lại. Vì vậy cung máu xanh lá của Bike không thể chồng lên dấu cộng/thanh máu xanh da trời của Car, kể cả trong frame handoff. Canvas dùng `ScaleWithScreenSize` ở mốc `1920x1080` và tự áp dụng `Screen.safeArea` cho màn hình tai thỏ.

- Tốc độ lấy từ `SimcadeCarDriver.SpeedKph`, cập nhật text tối đa 10 Hz. Tâm HUD lấy từ bounds của renderer thật thay vì pivot prefab, loại trừ particle/trail/line; khoảng cách trái/phải còn tự cộng half-width nhìn thấy theo góc camera. Vì vậy cùng một HUD giữ đúng tâm và kích thước trên các mẫu Car khác nhau. World target dùng local height `0.82m` và `SmoothDamp` `0.11s`.
- Typography dùng `Josefin Sans Bold`; speed `56px`, hậu tố `km/h` `29px` và shadow nhẹ `1px`/alpha `0.34` để không tạo quầng đen trên màn hình nhỏ.
- Thanh xăng nằm ngay dưới `km/h`, dùng sprite ImageGen flat vàng `VehicleFuelArc.png` kích thước nguồn `71x512`, alpha trong suốt, cong nhẹ sang trái (đã flip ngược hướng Car) và chỉ giữ shadow charcoal rất mềm. Không còn highlight, bevel hoặc gradient 3D. Một Image tối mờ làm rãnh nền; Image vàng phía trên dùng `Filled/Vertical` từ `E` lên `F`, nên `EventFuelChanged` chỉ đổi `fillAmount` và không rebuild custom mesh. Không có panel nền; texture tắt mipmap, clamp, giới hạn `512px` và nén cho mobile. Có thể chỉnh `Fuel Gauge Offset` trong Inspector.
- Thanh xăng đi theo world target bên trái Car cùng cụm `km/h`; thanh máu xanh da trời dùng world target đối xứng bên phải. Hai phía dùng chung tâm renderer và cùng side offset thích ứng, sau đó bù theo tâm sprite và mép đáy nên không còn lệch do pivot của từng model. `Health Screen Offset` chỉ là fine-tuning, mặc định `(0,0)`. Bar máu cao `184px`, dày `40px`, icon dấu cộng xanh da trời nằm chính giữa trên đỉnh; `EventHealthChanged` chỉ đổi `fillAmount`, không poll hay custom mesh.
- Cụm radio neo cách đáy safe area `70px`, gần sát cạnh dưới nhưng vẫn tránh home indicator/tai thỏ ngang. Đĩa radio quay `38°/s` khi phát. Bốn button PNG alpha có vùng chạm `88x88`; trạng thái Off được thể hiện bằng tint và tên station.
- Ba station CC0 dùng một `AudioSource` 2D, Vorbis quality `0.48`, `Streaming`, `loadInBackground=true`, `preloadAudioData=false`.
- Static dò sóng dài `0.55s` dùng source 2D riêng, mono PCM/preload vì file rất nhỏ; chỉ phát khi bật/tắt/chuyển station.
- Radio dừng khi rời Car và không giữ audio voice.

## Ví dụ gọi API

```csharp
CarEntry car = carObject.GetComponent<CarEntry>();

// Cửa gần nhất trong bốn cửa, không teleport Player.
if (car.CanRequestEnter(player, CarEntrySideMode.Automatic))
{
    car.RequestEnter(player);
}

// Car quyết định exit thường hay bailout theo tốc độ thực tế.
car.RequestExit(player);
```

```csharp
SimcadeCarDriver driver = carObject.GetComponent<SimcadeCarDriver>();

// Các button mobile phải gọi true ở PointerDown và false ở PointerUp.
driver.SetVirtualSlowAccelerateInput(true);
driver.SetVirtualSlowAccelerateInput(false);
```

```csharp
SimcadeCarHealth health = carObject.GetComponent<SimcadeCarHealth>();
SimcadeCarFuel fuel = carObject.GetComponent<SimcadeCarFuel>();
SimcadeCarDashboard dashboard = carObject.GetComponent<SimcadeCarDashboard>();

health.RepairFull();
fuel.RefuelFull();
dashboard.SetRadioEnabled(true);
dashboard.NextTrack();
```

```csharp
FranklinBikeHealth bikeHealth = bikeObject.GetComponent<FranklinBikeHealth>();

bikeHealth.ApplyDamage(15f);
bikeHealth.Repair(10f);
bikeHealth.RepairFull();
```

## Luồng telemetry, health và radio

```mermaid
flowchart LR
    Rigidbody["Rigidbody velocity"] --> DriverSpeed["SimcadeCarDriver.SpeedKph"]
    DriverSpeed -->|"10 Hz; chỉ khi giá trị đổi"| SpeedText["Speed text"]
    CarWorld["Car world point bên trái"] --> Project["Camera.WorldToScreenPoint"]
    Project --> Smooth["SmoothDamp 0.11 s"]
    Smooth --> SpeedText
    Collision["OnCollisionEnter"] --> Impact["Một lần phân loại + cooldown"]
    Impact --> SoundFx["Audio + pooled sparks/debris"]
    Impact --> Damage["SimcadeCarHealth.ApplyDamage"]
    Damage --> GC2["GC2 health-attribute-id"]
    GC2 -->|"EventHealthChanged"| HealthBar["Larger mirrored vertical health fill + color"]
    RadioButtons["Power / Prev / Play / Next"] --> Tune["0.55 s radio static"]
    RadioButtons --> RadioSource["Một 2D music AudioSource"]
    RadioSource --> Stream["3 Vorbis Streaming station"]
    RadioSource --> Disc["ImageGen disc quay khi phát"]
    BikeCollision["Bike OnCollisionEnter"] --> BikeImpact["Phân loại + cooldown dùng chung"]
    BikeImpact --> BikeDamage["FranklinBikeHealth.ApplyDamage"]
    BikeDamage --> BikeGC2["Bike GC2 health-attribute-id"]
    BikeGC2 -->|"0 HP"| BikeDisable["Khóa input + chặn enter"]
    BikeImpact --> BikeDent["Render-body deformation"]
    BikeImpact --> BikePlayer["Heavy: trừ Player hp"]
    BikeGC2 --> BikeVfx["Smoke / warning fire / explosion"]
    BikeVfx --> BikeWreck["Dynamic fallen wreck + Player hp 0"]
```

## Radio CC0 và sprite ImageGen

Playlist không còn dùng menu background `Franklin_FM.wav`. Các file runtime mới:

| Asset | Nguồn / giấy phép | Import mobile |
|---|---|---|
| [`City_Loop_CC0.mp3`](Vehicles/Car/Audio/Radio/Stations/City_Loop_CC0.mp3) | [City Loop — OpenGameArt](https://opengameart.org/content/city-loop-0), CC0/Public Domain | Vorbis `0.48`, Streaming, không preload |
| [`Vision_CC0.mp3`](Vehicles/Car/Audio/Radio/Stations/Vision_CC0.mp3) | [Vision — OpenGameArt](https://opengameart.org/content/vision), CC0 | Vorbis `0.48`, Streaming, không preload |
| [`Iso1nhab1tans_CC0.mp3`](Vehicles/Car/Audio/Radio/Stations/Iso1nhab1tans_CC0.mp3) | [Iso1nhab1tans — OpenGameArt](https://opengameart.org/content/iso1nhab1tans), CC0 | Vorbis `0.48`, Streaming, không preload |
| [`Radio_Tune_Static_CC0.mp3`](Vehicles/Car/Audio/Radio/SFX/Radio_Tune_Static_CC0.mp3) | [Static — OpenGameArt](https://opengameart.org/content/static), CC0; cắt/fade thành cue `0.55s` | Mono PCM, Decompress On Load, preload |

Sprite [`Vehicles/Car/Textures/UI/Generated/`](Vehicles/Car/Textures/UI/Generated/) được tạo bằng built-in ImageGen theo ảnh HUD tham chiếu, sau đó tách chroma thành PNG alpha. Runtime chỉ giữ các texture đã crop/downscale: disc `256²`, button `192²`, health frame `1024x100`; không đưa ảnh nguồn độ phân giải lớn vào build.

## Luồng enter

```mermaid
flowchart TD
    A["RequestEnter(Character, side)"] --> B["Resolve cửa/ghế khả dụng gần nhất"]
    B --> C{"Ghế sau?"}
    C -- Có --> D["GC2 đi tới cửa sau"]
    D --> E["Mở cửa + animation vào"]
    E --> F["Khóa pose ngồi; hai tay lên đùi"]
    C -- Không --> G{"Ghế lái có NPC?"}
    G -- Không --> H["GC2 đi tới điểm mở cửa trước"]
    H --> I["Mở cửa + entry hoặc mirrored entry"]
    I --> J["Khóa xương vào Driver Seat"]
    J --> K["Bật Sim-Cade camera/UI/control"]
    G -- Có, cửa trái --> L["Mở cửa một lần; kéo NPC ra"]
    L --> I
    G -- Có, cửa phải --> M["Vào ghế phụ"]
    M --> N["Nghiêng thân; tay phải bám vô lăng"]
    N --> O["Tay trái đẩy NPC qua cửa trái"]
    O --> J
```

Điểm đứng cửa được tiếp cận bằng motion của GC2. Việc căn root/khung xương vào ghế chỉ bắt đầu trong đoạn animation bước vào cabin.

## Luồng exit theo tốc độ

```mermaid
flowchart TD
    A["RequestExit"] --> B{"Tốc độ > 50 km/h?"}
    B -- Không --> C{"Xe đã <= 0.8 km/h?"}
    C -- Không --> D["BeginExitStop: giảm tốc tự nhiên"]
    D --> C
    C -- Có --> E["Mở cửa, exit animation, đóng cửa"]
    B -- Có --> F["Giữ pose ngồi; tay thò mở cửa"]
    F --> G["Phát nhanh frame 0–49 CarGetKickedOutL"]
    G --> H["Ragdoll + kế thừa vận tốc xe"]
    H --> K["Trừ Player Traits hp theo tốc độ"]
    H --> I["Camera xe giữ 2 giây; động cơ tiếp tục"]
    H --> J["Cửa khép tự nhiên trong 2 giây, không đóng kín"]
```

`SeatedSkeletonPoseGuard` giữ pelvis/spine/head ở pose ghế trong điểm chuyển camera và trước bailout, ngăn locomotion graph làm Player đứng hoặc nhô lên nóc xe vài frame.

Damage bailout chỉ áp dụng cho GC2 Player khi tốc độ lúc bắt đầu exit lớn hơn `50 km/h`, sau khi ragdoll đã nhận pose. Công thức mặc định là `min(60, 10 + (speedKph - 50) * 0.5)`: 60 km/h mất 15 HP, 100 km/h mất 35 HP và từ 150 km/h trở lên mất tối đa 60 HP. Giá trị được ghi trực tiếp vào Player Traits `hp`, tự clamp theo Min/Max của GC2. Exit chậm sau khi xe dừng và NPC không bị trừ HP bởi nhánh này. Các thông số nằm trong nhóm `Moving Exit Traits Damage` của `CarEntry`.

## Authoring và validation

Các menu Car được giữ tối thiểu trong Unity:

- `Tools > Franklin Game > Car Entry Live Setup`: chỉnh/preview điểm đứng, doorway, ghế, tay nắm, cửa và animation Player trong Scene view.
- `Tools > Franklin Game > Install Sim-Cade Car`: installer tổng; tự gắn physics, entry/exit, deformation, dashboard/radio và damage VFX theo đúng thứ tự.
- `Tools > Franklin Game > Validate Consolidated Car Assets`: kiểm tra cấu trúc, prefab Car, toàn bộ Sim-Cade stack, bốn cửa/ghế, camera/mobile UI, audio/VFX, bailout và NPC carjacking.

Các installer/validator thành phần vẫn là API Editor nội bộ để installer tổng gọi, nhưng không tạo thêm mục trong menu. `Consolidate()` cũng được giữ làm API migration nội bộ vì asset đã gom xong.

Prefab chính: `Vehicles/Car/Prefabs/Car.prefab`.

## Quy tắc mở rộng

1. Logic chỉ dành cho sedan Car đặt trong `Vehicles/Car/Runtime` hoặc `Vehicles/Car/Editor`.
2. API dùng từ hai loại vehicle trở lên đặt trong `Core/Runtime/Vehicle`, `Core/Runtime/Instructions` hoặc layer shared tương ứng.
3. Không sửa trực tiếp mã trong `ThirdParty/Sim-Cade Vehicle Physics` trừ adapter đường dẫn/package compatibility; gameplay tùy biến đặt ở `SimcadeCarDriver`.
4. Không gắn `Legacy/PhysicsCarController` hoặc `SimcadeRvrCarPhysics` lên prefab Car chính thức.
5. Khi thêm asset, luôn di chuyển trong Unity/`AssetDatabase` để giữ `.meta` và GUID.
6. UI hold dùng `PointerDown`/`PointerUp`; không cấp phát hoặc tìm component trong `Update`/`FixedUpdate`.
7. Collision VFX tiếp tục dùng pool; không `Instantiate` mảnh vụn cho từng contact trên mobile.
8. Track radio dài phải dùng `Streaming`; không bật preload hoặc `Decompress On Load` trên mobile.
9. Telemetry text không được cập nhật 60 lần/giây; giữ tần suất 10 Hz hoặc thấp hơn.
10. Chỉ world-follow position của speed text chạy mỗi frame; không cấp phát, không tìm component và không đọc health/radio bằng polling.
11. HUD Car không được thêm panel nền toàn chiều rộng; giữ alpha sprite và safe-area để không che gameplay.
12. Damage smoke/fire/explosion/Player burn phải dùng component event-driven và ParticleSystem đã có sẵn trong prefab; không spawn effect lúc nhận damage.
13. Màu cháy đen phải dùng `MaterialPropertyBlock`; không sửa `sharedMaterial` và không tạo material runtime cho từng Car.
14. Wheel cleanup chỉ chạy `FixedUpdate` trong vài giây đang bay; sau khi nằm phẳng phải khóa Rigidbody/collider hoàn toàn, giữ 5 giây rồi chìm và tự dọn.
15. Wreck cleanup chỉ kiểm tra mỗi `0.5s` sau mốc 15 giây; không dùng kiểm tra camera/khoảng cách mỗi frame.
16. Smoke/fire dùng world-space `Force over Lifetime`; cập nhật airflow tối đa 8 Hz và dừng coroutine khi không có loop VFX hoạt động.
17. Car động giữ primitive `BoxCollider`; visual dent không được cập nhật MeshCollider hoặc gọi runtime mesh cooking.
18. Deformation dùng 10 render mesh visible từ `Vehicles/Car/Models/Car.FBX`; cửa lái phải là `DoorFL (1)` active, không phải `DoorFL` inactive.
19. Mỗi impact Edy chỉ quét panel có renderer bounds nằm trong radius, tối đa 12 impact; mesh thay đổi phải tính lại normals và bounds để vết móp hiển thị đúng.
20. Không tạo thêm `MenuItem`, EditorWindow, wizard hoặc tool preview mới nếu người dùng chưa yêu cầu rõ. Ưu tiên API/component, installer tổng và validator tổng hiện có để menu không bị rối.
21. Ragdoll Player phải dùng `ContinuousDynamic`, interpolation và solver profile của `FranklinRagdollGroundGuard`; chỉ sửa vị trí khi đáy pelvis đã xuyên quá tolerance, không snap nhân vật xuống ground khi vẫn ở trên không.
22. Car dùng một `MobileBlobShadow` trên root: footprint box bo góc rất nhẹ bằng
    superellipse bậc 8, raster bằng một oversized triangle duy nhất nên không còn
    cạnh chia chéo. `Opacity 0.94` và dark core `0.58` mô phỏng gầm xe
    che gần hết ánh sáng. Bounds Car là `1.91m × 4.23m` (rộng × dài) và có tâm local
    X `-0.43m`; không được đặt FBS tại pivot `(0,0)` vì mesh Car lệch pivot.
    Probe ground `10 Hz`, distance check `4 Hz` có stagger và cull hoàn toàn sau `45m`.
    Khi bị cull phải dừng cả renderer lẫn ground raycast. API authoring dùng chung là
    `FranklinGame.Rendering.Editor.FranklinBlobShadowInstaller.InstallAll()`;
    runtime có `SetRectangle(width, length)` và không tạo thêm material instance hoặc
    collider runtime. `FranklinBlobShadow.SetGlobalEnabled(bool)` là master switch lưu
    trạng thái, áp dụng đồng thời cho Player/NPC/Car/Bike và dừng toàn bộ update khi tắt.
23. Player FBS phải gọi `SetSuspended(true)` ngay khi shared interaction manager bắt đầu
    enter Car/Bike và chỉ `SetSuspended(false)` sau exit/crash release. Ground probe Player
    bỏ qua collider có attached Rigidbody để không chiếu lên nóc/thân vehicle.
24. Character FBS mặc định bật `Suppress Inside Shadow Owner`. Khi Player/NPC/passenger
    được parent vào seat dưới một Car/Bike có FBS, FBS Character tắt tự động để không
    double với vehicle. Khi detach do exit, carjack, eject hoặc crash, FBS bật lại bằng
    callback `OnTransformParentChanged`; không quét parent hierarchy mỗi frame.

## QA tối thiểu

| Nhánh | Cần kiểm tra |
|---|---|
| Enter trái/phải không NPC | Player đi tới đúng cửa, cửa mở một lần, không đứng/nhô lên nóc. |
| Enter trái có NPC | Kéo NPC đúng landing point, NPC tắt vật lý khi ngồi và phục hồi một lần khi ra. |
| Enter phải có NPC | Player vào ghế phụ, thân nghiêng/nhìn NPC, tay phải bám vô lăng, tay trái đẩy NPC. |
| Ghế sau trái/phải | Cửa gần nhất được chọn; hai tay giữ trên đùi, không giơ lên trần. |
| Exit dưới 50 km/h | Xe giảm tốc tự nhiên tới dừng rồi mới mở cửa; không sinh khói phanh giả. |
| Exit trên 50 km/h | Không đứng lên; dùng 50 frame đầu, ragdoll, trừ Traits `hp` đúng một lần theo tốc độ, giữ camera/engine, cửa khép không kín. |
| Camera/UI mobile | Orbit trái/phải có input, tự trở về khi thả; button enter/exit/slow có kích thước và vùng ngón cái đúng. |
| Va chạm | Âm nhẹ/nặng nghe rõ hơn động cơ theo mức va chạm; spark/debris được pool; panel gần contact móp theo severity. |
| Deformation mobile | Từ `2.5m/s` trở lên phải thấy panel trong radius móp, ánh sáng/mesh bounds cập nhật ngay; quá 12 impact không tiếp tục sửa vertex. |
| Lệch lái do hỏng | Tông lệch trái/phải đủ mạnh làm xe kéo nhẹ về phía hỏng; bias không vượt `±0.16`, counter-steer và Reset API hoạt động. |
| Hư hỏng cửa | Tông mạnh đúng vùng cửa chỉ làm cửa bật và dao động trên bản lề, không tự rơi ngay khi chạm nền; cú cực mạnh/va chạm trực tiếp lần hai mới làm cửa rời. Cả bốn kính phải mở, lắc và rơi cùng đúng cửa. Enter/exit qua cửa đã mất không chạy rotation, SFX hoặc IK mở cửa. |
| Garage sửa cửa | Cửa lỏng hoặc rơi vẫn làm nút sửa khả dụng khi Car đang 100% máu; sau progress, cửa và kính trở lại đúng pose đóng, không còn Rigidbody/collider tạm và enter/exit dùng cửa bình thường. |
| Máu Car | Va chạm nhẹ/nặng trừ đúng một lần; fill/icon xanh da trời và Repair API cập nhật ngay trên HUD dùng chung. |
| Damage VFX | <=32% có khói; <=14% có warning fire; health 0 phải cảnh báo thêm 1.35s mới nổ; wreck terminal không nổ lại hoặc lái lại sau Repair. |
| Gió VFX | Khi đứng yên smoke/fire nghiêng theo world wind; khi xe chạy, luồng khí bẻ ngược hướng vận tốc; hạt cũ ở world-space không bị kéo cứng theo xe. |
| Phá hủy Car rỗng | Toàn bộ renderer Car cháy đen riêng instance; bốn bánh tách/văng, nằm phẳng, khóa physics 5 giây rồi chìm/ẩn; camera, dashboard và Car control ẩn. |
| Cleanup xác xe | Trước 15 giây không ẩn; sau 15 giây vẫn giữ nếu camera thấy hoặc Player gần hơn 35m; chỉ ẩn khi ngoài camera và Player đủ xa. |
| Phá hủy khi Player ngồi | Player nằm gần Car, ragdoll không tự đứng, cháy đen + Fire3, Traits `hp` = 0; camera giữ hết burst rồi lerp 0.75s tới Player, controls vẫn ẩn ngay. |
| Tốc độ | Hiển thị km/h bên trái thân xe, bám chuyển động/camera với target lag nhẹ; text không cập nhật khi số không đổi. |
| Radio | Power/Prev/Play/Next hoạt động, disc quay, static phát ngắn, ba station loop và rời xe thì dừng. |
| HUD không nền | Không có radio panel hoặc health rectangle background; PNG alpha không tạo viền chroma. |
| Safe area | Speed/radio/health không nằm dưới notch hoặc home indicator ở cả hai hướng landscape. |
| Blob shadow Car | Có đúng một child `MobileBlobShadow`; projection footprint `1.91 × 4.23m`, không có vệt chéo, center X `-0.43m`, opacity/core `0.94/0.58`, trục dài xoay theo hướng Car, bám ground, nhỏ/mờ khi Car bay và không render/raycast khi camera xa hơn 45m. |
| FBS Player khi dùng xe | Bắt đầu enter là Player FBS tắt; giữ tắt khi ngồi và trong exit; chỉ bật lại sau khi Player trở về đi bộ. FBS không được chiếu lên nóc Car/Bike. |
| FBS NPC/passenger | Khi Character nằm trong hierarchy ghế Car/Bike, chỉ giữ FBS vehicle; FBS Character phải tắt. Sau detach, Character FBS xuất hiện lại trên nền. |

Sau khi đổi prefab, animation hoặc anchor, chạy validator rồi mới QA trong Play Mode trên cấu hình mobile mục tiêu.

Để kiểm tra VFX và SFX độc lập với damage trong Play Mode, mở menu ngữ cảnh của component `SimcadeCarDamageEffects` và chọn `Damage FX/Preview Weak Smoke`, `Damage FX/Preview Critical Fire Warning`, `Damage FX/Preview Explosion + Fire` hoặc `Damage FX/Stop Preview`. Preview smoke/fire cũng bật đúng loop 3D tương ứng.
