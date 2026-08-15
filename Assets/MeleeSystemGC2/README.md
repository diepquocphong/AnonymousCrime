# Franklin Melee System (Game Creator 2)

## Tổng quan

Hệ thống melee này dùng module **Game Creator 2 Melee** và animation từ
**Fighting Animset Pro v1.0**. Mọi asset, code và tài liệu được tạo cho phần melee
đều nằm trong `Assets/MeleeSystemGC2`.

Combo không dùng clip animation ghép sẵn. Mỗi node của ComboTree GC2 chỉ phát một
animation đơn; việc nối đòn, buffer input và chuyển sang đòn kế tiếp do GC2 Melee xử lý.

## Bộ đòn hiện tại

| Skill | GC2 input | Animation đơn | Striker | Trừ HP |
| --- | --- | --- | --- | ---: |
| 1 | A | `KB_p_Jab_L_1` | tay trái | 7 |
| 2 | B | `KB_p_Jab_R_1` | tay phải | 8 |
| 3 | C | `KB_p_Hook_L` | tay trái | 11 |
| 4 | D | `KB_p_MidKickFront_R` | chân phải | 17 |
| 5 | E | `KB_p_Hook_R` | tay phải | 12 |
| 6 | F | `KB_p_Uppercut_L` | tay trái | 14 |
| 7 | G | `KB_p_Uppercut_R` | tay phải | 15 |
| 8 | H | `KB_p_MidKickFront_L` | chân trái | 16 |

Controller không chạy thứ tự A→H cố định. Mỗi lần bấm Fight, nó chọn ngẫu nhiên trong
nhóm Skill đang có số lần sử dụng thấp nhất. Skill vừa dùng bị loại khỏi lượt kế tiếp
nếu còn bất kỳ Skill hợp lệ nào khác. Do đó tám Skill được phân phối cân bằng theo từng
vòng, không lặp ngay hai lần liên tiếp và các Skill ít/chưa dùng luôn được ưu tiên.

Một lần bấm vẫn được tính là đã dùng dù đòn đánh hụt hoặc Player đã trở lại Walk. Bộ
đếm chỉ tồn tại trong phiên runtime, không ghi vào asset và được chuẩn hóa sau mỗi vòng
để tránh số đếm tăng vô hạn.

Các clip như `KB_p_OneTwo`, `KB_p_DoubleJab`, `KB_p_Jab_LR_combo`,
`KB_m_MidKick_LL_2_combo` và các clip nhiều đòn tương tự không được tham chiếu bởi
bất kỳ Skill nào của hệ thống này.

## Luồng hoạt động

1. `Player.prefab` khởi tạo `FranklinMeleeController`.
2. Controller equip `Franklin Unarmed Weapon.asset` vào GC2 Character khi bắt đầu,
   nhưng chưa bật combat locomotion.
3. Nút `Fight` trong `CanvasPlayerControl.prefab` gọi
   `FranklinFightButton.Press()`.
4. Controller dùng bộ chọn least-used để gửi một key từ `MeleeKey.A` đến `MeleeKey.H`
   vào `MeleeStance` của GC2.
5. `Franklin Unarmed Combos.asset` có tám root `AnyTime`; GC2 chọn Skill khớp với
   input hiện tại, kể cả khi đòn trước không trúng mục tiêu.
6. Khi Skill vào Strike phase, chỉ striker có ID tương ứng được bật.
7. Khi trúng Character có `Traits`, Skill dùng GC2 Stats instruction để trừ Attribute
   `HP`.

Tốc độ anticipation/strike/recovery của toàn bộ Skill là `0.60 / 0.4667 / 1.00`, bằng
đúng một phần ba cấu hình cũ `1.80 / 1.40 / 3.00`. Input buffer được tăng theo cùng tỷ
lệ từ `0.55` lên `1.65` giây để GC2 không làm rơi input combo khi animation kéo dài.
Mỗi lần bấm Fight khởi động lại watchdog thời gian thực `3.3` giây (không bị ảnh hưởng
bởi `Time.timeScale`); nếu GC2 vẫn còn ở phase
đánh khi hết thời gian này, controller gọi `ForceCancel()` để
Player trở lại locomotion/Walk. Vì vậy state đánh không còn bị giữ gần 5 giây.

Thuộc tính `Global Skill Speed` nằm ngay dưới `Attack Skills` trên
`FranklinMeleeController` và điều khiển chung cả tám Skill:

