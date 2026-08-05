# Texture Transform Tool

Unity Editor utility for rotating and flipping one or more selected `Texture2D` assets.

## Features

- Rotate 180°
- Rotate 90° clockwise
- Rotate 90° counter-clockwise
- Rotate by an arbitrary clockwise angle
- Expand or retain the original canvas for arbitrary rotation
- Nearest-neighbour and bilinear sampling
- Flip the canvas horizontally or vertically
- Batch-process multiple selected textures
- Create safe PNG copies (default) or replace supported original source files
- Preserve compatible Texture Importer settings on copies

## Usage

1. In Unity's Project window, select one or more Texture2D assets.
2. Choose **Tools > Texture Transform > Open Tool...** for all options.
3. You can also run the six quick commands directly from:
   - **Tools > Texture Transform**
   - Right-click selection, then **Texture Transform**

Quick commands always create a uniquely named PNG copy beside the source. The full window
also offers a **Replace Original** mode for PNG, JPG, TGA and EXR files. For direct replacement,
use **Tools > Texture Transform > Replace Original** or the matching right-click submenu.
Unity always asks for confirmation before replacing source files.

## Notes

- Positive arbitrary angles rotate clockwise.
- Transparent pixels are used around an expanded arbitrary rotation.
- Multiple-sprite atlas rectangles are not transferred to a transformed copy because their
  coordinates would be stale. The copy is imported as a single sprite instead.
- Rotating a tangent-space normal map also changes its orientation. Review normal-map output
  before using it in production.
- The tool is Editor-only and adds no runtime code to builds.

## Compatibility

Built and verified with Unity 6000.3.20f1. The implementation only uses long-standing Unity
Editor APIs and is intended to work with recent Unity LTS releases.
