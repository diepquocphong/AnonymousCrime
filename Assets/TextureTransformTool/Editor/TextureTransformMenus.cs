using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TextureTransformTool
{
    internal static class TextureTransformMenus
    {
        private const string ToolsRoot = "Tools/Texture Transform/";
        private const string AssetsRoot = "Assets/Texture Transform/";
        private const string ToolsReplaceRoot = ToolsRoot + "Replace Original/";
        private const string AssetsReplaceRoot = AssetsRoot + "Replace Original/";

        [MenuItem(ToolsRoot + "180°", priority = 20)]
        [MenuItem(AssetsRoot + "180°", priority = 2000)]
        private static void Rotate180()
        {
            ExecuteQuick(TextureTransformOperation.Rotate180);
        }

        [MenuItem(ToolsRoot + "90° Clockwise", priority = 21)]
        [MenuItem(AssetsRoot + "90° Clockwise", priority = 2001)]
        private static void RotateClockwise90()
        {
            ExecuteQuick(TextureTransformOperation.RotateClockwise90);
        }

        [MenuItem(ToolsRoot + "90° Counter Clockwise", priority = 22)]
        [MenuItem(AssetsRoot + "90° Counter Clockwise", priority = 2002)]
        private static void RotateCounterClockwise90()
        {
            ExecuteQuick(TextureTransformOperation.RotateCounterClockwise90);
        }

        [MenuItem(ToolsRoot + "Arbitrary...", priority = 23)]
        [MenuItem(AssetsRoot + "Arbitrary...", priority = 2003)]
        private static void Arbitrary()
        {
            TextureTransformWindow.OpenForOperation(TextureTransformOperation.Arbitrary);
        }

        [MenuItem(ToolsRoot + "Flip Canvas Horizontal", priority = 50)]
        [MenuItem(AssetsRoot + "Flip Canvas Horizontal", priority = 2030)]
        private static void FlipHorizontal()
        {
            ExecuteQuick(TextureTransformOperation.FlipHorizontal);
        }

        [MenuItem(ToolsRoot + "Flip Canvas Vertical", priority = 51)]
        [MenuItem(AssetsRoot + "Flip Canvas Vertical", priority = 2031)]
        private static void FlipVertical()
        {
            ExecuteQuick(TextureTransformOperation.FlipVertical);
        }

        [MenuItem(ToolsReplaceRoot + "180°", priority = 70)]
        [MenuItem(AssetsReplaceRoot + "180°", priority = 2050)]
        private static void ReplaceRotate180()
        {
            ExecuteReplace(TextureTransformOperation.Rotate180);
        }

        [MenuItem(ToolsReplaceRoot + "90° Clockwise", priority = 71)]
        [MenuItem(AssetsReplaceRoot + "90° Clockwise", priority = 2051)]
        private static void ReplaceRotateClockwise90()
        {
            ExecuteReplace(TextureTransformOperation.RotateClockwise90);
        }

        [MenuItem(ToolsReplaceRoot + "90° Counter Clockwise", priority = 72)]
        [MenuItem(AssetsReplaceRoot + "90° Counter Clockwise", priority = 2052)]
        private static void ReplaceRotateCounterClockwise90()
        {
            ExecuteReplace(TextureTransformOperation.RotateCounterClockwise90);
        }

        [MenuItem(ToolsReplaceRoot + "Arbitrary...", priority = 73)]
        [MenuItem(AssetsReplaceRoot + "Arbitrary...", priority = 2053)]
        private static void ReplaceArbitrary()
        {
            TextureTransformWindow.OpenForOperation(
                TextureTransformOperation.Arbitrary,
                TextureTransformOutput.ReplaceOriginal);
        }

        [MenuItem(ToolsReplaceRoot + "Flip Canvas Horizontal", priority = 80)]
        [MenuItem(AssetsReplaceRoot + "Flip Canvas Horizontal", priority = 2060)]
        private static void ReplaceFlipHorizontal()
        {
            ExecuteReplace(TextureTransformOperation.FlipHorizontal);
        }

        [MenuItem(ToolsReplaceRoot + "Flip Canvas Vertical", priority = 81)]
        [MenuItem(AssetsReplaceRoot + "Flip Canvas Vertical", priority = 2061)]
        private static void ReplaceFlipVertical()
        {
            ExecuteReplace(TextureTransformOperation.FlipVertical);
        }

        [MenuItem(ToolsRoot + "180°", true)]
        [MenuItem(ToolsRoot + "90° Clockwise", true)]
        [MenuItem(ToolsRoot + "90° Counter Clockwise", true)]
        [MenuItem(ToolsRoot + "Flip Canvas Horizontal", true)]
        [MenuItem(ToolsRoot + "Flip Canvas Vertical", true)]
        [MenuItem(AssetsRoot + "180°", true)]
        [MenuItem(AssetsRoot + "90° Clockwise", true)]
        [MenuItem(AssetsRoot + "90° Counter Clockwise", true)]
        [MenuItem(AssetsRoot + "Flip Canvas Horizontal", true)]
        [MenuItem(AssetsRoot + "Flip Canvas Vertical", true)]
        private static bool ValidateQuickTransform()
        {
            return TextureTransformProcessor.HasSelectedTextures();
        }

        [MenuItem(AssetsRoot + "Arbitrary...", true)]
        private static bool ValidateAssetsArbitrary()
        {
            return TextureTransformProcessor.HasSelectedTextures();
        }

        [MenuItem(ToolsReplaceRoot + "180°", true)]
        [MenuItem(ToolsReplaceRoot + "90° Clockwise", true)]
        [MenuItem(ToolsReplaceRoot + "90° Counter Clockwise", true)]
        [MenuItem(ToolsReplaceRoot + "Flip Canvas Horizontal", true)]
        [MenuItem(ToolsReplaceRoot + "Flip Canvas Vertical", true)]
        [MenuItem(AssetsReplaceRoot + "180°", true)]
        [MenuItem(AssetsReplaceRoot + "90° Clockwise", true)]
        [MenuItem(AssetsReplaceRoot + "90° Counter Clockwise", true)]
        [MenuItem(AssetsReplaceRoot + "Flip Canvas Horizontal", true)]
        [MenuItem(AssetsReplaceRoot + "Flip Canvas Vertical", true)]
        private static bool ValidateReplaceTransform()
        {
            IReadOnlyList<string> paths = TextureTransformProcessor.GetSelectedTexturePaths();
            return paths.Count > 0 && paths.All(TextureTransformProcessor.CanOverwrite);
        }

        [MenuItem(ToolsReplaceRoot + "Arbitrary...", true)]
        [MenuItem(AssetsReplaceRoot + "Arbitrary...", true)]
        private static bool ValidateReplaceArbitrary()
        {
            return ValidateReplaceTransform();
        }

        private static void ExecuteQuick(TextureTransformOperation operation)
        {
            TextureTransformRequest request = new TextureTransformRequest
            {
                operation = operation,
                output = TextureTransformOutput.CreatePngCopy,
                copyImporterSettings = true
            };

            try
            {
                IReadOnlyList<string> outputs = TextureTransformProcessor.TransformSelection(request);
                string message = outputs.Count == 1
                    ? $"Created:\n{outputs[0]}"
                    : $"Created {outputs.Count} transformed textures.";
                EditorUtility.DisplayDialog("Texture Transform Complete", message, "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Texture Transform Failed", exception.Message, "OK");
            }
        }

        private static void ExecuteReplace(TextureTransformOperation operation)
        {
            IReadOnlyList<string> selectedPaths = TextureTransformProcessor.GetSelectedTexturePaths();
            bool confirmed = EditorUtility.DisplayDialog(
                "Replace Original Texture Files?",
                $"Replace {selectedPaths.Count} original file(s) using '{GetOperationName(operation)}'? " +
                "This file operation cannot be undone by Unity.",
                "Replace Original",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            TextureTransformRequest request = new TextureTransformRequest
            {
                operation = operation,
                output = TextureTransformOutput.ReplaceOriginal
            };

            try
            {
                IReadOnlyList<string> outputs = TextureTransformProcessor.TransformSelection(request);
                string message = outputs.Count == 1
                    ? $"Replaced:\n{outputs[0]}"
                    : $"Replaced {outputs.Count} original textures.";
                EditorUtility.DisplayDialog("Texture Replace Complete", message, "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Texture Replace Failed", exception.Message, "OK");
            }
        }

        private static string GetOperationName(TextureTransformOperation operation)
        {
            return new TextureTransformRequest { operation = operation }.GetDisplayName();
        }
    }
}