- `1.0`: tốc độ hiện tại đã giảm ba lần;
- nhỏ hơn `1.0`: toàn bộ Skill chậm hơn;
- lớn hơn `1.0`: toàn bộ Skill nhanh hơn;
- phạm vi Inspector là `0.1–3.0`.

Controller nhân đồng bộ anticipation, strike và recovery với giá trị này. Input buffer,
attack easing và watchdog được chia theo cùng multiplier để phase, hit timing và thời
điểm trở lại Walk của GC2 không lệch nhau. Giá trị đổi khi đang Play có hiệu lực từ đòn
tiếp theo; code khác cũng có thể gán qua property `GlobalSkillSpeed`.

`Use Global Skill Trail` là công tắc tổng cho trail của cả tám Skill. Khi tắt, mọi
trail đấm/đá đều bị vô hiệu hóa; khi bật lại, controller khôi phục đúng trạng thái trail
được cấu hình sẵn trong từng Skill. Có thể đổi lúc runtime qua property
`UseGlobalSkillTrail`. Tắt trail cũng giảm phần cập nhật và dựng mesh trail trên mobile.

`Attack Movement Speed` nằm ngay dưới `Global Skill Speed`, mặc định là `0.5`. Khi
GC2 ở một trong ba phase Anticipation, Strike hoặc Recovery, tốc độ di chuyển
`Character.Motion.LinearSpeed` được giữ ở `0.5`. Controller lưu tốc độ trước khi đánh
và khôi phục nó khi trở lại Walk, chuyển sang Reaction, bị ForceCancel hoặc component
bị tắt. Bấm combo liên tục không ghi đè mất tốc độ gốc đã lưu.

Việc chọn và nối đòn vẫn do `MeleeStance`, `ComboSelector` và `ComboTree` của GC2 xử
lý; controller chỉ cung cấp input A–H và chốt thời gian thoát an toàn.

### Tương thích khi equip súng

Ngay khi `FranklinShooterSystem` bắt đầu đổi sang một `ShooterWeapon`, hệ thống gọi
`FranklinMeleeController.CancelAllMeleeStates()`. Lệnh này hủy ngay attack/reaction
phase và input buffer, combat locomotion layer `1`, sidestep Dash, các coroutine ease,
giới hạn tốc độ melee cùng toàn bộ gesture đấm, đá, reaction và sidestep. Transition
được đặt `0`, nên state melee không chờ animation kết thúc trước khi nhường cho súng.

Controller cũng nghe `Character.Combat.EventEquip` để xử lý súng được equip từ GC2
Visual Scripting hoặc hệ thống khác. Trong khi còn `ShooterWeapon` đang trang bị,
`Fight()` và lách trái/phải đều bị chặn, kể cả khi được gọi từ code thay vì UI.

## Unarmed Combat Locomotion State

`States/Franklin Unarmed Combat Locomotion.asset` là GC2
`StateBasicLocomotion` riêng cho tư thế không vũ khí. State dùng các clip đơn từ
`KB_Movement.fbx` theo tám hướng:

| Hướng | Animation |
| --- | --- |
| Đứng yên | `KB_Idle_1` |
| Tiến / lùi | `KB_WalkFwd1` / `KB_WalkBwd` |
| Phải / trái | `KB_Sidestep_R` / `KB_Sidestep_L` |
| Chéo trước phải / trái | `KB_WalkRight45` / `KB_WalkLeft45` |
| Chéo sau phải / trái | `KB_WalkRight135` / `KB_WalkLeft135` |

Field `State` của `Franklin Unarmed Weapon.asset` được giữ `None`, vì vậy việc equip
weapon không tự bật combat locomotion. Chỉ `FranklinMeleeController.Fight()` mới bật
state này ở layer `1`. Mỗi lần bấm Fight reset timer không hoạt động `2` giây. Khi hết
timer, controller chờ đòn hiện tại kết thúc nếu cần rồi blend state ra và trở lại Walk.

