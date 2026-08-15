# Franklin Shooter System GC2

Hệ thống shooter mobile tích hợp với **Game Creator 2 Shooter** và dùng model từ
**Weapons Low**. Toàn bộ code, UI ImageGen, catalog, weapon asset sinh tự động và model
được dùng bởi hệ thống đều nằm trong `Assets/ShooterSystemGC2`.

## Cách dùng

- Vào Play Mode: Player mặc định ở chế độ melee, Shooter không tự trang bị súng.
  M1911 chỉ là ô được focus ban đầu trong weapon wheel; súng chỉ được equip sau khi tap.
- Tap vào `Weapon Card` ở HUD góc phải trên để mở menu chọn súng.
- Weapon menu dùng vòng 8 lát bán trong suốt kiểu GTA; nền ngoài và tâm rỗng để vẫn thấy gameplay.
- Súng đang chọn được đánh dấu bằng đúng một lát vành khăn xanh navy bão hòa, màu đặc
  không alpha; lát này bật/tắt trực tiếp để luôn render rõ và không dùng ô vuông.
- Tap một trong 8 ô để equip: M1911, UZI, AK-74, M4, Benelli M4, M249, M107, RPG-7.
- Nút cam: giữ để tự ngắm; hệ thống chỉ bóp cò sau khi pose bắn đã blend xong.
  Thả trước khi pose sẵn sàng sẽ hủy phát bắn, thả sau đó sẽ dừng bắn và thoát ngắm.
- Fire button giữ quyền điều khiển từ `PointerDown` tới `PointerUp` của đúng ngón tay
  đã chạm. Kéo ngón tay ra ngoài button trong khi orbit camera không nhả cò; ngón tay
  orbit thứ hai cũng không làm gián đoạn trạng thái giữ Fire.
- Với súng `Single`, tiếp tục giữ Fire sẽ tự nhả/kích hoạt cò lại theo đúng fire rate
  của từng súng. Vòng lặp dừng khi thả Fire hoặc hết băng; hết băng vẫn cần một lần
  nhấn Fire mới để bắt đầu reload.
- Khi bắn, Player dùng trực tiếp `UnitFacingObjectDirection` gốc của GC2. Bộ đếm được
  reset theo từng viên đạn thực tế; sau viên cuối 1 giây không bắn, wrapper chỉ tắt
  mode và khôi phục hướng trước đó, thông thường là `Pivot`. Không còn facing class
  riêng của Franklin Shooter.
- Recoil camera theo cả trục ngang và dọc đã giảm 50% so với weapon template gốc.
- Fire gesture của cả tám súng dùng `Franklin Shooter Upper Body` mask để phối hợp
  với locomotion toàn thân ở layer dưới, đúng thứ tự animation của Shooter GC2.
- Khi bắt đầu giữ Fire trên mặt đất, Shooter giành quyền animation theo thứ tự cố định:
  đóng ngay Jog/Sprint layer `2` cùng run-start/run-stop Gesture, bật
  state `Shooter_Locomotion` gốc ở layer `7`, sau đó mới vào ADS layer `8`
  và chờ pose sẵn sàng mới bóp cò. Trong toàn bộ thời gian giữ Fire,
  `FranklinAnimationBridge` không còn ghi State, Gesture, facing layer hoặc
  `MoveToDirection`; GC2 Walk gốc cùng Player input điều khiển di chuyển và GC2 Shooter
  tự xử lý aim/rig. Nhả Fire sẽ trả quyền Jog/Sprint cho locomotion bridge.
- Các ADS Sight giữ `ShootingUsesFK/IK` trong toàn bộ fire Gesture. Tay, vai và spine
  không còn nhảy qua lại giữa rig ngắm và locomotion mỗi khi animation bóp cò chạy;
  recoil riêng của Shooter GC2 vẫn được áp dụng bình thường.
- ADS state layer `8` dùng `Franklin Shooter Upper Body`; layer `7` vẫn cung cấp
  locomotion toàn thân và tám hướng, còn layer `8` chỉ thay pose nhắm của thân trên.
