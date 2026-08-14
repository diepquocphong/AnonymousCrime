using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FranklinGame.Rendering
{
    /// <summary>
    /// Mobile-oriented controller for a gameplay owner's depth-based blob shadow.
    /// It keeps the shared material intact, throttles ground probes, and exposes
    /// a small runtime API for gameplay and quality systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("Franklin Game/Rendering/Mobile Blob Shadow")]
    public sealed class FranklinBlobShadow : MonoBehaviour
    {
        public const string GlobalEnabledPreferenceKey =
            "Franklin.FastBlobShadow.Enabled";
        private const string OwnerLayerMigrationPreferenceKey =
            "Franklin.FastBlobShadow.OwnerLayers.V1";

        public enum FootprintShape
        {
            Circle = 0,
            Ellipse = 1,
            Rectangle = 2
        }

        private const int GroundHitCapacity = 16;

        private static readonly HashSet<FranklinBlobShadow> Instances =
            new HashSet<FranklinBlobShadow>();
        private static readonly RaycastHit[] GroundHits =
            new RaycastHit[GroundHitCapacity];

        private static bool s_GlobalEnabled = true;
        private static bool s_GlobalStateLoaded;

        private static readonly int BlobColorId = Shader.PropertyToID("_BlobColor");
        private static readonly int BlobIntensityId = Shader.PropertyToID("_BlobIntensity");
        private static readonly int BlobPowerId = Shader.PropertyToID("_BlobPower");
        private static readonly int BlobShapeId = Shader.PropertyToID("_BlobShape");
        private static readonly int BlobCoreId = Shader.PropertyToID("_BlobCore");

        [Header("References")]
        [SerializeField] private Transform m_ShadowTransform;
        [SerializeField] private MeshRenderer m_ShadowRenderer;
        [SerializeField] private Camera m_RenderCamera;
        [SerializeField] private Transform m_OrientationTransform;
        [SerializeField] private Vector2 m_FootprintCenter;

        [Header("Appearance")]
        [SerializeField] private Color m_ShadowColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float m_Intensity = 0.58f;
        [SerializeField, Range(0.25f, 8f)] private float m_Power = 1.7f;
        [SerializeField, Range(0f, 0.9f)] private float m_Core = 0.12f;
        [SerializeField] private FootprintShape m_FootprintShape = FootprintShape.Circle;
        [SerializeField] private Vector3 m_VolumeSize = new Vector3(1f, 0.65f, 1f);

        [Header("Jump / Flight")]
        [SerializeField, Min(0f)] private float m_GroundedPivotHeight = 1f;
        [SerializeField, Min(0f)] private float m_AirborneFadeStart = 0.05f;
        [SerializeField, Min(0.1f)] private float m_AirborneHideHeight = 2.5f;
        [SerializeField, Range(0.05f, 1f)] private float m_MinAirborneScale = 0.25f;

        [Header("Ground Tracking")]
        [SerializeField] private bool m_FollowGround = true;
        [SerializeField] private LayerMask m_GroundLayers = ~0;
        [SerializeField, Min(0f)] private float m_ProbeStartHeight = 0.5f;
        [SerializeField, Min(0.05f)] private float m_MaxGroundDistance = 5f;
        [SerializeField, Min(0.02f)] private float m_ProbeInterval = 0.08f;
        [SerializeField, Min(0f)] private float m_GroundOffset = 0.025f;
        [SerializeField, Range(0f, 1f)] private float m_MinGroundNormalY = 0.35f;
        [SerializeField] private bool m_HideWhenGroundMissing = true;
        [SerializeField] private bool m_IgnoreRigidbodyReceivers = true;
        [SerializeField] private bool m_SuppressInsideShadowOwner = true;

        [Header("Mobile")]
        [SerializeField] private bool m_ConfigureCameraDepth = true;
        [SerializeField, Min(0f)] private float m_MaxVisibleDistance = 40f;
        [SerializeField, Min(0.05f)] private float m_DistanceCheckInterval = 0.25f;

        private MaterialPropertyBlock m_PropertyBlock;
        private Vector3 m_GroundPoint;
        private Vector3 m_GroundNormal = Vector3.up;
        private Camera m_DepthConfiguredCamera;
        private float m_NextGroundProbeTime;
        private float m_NextCameraSearchTime;
        private float m_NextDistanceCheckTime;
        private float m_AirborneHeight;
        private float m_AirborneOpacityMultiplier = 1f;
        private float m_AirborneScaleMultiplier = 1f;
        private bool m_ForceGroundProbe = true;
        private bool m_HasGround;
        private bool m_AirborneHidden;
        private bool m_DistanceVisible = true;
        private bool m_UserVisible = true;
        private bool m_Suspended;
        private bool m_InsideShadowOwner;

        public static event Action<bool> GlobalEnabledChanged;

        public static bool GlobalEnabled
        {
            get
            {
                EnsureGlobalStateLoaded();
                return s_GlobalEnabled;
            }
        }

        public bool IsVisible =>
            this.m_UserVisible &&
            !this.m_Suspended &&
            !this.m_InsideShadowOwner &&
            GlobalEnabled;
        public bool IsLocallyVisible => this.m_UserVisible;
        public bool IsSuspended => this.m_Suspended;
        public bool IsInsideShadowOwner => this.m_InsideShadowOwner;
        public bool HasGround => !this.m_FollowGround || this.m_HasGround;
        public Color ShadowColor => this.m_ShadowColor;
        public float Opacity => this.m_Intensity;
        public float CoreSize => this.m_Core;
        public FootprintShape Footprint => this.m_FootprintShape;
        public float AirborneHeight => this.m_AirborneHeight;
        public Camera RenderCamera => this.m_RenderCamera;

        private void Awake()
        {
            this.ResolveReferences();
            this.ConfigureRenderer();
            this.ApplyAppearance();
        }

        private void OnEnable()
        {
            Instances.Add(this);
            this.ResolveReferences();
            this.ConfigureRenderer();
            this.RefreshShadowOwnerSuppression();
            this.ResetAirborneState();
            this.ApplyAppearance();
            this.ApplyVolumeSize();

            if (!GlobalEnabled || this.m_Suspended || this.m_InsideShadowOwner)
            {
                this.SetRendererEnabled(false);
                return;
            }

            this.m_ForceGroundProbe = true;
            this.m_NextGroundProbeTime = 0f;
            this.m_NextDistanceCheckTime = 0f;
            this.m_DistanceVisible = true;
            this.TryResolveCamera(true);

            if (this.UpdateDistanceVisibility(true))
            {
                this.UpdatePlacement(true);
            }
            else
            {
                this.SetRendererEnabled(false);
            }
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            this.SetRendererEnabled(false);
        }

        private void LateUpdate()
        {
            if (!GlobalEnabled || this.m_Suspended || this.m_InsideShadowOwner)
            {
                this.SetRendererEnabled(false);
                return;
            }

            this.TryResolveCamera(false);

            if (!this.m_UserVisible || !this.UpdateDistanceVisibility(false))
            {
                this.SetRendererEnabled(false);
                return;
            }

            this.UpdatePlacement(false);
        }

        private void OnValidate()
        {
            this.m_Intensity = Mathf.Clamp01(this.m_Intensity);
            this.m_Power = Mathf.Clamp(this.m_Power, 0.25f, 8f);
            this.m_Core = Mathf.Clamp(this.m_Core, 0f, 0.9f);
            this.m_VolumeSize.x = Mathf.Max(0.01f, this.m_VolumeSize.x);
            this.m_VolumeSize.y = Mathf.Max(0.01f, this.m_VolumeSize.y);
            this.m_VolumeSize.z = Mathf.Max(0.01f, this.m_VolumeSize.z);
            if (this.m_FootprintShape == FootprintShape.Circle)
            {
                float diameter = Mathf.Max(this.m_VolumeSize.x, this.m_VolumeSize.z);
                this.m_VolumeSize.x = diameter;
                this.m_VolumeSize.z = diameter;
            }
            this.m_GroundedPivotHeight = Mathf.Max(0f, this.m_GroundedPivotHeight);
            this.m_AirborneFadeStart = Mathf.Max(0f, this.m_AirborneFadeStart);
            this.m_AirborneHideHeight = Mathf.Max(
                this.m_AirborneFadeStart + 0.1f,
                this.m_AirborneHideHeight
            );
            this.m_MinAirborneScale = Mathf.Clamp(this.m_MinAirborneScale, 0.05f, 1f);
            this.m_MaxGroundDistance = Mathf.Max(0.05f, this.m_MaxGroundDistance);
            this.m_ProbeInterval = Mathf.Max(0.02f, this.m_ProbeInterval);
            this.m_MaxVisibleDistance = Mathf.Max(0f, this.m_MaxVisibleDistance);
            this.m_DistanceCheckInterval = Mathf.Max(0.05f, this.m_DistanceCheckInterval);

            this.ApplyVolumeSize();
            this.ApplyAppearance();
            this.m_ForceGroundProbe = true;
        }

        private void OnTransformParentChanged()
        {
            this.RefreshShadowOwnerSuppression();
        }

        /// <summary>Enables or disables the shadow without disabling this component.</summary>
        public void SetVisible(bool visible)
        {
            if (visible && !this.m_UserVisible) this.m_ForceGroundProbe = true;
            this.m_UserVisible = visible;
            this.UpdateRendererVisibility();
        }

        /// <summary>
        /// Temporarily pauses one shadow without changing its local visibility
        /// preference. Used while Player is entering, seated in, or exiting a vehicle.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            if (this.m_Suspended == suspended) return;

            this.m_Suspended = suspended;
            if (suspended)
            {
                this.SetRendererEnabled(false);
                return;
            }

            if (GlobalEnabled && this.isActiveAndEnabled)
            {
                this.ApplyGlobalEnabledState(true);
            }
        }

        /// <summary>
        /// Master switch for every Franklin blob shadow. The value is persisted
        /// by default so the user's graphics preference survives app restarts.
        /// Disabling it stops rendering, camera checks, distance checks and ground probes.
        /// </summary>
        public static void SetGlobalEnabled(bool enabled, bool persist = true)
        {
            EnsureGlobalStateLoaded();
            bool changed = s_GlobalEnabled != enabled;
            s_GlobalEnabled = enabled;

            if (persist)
            {
                PlayerPrefs.SetInt(GlobalEnabledPreferenceKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            foreach (FranklinBlobShadow instance in Instances)
            {
                if (instance != null) instance.ApplyGlobalEnabledState(enabled);
            }

            if (changed) GlobalEnabledChanged?.Invoke(enabled);
        }

        /// <summary>Toggles the master switch and returns its new state.</summary>
        public static bool ToggleGlobalEnabled(bool persist = true)
        {
            bool enabled = !GlobalEnabled;
            SetGlobalEnabled(enabled, persist);
            return enabled;
        }

        /// <summary>Deletes the saved override and restores the default enabled state.</summary>
        public static void ResetGlobalEnabled()
        {
            PlayerPrefs.DeleteKey(GlobalEnabledPreferenceKey);
            PlayerPrefs.Save();
            s_GlobalStateLoaded = true;
            SetGlobalEnabled(true, false);
        }

        /// <summary>
        /// Sets camera-distance culling. Checks are throttled and all ground
        /// probes stop while the shadow is outside the configured distance.
        /// A max distance of zero disables distance culling.
        /// </summary>
        public void SetDistanceCulling(float maxDistance, float checkInterval = 0.25f)
        {
            this.m_MaxVisibleDistance = Mathf.Max(0f, maxDistance);
            this.m_DistanceCheckInterval = Mathf.Max(0.05f, checkInterval);
            this.m_NextDistanceCheckTime = 0f;
            if (
                GlobalEnabled &&
                !this.m_Suspended &&
                !this.m_InsideShadowOwner
            )
            {
                this.UpdateDistanceVisibility(true);
            }
            this.UpdateRendererVisibility();
        }

        /// <summary>Sets the shared-material override color for this shadow only.</summary>
        public void SetColor(Color color)
        {
            this.m_ShadowColor = color;
            this.ApplyAppearance();
        }

        /// <summary>Sets opacity in the 0..1 range without creating a material instance.</summary>
        public void SetOpacity(float opacity)
        {
            this.m_Intensity = Mathf.Clamp01(opacity);
            this.ApplyAppearance();
        }

        /// <summary>Controls the radial falloff. Higher values make the blob fuller.</summary>
        public void SetPower(float power)
        {
            this.m_Power = Mathf.Clamp(power, 0.25f, 8f);
            this.ApplyAppearance();
        }

        /// <summary>Sets the fully dark center before the soft edge begins.</summary>
        public void SetCoreSize(float coreSize)
        {
            this.m_Core = Mathf.Clamp(coreSize, 0f, 0.9f);
            this.ApplyAppearance();
        }

        /// <summary>
        /// Sets the transform that supplies vehicle forward and the visual-center
        /// offset expressed in that transform's local X/Z plane.
        /// </summary>
        public void SetAlignment(Transform orientationTransform, Vector2 footprintCenter)
        {
            this.m_OrientationTransform = orientationTransform != null
                ? orientationTransform
                : this.transform;
            this.m_FootprintCenter = footprintCenter;
            this.RefreshGround();
        }

        /// <summary>Changes width and length while preserving the current footprint shape.</summary>
        public void SetSize(float width, float length)
        {
            width = Mathf.Max(0.01f, width);
            length = Mathf.Max(0.01f, length);

            if (this.m_FootprintShape == FootprintShape.Circle)
            {
                this.SetDiameter(Mathf.Max(width, length));
                return;
            }

            this.m_VolumeSize.x = width;
            this.m_VolumeSize.z = length;
            this.ApplyVolumeSize();
        }

        /// <summary>Changes the circular shadow diameter.</summary>
        public void SetDiameter(float diameter)
        {
            diameter = Mathf.Max(0.01f, diameter);
            this.m_FootprintShape = FootprintShape.Circle;
            this.m_VolumeSize.x = diameter;
            this.m_VolumeSize.z = diameter;
            this.ApplyAppearance();
            this.ApplyVolumeSize();
        }

        /// <summary>Changes to an elliptical footprint with independent width and length.</summary>
        public void SetEllipse(float width, float length)
        {
            this.m_FootprintShape = FootprintShape.Ellipse;
            this.m_VolumeSize.x = Mathf.Max(0.01f, width);
            this.m_VolumeSize.z = Mathf.Max(0.01f, length);
            this.ApplyAppearance();
            this.ApplyVolumeSize();
        }

        /// <summary>Changes to a soft rectangular footprint for cars and wide vehicles.</summary>
        public void SetRectangle(float width, float length)
        {
            this.m_FootprintShape = FootprintShape.Rectangle;
            this.m_VolumeSize.x = Mathf.Max(0.01f, width);
            this.m_VolumeSize.z = Mathf.Max(0.01f, length);
            this.ApplyAppearance();
            this.ApplyVolumeSize();
        }

        /// <summary>Changes only the footprint shader shape, preserving width and length.</summary>
        public void SetFootprintShape(FootprintShape shape)
        {
            this.m_FootprintShape = shape;
            if (shape == FootprintShape.Circle)
            {
                float diameter = Mathf.Max(this.m_VolumeSize.x, this.m_VolumeSize.z);
                this.m_VolumeSize.x = diameter;
                this.m_VolumeSize.z = diameter;
            }

            this.ApplyAppearance();
            this.ApplyVolumeSize();
        }

        /// <summary>Changes the 3D projection volume while preserving the footprint shape.</summary>
        public void SetVolumeSize(Vector3 size)
        {
            this.m_VolumeSize = new Vector3(
                Mathf.Max(0.01f, size.x),
                Mathf.Max(0.01f, size.y),
                Mathf.Max(0.01f, size.z)
            );
            if (this.m_FootprintShape == FootprintShape.Circle)
            {
                float diameter = Mathf.Max(this.m_VolumeSize.x, this.m_VolumeSize.z);
                this.m_VolumeSize.x = diameter;
                this.m_VolumeSize.z = diameter;
            }
            this.ApplyVolumeSize();
        }

        /// <summary>
        /// Configures how the shadow shrinks and fades as the player leaves the ground.
        /// Heights are measured above the grounded player pivot position.
        /// </summary>
        public void SetAirborneResponse(
            float groundedPivotHeight,
            float fadeStartHeight,
            float hideHeight,
            float minimumScale = 0.25f
        )
        {
            this.m_GroundedPivotHeight = Mathf.Max(0f, groundedPivotHeight);
            this.m_AirborneFadeStart = Mathf.Max(0f, fadeStartHeight);
            this.m_AirborneHideHeight = Mathf.Max(this.m_AirborneFadeStart + 0.1f, hideHeight);
            this.m_MinAirborneScale = Mathf.Clamp(minimumScale, 0.05f, 1f);
            this.RefreshGround();
        }

        /// <summary>Turns the throttled ground probe on or off.</summary>
        public void SetGroundTracking(bool enabled)
        {
            this.m_FollowGround = enabled;
            this.m_ForceGroundProbe = true;
            this.UpdatePlacement(true);
        }

        /// <summary>Changes which physics layers can receive the shadow.</summary>
        public void SetGroundLayers(LayerMask layers)
        {
            this.m_GroundLayers = layers;
            this.RefreshGround();
        }

        /// <summary>Forces a ground refresh without waiting for the probe interval.</summary>
        public void RefreshGround()
        {
            this.m_ForceGroundProbe = true;
            this.UpdatePlacement(true);
        }

        /// <summary>Assigns the gameplay camera and optionally enables its depth texture.</summary>
        public void SetRenderCamera(Camera camera, bool configureDepth = true)
        {
            this.m_RenderCamera = camera;
            this.m_DepthConfiguredCamera = null;
            this.m_NextDistanceCheckTime = 0f;
            if (
                GlobalEnabled &&
                !this.m_Suspended &&
                !this.m_InsideShadowOwner &&
                configureDepth &&
                this.m_ConfigureCameraDepth
            )
            {
                if (EnsureCameraDepth(camera)) this.m_DepthConfiguredCamera = camera;
            }

            this.UpdateRendererVisibility();
        }

        /// <summary>Finds the API on an owner, its parents, or its children.</summary>
        public static bool TryGet(Component owner, out FranklinBlobShadow shadow)
        {
            shadow = null;
            if (owner == null) return false;

            shadow = owner.GetComponent<FranklinBlobShadow>();
            if (shadow == null) shadow = owner.GetComponentInParent<FranklinBlobShadow>();
            if (shadow == null) shadow = owner.GetComponentInChildren<FranklinBlobShadow>(true);
            return shadow != null;
        }

        /// <summary>Enables the camera depth texture required by the URP shader.</summary>
        public static bool EnsureCameraDepth(Camera camera)
        {
            if (camera == null) return false;

            camera.depthTextureMode |= DepthTextureMode.Depth;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthTexture = true;
            return true;
        }

        private void ResolveReferences()
        {
            if (this.m_ShadowTransform == null)
            {
                Transform child = this.transform.Find("MobileBlobShadow");
                if (child != null) this.m_ShadowTransform = child;
            }

            if (this.m_ShadowRenderer == null && this.m_ShadowTransform != null)
            {
                this.m_ShadowRenderer = this.m_ShadowTransform.GetComponent<MeshRenderer>();
            }

            // Keep the visible FBS volume on the same gameplay layer as its
            // owner. This also repairs prefab variants created before the layer
            // contract was introduced.
            if (this.m_ShadowTransform != null &&
                this.m_ShadowTransform.gameObject.layer != this.gameObject.layer)
            {
                this.m_ShadowTransform.gameObject.layer = this.gameObject.layer;
            }

            if (this.m_OrientationTransform == null)
            {
                this.m_OrientationTransform = this.transform;
            }
        }

        private void ConfigureRenderer()
        {
            if (this.m_ShadowRenderer == null) return;

            this.m_ShadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            this.m_ShadowRenderer.receiveShadows = false;
            this.m_ShadowRenderer.lightProbeUsage = LightProbeUsage.Off;
            this.m_ShadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            this.m_ShadowRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            this.m_ShadowRenderer.allowOcclusionWhenDynamic = true;
        }

        private void ApplyAppearance()
        {
            if (this.m_ShadowRenderer == null) return;

            if (this.m_PropertyBlock == null)
            {
                this.m_PropertyBlock = new MaterialPropertyBlock();
            }

            this.m_ShadowRenderer.GetPropertyBlock(this.m_PropertyBlock);
            this.m_PropertyBlock.SetColor(BlobColorId, this.m_ShadowColor);
            this.m_PropertyBlock.SetFloat(
                BlobIntensityId,
                this.m_Intensity * this.m_AirborneOpacityMultiplier
            );
            this.m_PropertyBlock.SetFloat(BlobPowerId, this.m_Power);
            this.m_PropertyBlock.SetFloat(BlobCoreId, this.m_Core);
            this.m_PropertyBlock.SetFloat(
                BlobShapeId,
                this.m_FootprintShape == FootprintShape.Rectangle ? 1f : 0f
            );
            this.m_ShadowRenderer.SetPropertyBlock(this.m_PropertyBlock);
        }

        private void ApplyVolumeSize()
        {
            if (this.m_ShadowTransform != null)
            {
                float width = Mathf.Max(0.01f, this.m_VolumeSize.x);
                float length = Mathf.Max(0.01f, this.m_VolumeSize.z);
                if (this.m_FootprintShape == FootprintShape.Circle)
                {
                    float diameter = Mathf.Max(width, length);
                    width = diameter;
                    length = diameter;
                    this.m_VolumeSize.x = diameter;
                    this.m_VolumeSize.z = diameter;
                }

                this.m_ShadowTransform.localScale = new Vector3(
                    width * this.m_AirborneScaleMultiplier,
                    Mathf.Max(0.01f, this.m_VolumeSize.y),
                    length * this.m_AirborneScaleMultiplier
                );
            }
        }

        private void TryResolveCamera(bool immediate)
        {
            if (this.m_RenderCamera != null && this.m_RenderCamera.isActiveAndEnabled)
            {
                if (
                    this.m_ConfigureCameraDepth &&
                    this.m_DepthConfiguredCamera != this.m_RenderCamera &&
                    EnsureCameraDepth(this.m_RenderCamera)
                )
                {
                    this.m_DepthConfiguredCamera = this.m_RenderCamera;
                }
                return;
            }

            float now = Time.unscaledTime;
            if (!immediate && now < this.m_NextCameraSearchTime) return;

            this.m_NextCameraSearchTime = now + 1f;
            this.m_RenderCamera = Camera.main;
            this.m_DepthConfiguredCamera = null;
            this.m_NextDistanceCheckTime = 0f;
            if (this.m_ConfigureCameraDepth && EnsureCameraDepth(this.m_RenderCamera))
            {
                this.m_DepthConfiguredCamera = this.m_RenderCamera;
            }
        }

        private bool UpdateDistanceVisibility(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now < this.m_NextDistanceCheckTime)
            {
                return this.m_DistanceVisible;
            }

            float stagger = 0.85f + (this.GetInstanceID() & 31) * 0.01f;
            this.m_NextDistanceCheckTime = now + this.m_DistanceCheckInterval * stagger;

            bool wasVisible = this.m_DistanceVisible;
            if (this.m_MaxVisibleDistance <= 0f || this.m_RenderCamera == null)
            {
                this.m_DistanceVisible = true;
            }
            else
            {
                float maxDistanceSquared = this.m_MaxVisibleDistance * this.m_MaxVisibleDistance;
                this.m_DistanceVisible =
                    (this.m_RenderCamera.transform.position - this.transform.position).sqrMagnitude
                    <= maxDistanceSquared;
            }

            if (!wasVisible && this.m_DistanceVisible)
            {
                this.m_ForceGroundProbe = true;
            }

            return this.m_DistanceVisible;
        }

        private void UpdatePlacement(bool force)
        {
            if (this.m_ShadowTransform == null || this.m_ShadowRenderer == null) return;
            if (!GlobalEnabled || this.m_Suspended || this.m_InsideShadowOwner)
            {
                this.SetRendererEnabled(false);
                return;
            }

            if (!this.m_FollowGround)
            {
                this.m_HasGround = true;
                this.ResetAirborneState();
                Vector3 position = this.GetFootprintWorldCenter() + Vector3.up * this.m_GroundOffset;
                Quaternion rotation = this.GetGroundAlignedRotation(Vector3.up);
                this.m_ShadowTransform.SetPositionAndRotation(position, rotation);
                this.UpdateRendererVisibility();
                return;
            }

            float now = Time.time;
            if (force || this.m_ForceGroundProbe || now >= this.m_NextGroundProbeTime)
            {
                this.m_ForceGroundProbe = false;
                this.m_NextGroundProbeTime = now + this.m_ProbeInterval;
                this.m_HasGround = this.TrySampleGround(out this.m_GroundPoint, out this.m_GroundNormal);
            }

            if (this.m_HasGround)
            {
                Vector3 ownerPosition = this.GetFootprintWorldCenter();
                float planeDistance = Vector3.Dot(ownerPosition - this.m_GroundPoint, this.m_GroundNormal);
                float normalY = Mathf.Max(0.01f, this.m_GroundNormal.y);
                float verticalHeight = planeDistance / normalY;
                float airborneHeight = Mathf.Max(0f, verticalHeight - this.m_GroundedPivotHeight);
                this.UpdateAirborneState(airborneHeight);

                Vector3 position = ownerPosition - this.m_GroundNormal * planeDistance;
                position += this.m_GroundNormal * this.m_GroundOffset;

                Quaternion rotation = this.GetGroundAlignedRotation(this.m_GroundNormal);
                this.m_ShadowTransform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                this.UpdateAirborneState(this.m_AirborneHideHeight);
            }

            this.UpdateRendererVisibility();
        }

        private Quaternion GetGroundAlignedRotation(Vector3 groundNormal)
        {
            Transform orientation = this.m_OrientationTransform != null
                ? this.m_OrientationTransform
                : this.transform;
            Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, groundNormal);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.Cross(orientation.right, groundNormal);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                return Quaternion.FromToRotation(Vector3.up, groundNormal);
            }

            return Quaternion.LookRotation(forward.normalized, groundNormal);
        }

        private Vector3 GetFootprintWorldCenter()
        {
            Transform orientation = this.m_OrientationTransform != null
                ? this.m_OrientationTransform
                : this.transform;
            Vector3 center = orientation.TransformPoint(
                new Vector3(this.m_FootprintCenter.x, 0f, this.m_FootprintCenter.y)
            );
            center.y = this.transform.position.y;
            return center;
        }

        private void UpdateAirborneState(float airborneHeight)
        {
            this.m_AirborneHeight = Mathf.Max(0f, airborneHeight);
            float ratio = Mathf.InverseLerp(
                this.m_AirborneFadeStart,
                this.m_AirborneHideHeight,
                this.m_AirborneHeight
            );

            float opacityMultiplier = 1f - ratio;
            float scaleMultiplier = Mathf.Lerp(1f, this.m_MinAirborneScale, ratio);
            bool appearanceChanged = Mathf.Abs(
                this.m_AirborneOpacityMultiplier - opacityMultiplier
            ) > 0.005f;
            bool scaleChanged = Mathf.Abs(
                this.m_AirborneScaleMultiplier - scaleMultiplier
            ) > 0.005f;

            this.m_AirborneOpacityMultiplier = opacityMultiplier;
            this.m_AirborneScaleMultiplier = scaleMultiplier;
            this.m_AirborneHidden = ratio >= 0.999f;

            if (appearanceChanged) this.ApplyAppearance();
            if (scaleChanged) this.ApplyVolumeSize();
        }

        private void ResetAirborneState()
        {
            bool appearanceChanged = !Mathf.Approximately(this.m_AirborneOpacityMultiplier, 1f);
            bool scaleChanged = !Mathf.Approximately(this.m_AirborneScaleMultiplier, 1f);

            this.m_AirborneHeight = 0f;
            this.m_AirborneOpacityMultiplier = 1f;
            this.m_AirborneScaleMultiplier = 1f;
            this.m_AirborneHidden = false;

            if (appearanceChanged) this.ApplyAppearance();
            if (scaleChanged) this.ApplyVolumeSize();
        }

        private bool TrySampleGround(out Vector3 point, out Vector3 normal)
        {
            Vector3 origin = this.GetFootprintWorldCenter() + Vector3.up * this.m_ProbeStartHeight;
            float distance = this.m_ProbeStartHeight + this.m_MaxGroundDistance;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                GroundHits,
                distance,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = float.PositiveInfinity;
            int nearestIndex = -1;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.collider == null || hit.normal.y < this.m_MinGroundNormalY) continue;
                if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform)) continue;
                if (
                    this.m_IgnoreRigidbodyReceivers &&
                    hit.collider.attachedRigidbody != null
                )
                {
                    continue;
                }
                if (hit.distance >= nearestDistance) continue;

                nearestDistance = hit.distance;
                nearestIndex = i;
            }

            if (nearestIndex >= 0)
            {
                point = GroundHits[nearestIndex].point;
                normal = GroundHits[nearestIndex].normal;
                return true;
            }

            point = default;
            normal = Vector3.up;
            return false;
        }

        private void UpdateRendererVisibility()
        {
            bool groundVisible = !this.m_FollowGround || this.m_HasGround || !this.m_HideWhenGroundMissing;
            bool airborneVisible = !this.m_AirborneHidden;

            this.SetRendererEnabled(
                GlobalEnabled &&
                !this.m_Suspended &&
                !this.m_InsideShadowOwner &&
                this.m_UserVisible &&
                groundVisible &&
                airborneVisible &&
                this.m_DistanceVisible
            );
        }

        private void ApplyGlobalEnabledState(bool enabled)
        {
            if (!enabled || this.m_Suspended || this.m_InsideShadowOwner)
            {
                this.SetRendererEnabled(false);
                return;
            }

            if (!this.isActiveAndEnabled) return;

            this.m_ForceGroundProbe = true;
            this.m_NextGroundProbeTime = 0f;
            this.m_NextDistanceCheckTime = 0f;
            this.m_DistanceVisible = true;
            this.TryResolveCamera(true);

            if (this.UpdateDistanceVisibility(true))
            {
                this.UpdatePlacement(true);
            }
            else
            {
                this.SetRendererEnabled(false);
            }
        }

        private void RefreshShadowOwnerSuppression()
        {
            bool wasSuppressed = this.m_InsideShadowOwner;
            this.m_InsideShadowOwner = false;

            if (this.m_SuppressInsideShadowOwner)
            {
                for (Transform current = this.transform.parent;
                     current != null;
                     current = current.parent)
                {
                    if (
                        current.TryGetComponent(out FranklinBlobShadow ownerShadow) &&
                        ownerShadow != this
                    )
                    {
                        this.m_InsideShadowOwner = true;
                        break;
                    }
                }
            }

            if (this.m_InsideShadowOwner)
            {
                this.SetRendererEnabled(false);
                return;
            }

            if (wasSuppressed)
            {
                this.m_ForceGroundProbe = true;
                this.m_NextGroundProbeTime = 0f;
                this.m_NextDistanceCheckTime = 0f;
            }
        }

        private static void EnsureGlobalStateLoaded()
        {
            if (s_GlobalStateLoaded) return;

            // V1 repairs projects that saved the master switch OFF before the
            // Player/Npc/Bike/Car layer contract was installed. Run only once;
            // subsequent player choices keep persisting normally.
            if (PlayerPrefs.GetInt(OwnerLayerMigrationPreferenceKey, 0) == 0)
            {
                s_GlobalEnabled = true;
                PlayerPrefs.SetInt(GlobalEnabledPreferenceKey, 1);
                PlayerPrefs.SetInt(OwnerLayerMigrationPreferenceKey, 1);
                PlayerPrefs.Save();
            }
            else
            {
                s_GlobalEnabled =
                    PlayerPrefs.GetInt(GlobalEnabledPreferenceKey, 1) != 0;
            }
            s_GlobalStateLoaded = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instances.Clear();
            s_GlobalEnabled = true;
            s_GlobalStateLoaded = false;
            GlobalEnabledChanged = null;
        }

        private void SetRendererEnabled(bool enabled)
        {
            if (this.m_ShadowRenderer != null && this.m_ShadowRenderer.enabled != enabled)
            {
                this.m_ShadowRenderer.enabled = enabled;
            }
        }
    }
}