Walk mặc định là locomotion nền ở layer `-1`. Jog và Sprint dùng chung layer `2`, cao
hơn Unarmed Combat Locomotion layer `1`; vì vậy khi Player Jog/Sprint, state di chuyển
nhanh được ưu tiên. Ngay khi layer Jog/Sprint hoạt động, controller dừng hoàn toàn
Unarmed Combat Locomotion, hủy timer hai giây đang chờ và nhả ngay giới hạn
`Attack Movement Speed = 0.5`. Controller không khôi phục tốc độ Walk cũ trong nhánh
này; nó giữ nguyên tốc độ Jog/Sprint vừa được `FranklinAnimationBridge` chọn. State
combat không tự bật lại khi Jog/Sprint kết thúc; người chơi phải bấm Fight lần mới.
Các Skill đánh ở layer `6` vẫn có ưu tiên cao nhất. State không override thuộc tính tốc
độ locomotion, nên tốc độ Walk/Run bình thường vẫn giữ nguyên. Trong lúc đánh, tám
Skill/Gesture tiếp tục overlay lên locomotion và
`FranklinMeleeController` mới giới hạn tốc độ ở `Attack Movement Speed` (`0.5`).

Shooter giữ thứ tự GC2 ở layer `7`, Aim ở `8` và Reload ở `9`, nhưng state giữ súng
cục bộ dùng upper-body AvatarMask và không override Speed. Vì vậy các layer Shooter cao
hơn không phủ Root/chân hoặc ép tốc độ, còn Jog/Sprint layer `2` vẫn chạy đúng ở thân dưới.

## Lách trái / lách phải

Hai nút `Sidestep Left` và `Sidestep Right` được đặt cạnh nút Fight trong
`CanvasPlayerControl.prefab`. Mỗi lần bấm gọi trực tiếp
`FranklinMeleeController.SidestepLeft()` hoặc `SidestepRight()` và dùng cơ chế
`Character.Dash` chuẩn của GC2, không tự dịch `Transform`. Vì vậy lách tự tuân theo
điều kiện grounded, số lần dash liên tiếp và `Dash Cooldown` đang cấu hình trên GC2
Character.

Hai nút lách mặc định bị ẩn và chỉ hiện khi toggle `Object Direction` thực sự đổi
`Character.Facing` sang `UnitFacingObjectDirection`. Khi Object Direction tắt, hệ
thống khác thay Facing, nhóm điều khiển on-foot bị tắt hoặc component UI bị disable,
hai nút lách được ẩn ngay. `FranklinSidestepVisibility` nghe trực tiếp
`CharacterKernel.EventChangeFacing`; sau lúc tìm thấy Player, nó không polling mỗi
frame nên phù hợp mobile và không phụ thuộc màu hiển thị của nút toggle.

Động tác dùng hai clip đơn `KB_Sidestep_L` / `KB_Sidestep_R` từ
`Animations/KB_Movement.fbx`. Mặc định Player di chuyển ngang với velocity `2`, trong
`0.30` giây, blend in/out `0.08 / 0.12` giây và có cửa sổ invincibility `0.16` giây.
Trong cửa sổ này, GC2 nhận lách thành một Dodge thật (`Dash.IsDodge`) và vẫn gọi luồng
`On Dodge` của weapon khi né thành công.

Các thuộc tính tổng nằm trong group `Sidestep Dodge` của
`FranklinMeleeController`: clip trái/phải, velocity, duration, gravity,
invincibility, animation speed và transition. Tốc độ mặc định
`Sidestep Animation Speed = 0.5`, bằng một nửa tốc độ khớp clip ban đầu; multiplier
lớn/nhỏ hơn `1` cho phép chỉnh cảm giác animation mà không đổi khoảng lách.

Lách chỉ bắt đầu khi GC2 melee đang ở phase `None`. Nút sẽ bị bỏ qua trong
Anticipation, Strike, Recovery hoặc Reaction, nhờ đó không cắt animation đánh, hit
timing hay reaction. Khi lách hợp lệ, combat locomotion được bật/reset timer hai giây;
GC2 khóa chân trong đúng thời gian Dash rồi tự nhả. Hai nút dùng input một lần qua
`Button.onClick`, không chạy polling mỗi frame.

## Âm thanh melee

Mỗi Skill được gán năm nhóm âm thanh qua `SkillEffects` chuẩn của GC2:

- `Sound Use`: effort voice ngắn, phát ngay khi bắt đầu tung đòn;
- `Sound Strike`: một swing riêng, chỉ phát khi đòn bước vào phase Strike;
- `Sound Hit`: một body impact riêng, chỉ phát sau khi Striker xác nhận trúng mục tiêu;
- `Sound Blocked`: dùng luân phiên bốn clip blocked;
- `Sound Parried`: dùng biến thể blocked kế tiếp để không trùng ngay với âm block.

