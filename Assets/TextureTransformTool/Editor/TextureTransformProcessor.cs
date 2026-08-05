using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TextureTransformTool
{
    internal static class TextureTransformProcessor
    {
        private static readonly string[] OverwritableExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".exr" };

        public static IReadOnlyList<string> GetSelectedTexturePaths()
        {
            return Selection.GetFiltered<Texture2D>(SelectionMode.Assets)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool HasSelectedTextures()
        {
            return GetSelectedTexturePaths().Count > 0;
        }

        public static IReadOnlyList<string> TransformSelection(TextureTransformRequest request)
        {
            IReadOnlyList<string> paths = GetSelectedTexturePaths();
            if (paths.Count == 0)
            {
                throw new InvalidOperationException("Select at least one Texture2D asset in the Project window.");
            }

            if (request.output == TextureTransformOutput.ReplaceOriginal)
            {
                string unsupported = paths.FirstOrDefault(path => !CanOverwrite(path));
                if (!string.IsNullOrEmpty(unsupported))
                {
                    throw new InvalidOperationException(
                        $"Cannot overwrite '{unsupported}'. Overwrite supports PNG, JPG, TGA and EXR. " +
                        "Choose Create PNG Copy for other source formats.");
                }
            }

            List<string> outputs = new List<string>(paths.Count);
            try
            {
                for (int index = 0; index < paths.Count; index++)
                {
                    string sourcePath = paths[index];
                    EditorUtility.DisplayProgressBar(
                        "Texture Transform",
                        $"{request.GetDisplayName()}: {sourcePath}",
                        (float)index / paths.Count);
                    outputs.Add(TransformAsset(sourcePath, request));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string output in outputs)
            {
                AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceUpdate);
            }

            if (outputs.Count == 1)
            {
                UnityEngine.Object result = AssetDatabase.LoadAssetAtPath<Texture2D>(outputs[0]);
                if (result != null)
                {
                    Selection.activeObject = result;
                    EditorGUIUtility.PingObject(result);
                }
            }

            return outputs;
        }

        public static bool CanOverwrite(string assetPath)
        {
            string extension = Path.GetExtension(assetPath);
            return OverwritableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static string TransformAsset(string sourcePath, TextureTransformRequest request)
        {
            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (sourceTexture == null)
            {
                throw new InvalidOperationException($"'{sourcePath}' is not a Texture2D asset.");
            }

            TexturePixelData source = ReadPixels(sourceTexture, sourcePath);
            TexturePixelData transformed = TexturePixelTransformer.Transform(
                source.pixels,
                source.width,
                source.height,
                request);

            string outputPath = request.output == TextureTransformOutput.ReplaceOriginal
                ? sourcePath
                : CreateUniqueOutputPath(sourcePath, request.GetFileSuffix());

            byte[] encoded = Encode(transformed, Path.GetExtension(outputPath));
            string absoluteOutputPath = ToAbsolutePath(outputPath);
            if (File.Exists(absoluteOutputPath) && !AssetDatabase.MakeEditable(outputPath))
            {
                throw new IOException($"Unity could not make '{outputPath}' writable.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath) ?? Application.dataPath);
            File.WriteAllBytes(absoluteOutputPath, encoded);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);

            if (request.output == TextureTransformOutput.CreatePngCopy && request.copyImporterSettings)
            {
                CopyImporterSettings(sourcePath, outputPath);
            }

            return outputPath;
        }

        private static TexturePixelData ReadPixels(Texture2D sourceTexture, string sourcePath)
        {
            // Reading PNG/JPG bytes directly preserves the full source resolution and raw channels,
            // even when the Unity importer is configured as a normal map or has a max-size override.
            string extension = Path.GetExtension(sourcePath);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (decoded.LoadImage(File.ReadAllBytes(ToAbsolutePath(sourcePath)), false))
                    {
                        return new TexturePixelData(decoded.width, decoded.height, decoded.GetPixels32());
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }

            try
            {
                return new TexturePixelData(sourceTexture.width, sourceTexture.height, sourceTexture.GetPixels32());
            }
            catch (UnityException)
            {
                return ReadPixelsThroughImporterOrGpu(sourceTexture, sourcePath);
            }
        }

        private static TexturePixelData ReadPixelsThroughImporterOrGpu(
            Texture2D sourceTexture,
            string sourcePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null)
            {
                return ReadPixelsThroughGpu(sourceTexture);
            }

            bool originalReadable = importer.isReadable;
            TextureImporterCompression originalCompression = importer.textureCompression;
            bool originalCrunchedCompression = importer.crunchedCompression;

            try
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();

                Texture2D readable = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                return new TexturePixelData(readable.width, readable.height, readable.GetPixels32());
            }
            finally
            {
                importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = originalReadable;
                    importer.textureCompression = originalCompression;
                    importer.crunchedCompression = originalCrunchedCompression;
                    importer.SaveAndReimport();
                }
            }
        }

        private static TexturePixelData ReadPixelsThroughGpu(Texture2D sourceTexture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            Texture2D readable = new Texture2D(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGBA32,
                false);

            try
            {
                Graphics.Blit(sourceTexture, temporary);
                RenderTexture.active = temporary;
                readable.ReadPixels(
                    new Rect(0, 0, sourceTexture.width, sourceTexture.height),
                    0,
                    0,
                    false);
                readable.Apply(false, false);
                return new TexturePixelData(readable.width, readable.height, readable.GetPixels32());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static byte[] Encode(TexturePixelData data, string extension)
        {
            Texture2D output = new Texture2D(data.width, data.height, TextureFormat.RGBA32, false, false);
            try
            {
                output.SetPixels32(data.pixels);
                output.Apply(false, false);

                switch (extension.ToLowerInvariant())
                {
                    case ".jpg":
                    case ".jpeg":
                        return output.EncodeToJPG(95);
                    case ".tga":
                        return output.EncodeToTGA();
                    case ".exr":
                        return output.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                    default:
                        return output.EncodeToPNG();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static string CreateUniqueOutputPath(string sourcePath, string suffix)
        {
            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets";
            string filename = Path.GetFileNameWithoutExtension(sourcePath);
            return AssetDatabase.GenerateUniqueAssetPath($"{directory}/{filename}{suffix}.png");
        }

        private static void CopyImporterSettings(string sourcePath, string outputPath)
        {
            TextureImporter source = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            TextureImporter destination = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (source == null || destination == null)
            {
                return;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            source.ReadTextureSettings(settings);

            // Sprite-sheet rectangles cannot be copied safely after rotation. Keep the output
            // usable as one sprite instead of importing stale rectangles.
            if (settings.spriteMode == (int)SpriteImportMode.Multiple)
            {
                settings.spriteMode = (int)SpriteImportMode.Single;
            }

            destination.SetTextureSettings(settings);
            destination.SaveAndReimport();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
