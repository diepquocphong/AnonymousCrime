using System;
using UnityEngine;

namespace TextureTransformTool
{
    internal enum TextureTransformOperation
    {
        Rotate180,
        RotateClockwise90,
        RotateCounterClockwise90,
        Arbitrary,
        FlipHorizontal,
        FlipVertical
    }

    internal enum TextureTransformSampling
    {
        Nearest,
        Bilinear
    }

    internal enum TextureTransformOutput
    {
        CreatePngCopy,
        ReplaceOriginal
    }

    [Serializable]
    internal sealed class TextureTransformRequest
    {
        public TextureTransformOperation operation = TextureTransformOperation.RotateClockwise90;
        public TextureTransformSampling sampling = TextureTransformSampling.Bilinear;
        public TextureTransformOutput output = TextureTransformOutput.CreatePngCopy;
        public float angle = 45f;
        public bool expandCanvas = true;
        public bool copyImporterSettings = true;

        public string GetFileSuffix()
        {
            switch (operation)
            {
                case TextureTransformOperation.Rotate180:
                    return "_rot180";
                case TextureTransformOperation.RotateClockwise90:
                    return "_rot90_cw";
                case TextureTransformOperation.RotateCounterClockwise90:
                    return "_rot90_ccw";
                case TextureTransformOperation.FlipHorizontal:
                    return "_flip_horizontal";
                case TextureTransformOperation.FlipVertical:
                    return "_flip_vertical";
                case TextureTransformOperation.Arbitrary:
                    string value = angle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                        .Replace("-", "neg")
                        .Replace(".", "_");
                    return "_rot" + value;
                default:
                    return "_transformed";
            }
        }

        public string GetDisplayName()
        {
            switch (operation)
            {
                case TextureTransformOperation.Rotate180:
                    return "180°";
                case TextureTransformOperation.RotateClockwise90:
                    return "90° Clockwise";
                case TextureTransformOperation.RotateCounterClockwise90:
                    return "90° Counter Clockwise";
                case TextureTransformOperation.Arbitrary:
                    return $"Arbitrary ({angle:0.##}°)";
                case TextureTransformOperation.FlipHorizontal:
                    return "Flip Canvas Horizontal";
                case TextureTransformOperation.FlipVertical:
                    return "Flip Canvas Vertical";
                default:
                    return operation.ToString();
            }
        }
    }

    internal readonly struct TexturePixelData
    {
        public readonly int width;
        public readonly int height;
        public readonly Color32[] pixels;

        public TexturePixelData(int width, int height, Color32[] pixels)
        {
            this.width = width;
            this.height = height;
            this.pixels = pixels;
        }
    }
}
