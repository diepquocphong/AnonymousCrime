using System;
using System.Collections.Generic;
using FranklinGame.Rendering;
using RobotAstro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FranklinGame.Settings
{
    /// <summary>
    /// Persistent, mobile-safe graphics settings API shared by the Settings UI
    /// and gameplay systems. Renderer features are only touched when a value
    /// changes; there is no per-frame renderer lookup or allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    [AddComponentMenu("Franklin Game/Settings/Mobile Graphics Settings")]
    public sealed class FranklinMobileGraphicsSettings : MonoBehaviour
    {
        public const string SsaoEnabledPreferenceKey =
            "Franklin.MobileGraphics.SSAO.Enabled";
        public const string QualityLevelPreferenceKey =
            "Franklin.MobileGraphics.Quality.Level";
        public const string AntiAliasingModePreferenceKey =
            "Franklin.MobileGraphics.AntiAliasing.Mode";
        public const string DepthOfFieldModePreferenceKey =
            "Franklin.MobileGraphics.DepthOfField.Mode";

        public const int QualityLow = 0;
        public const int QualityBalanced = 1;
        public const int QualityHigh = 2;
        public const int QualityAuto = 3;

        public const int AntiAliasingOff = 0;
        public const int AntiAliasingFxaa = 1;
        public const int AntiAliasingSmaa = 2;

        public const int DepthOfFieldOff =
            FranklinFastDepthOfFieldFeature.ModeOff;
        public const int DepthOfFieldNear =
            FranklinFastDepthOfFieldFeature.ModeNear;
        public const int DepthOfFieldFar =
            FranklinFastDepthOfFieldFeature.ModeFar;

        private const int DefaultQualityLevel = QualityAuto;
        private const int DefaultAntiAliasingMode = AntiAliasingFxaa;
        private const int DefaultDepthOfFieldMode = DepthOfFieldOff;
        private const int DefaultSsaoLayerMask =
            (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10);

        private static FranklinMobileGraphicsSettings s_Instance;
        private static bool s_StateLoaded;
        private static bool s_SsaoEnabled;
        private static int s_QualityLevel = DefaultQualityLevel;
        private static int s_AntiAliasingMode = DefaultAntiAliasingMode;
        private static int s_DepthOfFieldMode = DefaultDepthOfFieldMode;

        [Tooltip("SSAO features used by Mobile and Editor/PC renderer data.")]
        [SerializeField] private ScriptableRendererFeature[] m_SsaoFeatures =
            Array.Empty<ScriptableRendererFeature>();
        [Tooltip("Environment layers whose cameras are eligible for SSAO: " +
                 "Ground, Building, Wall and Prop by default.")]
        [SerializeField] private LayerMask m_SsaoLayerMask = DefaultSsaoLayerMask;

        private bool m_MissingFeatureWarningLogged;
        private bool m_SsaoFeatureStateKnown;
        private bool m_LastSsaoFeatureState;
        private readonly Dictionary<Camera, CameraAntiAliasingState>
            m_CameraAntiAliasingStates = new();

        public static event Action<bool> SsaoEnabledChanged;
        public static event Action<int> QualityLevelChanged;
        public static event Action<int> AntiAliasingModeChanged;
        public static event Action<int> DepthOfFieldModeChanged;

        public static bool FbsEnabled => FranklinBlobShadow.GlobalEnabled;

        public static bool SsaoEnabled
        {
            get
            {
                EnsureStateLoaded();
                return s_SsaoEnabled;
            }
        }

        public static int QualityLevel
        {
            get
            {
                EnsureStateLoaded();
                return s_QualityLevel;
            }
        }

        /// <summary>
        /// Current post-process anti-aliasing mode: Off, FXAA, or SMAA Low.
        /// FXAA is the mobile default because it has the lowest GPU cost.
        /// </summary>
        public static int AntiAliasingMode
        {
            get
            {
                EnsureStateLoaded();
                return s_AntiAliasingMode;
            }
        }

        /// <summary>Current focus preset: Off, Near, or Far.</summary>
        public static int DepthOfFieldMode
        {
            get
            {
                EnsureStateLoaded();
                return s_DepthOfFieldMode;
            }
        }

        public static bool HasSsaoFeature =>
            s_Instance != null && s_Instance.HasValidSsaoFeature();

        /// <summary>True when at least one installed SSAO render pass is active.</summary>
        public static bool SsaoRenderPassActive =>
            s_Instance != null && s_Instance.HasActiveSsaoFeature();

        public static LayerMask SsaoLayerMask => s_Instance != null
            ? s_Instance.m_SsaoLayerMask
            : (LayerMask)DefaultSsaoLayerMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Instance = null;
            s_StateLoaded = false;
            s_SsaoEnabled = false;
            s_QualityLevel = DefaultQualityLevel;
            s_AntiAliasingMode = DefaultAntiAliasingMode;
            s_DepthOfFieldMode = DefaultDepthOfFieldMode;
            SsaoEnabledChanged = null;
            QualityLevelChanged = null;
            AntiAliasingModeChanged = null;
            DepthOfFieldModeChanged = null;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                // A scene may briefly contain two player canvases during a handoff.
                // Keep the first settings owner and leave the duplicate HUD intact.
                this.enabled = false;
                return;
            }

            s_Instance = this;
            EnsureStateLoaded();
            ApplyQualityEffects(s_QualityLevel, false, false);
        }

        private void OnEnable()
        {
            // With Enter Play Mode scene/domain reload disabled, Awake might not
            // run again even though SubsystemRegistration reset static fields.
            if (s_Instance == null)
            {
                s_Instance = this;
                EnsureStateLoaded();
                ApplyQualityEffects(s_QualityLevel, false, false);
            }
            if (s_Instance != this) return;
            Camera.onPreCull -= this.OnCameraPreCull;
            Camera.onPreCull += this.OnCameraPreCull;
            this.ApplyAntiAliasingToActiveCameras();
        }

        private void Start()
        {
            // DynamicResolutionScaler registers in Awake. Start guarantees the
            // scene scaler is available regardless of script creation order.
            SetQualityLevel(QualityLevel, false);
        }

        private void OnDestroy()
        {
            if (s_Instance != this) return;
            Camera.onPreCull -= this.OnCameraPreCull;
            this.RestoreCameraAntiAliasingStates();
            s_Instance = null;
        }

        private void OnDisable()
        {
            if (s_Instance != this) return;
            Camera.onPreCull -= this.OnCameraPreCull;
            this.RestoreCameraAntiAliasingStates();
        }

        /// <summary>Master API for all Fast Blob Shadows in the game.</summary>
        public static void SetFbsEnabled(bool enabled, bool persist = true)
        {
            EnsureStateLoaded();
            if (s_QualityLevel == QualityLow) enabled = false;
            else if (s_QualityLevel == QualityBalanced ||
                     s_QualityLevel == QualityHigh)
            {
                enabled = true;
            }
            FranklinBlobShadow.SetGlobalEnabled(enabled, persist);
        }

        public static bool ToggleFbs(bool persist = true)
        {
            bool enabled = !FbsEnabled;
            SetFbsEnabled(enabled, persist);
            return FbsEnabled;
        }

        /// <summary>
        /// Enables or disables SSAO at the renderer-feature level. OFF skips the
        /// complete SSAO render pass rather than merely making it transparent.
        /// </summary>
        public static void SetSsaoEnabled(bool enabled, bool persist = true)
        {
            EnsureStateLoaded();
            if (s_QualityLevel == QualityLow ||
                s_QualityLevel == QualityBalanced)
            {
                enabled = false;
            }
            else if (s_QualityLevel == QualityHigh)
            {
                enabled = true;
            }
            bool changed = s_SsaoEnabled != enabled;
            s_SsaoEnabled = enabled;

            if (persist)
            {
                PlayerPrefs.SetInt(SsaoEnabledPreferenceKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (s_Instance != null) s_Instance.ApplySsaoFeatures(enabled);
            if (changed) SsaoEnabledChanged?.Invoke(enabled);
        }

        public static bool ToggleSsao(bool persist = true)
        {
            bool enabled = !SsaoEnabled;
            SetSsaoEnabled(enabled, persist);
            return SsaoEnabled;
        }

        /// <summary>
        /// Changes which environment layers make a gameplay camera eligible for
        /// SSAO. URP's built-in SSAO is screen-space, so it still evaluates all
        /// opaque pixels rendered by an eligible camera.
        /// </summary>
        public static void SetSsaoLayerMask(LayerMask layerMask)
        {
            if (s_Instance == null) return;
            s_Instance.m_SsaoLayerMask = layerMask;
            s_Instance.m_SsaoFeatureStateKnown = false;
            s_Instance.ApplySsaoFeatures(
                s_QualityLevel != QualityLow && s_SsaoEnabled
            );
        }

        /// <summary>
        /// Applies Auto/Low/Balanced/High to the dynamic-resolution controller and
        /// persists the selection. Returns false if no scaler is active yet.
        /// </summary>
        public static bool SetQualityLevel(int qualityLevel, bool persist = true)
        {
            EnsureStateLoaded();
            int nextLevel = Mathf.Clamp(qualityLevel, QualityLow, QualityAuto);
            bool changed = s_QualityLevel != nextLevel;
            s_QualityLevel = nextLevel;

            // A preset changes several dependent switches. Apply them without
            // flushing each preference separately, then commit the coherent
            // profile once to avoid repeated synchronous mobile-storage stalls.
            ApplyQualityEffects(nextLevel, false, changed);

            if (persist)
            {
                PersistCurrentSettings();
            }

            bool applied = ApplyQualityToScaler(nextLevel);
            if (changed) QualityLevelChanged?.Invoke(nextLevel);
            return applied;
        }

        /// <summary>
        /// Selects Off, mobile-recommended FXAA, or sharper SMAA Low. The setting
        /// is applied to every active URP Base gameplay camera and to cameras that
        /// become active later (for example when entering a car or bike).
        /// </summary>
        public static void SetAntiAliasingMode(int antiAliasingMode, bool persist = true)
        {
            EnsureStateLoaded();
            int nextMode = Mathf.Clamp(
                antiAliasingMode,
                AntiAliasingOff,
                AntiAliasingSmaa
            );
            if (s_QualityLevel == QualityLow ||
                s_QualityLevel == QualityBalanced)
            {
                nextMode = AntiAliasingOff;
            }
            else if (s_QualityLevel == QualityHigh)
            {
                nextMode = AntiAliasingSmaa;
            }
            bool changed = s_AntiAliasingMode != nextMode;
            s_AntiAliasingMode = nextMode;

            if (persist)
            {
                PlayerPrefs.SetInt(AntiAliasingModePreferenceKey, nextMode);
                PlayerPrefs.Save();
            }

            if (s_Instance != null) s_Instance.ApplyAntiAliasingToActiveCameras();
            if (changed) AntiAliasingModeChanged?.Invoke(nextMode);
        }

        /// <summary>
        /// Selects Off, Near focus, or Far focus. Low/Balanced force this pass
        /// off; High enables Far when selected but still permits a manual Off.
        /// </summary>
        public static void SetDepthOfFieldMode(
            int depthOfFieldMode,
            bool persist = true)
        {
            EnsureStateLoaded();
            int nextMode = Mathf.Clamp(
                depthOfFieldMode,
                DepthOfFieldOff,
                DepthOfFieldFar
            );
            if (s_QualityLevel == QualityLow ||
                s_QualityLevel == QualityBalanced)
            {
                nextMode = DepthOfFieldOff;
            }
            bool changed = s_DepthOfFieldMode != nextMode;
            s_DepthOfFieldMode = nextMode;
            FranklinFastDepthOfFieldFeature.SetRuntimeMode(nextMode);

            if (persist)
            {
                PlayerPrefs.SetInt(DepthOfFieldModePreferenceKey, nextMode);
                PlayerPrefs.Save();
            }

            if (changed) DepthOfFieldModeChanged?.Invoke(nextMode);
        }

        public static int ToggleDepthOfFieldMode(bool persist = true)
        {
            EnsureStateLoaded();
            int nextMode;
            if (s_QualityLevel == QualityHigh ||
                s_QualityLevel == QualityAuto)
            {
                nextMode = (s_DepthOfFieldMode + 1) %
                    (DepthOfFieldFar + 1);
            }
            else
            {
                nextMode = DepthOfFieldOff;
            }

            SetDepthOfFieldMode(nextMode, persist);
            return s_DepthOfFieldMode;
        }

        /// <summary>
        /// Flushes one coherent graphics snapshot. Web controls use this after a
        /// short debounce so several quick taps do not synchronously stall mobile
        /// storage once per field.
        /// </summary>
        public static void PersistCurrentSettings()
        {
            EnsureStateLoaded();
            PlayerPrefs.SetInt(QualityLevelPreferenceKey, s_QualityLevel);
            PlayerPrefs.SetInt(
                FranklinBlobShadow.GlobalEnabledPreferenceKey,
                FbsEnabled ? 1 : 0
            );
            PlayerPrefs.SetInt(
                SsaoEnabledPreferenceKey,
                s_SsaoEnabled ? 1 : 0
            );
            PlayerPrefs.SetInt(
                AntiAliasingModePreferenceKey,
                s_AntiAliasingMode
            );
            PlayerPrefs.SetInt(
                DepthOfFieldModePreferenceKey,
                s_DepthOfFieldMode
            );
            PlayerPrefs.Save();
        }

        /// <summary>Reapplies all saved settings without changing preferences.</summary>
        public static void ReapplyCurrentSettings()
        {
            EnsureStateLoaded();
            if (s_QualityLevel != QualityAuto)
            {
                ApplyQualityEffects(s_QualityLevel, false, false);
                ApplyQualityToScaler(s_QualityLevel);
                return;
            }
            FranklinBlobShadow.SetGlobalEnabled(FranklinBlobShadow.GlobalEnabled, false);
            if (s_Instance != null) s_Instance.ApplySsaoFeatures(s_SsaoEnabled);
            if (s_Instance != null) s_Instance.ApplyAntiAliasingToActiveCameras();
            FranklinFastDepthOfFieldFeature.SetRuntimeMode(s_DepthOfFieldMode);
            ApplyQualityToScaler(s_QualityLevel);
        }

        public static void ResetToMobileDefaults()
        {
            PlayerPrefs.DeleteKey(SsaoEnabledPreferenceKey);
            PlayerPrefs.DeleteKey(QualityLevelPreferenceKey);
            PlayerPrefs.DeleteKey(AntiAliasingModePreferenceKey);
            PlayerPrefs.DeleteKey(DepthOfFieldModePreferenceKey);
            PlayerPrefs.Save();

            s_StateLoaded = true;
            SetQualityLevel(DefaultQualityLevel, false);
            SetSsaoEnabled(false, false);
            SetAntiAliasingMode(DefaultAntiAliasingMode, false);
            SetDepthOfFieldMode(DefaultDepthOfFieldMode, false);
            FranklinBlobShadow.ResetGlobalEnabled();
        }

        private void ApplySsaoFeatures(bool enabled)
        {
            if (this.m_SsaoFeatureStateKnown &&
                this.m_LastSsaoFeatureState == enabled)
            {
                return;
            }

            bool applied = false;
            foreach (ScriptableRendererFeature feature in this.m_SsaoFeatures)
            {
                if (feature == null) continue;
                feature.SetActive(enabled);
                applied = true;
            }

            this.m_SsaoFeatureStateKnown = applied;
            this.m_LastSsaoFeatureState = enabled;

            if (applied || this.m_MissingFeatureWarningLogged) return;
            this.m_MissingFeatureWarningLogged = true;
            Debug.LogWarning(
                "Mobile Graphics Settings has no SSAO renderer feature. " +
                "Run Tools/Franklin Game/Install Mobile Settings + SSAO + DOF.",
                this
            );
        }

        private bool HasValidSsaoFeature()
        {
            foreach (ScriptableRendererFeature feature in this.m_SsaoFeatures)
            {
                if (feature != null) return true;
            }

            return false;
        }

        private bool HasActiveSsaoFeature()
        {
            foreach (ScriptableRendererFeature feature in this.m_SsaoFeatures)
            {
                if (feature != null && feature.isActive) return true;
            }

            return false;
        }

        private static void EnsureStateLoaded()
        {
            if (s_StateLoaded) return;

            // SSAO defaults OFF on mobile. FBS keeps its own default ON and its
            // own persistence key because gameplay systems already consume it.
            s_SsaoEnabled = PlayerPrefs.GetInt(SsaoEnabledPreferenceKey, 0) != 0;
            s_QualityLevel = Mathf.Clamp(
                PlayerPrefs.GetInt(QualityLevelPreferenceKey, DefaultQualityLevel),
                QualityLow,
                QualityAuto
            );
            s_AntiAliasingMode = Mathf.Clamp(
                PlayerPrefs.GetInt(
                    AntiAliasingModePreferenceKey,
                    DefaultAntiAliasingMode
                ),
                AntiAliasingOff,
                AntiAliasingSmaa
            );
            s_DepthOfFieldMode = Mathf.Clamp(
                PlayerPrefs.GetInt(
                    DepthOfFieldModePreferenceKey,
                    DefaultDepthOfFieldMode
                ),
                DepthOfFieldOff,
                DepthOfFieldFar
            );
            s_StateLoaded = true;
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game) return;

            bool cameraUsesSsaoLayers =
                (camera.cullingMask & this.m_SsaoLayerMask.value) != 0;
            this.ApplySsaoFeatures(
                s_QualityLevel != QualityLow &&
                s_SsaoEnabled &&
                cameraUsesSsaoLayers
            );

            if (
                this.m_CameraAntiAliasingStates.ContainsKey(camera))
            {
                return;
            }

            this.ApplyAntiAliasing(camera);
        }

        private void ApplyAntiAliasingToActiveCameras()
        {
            foreach (Camera camera in Camera.allCameras)
            {
                this.ApplyAntiAliasing(camera);
            }

            // Also update cameras already known to the manager but currently
            // disabled during a gameplay camera handoff.
            foreach (KeyValuePair<Camera, CameraAntiAliasingState> pair in
                     this.m_CameraAntiAliasingStates)
            {
                if (pair.Key == null || pair.Value.Data == null) continue;
                ApplyAntiAliasingMode(pair.Value);
            }
        }

        private void ApplyAntiAliasing(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) ||
                data.renderType != CameraRenderType.Base)
            {
                return;
            }

            if (!this.m_CameraAntiAliasingStates.TryGetValue(
                    camera,
                    out CameraAntiAliasingState state))
            {
                state = new CameraAntiAliasingState(
                    data,
                    data.renderPostProcessing,
                    data.volumeLayerMask,
                    data.antialiasing,
                    data.antialiasingQuality
                );
                this.m_CameraAntiAliasingStates.Add(camera, state);
            }

            ApplyAntiAliasingMode(state);
        }

        private static void ApplyAntiAliasingMode(CameraAntiAliasingState state)
        {
            if (state.Data == null) return;

            if (s_QualityLevel == QualityLow ||
                s_QualityLevel == QualityBalanced)
            {
                state.Data.antialiasing = AntialiasingMode.None;
                state.Data.renderPostProcessing = false;
                state.Data.volumeLayerMask = state.VolumeLayerMask;
                state.Data.antialiasingQuality = state.AntialiasingQuality;
                return;
            }

            if (s_QualityLevel == QualityHigh)
            {
                state.Data.antialiasing =
                    AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                state.Data.renderPostProcessing = true;
                state.Data.volumeLayerMask = state.VolumeLayerMask;
                state.Data.antialiasingQuality = AntialiasingQuality.Low;
                return;
            }

            if (s_AntiAliasingMode == AntiAliasingOff)
            {
                state.Data.antialiasing = AntialiasingMode.None;
                state.Data.renderPostProcessing = state.RenderPostProcessing;
                state.Data.volumeLayerMask = state.VolumeLayerMask;
                state.Data.antialiasingQuality = state.AntialiasingQuality;
                return;
            }

            // AA is part of URP's post-processing resolve. If this camera did not
            // previously use post-processing, mask Volumes out so enabling AA
            // cannot accidentally enable Bloom, Vignette, or Tonemapping too.
            state.Data.renderPostProcessing = true;
            state.Data.volumeLayerMask = state.RenderPostProcessing
                ? state.VolumeLayerMask
                : 0;
            state.Data.antialiasing = s_AntiAliasingMode == AntiAliasingSmaa
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.FastApproximateAntialiasing;
            state.Data.antialiasingQuality = AntialiasingQuality.Low;
        }

        private void RestoreCameraAntiAliasingStates()
        {
            foreach (CameraAntiAliasingState state in
                     this.m_CameraAntiAliasingStates.Values)
            {
                if (state.Data == null) continue;
                state.Data.renderPostProcessing = state.RenderPostProcessing;
                state.Data.volumeLayerMask = state.VolumeLayerMask;
                state.Data.antialiasing = state.Antialiasing;
                state.Data.antialiasingQuality = state.AntialiasingQuality;
            }

            this.m_CameraAntiAliasingStates.Clear();
        }

        private static void ApplyQualityEffects(
            int qualityLevel,
            bool persist,
            bool qualityChanged)
        {
            switch (qualityLevel)
            {
                case QualityLow:
                    SetFbsEnabled(false, persist);
                    SetSsaoEnabled(false, persist);
                    SetAntiAliasingMode(AntiAliasingOff, persist);
                    SetDepthOfFieldMode(DepthOfFieldOff, persist);
                    break;
                case QualityBalanced:
                    SetFbsEnabled(true, persist);
                    SetSsaoEnabled(false, persist);
                    SetAntiAliasingMode(AntiAliasingOff, persist);
                    SetDepthOfFieldMode(DepthOfFieldOff, persist);
                    break;
                case QualityHigh:
                    SetFbsEnabled(true, persist);
                    SetSsaoEnabled(true, persist);
                    SetAntiAliasingMode(AntiAliasingSmaa, persist);
                    SetDepthOfFieldMode(
                        qualityChanged &&
                        s_DepthOfFieldMode == DepthOfFieldOff
                            ? DepthOfFieldFar
                            : s_DepthOfFieldMode,
                        persist
                    );
                    break;
                default:
                    FranklinBlobShadow.SetGlobalEnabled(
                        FranklinBlobShadow.GlobalEnabled,
                        false
                    );
                    if (s_Instance != null)
                    {
                        s_Instance.ApplySsaoFeatures(s_SsaoEnabled);
                        s_Instance.ApplyAntiAliasingToActiveCameras();
                    }
                    FranklinFastDepthOfFieldFeature.SetRuntimeMode(
                        s_DepthOfFieldMode
                    );
                    break;
            }
        }

        private readonly struct CameraAntiAliasingState
        {
            public readonly UniversalAdditionalCameraData Data;
            public readonly bool RenderPostProcessing;
            public readonly LayerMask VolumeLayerMask;
            public readonly AntialiasingMode Antialiasing;
            public readonly AntialiasingQuality AntialiasingQuality;

            public CameraAntiAliasingState(
                UniversalAdditionalCameraData data,
                bool renderPostProcessing,
                LayerMask volumeLayerMask,
                AntialiasingMode antialiasing,
                AntialiasingQuality antialiasingQuality)
            {
                this.Data = data;
                this.RenderPostProcessing = renderPostProcessing;
                this.VolumeLayerMask = volumeLayerMask;
                this.Antialiasing = antialiasing;
                this.AntialiasingQuality = antialiasingQuality;
            }
        }

        private static bool ApplyQualityToScaler(int qualityLevel)
        {
            return qualityLevel == QualityAuto
                ? DynamicResolutionScaler.ApplyAutomaticDeviceProfile()
                : DynamicResolutionScaler.ApplyMenuGraphicsQuality(qualityLevel);
        }
    }
}