Profile hiện tại ưu tiên cảm giác đời thực nhẹ: tám swing dùng foley chuyển động vải
`foley_cloth_light_fast_movement`, còn impact/block dùng
`foley_cloth_sports_glove_catch` từ `Punch Sound Pack`. Các clip heavy, heavy-long,
body-impact sâu và blocked kiểu game không còn được dùng. Vì GC2 chỉ gọi `Sound Hit`
sau khi hit hợp lệ, đánh hụt chỉ có tiếng chuyển động nhẹ và không phát impact giả.

Sáu clip `voice_male_effort_grunt_01–06` trong cùng pack được phân bổ cho tám Skill.
Đây là effort voice rất ngắn (`0.18–0.32` giây), không dùng nhóm pain/death; hệ thống
chọn Skill ít dùng cũng giúp voice không bị lặp liên tục.

Pain voice được xử lý ở Character nhận đòn bằng `FranklinDamagePainAudio`, không phát
từ Skill của người đánh. Component nghe trực tiếp `Traits.RuntimeAttributes.EventChange`,
lọc Attribute `HP` và chỉ phát khi `LastChange < 0`. Vì vậy melee hit vừa gây damage vừa
vào Reaction chỉ phát một pain voice; heal hoặc thay đổi Attribute khác không phát.
Tám clip pain ngắn nhất (`0.28–0.42` giây) được chọn không lặp liên tiếp, volume mặc định
`0.55` và cooldown `0.22` giây để damage nhiều lần trong một frame không chồng tiếng.

Toàn bộ 34 one-shot được giữ trong `Audio/`, force mono nhưng tắt normalize để giữ âm
lượng foley tự nhiên, Vorbis quality `0.45`, `Optimize Sample Rate` và
`Decompress On Load`. Cấu hình này giảm dung lượng build và tránh chi phí giải mã giữa
lúc giao chiến trên mobile; các clip đều ngắn và được preload.

## Quad Ease In/Out tổng

`FranklinMeleeController` giữ một cấu hình dùng chung cho toàn bộ tám Skill trong group
`Global Melee Animation Ease`:

- `Use Animation Ease`: bật hoặc tắt easing cho toàn bộ melee;
- `Animation Ease`: loại đường cong, mặc định `QuadInOut`;
- `Ease In Duration`: thời gian tăng tốc đầu đòn, mặc định `0.30` giây;
- `Ease Out Duration`: thời gian hạ tốc cuối đòn, mặc định `0.42` giây;
- `Ease Edge Speed Multiplier`: hệ số tốc độ tại hai đầu, mặc định `0.60`.

Controller tham chiếu danh sách tám Skill theo thứ tự A–H. Khi GC2 bắt đầu gesture,
tốc độ animation được nội suy từ hệ số biên lên tốc độ phase hiện tại rồi hạ xuống bằng
đường cong `QuadInOut`. Thay đổi các thuộc tính trên một controller sẽ tác động lên tất
cả đòn melee của Player đó, không cần sửa từng Skill asset.

## Hit reaction

`Franklin Hit Reactions.asset` là GC2 `MeleeReaction` dùng các clip đơn trong
`KB_Hits.fbx`. Reaction được chọn theo hướng đòn đi vào Character:

- phía trước: biến thể `MidFront`;
- bên trái: biến thể `MidLeft`;
- bên phải: biến thể `MidRight`;
- phía sau: biến thể `HighBack`;
- hướng còn lại dùng reaction trước mặt làm fallback.

Đòn có Power từ `7` trở lên dùng animation `Stagger`; đòn nhẹ dùng các biến thể
`Weak` và tự đổi giữa các clip độc lập để reaction bớt lặp. Reaction profile được gán
làm reaction mặc định cho cả `Player.prefab` và `NPC.prefab`.

`FranklinMeleeController` có group `Global Melee Hit Reaction Ease` để cấu hình tổng
cho reaction của Player:

- `Use Reaction Ease` và `Reaction Ease`, mặc định `QuadInOut`;
- `Reaction Ease In Duration`, mặc định `0.08` giây;
- `Reaction Ease Out Duration`, mặc định `0.12` giây;
- `Reaction Edge Speed Multiplier`, mặc định `0.65`;
- `Global Reaction Speed`, mặc định `1.0`, phạm vi `0.1–3.0`.

