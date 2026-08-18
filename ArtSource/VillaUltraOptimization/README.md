# Franklin Villa Ultra Optimization

This is a derivative of the original villa prefab. The source folder is intentionally left unchanged.

## Unity prefab

`Assets/Model/franklin-villa-estate-ultra-optimized/franklin-villa-estate-ultra-optimized.prefab`

Gameplay-balanced collider variant:

`Assets/Model/franklin-villa-estate-ultra-optimized/franklin-villa-estate-ultra-collider-optimized.prefab`

## Result

| Metric | Original | Ultra optimized | Reduction |
|---|---:|---:|---:|
| MeshRenderer | 461 | 26 | 94.36% |
| Serialized material slots / estimated base draws | 498 | 41 | 91.77% |
| Unique rendered meshes | 440 | 26 | 94.09% |
| Used materials | 26 | 4 | 84.62% |
| Local referenced textures | 5 | 1 atlas | 80.00% |
| Rendered triangles | 16,520 | 16,520 | no decimation |
| Colliders | 461 | 461 | unchanged |

The complete 517-node source hierarchy is retained. The optimizer removes only the original
MeshRenderer/MeshFilter components and creates 26 regional or moving-pivot visual batches.
The two sliding gate leaves, four sectional garage panels, main door, balcony door and two pool
door chains each keep a separate zeroed visual child beneath their original moving anchor.

Twenty-three opaque Piglet materials are represented by one local URP material. Their base colour,
texture, metallic, roughness and emission values are baked into UV3–UV5. Five 256x256 textures are
packed without rescaling into one 816x544 RGB atlas with eight-pixel wrap gutters. The atlas is BC1
on Standalone and ASTC 4x4 on Android/iOS. Glass and water retain the original two-pass Piglet
zwrite/transparent behaviour, which is why the 26 renderers use 41 slots rather than 26.

## QA

- Unity 6000.3.21f1 / URP 17.3 shader compile passed.
- 16,520 triangles and 17,891 rendered vertices are unchanged.
- Bounds delta is below 0.01 m.
- All 455 BoxCollider and six ramp MeshCollider components are retained.
- All original transform paths and local transforms are fingerprint-checked.
- Optimized prefab has no dependency back to the original villa asset folder.
- Same-camera image comparison: Front SSIM 0.9977, Top SSIM 0.9802, Hero SSIM 0.9696.
  The larger Hero difference is mainly oblique mip/shadow appearance from replacing 23 ShaderGraph
  materials with one consolidated shader; geometry, silhouette, glass and gameplay layout remain intact.

The custom opaque shader targets shader model 3.5 (Metal, Vulkan and GLES3). It includes native
Forward, ShadowCaster, DepthOnly and DepthNormals passes. It intentionally has no Meta pass; add an
atlas-aware Meta pass before rebaking Progressive Lightmapper data.

## Collider-optimized variant

The collider variant shares the exact same 26 visual meshes, four materials and atlas. It replaces
the original `455 BoxCollider + 6 MeshCollider` layout with `115 BoxCollider` components and no
MeshCollider, a 75.05% reduction. Decorative parking/helipad markings, glows, foliage leaves and
zero-thickness pool water no longer collide. The six cuboid ramp MeshColliders are represented by
exact local BoxColliders, while the 16 stair steps use one smooth inclined walking proxy.

Each of the two gate leaves, four garage panels, main door, balcony door and two pool-slider chains
keeps one collider directly below its original pivot. These ten moving anchors also receive a
gravity-free kinematic Rigidbody so moving a door does not rebuild PhysX static broadphase data.
The fixed window banks use eight proxies, with the long upper facade split on both sides of the
balcony doorway. Furniture groups, kitchen units, palms and planters use coarse semantic proxies;
floors, walls, pool shell, vehicle ramp, rooftop and boundary collision remain gameplay-safe.

Collider QA passed in Unity 6000.3.21f1: `115` enabled, non-trigger, non-zero BoxColliders; `0`
MeshColliders; `10` kinematic moving bodies; `15` ramp colliders; unchanged `26 renderers / 41 slots /
16,520 triangles`. A player-sized capsule also passes the opened balcony door. The final same-camera
Hero, Front and Top renders are pixel-identical to the Ultra prefab.