- Khoảng nở hiển thị của tâm ngắm khi bắn được thu còn 50%, không thay đổi độ tản đạn thực tế.
- Nút xanh lá: nạp đạn.
- Khi reload, giữa màn hình hiện viền tròn màu vàng; một `Image` dùng `Radial360`
  fill theo tiến độ và tự ẩn khi hoàn tất. UI không có text và không chặn raycast.
- Nút cyan hình người cầm súng thấp: bật/tắt chế độ đi cẩn thận. Sau khi trang bị
  hoặc đổi súng, mặc định luôn là đi bộ bình thường.
- Nút hình con mắt/tâm ngắm bật/tắt camera góc nhìn thứ nhất của GC2 chỉ hiện khi Player
  đang có Shooter weapon. Chuyển sang melee sẽ tắt FPS, trả về TPS và ẩn nút; vì vậy mọi
  walk/jog/sprint trong melee đều chỉ dùng TPS. FPS dùng mount mắt ổn định đặt trên
  Animator root, không lấy dao động trực tiếp từ Head bone khi vừa chạy vừa ngắm/bắn.
- Lựa chọn FPS của Shooter được lưu qua `PlayerPrefs` bằng key
  `Franklin.Camera.OnFoot.Shooter.FirstPerson`; khi trang bị lại súng, lựa chọn trước đó
  được khôi phục. Melee luôn ưu tiên TPS bất kể preference FPS cũ của profile di chuyển.
- `Player.prefab` có child bắt buộc `ManagerCameraFPS`. Đây là owner duy nhất của profile
  FPS cho Shooter on-foot (kể cả walk/jog/sprint), Car và Bike; các luồng phương tiện
  không còn snapshot, orbit hoặc ghi cấu hình Camera Shot theo cách riêng. Inspector tập trung
  `Position` on-foot/Shooter, `Bike Position`, `Bike Shooter Position`, `Car Position`,
  `Car Rear Position`, FOV, Pitch/Yaw, giới hạn pitch/yaw, sensitivity, smooth time,
  near clip và tốc độ auto Alignment. `Install or Repair` tự tạo/sửa lại child này nếu thiếu.
- Trong Play Mode, thay đổi các giá trị trên `ManagerCameraFPS` được preview realtime khi
  một context FPS đang active. FOV, sensitivity, radius, clip, alignment và angular speed
  áp dụng ngay; Pitch/Yaw chỉ cập nhật đúng trục vừa sửa nên không reset orbit còn lại.
  Position/Fallback Position, Bike Position, Bike Shooter Position, Car Position và Car
  Rear Position được refresh một lần sau Animator; Car đồng thời recache cả forward/rear
  mount nên giữ đúng vị trí mới khi bật/tắt rear view. Nếu FPS đang tắt, giá trị mới được
  dùng ở lần bật kế tiếp. Chỉnh riêng Position trong lúc Bike ADS không hủy zoom FOV của Sight.
- FPS không tạo Camera/Shot mới và không tự cộng góc quay. Nó dùng chính
  `ShotTypeThirdPerson` đang active từ `Camera Shot.prefab`; input, pitch/yaw, smoothing,
  constraint và auto Alignment đều chạy bởi `ShotSystemThirdPerson` gốc của GC2. Input
  mobile chọn ngón orbit rảnh ở nửa phải và loại touch trên UI tại thời điểm bắt đầu; sau
  đó capture đúng pointer đến khi `press` thật sự kết thúc, nên dừng kéo hoặc đi qua ranh
  giới/UI không làm mất ownership. Một ngón vẫn có thể giữ Fire khi ngón thứ hai orbit.
  Trên Unity Device Simulator, khi đã có Touchscreen press thì binding Mouse của cùng cử
  chỉ không được phép chạy fallback lần hai; GC2 input và Alignment gate vì vậy luôn thấy
  cùng một owner. Panel HUD trong suốt không chặn orbit, chỉ UI có handler tương tác mới
  giữ touch của chính nó. Các nút ga/lái/phanh không khóa ngón orbit thứ hai; riêng Rear
  View tiếp tục giữ camera độc quyền trong thời gian button được hold.
