# Metro Police Station — Three.js / Unity Mobile

Mô hình sở cảnh sát hư cấu được dựng procedural bằng Three.js, lấy cảm hứng từ ngôn ngữ trình bày GTA-style trong các ảnh tham chiếu nhưng không sao chép logo, tên riêng hoặc bố cục của tài sản có sẵn.

## File bàn giao

- `dist/PoliceStation_Mobile_Unity.glb`: bản chính cho Unity/Piglet, glTF 2.0 core, không extension bắt buộc.
- `dist/PoliceStation_Mobile_Web_Meshopt.glb`: bản nén Meshopt cho Three.js/WebGL.
- `dist/build-report.json`: thống kê build, SHA-256 và kết quả audit.
- `dist/preview-hero.png`: preview render sau khi kiểm tra trực quan.

## Thông số

- 5.549 triangles, 8.107 vertices.
- 3 mesh primitives / 3 material PBR dùng chung.
- Tất cả material đều `OPAQUE`; kính được stylize thành kính xanh đen opaque để tránh transparent overdraw và pass phụ trong Piglet URP.
- Màu chi tiết nằm trong `COLOR_0` dạng normalized `u8`; không dùng texture atlas nên giảm memory, import time và texture sampling.
- Không dùng `BoxGeometry`. Tường, mái, đường, cửa, cửa sổ, biển hiệu, curb, canopy và markings được ghép từ `PlaneGeometry`/custom quads; cột và props chỉ dùng cylinder 6–10 cạnh khi cần silhouette.
- Kích thước khoảng 68 × 17.9 × 56.1 m; Y-up; 1 unit = 1 m; mặt tiền hướng `-Z`.
- Có các empty node `SOCKET_*` và `COLLIDER_HINTS/COL_*` để định vị gameplay/collider trong Unity.

## Import Unity

Project hiện dùng Piglet. Để Piglet tạo prefab, kéo file `PoliceStation_Mobile_Unity.glb` từ Finder vào Project Browser của Unity. Không dùng bản Meshopt vì Piglet 1.2.0 không hỗ trợ `EXT_meshopt_compression`.

Sau import:

1. Giữ shader Piglet URP để `COLOR_0` tiếp tục điều khiển màu bề mặt.
2. Mark ba renderer trong `LOD0_RenderBatches` là Static.
3. Tạo BoxCollider theo transform của các node `COL_*`, sau đó có thể xóa group `COLLIDER_HINTS` nếu không cần.
4. Tắt Read/Write trên ba mesh sau khi collider/batching đã hoàn tất.
5. Dùng baked lighting/AO cho mobile; model không nhúng light runtime.

## Build lại

```bash
cd "/Users/diepquocphong/Documents/Franklin Game/ArtSource/PoliceStation3D"
npm install
npm run build
```

Pipeline build xuất GLB bằng `THREE.GLTFExporter`, sau đó chạy dedup → prune → weld → vertex-cache reorder cho bản Unity. Bản web được nén riêng bằng Meshopt. Chạy viewer bằng `npm run viewer`.
