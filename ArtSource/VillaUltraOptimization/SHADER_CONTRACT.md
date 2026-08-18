# Villa Ultra material contract

This shader consolidates the 23 opaque Piglet materials used by the original villa into one
URP material while preserving their per-face base colour, texture, metallic, roughness and
emission values.

## Mesh channels

Each triangle must have a single source material. Duplicate vertices at any material boundary
before flattening submeshes.

| Unity channel | Shader semantic | Value |
|---|---|---|
| UV0 / channel 0 | `TEXCOORD0` | Original source UV, unchanged |
| UV2 / channel 1 | `TEXCOORD1` | Existing lightmap UV, unchanged |
| UV3 / channel 2 | `TEXCOORD2` | `_baseColorFactor` RGBA in linear space |
| UV4 / channel 3 | `TEXCOORD3` | `_emissiveFactor.rgb` in linear space, `_metallicFactor` in W |
| UV5 / channel 4 | `TEXCOORD4` | `_roughnessFactor` in X, atlas tile index in Y; Z/W zero |

Write channels 2–4 as `Vector4` data. Float32 is the safest first build. Float16 is acceptable
after image-diff validation. Do not use `Color32` for Piglet base factors: the very dark metal
values lose visibly significant precision when quantized to eight bits.

## Base atlas

Create an 816x544 sRGB atlas with two rows and three columns. Each cell is 272x272: the original
256x256 image is copied without rescaling and surrounded by an eight-pixel wrap gutter. Pixel
coordinates here use Unity's bottom-left texture origin.

| Tile | Content |
|---:|---|
| 0 | Opaque white, used by every solid-colour material |
| 1 | `texture_1.png` — asphalt |
| 2 | `texture_2.png` — concreteDark |
| 3 | `texture_3.png` — concrete |
| 4 | `texture_4.png` — wood |
| 5 | `texture_5.png` — tile |

`texture_0.png` is unused by the prefab. Import the atlas with sRGB on, alpha source none,
mipmaps on, wrap clamp, readable off, NPOT scale none, Max Size 1024, BC1 on Standalone and
ASTC 4x4 on Android/iOS. The shader performs its own per-cell repeat and supplies explicit
gradients, while the gutter prevents cross-cell mip bleeding.

## Exact source property transfer

Use `Material.GetColor("_baseColorFactor")`, `GetColor("_emissiveFactor")`,
`GetFloat("_metallicFactor")`, and `GetFloat("_roughnessFactor")` while creating each source
triangle stream. All 23 opaque source materials use the Piglet metallic-roughness opaque graph;
none use normal, metallic-roughness, emissive, or occlusion textures. Therefore these four
values plus the base atlas reproduce the complete source shading inputs.

## Transparent materials

Keep exactly three shared Piglet materials for the transparent path:

1. `zwrite` depth prepass,
2. `glass` (`base=(0.2663556, 0.45641103, 0.57112485, 0.45)`, metallic 0,
   roughness 0.12),
3. `UnityPBR_25` water (`base=(0.054480277, 0.4452012, 0.55201143, 0.82)`, metallic 0,
   roughness 0.2).

This produces four material assets total including `VillaUltraOpaque`. It also retains Piglet's
exact transparent sorting/depth appearance. Merge glass and water by culling zone/pivot, but do
not merge them globally because transparent sorting is renderer-bounds dependent.

An optional one-pass transparent shader using `Blend SrcAlpha OneMinusSrcAlpha` plus `ZWrite On`
can reduce transparent slots further, but it cannot be pixel-identical where glass sheets overlap.
Do not use it for the visual-preserving build.

## Expected renderer/slot budget

Build one opaque mesh/render per spatial culling zone and one per animated pivot. With roughly
8–10 static zones and the required gate/door/garage pivots, the practical target is 18–28 unique
render meshes, 18–28 renderers, and about 24–38 material slots including transparent depth
prepasses. The source triangles and all collider GameObjects remain unchanged.

## Compatibility notes

The shader is authored and import-tested against Unity 6000.3.21f1 with URP 17.3 and targets
Shader Model 3.5 (GLES3/Metal/Vulkan class hardware; not GLES2). Its forward, shadow, depth and
depth-normal passes share an identical `UnityPerMaterial` constant buffer for SRP Batcher
compatibility. The original villa has no opaque alpha-test or normal-map inputs, so the compact
depth passes are sufficient. A Meta pass is intentionally not included: if the combined prefab
will be rebaked by Progressive Lightmapper, add an atlas-aware Meta pass first. Existing runtime
light probes and already assigned lightmaps continue through the forward pass as long as UV2 and
the renderer lightmap index/scale-offset are preserved or regenerated after combination.