- Khi ngón orbit còn chạm màn hình (kể cả đứng yên với delta bằng `0`), manager chỉ tạm
  tắt property `Alignment.AutoAlign`; nó không tự xoay camera. Sau khi ngón tay thực sự
  được thả, manager đợi `0.1s` rồi bật lại Alignment. Trên Car/Bike, pivot hướng chỉ lấy
  yaw ổn định của xe và dùng smooth time
  nhanh `0.18s`, nên Camera Shot trả nhanh về hướng xe mà không align giữa lúc người dùng
  còn orbit. Nếu người dùng bắt đầu orbit khi Alignment đang chạy, manager giữ rotation
  hiện tại và gọi `ShotSystemThirdPerson.SetRotation` đúng một lần để xóa quán tính
  SmoothDamp cũ trước khi GC2 nhận delta mới; vì vậy hai hướng không còn kéo ngược nhau.
- Khi Bike FPS đang aim/bắn, zoom FOV do GC2 Sight vẫn được giữ trong lúc hold. Sau
  `ExitSight`, Bike hủy đúng tween FOV hard-code của Sight rồi áp lại profile
  `ManagerCameraFPS`; camera đồng thời dùng `Bike Shooter Position`. Khi nhả Fire, camera
  trở lại `Bike Position`; pitch/yaw/orbit không bị reset và offset Aim TPS không còn sót.
- Khi bất kỳ profile FPS nào đang active, manager dùng API GC2
  `Character.Motion.AngularSpeed`: lưu đúng giá trị hiện tại, giữ ở `200000` kể cả khi
  state walk/run vừa ghi lại `1800`, rồi khôi phục giá trị đã lưu khi owner FPS cuối cùng
  nhả camera. `FranklinObjectDirectionToggle` vẫn dùng `UnitFacingObjectDirection` gốc để
  xoay toàn thân theo yaw camera; pitch chỉ thuộc camera.
- Khi FPS active ở Player movement, Shooter, Bike hoặc Car, `ManagerCameraFPS` acquire
  idle-variation suppression của `FranklinAnimationBridge`. Idle gesture đang chạy được
  blend-out ngay; base locomotion và pose ngồi xe vẫn tiếp tục. Khi tắt FPS, manager chỉ
  release owner của chính nó nên không mở idle nếu Shooter/Phone hoặc hệ thống khác vẫn
  còn giữ suppression.
- Khi bắt đầu vào Car/Bike, FPS mặt đất tự trả camera về shot trước đó rồi nhường đúng
  owner cho phương tiện. Nút camera và preference riêng của từng context vẫn được giữ,
  nhưng tất cả dùng chung manager/Camera Shot. Sau khi xuống xe, profile FPS mặt đất đúng
  với trạng thái vũ khí hiện tại được tự khôi phục; phone, weapon wheel và camera cutscene
  cũng được ưu tiên tương tự.
- Nút nắm đấm: unequip súng và chuyển về chế độ melee. Khi Player đang cầm súng
  hợp lệ trên Bike, nút này vẫn hiện bên trái Fire và có thể chuyển về melee ngay
  trên ghế; sau khi unequip, Shooter HUD tự ẩn và pose/IK Bike tiếp tục quản lý rider.
- Khi cầm súng, nút `Fight` và hai nút `Sidestep` của melee tự ẩn; `Jump` vẫn giữ nguyên.
- Khi cầm súng, các idle variation của Player bị khóa để không ghi đè Shooter stance; chuyển về melee thì tự mở lại.
- State `Shooter_Locomotion` chỉ chạy ở layer `7` trong lúc aim/bắn hoặc khi bật chế
  độ đi cẩn thận. Đây là bản sao nguyên trạng của asset GC2 gốc: full-body,
  tám hướng và `Speed Override` của Shooter. Cả M1911, UZI, AK-74, M4, Benelli M4,
  M249, M107 và RPG-7 đều liên kết trực tiếp tới cùng state này. Khi ngồi Bike, state
  full-body không được bật để animation ghế lái/hành khách vẫn sở hữu pelvis và chân.
