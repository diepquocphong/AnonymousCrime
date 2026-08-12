using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RobotAstro
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("Robot Astro/Dynamic Resolution Scaler")]
    public sealed class DynamicResolutionScaler : MonoBehaviour
    {
        private static DynamicResolutionScaler s_Instance;

        public enum DeviceProfile
        {
            Auto,
            Low,
            Balanced,
            High,
            Ultra
        }

        public enum ScalingBackend
        {
            Auto,
            URPRenderScale,
            ScalableBufferManager
        }

        private struct RuntimeProfile
        {
            public float MinScale;
            public float MaxScale;
            public float InitialScale;
            public float DownStep;
            public float UpStep;
            public int TargetFrameRate;
        }

        [Header("Profile")]
        [SerializeField] private DeviceProfile m_Profile = DeviceProfile.Auto;
        [SerializeField] private ScalingBackend m_Backend = ScalingBackend.Auto;
        [SerializeField] private bool m_AutoConfigureForDevice = true;

        [Header("Frame Rate")]
        [SerializeField] private bool m_ApplyTargetFrameRate = true;
        [SerializeField, Range(24, 120)] private int m_TargetFrameRate = 60;
        [SerializeField, Range(0, 20)] private int m_FrameRateTolerance = 3;

        [Header("Scale")]
        [SerializeField, Range(0.35f, 1f)] private float m_MinScale = 0.6f;
        [SerializeField, Range(0.35f, 1f)] private float m_MaxScale = 1f;
        [SerializeField, Range(0.35f, 1f)] private float m_InitialScale = 0.85f;
        [SerializeField, Range(0.01f, 0.25f)] private float m_DownscaleStep = 0.08f;
        [SerializeField, Range(0.01f, 0.2f)] private float m_UpscaleStep = 0.04f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float m_SampleDuration = 0.75f;
        [SerializeField, Min(0f)] private float m_DownscaleCooldown = 0.5f;
        [SerializeField, Min(0f)] private float m_UpscaleCooldown = 1.5f;
        [SerializeField, Min(0f)] private float m_StableTimeBeforeUpscale = 3f;

        [Header("Cameras")]
        [SerializeField] private bool m_EnableDynamicResolutionOnCameras = true;
        [SerializeField] private bool m_UseAllCameras = true;
        [SerializeField] private Camera[] m_Cameras = System.Array.Empty<Camera>();

        [Header("Lifecycle")]
        [SerializeField] private bool m_DontDestroyOnLoad = true;
        [SerializeField] private bool m_ResetScaleOnDisable = true;
        [SerializeField] private bool m_LogScaleChanges;

        private RuntimeProfile m_RuntimeProfile;
        private DeviceProfile m_ActiveProfile;
        private ScalingBackend m_ActiveBackend;
        private UniversalRenderPipelineAsset m_URPAsset;

        private float m_OriginalURPRenderScale = 1f;
        private float m_CurrentScale = 1f;
        private float m_CurrentFps;
        private float m_SampleTime;
        private int m_SampleFrames;
        private float m_TimeSinceLastScaleChange;
        private float m_TimeAboveTarget;

        public float CurrentScale => this.m_CurrentScale;
        public float CurrentFps => this.m_CurrentFps;
        public DeviceProfile ActiveProfile => this.m_ActiveProfile;
        public ScalingBackend ActiveBackend => this.m_ActiveBackend;
        public static string ActiveProfileName =>
            s_Instance != null && s_Instance.isActiveAndEnabled
                ? s_Instance.m_ActiveProfile.ToString().ToLowerInvariant()
                : "auto";

        /// <summary>
        /// Restores hardware-based profile selection and keeps adapting render
        /// scale against the measured frame rate.
        /// </summary>
        public static bool ApplyAutomaticDeviceProfile()
        {
            if (s_Instance == null || !s_Instance.isActiveAndEnabled)
            {
                return false;
            }

            s_Instance.m_Profile = DeviceProfile.Auto;
            s_Instance.m_AutoConfigureForDevice = true;
            s_Instance.ForceRecalculateProfile();
            return true;
        }

        /// <summary>
        /// Applies a player-selected Low/Medium/High override to the active scaler.
        /// Returns false while a gameplay scaler is not available.
        /// </summary>
        public static bool ApplyMenuGraphicsQuality(int qualityLevel)
        {
            if (s_Instance == null || !s_Instance.isActiveAndEnabled)
            {
                return false;
            }

            s_Instance.ApplyMenuGraphicsQualityInternal(qualityLevel);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Instance = null;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                this.gameObject.SetActive(false);
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            if (this.m_DontDestroyOnLoad)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void OnEnable()
        {
            this.m_URPAsset = this.ResolveURPAsset();
            this.m_OriginalURPRenderScale = this.m_URPAsset != null
                ? this.m_URPAsset.renderScale
                : 1f;

            this.m_ActiveProfile = this.m_Profile == DeviceProfile.Auto
                ? ResolveAutoProfile()
                : this.m_Profile;

            this.m_RuntimeProfile = this.BuildRuntimeProfile(this.m_ActiveProfile);
            this.m_ActiveBackend = this.ResolveBackend();

            if (this.m_ApplyTargetFrameRate)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = this.m_RuntimeProfile.TargetFrameRate;
            }

            this.ApplyCameraDynamicResolution();
            this.SetScale(this.m_RuntimeProfile.InitialScale, true);
            this.ResetSampling();
        }

        private void OnDisable()
        {
            if (!this.m_ResetScaleOnDisable) return;

            if (this.m_URPAsset != null)
            {
                this.m_URPAsset.renderScale = this.m_OriginalURPRenderScale;
            }

            ScalableBufferManager.ResizeBuffers(1f, 1f);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f) return;

            this.m_SampleTime += deltaTime;
            this.m_SampleFrames += 1;
            this.m_TimeSinceLastScaleChange += deltaTime;

            if (this.m_SampleTime < this.m_SampleDuration) return;

            this.m_CurrentFps = this.m_SampleFrames / this.m_SampleTime;
            this.EvaluateScale();
            this.ResetSampling();
        }

        public void SetScale(float scale)
        {
            this.SetScale(scale, false);
        }

        public void ForceRecalculateProfile()
        {
            this.m_ActiveProfile = this.m_Profile == DeviceProfile.Auto
                ? ResolveAutoProfile()
                : this.m_Profile;

            this.m_RuntimeProfile = this.BuildRuntimeProfile(this.m_ActiveProfile);
            if (this.m_ApplyTargetFrameRate)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate =
                    this.m_RuntimeProfile.TargetFrameRate;
            }
            this.SetScale(this.m_RuntimeProfile.InitialScale, true);
            this.ResetSampling();
        }

        private void ApplyMenuGraphicsQualityInternal(int qualityLevel)
        {
            switch (Mathf.Clamp(qualityLevel, 0, 2))
            {
                case 0:
                    this.m_Profile = DeviceProfile.Low;
                    break;
                case 2:
                    this.m_Profile = DeviceProfile.High;
                    break;
                default:
                    this.m_Profile = DeviceProfile.Balanced;
                    break;
            }

            // Menu quality is an explicit player choice, so it takes precedence
            // over the automatic hardware classification for this session.
            this.m_AutoConfigureForDevice = true;
            this.ForceRecalculateProfile();
        }

        private void EvaluateScale()
        {
            int targetFrameRate = this.m_RuntimeProfile.TargetFrameRate;
            float lowerLimit = targetFrameRate - this.m_FrameRateTolerance;
            float upperLimit = targetFrameRate + this.m_FrameRateTolerance;

            if (this.m_CurrentFps < lowerLimit)
            {
                this.m_TimeAboveTarget = 0f;

                if (this.m_TimeSinceLastScaleChange < this.m_DownscaleCooldown) return;

                float missRatio = Mathf.Clamp01((lowerLimit - this.m_CurrentFps) / targetFrameRate);
                float stepMultiplier = Mathf.Lerp(1f, 2f, missRatio);
                float nextScale = this.m_CurrentScale - this.m_RuntimeProfile.DownStep * stepMultiplier;

                this.SetScale(nextScale, false);
                return;
            }

            if (this.m_CurrentFps > upperLimit)
            {
                this.m_TimeAboveTarget += this.m_SampleTime;

                bool canUpscale =
                    this.m_TimeAboveTarget >= this.m_StableTimeBeforeUpscale &&
                    this.m_TimeSinceLastScaleChange >= this.m_UpscaleCooldown;

                if (!canUpscale) return;

                this.SetScale(this.m_CurrentScale + this.m_RuntimeProfile.UpStep, false);
                return;
            }

            this.m_TimeAboveTarget = 0f;
        }

        private void SetScale(float scale, bool instant)
        {
            float minScale = Mathf.Min(
                this.m_RuntimeProfile.MinScale,
                this.m_RuntimeProfile.MaxScale
            );

            float maxScale = Mathf.Max(
                this.m_RuntimeProfile.MinScale,
                this.m_RuntimeProfile.MaxScale
            );

            float nextScale = Mathf.Clamp(scale, minScale, maxScale);
            if (!instant && Mathf.Approximately(this.m_CurrentScale, nextScale)) return;

            this.m_CurrentScale = nextScale;
            this.m_TimeSinceLastScaleChange = 0f;
            this.ApplyScale(nextScale);

            if (this.m_LogScaleChanges)
            {
                Debug.Log(
                    $"Dynamic Resolution: {nextScale:0.00} ({this.m_CurrentFps:0.0} FPS, {this.m_ActiveProfile}, {this.m_ActiveBackend})",
                    this
                );
            }
        }

        private void ApplyScale(float scale)
        {
            switch (this.m_ActiveBackend)
            {
                case ScalingBackend.URPRenderScale:
                    if (this.m_URPAsset != null)
                    {
                        this.m_URPAsset.renderScale = scale;
                    }
                    break;

                case ScalingBackend.ScalableBufferManager:
                    ScalableBufferManager.ResizeBuffers(scale, scale);
                    break;
            }
        }

        private RuntimeProfile BuildRuntimeProfile(DeviceProfile profile)
        {
            RuntimeProfile runtime = this.m_AutoConfigureForDevice
                ? GetDefaultProfile(profile)
                : new RuntimeProfile
                {
                    MinScale = this.m_MinScale,
                    MaxScale = this.m_MaxScale,
                    InitialScale = this.m_InitialScale,
                    DownStep = this.m_DownscaleStep,
                    UpStep = this.m_UpscaleStep,
                    TargetFrameRate = this.m_TargetFrameRate
                };

            runtime.MinScale = Mathf.Clamp(runtime.MinScale, 0.35f, 1f);
            runtime.MaxScale = Mathf.Clamp(runtime.MaxScale, runtime.MinScale, 1f);
            runtime.InitialScale = Mathf.Clamp(runtime.InitialScale, runtime.MinScale, runtime.MaxScale);
            runtime.DownStep = Mathf.Max(0.01f, runtime.DownStep);
            runtime.UpStep = Mathf.Max(0.01f, runtime.UpStep);
            runtime.TargetFrameRate = Mathf.Clamp(runtime.TargetFrameRate, 24, 120);

            return runtime;
        }

        private ScalingBackend ResolveBackend()
        {
            if (this.m_Backend != ScalingBackend.Auto) return this.m_Backend;

            return this.m_URPAsset != null
                ? ScalingBackend.URPRenderScale
                : ScalingBackend.ScalableBufferManager;
        }

        private UniversalRenderPipelineAsset ResolveURPAsset()
        {
            RenderPipelineAsset qualityAsset = QualitySettings.renderPipeline;
            if (qualityAsset is UniversalRenderPipelineAsset qualityURP) return qualityURP;

            RenderPipelineAsset graphicsAsset = GraphicsSettings.currentRenderPipeline;
            return graphicsAsset as UniversalRenderPipelineAsset;
        }

        private void ApplyCameraDynamicResolution()
        {
            if (!this.m_EnableDynamicResolutionOnCameras) return;

            if (this.m_UseAllCameras)
            {
                Camera[] cameras = Camera.allCameras;
                foreach (Camera targetCamera in cameras)
                {
                    if (targetCamera != null) targetCamera.allowDynamicResolution = true;
                }

                return;
            }

            foreach (Camera targetCamera in this.m_Cameras)
            {
                if (targetCamera != null) targetCamera.allowDynamicResolution = true;
            }
        }

        private void ResetSampling()
        {
            this.m_SampleTime = 0f;
            this.m_SampleFrames = 0;
        }

        private static RuntimeProfile GetDefaultProfile(DeviceProfile profile)
        {
            switch (profile)
            {
                case DeviceProfile.Low:
                    return new RuntimeProfile
                    {
                        MinScale = 0.50f,
                        MaxScale = 0.90f,
                        InitialScale = 0.70f,
                        DownStep = 0.10f,
                        UpStep = 0.03f,
                        TargetFrameRate = Application.isMobilePlatform ? 30 : 45
                    };

                case DeviceProfile.Balanced:
                    return new RuntimeProfile
                    {
                        MinScale = 0.60f,
                        MaxScale = 1.00f,
                        InitialScale = 0.85f,
                        DownStep = 0.08f,
                        UpStep = 0.04f,
                        TargetFrameRate = 60
                    };

                case DeviceProfile.High:
                    return new RuntimeProfile
                    {
                        MinScale = 0.75f,
                        MaxScale = 1.00f,
                        InitialScale = 1.00f,
                        DownStep = 0.06f,
                        UpStep = 0.03f,
                        TargetFrameRate = 60
                    };

                case DeviceProfile.Ultra:
                    return new RuntimeProfile
                    {
                        MinScale = 0.85f,
                        MaxScale = 1.00f,
                        InitialScale = 1.00f,
                        DownStep = 0.05f,
                        UpStep = 0.025f,
                        TargetFrameRate = 60
                    };

                default:
                    return GetDefaultProfile(DeviceProfile.Balanced);
            }
        }

        private static DeviceProfile ResolveAutoProfile()
        {
            int systemMemory = SystemInfo.systemMemorySize;
            int graphicsMemory = SystemInfo.graphicsMemorySize;
            int processorCount = SystemInfo.processorCount;

            bool hasSystemMemory = systemMemory > 0;
            bool hasGraphicsMemory = graphicsMemory > 0;

            if (Application.isMobilePlatform)
            {
                if ((hasSystemMemory && systemMemory <= 3072) ||
                    (hasGraphicsMemory && graphicsMemory <= 1024) ||
                    processorCount <= 4)
                {
                    return DeviceProfile.Low;
                }

                if ((hasSystemMemory && systemMemory <= 6144) ||
                    (hasGraphicsMemory && graphicsMemory <= 2048) ||
                    processorCount <= 6)
                {
                    return DeviceProfile.Balanced;
                }

                return DeviceProfile.High;
            }

            if ((hasSystemMemory && systemMemory <= 6144) ||
                (hasGraphicsMemory && graphicsMemory <= 2048) ||
                processorCount <= 4)
            {
                return DeviceProfile.Balanced;
            }

            if ((hasSystemMemory && systemMemory >= 16384) &&
                (hasGraphicsMemory && graphicsMemory >= 6144) &&
                processorCount >= 8)
            {
                return DeviceProfile.Ultra;
            }

            return DeviceProfile.High;
        }
    }
}