`Global Reaction Speed` đồng bộ tốc độ clip, thời lượng phase reaction của GC2,
transition và thời gian Ease In/Out. Giá trị `0.5` làm reaction chậm còn một nửa,
`2.0` làm reaction nhanh gấp đôi. Có thể thay đổi lúc chạy bằng property
`FranklinMeleeController.GlobalReactionSpeed`; giá trị mới áp dụng đầy đủ từ reaction
kế tiếp.

Player xử lý reaction ease trực tiếp trong `FranklinMeleeController`, nên không có
component reaction thứ hai trên Player. `FranklinMeleeReactionEase` chỉ là driver nội bộ
cho NPC và các field cấu hình của nó được ẩn. Toàn bộ cấu hình reaction của Player chỉ
chỉnh tại group `Global Melee Hit Reaction Ease` trên `FranklinMeleeController`.

## Tối ưu mobile

Installer áp dụng profile mobile chung cho melee:

- easing của attack và reaction cập nhật ở `30 Hz` thay vì chạy phép nội suy ở mọi
  frame; có thể chỉnh `Ease Updates Per Second` trên `FranklinMeleeController` từ
  `15–60`;
- GC2 `Character`, `MeleeStance`, `Args`, tốc độ và thời lượng clip được cache để giảm
  lookup và cấp phát trong lúc đánh;
- watchdog dùng vòng thời gian thực không tạo `WaitForSecondsRealtime` mới cho mỗi đòn;
- reaction chỉ chạy coroutine khi Character thực sự ở phase `Reaction`, đồng thời cache
  sẵn thời lượng của 12 clip;
- 34 one-shot melee được import mono, nén Vorbis và giải nén khi load để tránh decode
  đột ngột trong lúc đánh;
- Striker dùng một prediction và chỉ quét layer `Default (0)` cùng layer Character model
  `11`, giảm số collider phải kiểm tra;
- lách dùng motion transient, cooldown, legs-busy và invincibility có sẵn của GC2;
  controller không thêm physics query hoặc coroutine cập nhật vị trí riêng;
- bốn FBX animation dùng `Optimal Compression`, tắt material, camera, light, blendshape,
  Read/Write và bật Optimize Game Objects;
- sprite Fight và hai sprite Sidestep giới hạn `256 × 256`, không mipmap và dùng
  `ASTC 6×6` trên Android/iOS.

Các tối ưu này không thay đổi tám Skill, damage, thứ tự A–H hoặc lựa chọn reaction.

## Asset và cấu trúc

- `Animations/KB_Punches.fbx`: nhóm animation đấm đã import từ Fighting Animset Pro.
- `Animations/KB_Kicks.fbx`: nhóm animation đá đã import từ Fighting Animset Pro.
- `Animations/KB_Hits.fbx`: các animation reaction đơn từ Fighting Animset Pro.
- `Animations/KB_Movement.fbx`: idle và di chuyển tám hướng cho unarmed combat State.
- `Audio/Swings`: tám âm vung tay/chân, một biến thể cho mỗi Skill.
- `Audio/Impacts`: tám âm va chạm cơ thể, một biến thể cho mỗi Skill.
- `Audio/Blocks`: bốn biến thể dùng cho block và parry.
- `Audio/Voices`: sáu effort voice ngắn dùng lúc bắt đầu tung đòn.
- `Audio/Pain`: tám pain voice ngắn dùng khi GC2 Traits HP giảm.
- `Skills/`: tám GC2 Melee Skill, mỗi asset tham chiếu đúng một clip đơn.
- `Reactions/Franklin Hit Reactions.asset`: GC2 reaction theo hướng và Power.
- `States/Franklin Unarmed Combat Locomotion.asset`: GC2 locomotion tám hướng được
  bật bằng nút Fight và tự tắt sau hai giây không có input đánh.
- `UI/sidestep-left.png`, `UI/sidestep-right.png`: icon lách trái/phải đồng bộ bộ nút
  mobile hiện tại; hai button được installer nhúng cạnh Fight.
- `Franklin Unarmed Combos.asset`: ComboTree tám root A–H ở chế độ `AnyTime`; key được
  chọn bằng thống kê sử dụng runtime thay vì thứ tự cố định.
- `Franklin Unarmed Weapon.asset`: GC2 MeleeWeapon được Player tự equip.
- `Striker.prefab`: prefab GC2 Striker mẫu được spawn thành bốn nested-prefab trên
  hai bàn tay và hai bàn chân của Player.