- Tracer, muzzle flash, bullet impact và vụ nổ RPG dùng material
  `Universal Render Pipeline/Particles/Unlit` cục bộ, không còn dùng shader Built-in
  gây màu hồng trong URP.
- Ground, dốc và tường dùng chung một GPU-instanced bullet-decal batch với ring-buffer
  cố định 96 dấu, distance culling 36 m và không `Instantiate/Destroy` theo từng lỗ đạn.
  Decal xe vẫn ở batch riêng để bám đúng transform đang di chuyển.
- Particle `Dust` lấy từ `Hit_Gun` được dùng chung cho mọi súng thường qua pool của GC2
  (12 instance ban đầu, trả pool sau 1 giây). Child `Decal` 5 giây của prefab mẫu bị loại
  vì dấu đạn lâu dài đã do batch cố định xử lý; hit Character/Car/Bike không tạo bụi tường.
- Material model, vỏ đạn, projectile, laser và raycast nguồn trong
  `Shooter.Weapons@1.1.4` được bộ Repair tự nâng sang URP; màu gốc, metallic và
  smoothness được giữ lại. Vì vậy các reference GC2 còn dùng model nguồn cũng không
  hiện màu hồng.
- Shooter controls tự ẩn khi mở weapon menu hoặc khi mobile HUD đang bị khóa. Trên Bike,
  hệ thống dùng layout riêng theo ghế như phần dưới.
- Khi mở điện thoại, Shooter hủy yêu cầu bắn/ngắm, tạm unequip và ẩn đúng prop súng
  đang cầm. Sau khi animation cất điện thoại hoàn tất, hệ thống equip lại chính weapon
  và prop đó nên số đạn hiện tại được giữ nguyên. Nếu lúc restore Player đang lái Bike
  với súng nặng, cache điện thoại được chuyển sang cache Bike và chỉ restore khi xuống xe.
- Keyboard debug: `Tab`, `Esc`, `R`, phím `1` đến `8`.

## Damage Bike và Car

- Mỗi GC2 weapon có thêm `InstructionFranklinVehicleDamage` trong `On Hit`.
  Instruction tìm `FranklinBikeHealth` hoặc `SimcadeCarHealth` từ parent của collider
  trúng đạn, nên collider thân, bánh hoặc collider con đều trừ đúng health của xe.
- Damage cho mỗi projectile/pellet: M1911 `12`, UZI `5`, AK-74 `8`, M4 `8`,
  Benelli M4 `3` mỗi pellet, M249 `6`, M107 `35`, RPG-7 `50`.
- Người bắn không thể tự làm mất máu Bike/Car mà mình đang ngồi; xe khác vẫn nhận
  damage bình thường khi bắn từ ghế lái hoặc ghế sau.
- Collider của driver/passenger được nhận diện là Character hit và không bị quy đổi
  nhầm thành damage thân xe dù Character đang parent dưới seat của vehicle.
- Tích hợp này chỉ nối Shooter hit vào health sẵn có. Nó không tạo hoặc gọi trực tiếp
  deformation/destruction; hai hệ thống đó không nằm trong phạm vi thay đổi này.
- Installer luôn loại Instruction damage cũ rồi thêm lại đúng một bản, vì vậy chạy
  `Install or Repair` nhiều lần không làm một viên đạn bị tính damage lặp.

## Damage Character, headshot và Helmet

- Mỗi weapon có `InstructionFranklinCharacterDamage` trong `On Hit`. Instruction tìm
  `Character` từ collider trúng đạn rồi trừ Attribute GC2 Traits có ID `hp`; collider
  con trên model hoặc bone vẫn gây damage cho đúng Character.
- Headshot ưu tiên collider/bone có tên `Head`, `Skull` hoặc `Helmet`, sau đó đối chiếu
  hit point thật với Humanoid Head bone. Vì vậy hệ thống vẫn nhận headshot khi prefab
  chỉ có một collider Character bao toàn thân.

