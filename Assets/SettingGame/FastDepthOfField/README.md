# Franklin Fast Mobile Depth of Field

Đây là bản chuyển đổi URP/RenderGraph từ thuật toán blur độ phân giải thấp của
`Fast Mobile Depth of Field v1.0`. Không nhập scene mẫu, model, texture hoặc các
shader Built-in Pipeline của gói gốc.

## Chế độ

- `TẮT`: không enqueue render pass và không yêu cầu depth texture cho DOF.
- `GẦN`: giữ Player/khu vực gần sắc nét, bắt đầu làm mờ từ 15 m phía sau Player.
- `XA`: giữ Player/khu vực gần sắc nét, bắt đầu làm mờ từ 40 m phía sau Player,
  phù hợp lái xe và quan sát đường/phong cảnh.

Hiệu ứng dùng hai lượt blur ở 1/4 độ phân giải rồi composite một lần ở full
resolution bằng camera depth. DOF chỉ làm mờ một chiều ở xa; foreground, Player và
mặt đất quanh chân không bị làm mờ. Mốc khoảng cách tự theo
`ShortcutPlayer.Transform`, không quét scene mỗi frame. Không cần thay material của
map và không cấp phát `RenderTexture` bằng script mỗi frame; texture tạm do
RenderGraph quản lý. Cường độ hòa trộn blur được giới hạn ở 30% để giữ hình ảnh rõ
trên màn hình mobile.

Preset đồ họa: `THẤP` và `VỪA` bắt buộc tắt DOF. Khi vừa chọn `CAO`, DOF mặc định
bật ở `XA`, sau đó người chơi vẫn có thể chọn `TẮT/GẦN/XA`. `AUTO` cũng cho chọn đủ
ba trạng thái và mọi lựa chọn đều được lưu.

Runtime API nằm trong `FranklinMobileGraphicsSettings`:

```csharp
FranklinMobileGraphicsSettings.SetDepthOfFieldMode(1); // 0 Off, 1 Near, 2 Far
int mode = FranklinMobileGraphicsSettings.DepthOfFieldMode;
```
