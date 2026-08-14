using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FranklinGame.Rendering;
using FranklinGame.UI;
using FranklinGame.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FranklinGame.Settings.Editor
{
    /// <summary>
    /// Installs the lightweight SSAO feature and connects the Settings runtime
    /// components to the existing CanvasPlayerControl prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class FranklinMobileSettingsInstaller
    {
        private static int s_CaptureAfterFrame;
        private static bool s_PreviousRunInBackground;
        private const string PlayerCanvasPath =
            "Assets/Prefab/CanvasPlayerControl.prefab";
        private const string SettingsPrefabPath =
            "Assets/SettingGame/Setting.prefab";
        private const string MobileRendererPath =
            "Assets/Settings/Mobile_Renderer.asset";
        private const string PcRendererPath =
            "Assets/Settings/PC_Renderer.asset";
        private const string FastDofShaderPath =
            "Assets/SettingGame/FastDepthOfField/Shaders/FranklinFastMobileDepthOfField.shader";
        private const string HudFontPath =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/UI/_Fonts/JosefinSans-Bold.ttf";
        private const string GearSpritePath =
            "Assets/UI/FranklinPlayerHud/Resources/FranklinPlayerHud/settings-button.png";
        private const string SolidSpritePath =
            "Assets/UI/FranklinPlayerHud/Resources/FranklinPlayerHud/ui-solid.png";

        static FranklinMobileSettingsInstaller()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Tools/Franklin Game/Install Mobile Settings + SSAO + DOF")]
        public static void Install()
        {
            ScreenSpaceAmbientOcclusion mobileSsao = EnsureMobileSsaoFeature();
            ScreenSpaceAmbientOcclusion pcSsao = FindSsaoFeature(PcRendererPath);
            ConfigureForDesktop(pcSsao);
            EnsureFastDofFeature(MobileRendererPath);
            EnsureFastDofFeature(PcRendererPath);
            InstallSettingsPrefab(mobileSsao, pcSsao);
            RemoveSettingsComponentsFromPlayerCanvas();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin Settings components are installed on Setting.prefab. " +
                "SSAO defaults OFF; Fast Mobile DOF is installed at quarter resolution."
            );
        }

        [MenuItem("Tools/Franklin Game/Smoke Test Mobile Settings", true)]
        private static bool CanRunSmokeTest()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Tools/Franklin Game/Smoke Test Mobile Settings")]
        public static void RunSmokeTest()
        {
            FranklinMobileSettingsPanel panel =
                UnityEngine.Object.FindFirstObjectByType<FranklinMobileSettingsPanel>();
            if (panel == null)
            {
                Debug.LogError("[Franklin Settings Smoke Test] Settings panel was not found.");
                return;
            }

            bool originalFbs = FranklinMobileGraphicsSettings.FbsEnabled;
            bool originalSsao = FranklinMobileGraphicsSettings.SsaoEnabled;
            int originalQuality = FranklinMobileGraphicsSettings.QualityLevel;
            int originalAntiAliasing =
                FranklinMobileGraphicsSettings.AntiAliasingMode;
            try
            {
                panel.Close();
                panel.Open();
                if (!panel.IsOpen || !FranklinMobileHud.ControlsSuppressed)
                    throw new InvalidOperationException(
                        "Panel did not open or gameplay controls were not suppressed."
                    );
                panel.Close();
                if (panel.IsOpen)
                    throw new InvalidOperationException("Panel did not close.");

                bool testFbs = !originalFbs;
                FranklinMobileGraphicsSettings.SetFbsEnabled(testFbs, false);
                if (FranklinBlobShadow.GlobalEnabled != testFbs)
                    throw new InvalidOperationException("FBS master toggle did not apply.");

                if (!FranklinMobileGraphicsSettings.HasSsaoFeature)
                    throw new InvalidOperationException("SSAO renderer feature is missing.");
                bool testSsao = !originalSsao;
                FranklinMobileGraphicsSettings.SetSsaoEnabled(testSsao, false);
                if (FranklinMobileGraphicsSettings.SsaoRenderPassActive != testSsao)
                    throw new InvalidOperationException("SSAO render pass state did not apply.");

                int testQuality = (originalQuality + 1) % 4;
                FranklinMobileGraphicsSettings.SetQualityLevel(testQuality, false);
                if (FranklinMobileGraphicsSettings.QualityLevel != testQuality)
                    throw new InvalidOperationException("Quality selection did not apply.");

                Camera aaCamera = Camera.allCameras.FirstOrDefault(camera =>
                    camera != null &&
                    camera.TryGetComponent(out UniversalAdditionalCameraData cameraData) &&
                    cameraData.renderType == CameraRenderType.Base
                );
                if (aaCamera == null ||
                    !aaCamera.TryGetComponent(
                        out UniversalAdditionalCameraData additionalCameraData))
                {
                    throw new InvalidOperationException(
                        "No active URP Base gameplay camera was found for anti-aliasing."
                    );
                }

                int testAntiAliasing = (originalAntiAliasing + 1) % 3;
                FranklinMobileGraphicsSettings.SetAntiAliasingMode(
                    testAntiAliasing,
                    false
                );
                AntialiasingMode expectedAntiAliasing = testAntiAliasing switch
                {
                    FranklinMobileGraphicsSettings.AntiAliasingOff =>
                        AntialiasingMode.None,
                    FranklinMobileGraphicsSettings.AntiAliasingSmaa =>
                        AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    _ => AntialiasingMode.FastApproximateAntialiasing
                };
                if (additionalCameraData.antialiasing != expectedAntiAliasing)
                {
                    throw new InvalidOperationException(
                        "Anti-aliasing selection did not reach the gameplay camera."
                    );
                }
                if (testAntiAliasing !=
                        FranklinMobileGraphicsSettings.AntiAliasingOff &&
                    (!additionalCameraData.renderPostProcessing ||
                     additionalCameraData.antialiasingQuality !=
                        AntialiasingQuality.Low))
                {
                    throw new InvalidOperationException(
                        "Mobile anti-aliasing post-process or Low quality was not applied."
                    );
                }

                FranklinFpsCounter fpsCounter =
                    UnityEngine.Object.FindFirstObjectByType<FranklinFpsCounter>();
                if (fpsCounter == null)
                    throw new InvalidOperationException("FPS counter did not auto-spawn.");
                Canvas fpsCanvas = fpsCounter.GetComponentInParent<Canvas>();
                if (fpsCanvas == null ||
                    fpsCanvas.gameObject.name != "CanvasPlayerControl")
                {
                    throw new InvalidOperationException(
                        "FPS counter was not attached to CanvasPlayerControl."
                    );
                }

                Debug.Log(
                    "[Franklin Settings Smoke Test] PASS: panel, input lock, FBS, " +
                    "SSAO render pass, quality API, anti-aliasing camera API and " +
                    "auto-spawned FPS counter."
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                panel.Close();
                FranklinMobileGraphicsSettings.SetFbsEnabled(originalFbs, false);
                FranklinMobileGraphicsSettings.SetSsaoEnabled(originalSsao, false);
                FranklinMobileGraphicsSettings.SetQualityLevel(originalQuality, false);
                FranklinMobileGraphicsSettings.SetAntiAliasingMode(
                    originalAntiAliasing,
                    false
                );
            }
        }

        [MenuItem("Tools/Franklin Game/Open + Capture Mobile Settings", true)]
        private static bool CanCaptureRuntimePreview()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Tools/Franklin Game/Open + Capture Mobile Settings")]
        public static void CaptureRuntimePreview()
        {
            FranklinMobileSettingsPanel panel =
                UnityEngine.Object.FindFirstObjectByType<FranklinMobileSettingsPanel>();
            if (panel == null)
            {
                Debug.LogError("Mobile Settings capture could not find the panel.");
                return;
            }

            panel.Open();
            s_PreviousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // Wait two game frames so the newly enabled Canvas has completed a
            // layout and render pass before visual QA capture.
            s_CaptureAfterFrame = Time.frameCount + 2;
            EditorApplication.update -= CaptureOpenedPanel;
            EditorApplication.update += CaptureOpenedPanel;
            Debug.Log("Mobile Settings runtime preview queued after two game frames.");
        }

        private static void CaptureOpenedPanel()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= CaptureOpenedPanel;
                Application.runInBackground = s_PreviousRunInBackground;
                return;
            }
            if (Time.frameCount < s_CaptureAfterFrame) return;
            EditorApplication.update -= CaptureOpenedPanel;
            Application.runInBackground = s_PreviousRunInBackground;

            string capturePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Temp/FranklinMobileSettingsRuntime.png")
            );
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture(1);
            if (capture == null)
            {
                Debug.LogError("Mobile Settings runtime preview capture returned null.");
                return;
            }

            File.WriteAllBytes(capturePath, capture.EncodeToPNG());
            UnityEngine.Object.Destroy(capture);
            Debug.Log($"Mobile Settings runtime preview saved: {capturePath}");
        }

        private static void InstallIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallIfNeeded;
                return;
            }

            GameObject settings =
                AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            GameObject canvas =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCanvasPath);
            ScreenSpaceAmbientOcclusion mobileSsao = FindSsaoFeature(MobileRendererPath);
            FranklinFastDepthOfFieldFeature mobileDof =
                FindFastDofFeature(MobileRendererPath);
            FranklinFastDepthOfFieldFeature pcDof =
                FindFastDofFeature(PcRendererPath);
            bool settingsReady = settings != null &&
                settings.GetComponent<FranklinMobileGraphicsSettings>() != null &&
                settings.GetComponent<FranklinMobileSettingsPanel>() != null;
            bool canvasClean = canvas != null &&
                canvas.GetComponent<FranklinMobileGraphicsSettings>() == null &&
                canvas.GetComponent<FranklinMobileSettingsPanel>() == null;
            if (settingsReady && canvasClean && mobileSsao != null &&
                mobileDof != null && pcDof != null)
            {
                return;
            }

            Install();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            // Runtime toggles operate directly on renderer-feature instances.
            // Restore asset-active state after Play Mode so URP build stripping
            // always sees SSAO and keeps the variants required by the toggle.
            RestoreBuildFeatureState(MobileRendererPath);
            RestoreBuildFeatureState(PcRendererPath);
        }

        private static void RestoreBuildFeatureState(string rendererPath)
        {
            ScreenSpaceAmbientOcclusion feature = FindSsaoFeature(rendererPath);
            if (feature == null || feature.isActive) return;

            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            ScriptableRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData != null)
            {
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
            }
            AssetDatabase.SaveAssetIfDirty(feature);
            if (rendererData != null) AssetDatabase.SaveAssetIfDirty(rendererData);
        }

        private static ScreenSpaceAmbientOcclusion EnsureMobileSsaoFeature()
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MobileRendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"Mobile Settings could not load {MobileRendererPath}");
                return null;
            }

            ScreenSpaceAmbientOcclusion feature = FindSsaoFeature(MobileRendererPath);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "Mobile SSAO (Franklin Low Cost)";
                // Keep the asset active so URP's build-time shader stripper
                // retains SSAO variants. Runtime Settings applies the saved
                // (mobile-default OFF) state in Awake before the first frame.
                feature.SetActive(true);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature,
                    out _,
                    out long localId
                );

                SerializedObject rendererObject = new(rendererData);
                rendererObject.Update();
                SerializedProperty features =
                    rendererObject.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap =
                    rendererObject.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.arraySize += 1;
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                featureMap.arraySize += 1;
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
                rendererObject.ApplyModifiedPropertiesWithoutUndo();
            }

            ConfigureForMobile(feature);
            feature.SetActive(true);
            feature.Create();
            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            return feature;
        }

        private static FranklinFastDepthOfFieldFeature EnsureFastDofFeature(
            string rendererPath)
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"Mobile Settings could not load {rendererPath}");
                return null;
            }

            FranklinFastDepthOfFieldFeature feature =
                FindFastDofFeature(rendererPath);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<
                    FranklinFastDepthOfFieldFeature>();
                feature.name = "Fast Mobile DOF (Franklin Quarter Res)";
                feature.SetActive(true);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature,
                    out _,
                    out long localId
                );

                SerializedObject rendererObject = new(rendererData);
                rendererObject.Update();
                SerializedProperty features =
                    rendererObject.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap =
                    rendererObject.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.arraySize += 1;
                features.GetArrayElementAtIndex(index).objectReferenceValue =
                    feature;
                featureMap.arraySize += 1;
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
                rendererObject.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject featureObject = new(feature);
            featureObject.Update();
            featureObject.FindProperty("m_Shader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>(FastDofShaderPath);
            featureObject.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            feature.Create();
            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            return feature;
        }

        private static void ConfigureForMobile(ScreenSpaceAmbientOcclusion feature)
        {
            if (feature == null) return;
            SerializedObject serializedFeature = new(feature);
            serializedFeature.Update();
            SerializedProperty settings = serializedFeature.FindProperty("m_Settings");
            SetInt(settings, "AOMethod", 1);       // Interleaved gradient: no noise texture fetch.
            SetBool(settings, "Downsample", true); // Half resolution.
            SetBool(settings, "AfterOpaque", false);
            SetInt(settings, "Source", 0);          // Reconstruct from depth; no normals prepass.
            SetInt(settings, "NormalSamples", 0);   // Low.
            SetFloat(settings, "Intensity", 1.15f);
            SetFloat(settings, "DirectLightingStrength", 0.35f);
            SetFloat(settings, "Radius", 0.24f);
            SetInt(settings, "Samples", 2);         // Low: four AO samples.
            SetInt(settings, "BlurQuality", 2);     // Low-cost Kawase blur.
            SetFloat(settings, "Falloff", 80f);
            SetInt(settings, "SampleCount", -1);
            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureForDesktop(ScreenSpaceAmbientOcclusion feature)
        {
            if (feature == null) return;
            SerializedObject serializedFeature = new(feature);
            serializedFeature.Update();
            SerializedProperty settings = serializedFeature.FindProperty("m_Settings");
            SetFloat(settings, "Intensity", 1.15f);
            SetFloat(settings, "DirectLightingStrength", 0.35f);
            SetFloat(settings, "Radius", 0.24f);
            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }

        private static void InstallSettingsPrefab(
            ScreenSpaceAmbientOcclusion mobileSsao,
            ScreenSpaceAmbientOcclusion pcSsao)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (source == null)
            {
                Debug.LogError($"Mobile Settings could not load {SettingsPrefabPath}");
                return;
            }

            GameObject settingsRoot = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
            try
            {
                FranklinMobileGraphicsSettings graphics =
                    settingsRoot.GetComponent<FranklinMobileGraphicsSettings>() ??
                    settingsRoot.AddComponent<FranklinMobileGraphicsSettings>();
                FranklinMobileSettingsPanel panel =
                    settingsRoot.GetComponent<FranklinMobileSettingsPanel>() ??
                    settingsRoot.AddComponent<FranklinMobileSettingsPanel>();

                List<ScreenSpaceAmbientOcclusion> features = new();
                if (mobileSsao != null) features.Add(mobileSsao);
                if (pcSsao != null && pcSsao != mobileSsao) features.Add(pcSsao);

                SerializedObject graphicsObject = new(graphics);
                SerializedProperty featureArray =
                    graphicsObject.FindProperty("m_SsaoFeatures");
                featureArray.arraySize = features.Count;
                for (int i = 0; i < features.Count; i++)
                {
                    featureArray.GetArrayElementAtIndex(i).objectReferenceValue =
                        features[i];
                }
                graphicsObject.FindProperty("m_SsaoLayerMask").intValue =
                    (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10);
                graphicsObject.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject panelObject = new(panel);
                panelObject.FindProperty("m_Font").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
                panelObject.FindProperty("m_GearSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(GearSpritePath);
                panelObject.FindProperty("m_SolidSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
                panelObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(settingsRoot, SettingsPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(settingsRoot);
            }
        }

        private static void RemoveSettingsComponentsFromPlayerCanvas()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCanvasPath);
            if (source == null) return;

            GameObject canvas = PrefabUtility.LoadPrefabContents(PlayerCanvasPath);
            try
            {
                FranklinMobileSettingsPanel panel =
                    canvas.GetComponent<FranklinMobileSettingsPanel>();
                FranklinMobileGraphicsSettings graphics =
                    canvas.GetComponent<FranklinMobileGraphicsSettings>();
                bool changed = panel != null || graphics != null;
                if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
                if (graphics != null) UnityEngine.Object.DestroyImmediate(graphics);
                if (changed) PrefabUtility.SaveAsPrefabAsset(canvas, PlayerCanvasPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvas);
            }
        }

        private static ScreenSpaceAmbientOcclusion FindSsaoFeature(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<ScreenSpaceAmbientOcclusion>()
                .FirstOrDefault();
        }

        private static FranklinFastDepthOfFieldFeature FindFastDofFeature(
            string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<FranklinFastDepthOfFieldFeature>()
                .FirstOrDefault();
        }

        private static void SetBool(
            SerializedProperty parent,
            string propertyName,
            bool value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static void SetInt(
            SerializedProperty parent,
            string propertyName,
            int value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(propertyName);
            if (property != null) property.intValue = value;
        }

        private static void SetFloat(
            SerializedProperty parent,
            string propertyName,
            float value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(propertyName);
            if (property != null) property.floatValue = value;
        }
    }
}