| Weapon | Body | Head multiplier | Head damage | Armor absorbs |
| --- | ---: | ---: | ---: | ---: |
| M1911 | 25 | x3 | 75 | 80% |
| UZI | 12 | x3 | 36 | 65% |
| AK-74 | 30 | x3.5 | 105 | 50% |
| M4 Carbine | 28 | x3.75 | 105 | 48% |
| Benelli M4 | 14/pellet | x2 | 28/pellet | 70% |
| M249 | 22 | x4.5 | 99 | 45% |
| M107 | 100 | x2 | 200 | 20% |
| RPG-7 | 100 | x2 | 200 | 10% |

- Với mục tiêu có `100 hp`, M107/RPG-7 hạ bằng một hit thường; AK-74/M4 hạ bằng một
  headshot. Benelli tính riêng từng pellet nên tổng damage phụ thuộc số pellet trúng.
- `Player.prefab` đang liên kết đúng `Helmet_01.prefab`. Headshot đầu tiên khi Helmet
  đang nằm trên Head chỉ nhận `25%` headshot damage, sau đó nón tách khỏi Head, bật
  collider/Rigidbody, nhận lực theo hướng viên đạn và tự hủy sau 8 giây. Những phát
  sau không còn được Helmet giảm damage; người chơi vẫn có thể đội lại một instance
  Helmet mới bằng nút Helmet.
- Damage Character và damage Vehicle là hai instruction độc lập: bắn người đang ngồi
  trên Bike/Car chỉ trừ `hp` của Character, không trừ nhầm health của xe.

### Armor runtime và API

- `FranklinArmor` là reserve giáp độc lập gắn trên GC2 `Character`. Nếu giáp lớn hơn
  `0`, tỷ lệ trong cột `Armor absorbs` được trừ khỏi damage máu; mỗi điểm damage được
  chặn luôn tiêu hao đúng một điểm giáp. Giáp không bao giờ giảm damage miễn phí. Khi
  giáp không đủ, phần còn thiếu đi thẳng vào Traits `hp`.
- Thứ tự xử lý là `headshot -> Helmet -> Armor -> hp`. Helmet chỉ giảm phát headshot
  đầu và bay khỏi đầu; Armor sau đó vẫn phải tiêu hao nếu còn điểm.
- `Player.prefab` có sẵn `100/100` Armor. Số `100` và thanh xanh trên HUD đã liên kết
  event runtime, không còn là nội dung tĩnh.
- API có thể nhận Character root hoặc bất kỳ GameObject con nào:

```csharp
FranklinArmorAPI.Equip(character.gameObject, 100f, 100f);
FranklinArmorAPI.AddArmor(character.gameObject, 25f);
FranklinArmorAPI.SetArmor(character.gameObject, 50f);
float current = FranklinArmorAPI.GetCurrentArmor(character.gameObject);
FranklinArmorAPI.RemoveArmor(character.gameObject);
```

- Có thể subscribe trực tiếp `FranklinArmor.EventArmorChanged`,
  `EventDamageAbsorbed` và `EventArmorDepleted` cho pickup, shop, âm thanh hoặc effect.

## Bắn súng trên Bike

- Thứ tự state cố định: pose ngồi lái/ngồi sau ở layer `1`, state cầm súng ở layer
  `7`, sight/aim/bắn của GC2 ở layer `8`. Upper-body mask của Shooter giữ nguyên
  chân, bàn chân và pose ngồi do Bike quản lý.
- Trước khi animation lên ghế lái bắt đầu, Shooter hủy aim/fire và tắt
  `Object Direction`. Nếu đang cầm súng nặng, hệ thống tạm unequip và ẩn đúng prop
  runtime để state hai tay của súng không đè lên animation lên xe. Sau khi animation
  xuống Bike hoàn tất, đúng weapon/prop đó được restore và số đạn vẫn được giữ nguyên.
- Nếu quá trình lên Bike bị hủy do không thể tiếp cận hoặc dựng xe, súng nặng được
  restore ngay khi quá trình hủy hoàn tất. Sau va chạm văng khỏi xe, súng chỉ restore
  sau khi Player đã kết thúc ragdoll recovery; Player chết thì súng vẫn được giữ ẩn.
