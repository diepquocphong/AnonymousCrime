using System;
using UnityEngine;

namespace TextureTransformTool
{
    internal static class TexturePixelTransformer
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static TexturePixelData Transform(
            Color32[] source,
            int width,
            int height,
            TextureTransformRequest request)
        {
            ValidateSource(source, width, height);

            switch (request.operation)
            {
                case TextureTransformOperation.Rotate180:
                    return Rotate180(source, width, height);
                case TextureTransformOperation.RotateClockwise90:
                    return RotateClockwise90(source, width, height);
                case TextureTransformOperation.RotateCounterClockwise90:
                    return RotateCounterClockwise90(source, width, height);
                case TextureTransformOperation.FlipHorizontal:
                    return FlipHorizontal(source, width, height);
                case TextureTransformOperation.FlipVertical:
                    return FlipVertical(source, width, height);
                case TextureTransformOperation.Arbitrary:
                    return RotateArbitrary(
                        source,
                        width,
                        height,
                        request.angle,
                        request.expandCanvas,
                        request.sampling);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.operation), request.operation, null);
            }
        }

        private static TexturePixelData Rotate180(Color32[] source, int width, int height)
        {
            Color32[] destination = new Color32[source.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    destination[(y * width) + x] =
                        source[((height - 1 - y) * width) + (width - 1 - x)];
                }
            }

            return new TexturePixelData(width, height, destination);
        }

        private static TexturePixelData RotateClockwise90(Color32[] source, int width, int height)
        {
            int destinationWidth = height;
            int destinationHeight = width;
            Color32[] destination = new Color32[source.Length];

            for (int y = 0; y < destinationHeight; y++)
            {
                for (int x = 0; x < destinationWidth; x++)
                {
                    int sourceX = width - 1 - y;
                    int sourceY = x;
                    destination[(y * destinationWidth) + x] = source[(sourceY * width) + sourceX];
                }
            }

            return new TexturePixelData(destinationWidth, destinationHeight, destination);
        }

        private static TexturePixelData RotateCounterClockwise90(
            Color32[] source,
            int width,
            int height)
        {
            int destinationWidth = height;
            int destinationHeight = width;
            Color32[] destination = new Color32[source.Length];

            for (int y = 0; y < destinationHeight; y++)
            {
                for (int x = 0; x < destinationWidth; x++)
                {
                    int sourceX = y;
                    int sourceY = height - 1 - x;
                    destination[(y * destinationWidth) + x] = source[(sourceY * width) + sourceX];
                }
            }

            return new TexturePixelData(destinationWidth, destinationHeight, destination);
        }

        private static TexturePixelData FlipHorizontal(Color32[] source, int width, int height)
        {
            Color32[] destination = new Color32[source.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    destination[(y * width) + x] = source[(y * width) + (width - 1 - x)];
                }
            }

            return new TexturePixelData(width, height, destination);
        }

        private static TexturePixelData FlipVertical(Color32[] source, int width, int height)
        {
            Color32[] destination = new Color32[source.Length];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = (height - 1 - y) * width;
                Array.Copy(source, sourceRow, destination, y * width, width);
            }

            return new TexturePixelData(width, height, destination);
        }

        private static TexturePixelData RotateArbitrary(
            Color32[] source,
            int width,
            int height,
            float clockwiseAngle,
            bool expandCanvas,
            TextureTransformSampling sampling)
        {
            float normalizedAngle = Mathf.Repeat(clockwiseAngle, 360f);
            if (Mathf.Abs(normalizedAngle) < 0.0001f ||
                Mathf.Abs(normalizedAngle - 360f) < 0.0001f)
            {
                return new TexturePixelData(width, height, (Color32[])source.Clone());
            }

            if (Mathf.Abs(normalizedAngle - 90f) < 0.0001f)
            {
                return RotateClockwise90(source, width, height);
            }

            if (Mathf.Abs(normalizedAngle - 180f) < 0.0001f)
            {
                return Rotate180(source, width, height);
            }

            if (Mathf.Abs(normalizedAngle - 270f) < 0.0001f)
            {
                return RotateCounterClockwise90(source, width, height);
            }

            float radians = normalizedAngle * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);

            int destinationWidth = width;
            int destinationHeight = height;
            if (expandCanvas)
            {
                destinationWidth = Mathf.Max(
                    1,
                    Mathf.CeilToInt((Mathf.Abs(width * cosine)) + (Mathf.Abs(height * sine))));
                destinationHeight = Mathf.Max(
                    1,
                    Mathf.CeilToInt((Mathf.Abs(width * sine)) + (Mathf.Abs(height * cosine))));
            }

            Color32[] destination = new Color32[destinationWidth * destinationHeight];
            float sourceCenterX = width * 0.5f;
            float sourceCenterY = height * 0.5f;
            float destinationCenterX = destinationWidth * 0.5f;
            float destinationCenterY = destinationHeight * 0.5f;

            for (int y = 0; y < destinationHeight; y++)
            {
                float destinationY = (y + 0.5f) - destinationCenterY;
                for (int x = 0; x < destinationWidth; x++)
                {
                    float destinationX = (x + 0.5f) - destinationCenterX;

                    // Positive angles rotate clockwise. This is the inverse transform from
                    // a destination pixel centre back into the source image.
                    float sourceX = (cosine * destinationX) - (sine * destinationY) + sourceCenterX;
                    float sourceY = (sine * destinationX) + (cosine * destinationY) + sourceCenterY;

                    destination[(y * destinationWidth) + x] = sampling == TextureTransformSampling.Nearest
                        ? SampleNearest(source, width, height, sourceX, sourceY)
                        : SampleBilinear(source, width, height, sourceX, sourceY);
                }
            }

            return new TexturePixelData(destinationWidth, destinationHeight, destination);
        }

        private static Color32 SampleNearest(
            Color32[] source,
            int width,
            int height,
            float sourceX,
            float sourceY)
        {
            int x = Mathf.FloorToInt(sourceX);
            int y = Mathf.FloorToInt(sourceY);
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return Clear;
            }

            return source[(y * width) + x];
        }

        private static Color32 SampleBilinear(
            Color32[] source,
            int width,
            int height,
            float sourceX,
            float sourceY)
        {
            float sampleX = sourceX - 0.5f;
            float sampleY = sourceY - 0.5f;
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            float tx = sampleX - x0;
            float ty = sampleY - y0;

            ColorAccumulator accumulator = default;
            Accumulate(ref accumulator, GetPixelOrClear(source, width, height, x0, y0), (1f - tx) * (1f - ty));
            Accumulate(ref accumulator, GetPixelOrClear(source, width, height, x0 + 1, y0), tx * (1f - ty));
            Accumulate(ref accumulator, GetPixelOrClear(source, width, height, x0, y0 + 1), (1f - tx) * ty);
            Accumulate(ref accumulator, GetPixelOrClear(source, width, height, x0 + 1, y0 + 1), tx * ty);

            if (accumulator.alpha <= 0.000001f)
            {
                return Clear;
            }

            float inverseAlpha = 1f / accumulator.alpha;
            return new Color32(
                ToByte(accumulator.red * inverseAlpha),
                ToByte(accumulator.green * inverseAlpha),
                ToByte(accumulator.blue * inverseAlpha),
                ToByte(accumulator.alpha));
        }

        private static Color32 GetPixelOrClear(
            Color32[] source,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return Clear;
            }

            return source[(y * width) + x];
        }

        private static void Accumulate(ref ColorAccumulator accumulator, Color32 color, float weight)
        {
            float alpha = (color.a / 255f) * weight;
            accumulator.red += (color.r / 255f) * alpha;
            accumulator.green += (color.g / 255f) * alpha;
            accumulator.blue += (color.b / 255f) * alpha;
            accumulator.alpha += alpha;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static void ValidateSource(Color32[] source, int width, int height)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (width <= 0 || height <= 0 || source.Length != width * height)
            {
                throw new ArgumentException("Pixel data dimensions do not match the source array.");
            }
        }

        private struct ColorAccumulator
        {
            public float red;
            public float green;
            public float blue;
            public float alpha;
        }
    }
}
