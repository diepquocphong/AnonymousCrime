using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TextureTransformTool
{
    internal static class TextureTransformPackageBuilder
    {
        private const string PackageRoot = "Assets/TextureTransformTool";
        private const string PackageFilename = "TextureTransformTool.unitypackage";

        [MenuItem("Tools/Texture Transform/Export UnityPackage to Documents", priority = 100)]
        public static void ExportToDocuments()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Export(Path.Combine(documents, PackageFilename));
        }

        public static void BuildAndVerifyFromCommandLine()
        {
            RunPixelTransformSelfTests();
            RunReplaceIntegrationTest();

            string requestedPath = Environment.GetEnvironmentVariable("TEXTURE_TRANSFORM_PACKAGE_PATH");
            string outputPath = string.IsNullOrWhiteSpace(requestedPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), PackageFilename)
                : requestedPath;

            Export(outputPath);
            Debug.Log($"Texture Transform package build succeeded: {outputPath}");
        }

        private static void Export(string outputPath)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("The package output path must include a directory.", nameof(outputPath));
            }

            Directory.CreateDirectory(directory);
            AssetDatabase.ExportPackage(
                PackageRoot,
                outputPath,
                ExportPackageOptions.Recurse);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                throw new IOException($"Unity did not create a valid package at '{outputPath}'.");
            }

            Debug.Log($"Exported Texture Transform Tool to: {outputPath}");
        }

        private static void RunPixelTransformSelfTests()
        {
            Color32[] source =
            {
                Pixel(1), Pixel(2),
                Pixel(3), Pixel(4),
                Pixel(5), Pixel(6)
            };

            AssertTransform(
                source,
                2,
                3,
                TextureTransformOperation.RotateClockwise90,
                3,
                2,
                2, 4, 6,
                1, 3, 5);
            AssertTransform(
                source,
                2,
                3,
                TextureTransformOperation.RotateCounterClockwise90,
                3,
                2,
                5, 3, 1,
                6, 4, 2);
            AssertTransform(
                source,
                2,
                3,
                TextureTransformOperation.Rotate180,
                2,
                3,
                6, 5,
                4, 3,
                2, 1);
            AssertTransform(
                source,
                2,
                3,
                TextureTransformOperation.FlipHorizontal,
                2,
                3,
                2, 1,
                4, 3,
                6, 5);
            AssertTransform(
                source,
                2,
                3,
                TextureTransformOperation.FlipVertical,
                2,
                3,
                5, 6,
                3, 4,
                1, 2);

            TextureTransformRequest identityRequest = new TextureTransformRequest
            {
                operation = TextureTransformOperation.Arbitrary,
                angle = 360f,
                expandCanvas = true,
                sampling = TextureTransformSampling.Bilinear
            };
            TexturePixelData identity = TexturePixelTransformer.Transform(source, 2, 3, identityRequest);
            AssertPixels(identity, 2, 3, 1, 2, 3, 4, 5, 6);

            Debug.Log("Texture Transform pixel self-tests passed.");
        }

        private static void RunReplaceIntegrationTest()
        {
            const string testAssetPath = "Assets/__TextureTransformReplaceTest.png";
            string absoluteTestPath = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                testAssetPath);
            UnityEngine.Object[] previousSelection = Selection.objects;

            try
            {
                Texture2D sourceTexture = new Texture2D(2, 3, TextureFormat.RGBA32, false, false);
                try
                {
                    sourceTexture.SetPixels32(new[]
                    {
                        Pixel(1), Pixel(2),
                        Pixel(3), Pixel(4),
                        Pixel(5), Pixel(6)
                    });
                    sourceTexture.Apply(false, false);
                    File.WriteAllBytes(absoluteTestPath, sourceTexture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sourceTexture);
                }

                AssetDatabase.ImportAsset(testAssetPath, ImportAssetOptions.ForceSynchronousImport);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(testAssetPath);

                TextureTransformRequest request = new TextureTransformRequest
                {
                    operation = TextureTransformOperation.RotateClockwise90,
                    output = TextureTransformOutput.ReplaceOriginal
                };
                TextureTransformProcessor.TransformSelection(request);

                Texture2D replacedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (!replacedTexture.LoadImage(File.ReadAllBytes(absoluteTestPath), false))
                    {
                        throw new Exception("Replace integration test could not decode the replaced PNG.");
                    }

                    TexturePixelData result = new TexturePixelData(
                        replacedTexture.width,
                        replacedTexture.height,
                        replacedTexture.GetPixels32());
                    AssertPixels(result, 3, 2, 2, 4, 6, 1, 3, 5);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(replacedTexture);
                }

                Debug.Log("Texture Transform Replace Original integration test passed.");
            }
            finally
            {
                Selection.objects = previousSelection;
                AssetDatabase.DeleteAsset(testAssetPath);
            }
        }

        private static void AssertTransform(
            Color32[] source,
            int width,
            int height,
            TextureTransformOperation operation,
            int expectedWidth,
            int expectedHeight,
            params byte[] expected)
        {
            TextureTransformRequest request = new TextureTransformRequest { operation = operation };
            TexturePixelData result = TexturePixelTransformer.Transform(source, width, height, request);
            AssertPixels(result, expectedWidth, expectedHeight, expected);
        }

        private static void AssertPixels(
            TexturePixelData result,
            int expectedWidth,
            int expectedHeight,
            params byte[] expected)
        {
            if (result.width != expectedWidth || result.height != expectedHeight)
            {
                throw new Exception(
                    $"Self-test dimension failure: got {result.width}x{result.height}, " +
                    $"expected {expectedWidth}x{expectedHeight}.");
            }

            if (result.pixels.Length != expected.Length)
            {
                throw new Exception("Self-test pixel count failure.");
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (result.pixels[index].r != expected[index])
                {
                    throw new Exception(
                        $"Self-test pixel failure at {index}: got {result.pixels[index].r}, " +
                        $"expected {expected[index]}.");
                }
            }
        }

        private static Color32 Pixel(byte value)
        {
            return new Color32(value, 0, 0, 255);
        }
    }
}