- Người lái chỉ dùng được `M1911` và `UZI`. Tay trái tiếp tục bị IK khóa vào
  ghi-đông; state ngồi lái dùng mask `Franklin Bike Driver Seat And Left Hand` để
  loại `RightArm`, `RightFingers` và `RightHandIK`. Vì vậy tay phải và model súng
  chỉ đi theo pose Shooter layer 7/8, không còn bị clip lái kéo đồng thời về
  ghi-đông. Những ô súng nặng bị khóa và làm mờ trong weapon wheel khi đang lái.
- Người ngồi sau dùng được toàn bộ tám khẩu súng. IK hai tay được nhả cho Shooter,
  còn IK hai chân và lower-body pose vẫn do ghế hành khách giữ.
- Khi ngồi Bike, bắn không bật `Object Direction` vì Character root đang parent vào
  seat. Hướng bắn/aim do Shooter sight xử lý, tránh xoay lệch người khỏi yên xe.
- Khi Wheelie hoặc Burnout bắt đầu, Shooter hủy Fire/ADS rồi tạm unequip và ẩn đúng
  weapon prop đang cầm để hai tay hoàn toàn thuộc pose stunt. Khi tất cả stunt input
  đã nhả, hệ thống equip lại chính weapon/prop đã cache nên magazine và ammo không đổi.
  Exit, hết xăng, Bike damage-lock hoặc driver bị disable đều chủ động kết thúc trạng
  thái này để không làm mất súng.
- Khi giữ Fire để vào sight trên Bike, Third Person Camera Shot blend thêm shoulder
  `+0.55`, đẩy camera sang phải để Player/Bike nằm lệch trái và không che tâm ngắm.
  Thả Fire, đổi ghế hoặc xuống Bike sẽ blend shoulder về đúng giá trị Bike ban đầu.
- Ghế lái không hiện nút Reload. Bắn hết viên cuối chỉ làm băng đạn về 0; lần giữ
  Fire kế tiếp mới bắt đầu reload. Nếu vẫn giữ Fire, sau khi reload xong hệ thống
  chờ pose ngắm sẵn sàng rồi mới bắn tiếp. Trong lúc reload, lái trái/phải bị khóa;
  ga và phanh vẫn hoạt động. Ghế sau vẫn hiện nút Reload và reload thủ công bình thường.
- Trên HUD Bike, Fire dùng vị trí cũ của Helmet; nút Melee nằm ngay bên trái Fire,
  còn Helmet nằm bên trái Exit Vehicle. Brake được hạ thấp để không chồng nút khác.
  Ghế sau ẩn ga, phanh, lái, wheelie,
  burnout, đèn và helmet; chỉ giữ nút thoát xe cùng bộ điều khiển súng. Nút thoát gọi
  trực tiếp đúng passenger seat.

## Sửa chữa asset

Chạy `Tools > Franklin Game > Shooter System GC2 > Install or Repair`.
Installer an toàn khi chạy lặp lại và không tạo file mới ra ngoài
`Assets/ShooterSystemGC2`. Nó chỉ sửa các reference/material/Sight nguồn GC2 mà weapon
runtime đang dùng để bảo đảm URP và giữ ổn định rig lúc bắn.

## Cấu trúc

- `Runtime`: catalog, menu chọn súng, tap HUD, touch controls, manager FPS chung và
  Instruction damage xe.
- `Editor`: installer tạo catalog/GC2 weapon assets.
- `Resources/FranklinShooter`: asset runtime, UI ImageGen, upper-body mask, Shooter
  locomotion và bộ `Materials`/`Effects` URP cục bộ.
- `WeaponsLow`: Models, Prefabs, material và texture gốc với `.meta` được giữ nguyên.

Scene mẫu và lighting demo của Weapons Low đã bị loại bỏ vì không cần cho runtime. Toàn
bộ model súng và phụ kiện gốc vẫn được giữ.