- `Runtime/FranklinMeleeController.cs`: equip weapon, quản lý combat locomotion theo
  Fight input, ưu tiên Skill ít sử dụng, áp dụng Quad Ease In/Out tổng và watchdog
  thoát state đánh.
- `Runtime/FranklinFightButton.cs`: cầu nối từ Canvas prefab tới Player đang hoạt động.
- `Runtime/FranklinMeleeReactionEase.cs`: driver QuadInOut nội bộ dành cho reaction NPC.
- `Editor/FranklinMeleeInstaller.cs`: công cụ cài lại/refresh asset và prefab.
- `UI/fight-button.png`: sprite Fight nền trong suốt dùng trong Canvas.
- `UI/Source/fight-button-chroma.png`: ảnh nguồn ImageGen trước khi tách nền.

## Tích hợp prefab

Khi chạy `Install or Refresh`, installer spawn bốn instance từ `Striker.prefab`, reset
local transform về tâm bone và gán cấu hình riêng. `Assets/Prefab/Player.prefab` được
cập nhật với:

- `FranklinMeleeController` trên root;
- nested prefab `Melee Striker - Left Hand`, ID `franklin-left-hand`, bán kính `0.22`,
  dưới bone `hand.l`;
- nested prefab `Melee Striker - Right Hand`, ID `franklin-right-hand`, bán kính
  `0.22`, dưới bone `hand.r`;
- nested prefab `Melee Striker - Left Foot`, ID `franklin-left-foot`, bán kính `0.27`,
  dưới bone `foot.l`;
- nested prefab `Melee Striker - Right Foot`, ID `franklin-right-foot`, bán kính
  `0.27`, dưới bone `foot.r`.

Installer tự xóa `Striker` component kiểu cũ gắn trực tiếp trên bone trước khi spawn,
nhằm tránh một chi có hai vùng đánh trùng nhau. Nếu chạy refresh nhiều lần, instance
có cùng tên sẽ được tái sử dụng thay vì tạo bản sao mới.

`Assets/Prefab/CanvasPlayerControl.prefab` đã có nút `Fight` trong group
`Franklin On Foot Controls`, anchor góc phải dưới, vị trí `(-310, 175)` và kích thước
`185 × 185`.

`Assets/Prefab/NPC.prefab` được gán cùng `Franklin Hit Reactions.asset` và có
`FranklinMeleeReactionEase`, vì vậy NPC phản ứng theo hướng/Power khi trúng đòn của
Player. Cả Player và NPC có `FranklinDamagePainAudio` theo dõi HP trong GC2 Traits.

## Yêu cầu đối với mục tiêu

Để nhận damage đầy đủ, mục tiêu nên có:

- GC2 `Character`;
- GC2 Stats `Traits` chứa Attribute `HP`;
- collider trên cùng GameObject với `Character` để GC2 Striker nhận đúng target.

NPC prefab hiện có của project đã dùng cùng `Thief` Class/HP setup với Player nên phù
hợp với instruction trừ HP được cấu hình trong các Skill.

## Cài lại hoặc chỉnh sửa

Trong Unity chọn:

`Tools > Franklin Game > Melee GC2 > Install or Refresh`

Lệnh này có tính lặp lại an toàn: nó refresh tám Skill, hit reaction, ComboTree,
Weapon, striker, controller và nút Fight mà không tạo bản sao.

Muốn đổi nhịp combo, mở từng asset trong `Skills/` và chỉnh phase/cancel window trong
Melee Sequence của GC2. Muốn đổi thứ tự đòn, mở `Franklin Unarmed Combos.asset` bằng
Combo editor của GC2.

## ImageGen

Nút Fight được tạo bằng ImageGen built-in với prompt chính:

> Create a polished mobile game HUD combat button with a bold white clenched-fist
> pictogram, circular dark graphite plate and restrained cyan-blue rim accents. Centered,
> front-facing, readable at small size, no text or watermark, on a perfectly flat solid
> #00ff00 chroma-key background with no shadow, gradient, texture or reflection.

Nguồn chroma-key được tách nền cục bộ bằng soft matte và despill. File cuối là PNG có
alpha, kích thước gốc `1254 × 1254`; Unity importer giới hạn texture runtime ở `512` và
tắt mipmap cho UI.
