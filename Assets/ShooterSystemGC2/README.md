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
- Với súng `Single`, tiếp tục giữ Fire sẽ tự nhả/kích hoạt cò lại theo đúng fire rate
  của từng súng. Vòng lặp dừng khi thả Fire hoặc hết băng; hết băng vẫn cần một lần
  nhấn Fire mới để bắt đầu reload.
- Khi bắn, Player tự chuyển sang `Object Direction`. Bộ đếm được reset theo từng viên
  đạn thực tế; sau viên cuối 5 giây không bắn, hướng tự trở về `Pivot`.
- Recoil camera theo cả trục ngang và dọc đã giảm 50% so với weapon template gốc.
- Khoảng nở hiển thị của tâm ngắm khi bắn được thu còn 50%, không thay đổi độ tản đạn thực tế.
- Nút xanh lá: nạp đạn.
- Khi reload, giữa màn hình hiện viền tròn màu vàng; một `Image` dùng `Radial360`
  fill theo tiến độ và tự ẩn khi hoàn tất. UI không có text và không chặn raycast.
- Nút cyan hình người cầm súng thấp: bật/tắt chế độ đi cẩn thận. Sau khi trang bị
  hoặc đổi súng, mặc định luôn là đi bộ bình thường.
- Nút nắm đấm: unequip súng và chuyển về chế độ melee.
- Khi cầm súng, nút `Fight` và hai nút `Sidestep` của melee tự ẩn; `Jump` vẫn giữ nguyên.
- Khi cầm súng, các idle variation của Player bị khóa để không ghi đè Shooter stance; chuyển về melee thì tự mở lại.
- State giữ súng chạy ở layer `7` nhưng dùng `Franklin Shooter Upper Body` mask: chỉ
  thân trên, hai tay, ngón tay và Hand IK nhận pose súng. Root, hai chân và Foot IK vẫn
  do Walk/Jog/Sprint điều khiển. State này cũng tắt `Speed Override`, nên không ép tốc
  độ về Walk `2` khi Jog/Sprint đang đặt tốc độ `4/6`.
- Tracer, muzzle flash, bullet impact và vụ nổ RPG dùng material
  `Universal Render Pipeline/Particles/Unlit` cục bộ, không còn dùng shader Built-in
  gây màu hồng trong URP.
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
- Khi giữ Fire để vào sight trên Bike, Third Person Camera Shot blend thêm shoulder
  `+0.55`, đẩy camera sang phải để Player/Bike nằm lệch trái và không che tâm ngắm.
  Thả Fire, đổi ghế hoặc xuống Bike sẽ blend shoulder về đúng giá trị Bike ban đầu.
- Ghế lái không hiện nút Reload. Bắn hết viên cuối chỉ làm băng đạn về 0; lần giữ
  Fire kế tiếp mới bắt đầu reload. Nếu vẫn giữ Fire, sau khi reload xong hệ thống
  chờ pose ngắm sẵn sàng rồi mới bắn tiếp. Trong lúc reload, lái trái/phải bị khóa;
  ga và phanh vẫn hoạt động. Ghế sau vẫn hiện nút Reload và reload thủ công bình thường.
- Trên HUD Bike, Fire dùng vị trí cũ của Helmet; Helmet nằm bên trái Exit Vehicle;
  Brake được hạ thấp để không chồng nút khác. Ghế sau ẩn ga, phanh, lái, wheelie,
  burnout, đèn và helmet; chỉ giữ nút thoát xe cùng bộ điều khiển súng. Nút thoát gọi
  trực tiếp đúng passenger seat.

## Sửa chữa asset

Chạy `Tools > Franklin Game > Shooter System GC2 > Install or Repair`.
Installer an toàn khi chạy lặp lại; nó không sửa package Game Creator và không tạo file
ra ngoài `Assets/ShooterSystemGC2`.

## Cấu trúc

- `Runtime`: catalog, menu chọn súng, tap HUD và touch controls.
- `Editor`: installer tạo catalog/GC2 weapon assets.
- `Resources/FranklinShooter`: asset runtime, UI ImageGen, upper-body mask, Shooter
  locomotion và bộ `Materials`/`Effects` URP cục bộ.
- `WeaponsLow`: Models, Prefabs, material và texture gốc với `.meta` được giữ nguyên.

Scene mẫu và lighting demo của Weapons Low đã bị loại bỏ vì không cần cho runtime. Toàn
bộ model súng và phụ kiện gốc vẫn được giữ.
