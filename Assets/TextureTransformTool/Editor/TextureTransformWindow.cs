using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TextureTransformTool
{
    internal sealed class TextureTransformWindow : EditorWindow
    {
        private const float ButtonHeight = 28f;

        [SerializeField] private TextureTransformRequest request = new TextureTransformRequest();
        [SerializeField] private Vector2 scrollPosition;

        private IReadOnlyList<string> selectedPaths = Array.Empty<string>();

        [MenuItem("Tools/Texture Transform/Open Tool...", priority = 1)]
        public static void Open()
        {
            TextureTransformWindow window = GetWindow<TextureTransformWindow>();
            window.titleContent = new GUIContent("Texture Transform");
            window.minSize = new Vector2(390f, 520f);
            window.RefreshSelection();
            window.Show();
        }

        public static void OpenForOperation(
            TextureTransformOperation operation,
            TextureTransformOutput output = TextureTransformOutput.CreatePngCopy)
        {
            Open();
            TextureTransformWindow window = GetWindow<TextureTransformWindow>();
            window.request.operation = operation;
            window.request.output = output;
            window.Repaint();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Texture Transform");
            Selection.selectionChanged += RefreshSelection;
            RefreshSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RefreshSelection;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawSelection();
            EditorGUILayout.Space(10f);
            DrawOperations();
            EditorGUILayout.Space(12f);
            DrawOptions();
            EditorGUILayout.Space(14f);
            DrawApplyButton();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Texture Transform", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Rotate or flip one or more Texture2D assets selected in the Project window.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSelection()
        {
            EditorGUILayout.LabelField($"Selected Textures ({selectedPaths.Count})", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                if (selectedPaths.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Select at least one Texture2D asset in the Project window.",
                        MessageType.Info);
                    return;
                }

                int shown = Mathf.Min(selectedPaths.Count, 6);
                for (int index = 0; index < shown; index++)
                {
                    EditorGUILayout.LabelField("• " + Path.GetFileName(selectedPaths[index]), EditorStyles.miniLabel);
                }

                if (selectedPaths.Count > shown)
                {
                    EditorGUILayout.LabelField($"… and {selectedPaths.Count - shown} more", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawOperations()
        {
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            DrawOperationButton("180°", TextureTransformOperation.Rotate180);
            DrawOperationButton("90° Clockwise", TextureTransformOperation.RotateClockwise90);
            DrawOperationButton("90° Counter Clockwise", TextureTransformOperation.RotateCounterClockwise90);
            DrawOperationButton("Arbitrary...", TextureTransformOperation.Arbitrary);
            EditorGUILayout.Space(4f);
            DrawSeparator();
            EditorGUILayout.Space(4f);
            DrawOperationButton("Flip Canvas Horizontal", TextureTransformOperation.FlipHorizontal);
            DrawOperationButton("Flip Canvas Vertical", TextureTransformOperation.FlipVertical);

            if (request.operation == TextureTransformOperation.Arbitrary)
            {
                EditorGUILayout.Space(8f);
                using (new EditorGUI.IndentLevelScope())
                {
                    request.angle = EditorGUILayout.FloatField(
                        new GUIContent("Clockwise Angle", "Positive values rotate clockwise."),
                        request.angle);
                    request.expandCanvas = EditorGUILayout.Toggle(
                        new GUIContent("Expand Canvas", "Grow the output so no rotated pixels are cropped."),
                        request.expandCanvas);
                    request.sampling = (TextureTransformSampling)EditorGUILayout.EnumPopup(
                        new GUIContent("Sampling", "Nearest keeps hard pixel edges; Bilinear produces smoother results."),
                        request.sampling);
                }
            }
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            request.output = (TextureTransformOutput)EditorGUILayout.EnumPopup("Mode", request.output);

            if (request.output == TextureTransformOutput.CreatePngCopy)
            {
                request.copyImporterSettings = EditorGUILayout.Toggle(
                    new GUIContent("Copy Import Settings", "Copy compatible Texture Importer settings to the new PNG."),
                    request.copyImporterSettings);
                EditorGUILayout.HelpBox(
                    "Creates a uniquely named PNG beside each source texture. Source files are not changed.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Replace Original permanently replaces the selected source files. " +
                    "Only PNG, JPG, TGA and EXR are supported.",
                    MessageType.Warning);
            }
        }

        private void DrawApplyButton()
        {
            using (new EditorGUI.DisabledScope(selectedPaths.Count == 0))
            {
                string action = request.output == TextureTransformOutput.ReplaceOriginal
                    ? "Replace Original"
                    : "Create PNG Copy";
                string label = $"{action}: {request.GetDisplayName()}";
                if (GUILayout.Button(label, GUILayout.Height(36f)))
                {
                    Apply();
                }
            }
        }

        private void DrawOperationButton(string label, TextureTransformOperation operation)
        {
            bool selected = request.operation == operation;
            Color previous = GUI.backgroundColor;
            if (selected)
            {
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(ButtonHeight)))
            {
                request.operation = operation;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = previous;
        }

        private static void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.35f, 0.35f)
                : new Color(0.72f, 0.72f, 0.72f));
        }

        private void Apply()
        {
            if (request.output == TextureTransformOutput.ReplaceOriginal)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Replace Original Texture Files?",
                    $"This will replace {selectedPaths.Count} original file(s) with the transformed pixels. " +
                    "This file operation cannot be undone by Unity.",
                    "Replace Original",
                    "Cancel");
                if (!confirmed)
                {
                    return;
                }
            }

            try
            {
                IReadOnlyList<string> outputs = TextureTransformProcessor.TransformSelection(request);
                EditorUtility.DisplayDialog(
                    "Texture Transform Complete",
                    FormatResult(outputs, request.output),
                    "OK");
                RefreshSelection();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Texture Transform Failed", exception.Message, "OK");
            }
        }

        private static string FormatResult(
            IReadOnlyList<string> outputs,
            TextureTransformOutput outputMode)
        {
            string verb = outputMode == TextureTransformOutput.ReplaceOriginal ? "Replaced" : "Created";
            if (outputs.Count == 1)
            {
                return $"{verb}:\n{outputs[0]}";
            }

            return $"{verb} {outputs.Count} textures successfully.";
        }

        private void RefreshSelection()
        {
            selectedPaths = TextureTransformProcessor.GetSelectedTexturePaths();
            Repaint();
        }
    }
}
