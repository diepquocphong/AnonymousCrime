# Franklin Villa — original-model optimization

This pipeline creates a derivative from the original Unity prefab without redrawing or modifying the source asset.

## Output

- Unity prefab: `Assets/Model/franklin-villa-estate-mobile-optimized/franklin-villa-estate-mobile-optimized.prefab`
- QA report: `dist/VillaOriginalOptimizationReport.json`
- Render comparison: `dist/Preview/Villa_Original_vs_Optimized_Hero.png`

## Optimization policy

- Preserve all 16,520 rendered-instance triangles; no decimation.
- Preserve all 461 colliders.
- Preserve the exact hierarchy and transforms for every interactive gate/door/garage object and the complete vehicle ramp.
- Combine only static renderers with matching material and renderer state, inside bounded spatial/floor groups.
- Keep one-renderer groups unbaked to preserve their exact lighting and shadow behavior.
- Copy only the 26 referenced materials and 5 referenced textures.
- Remove opaque PNG alpha, retain 256×256 resolution and mipmaps, use BC1 on Standalone and ASTC 4×4 on Android/iOS.
- Keep the original Piglet `zwrite + transparent` pass order for glass and pool water.

The optimized derivative still uses the Piglet URP shaders already present in the Franklin Game project.
