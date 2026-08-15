using System.Reflection;
using FranklinGame.Animations;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// The single owner of every on-foot, Shooter, Bike and Car FPS profile.
    /// It changes configuration on the active GC2 Camera Shot but never creates
    /// another Camera, Shot or custom camera-rotation loop.
    /// </summary>
    // Run after Animator evaluation but before GC2 ShotCamera.LateUpdate (order 0).
    // This removes animated Neck/Head rotation from vehicle FPS pivots before
    // ShotSystemThirdPerson samples them for native alignment.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class FranklinFirstPersonCameraManager : MonoBehaviour
    {
        public enum Context
        {
            PlayerMovement,
            Shooter,
            Bike,
            Car
        }

        private const BindingFlags PRIVATE_INSTANCE =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Pivot", PRIVATE_INSTANCE);
        private static readonly FieldInfo SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Shoulder", PRIVATE_INSTANCE);
        private static readonly FieldInfo LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Lift", PRIVATE_INSTANCE);
        private static readonly FieldInfo RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Radius", PRIVATE_INSTANCE);
        private static readonly FieldInfo SENSITIVITY_X_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SensitivityX",
                PRIVATE_INSTANCE
            );
        private static readonly FieldInfo SENSITIVITY_Y_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SensitivityY",
                PRIVATE_INSTANCE
            );
        private static readonly FieldInfo SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SmoothTime", PRIVATE_INSTANCE);
        private static readonly FieldInfo MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_MaxYaw", PRIVATE_INSTANCE);
        private static readonly FieldInfo CURRENT_ROTATION_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_CurrentRotation",
                PRIVATE_INSTANCE
            );
        private static readonly FieldInfo CLIPPING_FIELD =
            typeof(ShotCamera).GetField("m_Clipping", PRIVATE_INSTANCE);
        private static readonly int FIELD_OF_VIEW_TWEEN_HASH =
            Tween.GetHash(typeof(TCamera), "projection");

        [Header("Player")]
        [SerializeField] private Character m_Player;

        [Header("On-foot / Shooter FPS Position")]
        [Tooltip("On-foot/Shooter eye offset in the heading transform's right/up/forward axes.")]
        [SerializeField] private Vector3 m_Position = new(0f, 0.025f, 0.055f);
        [Tooltip("Fallback position relative to Player when no Humanoid Head exists.")]
        [SerializeField] private Vector3 m_FallbackPosition =
            new(0f, 1.62f, 0.055f);

        [Header("Bike FPS Position")]
        [Tooltip("Bike FPS eye offset in the Bike heading axes.")]
        [SerializeField] private Vector3 m_BikePosition =
            new(0f, 0.025f, 0.055f);
        [Tooltip("Bike FPS eye offset while Shooter aim/fire is active.")]
        [SerializeField] private Vector3 m_BikeShooterPosition =
            new(0f, 0.025f, 0.055f);

        [Header("Car FPS Positions")]
        [Tooltip("Car forward FPS eye offset in the Car heading axes.")]
        [SerializeField] private Vector3 m_CarPosition =
            new(0f, 0.025f, 0.055f);
        [Tooltip("Car rear-view FPS eye offset in the Car heading axes.")]
        [SerializeField] private Vector3 m_CarRearPosition =
            new(0f, 0.025f, -0.185f);

        [Header("FPS View")]
        [SerializeField, Range(45f, 100f)] private float m_FieldOfView = 82f;
        [Tooltip("Initial local pitch offset when FPS is enabled.")]
        [SerializeField, Range(-89f, 89f)] private float m_Pitch;
        [Tooltip("Initial local yaw offset from Player/vehicle heading.")]
        [SerializeField, Range(-180f, 180f)] private float m_Yaw;

        [Header("GC2 Camera Shot")]
        [SerializeField, Range(20f, 170f)] private float m_MaxPitch = 110f;
        [Tooltip("Horizontal look range on each side. Zero means unrestricted yaw.")]
        [SerializeField, Range(0f, 89.5f)] private float m_MaxYawPerSide;
        [SerializeField] private Vector2 m_Sensitivity = new(0.2f, 0.2f);
        [SerializeField, Range(0.01f, 0.5f)] private float m_OrbitSmoothTime = 0.15f;
        [SerializeField, Range(0.005f, 0.1f)] private float m_Radius = 0.015f;
        [SerializeField, Range(0.01f, 0.3f)] private float m_NearClipPlane = 0.04f;

        [Header("GC2 Alignment")]
        [Tooltip("Time after the orbit pointer is released before GC2 auto-aligns.")]
        [SerializeField, Min(0f)] private float m_AlignmentReleaseDelay = 0.1f;
        [SerializeField, Range(0.05f, 1f)]
        private float m_OnFootAlignmentSmoothTime = 0.3f;
        [Tooltip("Fast return to the current Car/Bike heading.")]
        [SerializeField, Range(0.05f, 1f)]
        private float m_VehicleAlignmentSmoothTime = 0.18f;

        [Header("Character")]
        [SerializeField, Min(1800f)] private float m_FpsAngularSpeed = 200000f;

        private Object m_Owner;
        private Context m_Context;
        private MainCamera m_MainCamera;
        private ShotCamera m_ActiveShot;
        private ShotSystemThirdPerson m_ThirdPerson;
        private ShotSystemZoom m_Zoom;
        private Transform m_Pivot;
        private Transform m_AlignmentTarget;
        private bool m_BikeShooterPositionActive;
        private System.Action m_RefreshPreviewPose;
        private FranklinAnimationBridge m_AnimationBridge;
        private bool m_OwnsIdleVariationSuppression;
        private FranklinSprintCameraYaw m_SprintYaw;
        private bool m_SprintYawWasEnabled;
        private Snapshot m_Snapshot;
        private bool m_HasSnapshot;
        private bool m_OrbitHeld;
        private bool m_AlignmentDampingResetPending;
        private bool m_AlignmentSuspended;
        private float m_AlignmentResumeAt;
        private int m_OrbitSuppressions;
        private float m_OrbitSensitivityResumeAt;
        private bool m_OrbitSensitivityResumePending;
        private IUnitMotion m_RotationMotion;
        private float m_RotationBase;
        private float m_RotationLastApplied;
        private bool m_HasRotationSnapshot;
        private bool m_HasInspectorPreviewState;
        private Vector3 m_InspectorPreviewPosition;
        private Vector3 m_InspectorPreviewFallbackPosition;
        private Vector3 m_InspectorPreviewBikePosition;
        private Vector3 m_InspectorPreviewBikeShooterPosition;
        private Vector3 m_InspectorPreviewCarPosition;
        private Vector3 m_InspectorPreviewCarRearPosition;
        private float m_InspectorPreviewPitch;
        private float m_InspectorPreviewYaw;
        private bool m_InspectorPosePreviewPending;

        // Snapshot ownership, rather than Unity's overloaded Object null check,
        // is authoritative. A destroyed owner compares equal to null and must
        // still be able to release the GC2 profile and Character turn speed.
        public bool IsActive => this.m_HasSnapshot;
        public Context ActiveContext => this.m_Context;
        public ShotCamera ActiveShot => this.m_ActiveShot;
        public ShotSystemThirdPerson ActiveThirdPerson => this.m_ThirdPerson;
        public Vector3 Position => this.m_Position;
        public Vector3 FallbackPosition => this.m_FallbackPosition;
        public Vector3 BikePosition => this.m_BikePosition;
        public Vector3 BikeShooterPosition => this.m_BikeShooterPosition;
        public Vector3 CarPosition => this.m_CarPosition;
        public Vector3 CarRearPosition => this.m_CarRearPosition;
        public float FieldOfView => this.m_FieldOfView;
        public float Pitch => this.m_Pitch;
        public float Yaw => this.m_Yaw;

        private struct Snapshot
        {
            public object Pivot;
            public object Shoulder;
            public object Lift;
            public object Radius;
            public object SensitivityX;
            public object SensitivityY;
            public object SmoothTime;
            public float MaxPitch;
            public bool MaxYawEnabled;
            public float MaxYaw;
            public bool AutoAlign;
            public float AlignDelay;
            public float AlignSmoothTime;
            public float ZoomMinDistance;
            public float ZoomSmoothTime;
            public float FieldOfView;
            public float NearClipPlane;
            public object Clipping;
            public Vector2 Rotation;
        }

        public static FranklinFirstPersonCameraManager Resolve(
            Character player,
            bool createIfMissing = true)
        {
            if (player == null) return null;

            FranklinFirstPersonCameraManager manager =
                player.GetComponentInChildren<FranklinFirstPersonCameraManager>(true);
            if (manager != null || !createIfMissing) return manager;

            GameObject child = new("ManagerCameraFPS");
            child.transform.SetParent(player.transform, false);
            manager = child.AddComponent<FranklinFirstPersonCameraManager>();
            manager.m_Player = player;
            return manager;
        }

        private void Awake()
        {
            if (this.m_Player == null)
                this.m_Player = this.GetComponentInParent<Character>();
            this.CaptureInspectorPreviewState();
        }

        private void OnValidate()
        {
            bool hadPreviewState = this.m_HasInspectorPreviewState;
            bool positionChanged = !this.m_HasInspectorPreviewState ||
                this.m_Position != this.m_InspectorPreviewPosition ||
                this.m_FallbackPosition != this.m_InspectorPreviewFallbackPosition ||
                this.m_BikePosition != this.m_InspectorPreviewBikePosition ||
                this.m_BikeShooterPosition !=
                    this.m_InspectorPreviewBikeShooterPosition ||
                this.m_CarPosition != this.m_InspectorPreviewCarPosition ||
                this.m_CarRearPosition != this.m_InspectorPreviewCarRearPosition;
            bool pitchChanged = !this.m_HasInspectorPreviewState ||
                !Mathf.Approximately(this.m_Pitch, this.m_InspectorPreviewPitch);
            bool yawChanged = !this.m_HasInspectorPreviewState ||
                !Mathf.Approximately(this.m_Yaw, this.m_InspectorPreviewYaw);

            this.m_FieldOfView = Mathf.Clamp(this.m_FieldOfView, 45f, 100f);
            this.m_Pitch = Mathf.Clamp(this.m_Pitch, -89f, 89f);
            this.m_Yaw = Mathf.Clamp(this.m_Yaw, -180f, 180f);
            this.m_MaxPitch = Mathf.Clamp(this.m_MaxPitch, 20f, 170f);
            this.m_MaxYawPerSide = Mathf.Clamp(this.m_MaxYawPerSide, 0f, 89.5f);
            this.m_Sensitivity.x = Mathf.Max(0f, this.m_Sensitivity.x);
            this.m_Sensitivity.y = Mathf.Max(0f, this.m_Sensitivity.y);
            this.m_OrbitSmoothTime = Mathf.Clamp(this.m_OrbitSmoothTime, 0.01f, 0.5f);
            this.m_Radius = Mathf.Clamp(this.m_Radius, 0.005f, 0.1f);
            this.m_NearClipPlane = Mathf.Clamp(this.m_NearClipPlane, 0.01f, 0.3f);
            this.m_AlignmentReleaseDelay = Mathf.Max(0f, this.m_AlignmentReleaseDelay);
            this.m_OnFootAlignmentSmoothTime = Mathf.Clamp(
                this.m_OnFootAlignmentSmoothTime,
                0.05f,
                1f
            );
            this.m_VehicleAlignmentSmoothTime = Mathf.Clamp(
                this.m_VehicleAlignmentSmoothTime,
                0.05f,
                1f
            );
            this.m_FpsAngularSpeed = Mathf.Max(1800f, this.m_FpsAngularSpeed);

            if (Application.isPlaying && this.IsActive)
            {
                // Position and initial rotation have dedicated preview paths. Do
                // not reapply the viewport for those edits: while Bike Shooter
                // Position is tuned during ADS, the active Sight must retain its
                // intentional zoom FOV until ExitSight.
                bool selectivePreviewChange = hadPreviewState &&
                    (positionChanged || pitchChanged || yawChanged);
                if (!selectivePreviewChange)
                {
                    this.CancelFieldOfViewTween();
                    this.ApplyProfile(false);
                }
                if (positionChanged) this.m_InspectorPosePreviewPending = true;
                this.ApplyInspectorPreviewRotation(pitchChanged, yawChanged);
                this.UpdateAngularSpeed();
            }

            this.CaptureInspectorPreviewState();
        }

        private void LateUpdate()
        {
            if (!this.IsActive) return;

            if (this.m_Owner == null)
            {
                this.DeactivateInternal(this.CanRestoreViewport(), 0f);
                return;
            }

            if (this.m_MainCamera == null ||
                this.m_MainCamera.Transition.CurrentShotCamera != this.m_ActiveShot)
            {
                // External camera owns the viewport. Restore only the captured
                // Shot data and leave that external viewport untouched.
                this.DeactivateInternal(false, 0f);
                return;
            }

            if (this.m_InspectorPosePreviewPending)
            {
                this.ApplyInspectorPreviewPosition();
                this.m_InspectorPosePreviewPending = false;
            }

            this.UpdateHeadingPivot();
            this.UpdateAngularSpeed();
            this.UpdateAlignmentGate();
            this.UpdateOrbitSensitivityGate();
        }

        public bool Activate(
            Object owner,
            Context context,
            ShotCamera shot,
            Transform pivot,
            Transform alignmentTarget,
            System.Action refreshPreviewPose = null)
        {
            if (owner == null || pivot == null ||
                !this.ResolvePlayer() || !this.ResolveMainCamera() ||
                !TryGetThirdPerson(shot, out ShotSystemThirdPerson thirdPerson))
            {
                return false;
            }

            if (this.IsActive &&
                (this.m_Owner != owner || this.m_ActiveShot != shot))
            {
                bool restoreViewport = this.m_MainCamera.Transition
                    .CurrentShotCamera == this.m_ActiveShot;
                this.DeactivateInternal(restoreViewport, 0f);
            }

            if (this.IsActive)
            {
                this.m_Context = context;
                if (context != Context.Bike)
                    this.m_BikeShooterPositionActive = false;
                this.m_Pivot = pivot;
                this.m_AlignmentTarget = alignmentTarget;
                this.m_RefreshPreviewPose = refreshPreviewPose;
                this.AcquireIdleAnimationSuppression();
                this.ApplyProfile(false);
                return true;
            }

            this.m_Owner = owner;
            this.m_Context = context;
            this.m_ActiveShot = shot;
            this.m_ThirdPerson = thirdPerson;
            this.m_Zoom = shot.ShotType.GetSystem(ShotSystemZoom.ID) as ShotSystemZoom;
            this.m_Pivot = pivot;
            this.m_AlignmentTarget = alignmentTarget;
            this.m_BikeShooterPositionActive = false;
            this.m_RefreshPreviewPose = refreshPreviewPose;

            this.m_SprintYaw = shot.GetComponent<FranklinSprintCameraYaw>();
            this.m_SprintYawWasEnabled =
                this.m_SprintYaw != null && this.m_SprintYaw.enabled;
            if (this.m_SprintYawWasEnabled) this.m_SprintYaw.enabled = false;

            this.CaptureSnapshot();
            this.CaptureAngularSpeed();
            this.AcquireIdleAnimationSuppression();
            this.m_OrbitHeld = FranklinFirstPersonCameraInput.IsOrbitGestureHeld();
            this.m_AlignmentDampingResetPending = false;
            this.m_AlignmentResumeAt = Time.unscaledTime +
                                       this.m_AlignmentReleaseDelay;
            this.CancelFieldOfViewTween();
            this.ApplyProfile(true);
            this.CaptureInspectorPreviewState();
            this.m_MainCamera.Sync();
            return true;
        }

        public void Deactivate(
            Object owner,
            bool restoreViewport = true,
            float duration = 0f)
        {
            if (owner == null || this.m_Owner != owner) return;

            this.DeactivateInternal(restoreViewport, duration);
        }

        private void DeactivateInternal(bool restoreViewport, float duration)
        {
            if (!this.IsActive) return;

            this.RestoreSnapshot(restoreViewport, duration);
            this.RestoreAngularSpeed();
            this.ReleaseIdleAnimationSuppression();

            if (restoreViewport && this.CanRestoreViewport())
            {
                this.m_MainCamera.Sync();
            }

            this.m_Owner = null;
            this.m_ActiveShot = null;
            this.m_ThirdPerson = null;
            this.m_Zoom = null;
            this.m_Pivot = null;
            this.m_AlignmentTarget = null;
            this.m_BikeShooterPositionActive = false;
            this.m_RefreshPreviewPose = null;
            this.m_HasSnapshot = false;
            this.m_OrbitHeld = false;
            this.m_AlignmentDampingResetPending = false;
            this.m_AlignmentSuspended = false;
            this.m_AlignmentResumeAt = 0f;
            this.m_OrbitSuppressions = 0;
            this.m_OrbitSensitivityResumeAt = 0f;
            this.m_OrbitSensitivityResumePending = false;
            this.m_InspectorPosePreviewPending = false;
        }

        public bool IsOwnedBy(Object owner)
        {
            return owner != null && this.IsActive && this.m_Owner == owner;
        }

        public void SetAlignmentSuspended(Object owner, bool suspended)
        {
            if (!this.IsOwnedBy(owner)) return;
            this.m_AlignmentSuspended = suspended;
            if (!suspended)
            {
                this.m_AlignmentResumeAt = Time.unscaledTime +
                                           this.m_AlignmentReleaseDelay;
            }
            this.UpdateAlignmentGate();
        }

        public void SetOrbitSuppressed(Object owner, bool suppressed)
        {
            if (!this.IsOwnedBy(owner)) return;

            int previous = this.m_OrbitSuppressions;
            this.m_OrbitSuppressions = suppressed
                ? previous + 1
                : Mathf.Max(0, previous - 1);
            if (previous == this.m_OrbitSuppressions) return;

            if (this.m_OrbitSuppressions > 0)
            {
                this.m_OrbitSensitivityResumePending = false;
                this.ApplySensitivity(true);
                return;
            }

            this.m_OrbitSensitivityResumeAt = Time.unscaledTime + 0.08f;
            this.m_OrbitSensitivityResumePending = true;
        }

        public void SetDirection(Object owner, Vector3 direction, bool sync = true)
        {
            if (!this.IsOwnedBy(owner) || direction.sqrMagnitude <= 0.0001f) return;
            this.m_ThirdPerson.SetDirection(direction.normalized);
            if (sync) this.m_MainCamera?.Sync();
        }

        public void SetRotation(Object owner, Vector2 rotation, bool sync = true)
        {
            if (!this.IsOwnedBy(owner)) return;
            this.m_ThirdPerson.SetRotation(rotation);
            if (sync) this.m_MainCamera?.Sync();
        }

        public Vector2 GetRotation(Object owner)
        {
            return this.IsOwnedBy(owner)
                ? new Vector2(this.m_ThirdPerson.Pitch, this.m_ThirdPerson.Yaw)
                : Vector2.zero;
        }

        /// <summary>
        /// Reasserts the configured FPS profile without resetting pitch, yaw or
        /// orbit. Bike uses this after a GC2 Sight exits because several authored
        /// Sights tween the shared Main Camera back to a hard-coded FOV of 60.
        /// </summary>
        public void ReapplyOwnedProfile(Object owner)
        {
            if (!this.IsOwnedBy(owner)) return;

            this.CancelFieldOfViewTween();
            this.ApplyProfile(false);
        }

        /// <summary>
        /// Selects the separately authored Bike FPS position while Shooter aim/fire
        /// owns the Bike camera. Position refresh is deferred to LateUpdate so it
        /// samples the final Animator pose before the GC2 Shot is evaluated.
        /// </summary>
        public void SetBikeShooterPositionActive(Object owner, bool active)
        {
            if (!this.IsOwnedBy(owner) || this.m_Context != Context.Bike) return;
            if (this.m_BikeShooterPositionActive == active) return;

            this.m_BikeShooterPositionActive = active;
            this.m_InspectorPosePreviewPending = true;
        }

        public Vector3 GetEyeWorldPosition(
            Context context,
            Animator animator,
            Transform heading,
            Transform fallbackRoot)
        {
            return this.GetEyeWorldPosition(
                this.GetContextPosition(context),
                animator,
                heading,
                fallbackRoot
            );
        }

        public Vector3 GetEyeWorldPosition(
            Animator animator,
            Transform heading,
            Transform fallbackRoot)
        {
            return this.GetEyeWorldPosition(
                Context.PlayerMovement,
                animator,
                heading,
                fallbackRoot
            );
        }

        public Vector3 GetCarRearWorldPosition(
            Animator animator,
            Transform heading,
            Transform fallbackRoot)
        {
            return this.GetEyeWorldPosition(
                this.m_CarRearPosition,
                animator,
                heading,
                fallbackRoot
            );
        }

        private Vector3 GetEyeWorldPosition(
            Vector3 localPosition,
            Animator animator,
            Transform heading,
            Transform fallbackRoot)
        {
            Transform head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (head != null && heading != null)
            {
                return head.position + heading.right * localPosition.x +
                       heading.up * localPosition.y +
                       heading.forward * localPosition.z;
            }

            // Fallback Position is authored for on-foot. Apply the selected
            // profile's delta so Bike/Car remain independently adjustable even
            // when the character does not expose a Humanoid Head bone.
            Vector3 fallbackPosition = this.m_FallbackPosition +
                                       localPosition - this.m_Position;
            return fallbackRoot != null
                ? fallbackRoot.TransformPoint(fallbackPosition)
                : this.transform.TransformPoint(fallbackPosition);
        }

        private Vector3 GetContextPosition(Context context)
        {
            return context switch
            {
                Context.Bike => this.m_BikeShooterPositionActive
                    ? this.m_BikeShooterPosition
                    : this.m_BikePosition,
                Context.Car => this.m_CarPosition,
                _ => this.m_Position
            };
        }

        private void CaptureSnapshot()
        {
            EnablerAngle180 maxYaw = MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) as
                EnablerAngle180;

            this.m_Snapshot = new Snapshot
            {
                Pivot = PIVOT_FIELD?.GetValue(this.m_ThirdPerson),
                Shoulder = SHOULDER_FIELD?.GetValue(this.m_ThirdPerson),
                Lift = LIFT_FIELD?.GetValue(this.m_ThirdPerson),
                Radius = RADIUS_FIELD?.GetValue(this.m_ThirdPerson),
                SensitivityX = SENSITIVITY_X_FIELD?.GetValue(this.m_ThirdPerson),
                SensitivityY = SENSITIVITY_Y_FIELD?.GetValue(this.m_ThirdPerson),
                SmoothTime = SMOOTH_TIME_FIELD?.GetValue(this.m_ThirdPerson),
                MaxPitch = this.m_ThirdPerson.MaxPitch,
                MaxYawEnabled = maxYaw?.IsEnabled ?? false,
                MaxYaw = maxYaw?.Value ?? 0f,
                AutoAlign = this.m_ThirdPerson.Alignment.AutoAlign,
                AlignDelay = this.m_ThirdPerson.Alignment.Delay,
                AlignSmoothTime = this.m_ThirdPerson.Alignment.SmoothTime,
                ZoomMinDistance = this.m_Zoom?.MinDistance ?? 0f,
                ZoomSmoothTime = this.m_Zoom?.SmoothTime ?? 0f,
                FieldOfView = this.m_MainCamera.Viewport.FieldOfView,
                NearClipPlane = this.m_MainCamera.Viewport.ClipPlaneNear,
                Clipping = CLIPPING_FIELD?.GetValue(this.m_ActiveShot),
                Rotation = new Vector2(
                    this.m_ThirdPerson.Pitch,
                    this.m_ThirdPerson.Yaw
                )
            };
            this.m_HasSnapshot = true;
        }

        private void ApplyProfile(bool resetRotation)
        {
            if (!this.IsActive || this.m_ThirdPerson == null || this.m_Pivot == null)
                return;

            PIVOT_FIELD?.SetValue(
                this.m_ThirdPerson,
                GetGameObjectInstance.Create(this.m_Pivot.gameObject)
            );
            this.SetDecimal(SHOULDER_FIELD, 0f);
            this.SetDecimal(LIFT_FIELD, 0f);
            this.SetDecimal(RADIUS_FIELD, this.m_Radius);
            this.SetDecimal(SMOOTH_TIME_FIELD, this.m_OrbitSmoothTime);
            this.ApplySensitivity(this.m_OrbitSuppressions > 0);

            this.m_ThirdPerson.MaxPitch = this.m_MaxPitch;
            this.m_ThirdPerson.Alignment.Delay = this.m_AlignmentReleaseDelay;
            this.m_ThirdPerson.Alignment.SmoothTime = this.IsVehicleContext
                ? this.m_VehicleAlignmentSmoothTime
                : this.m_OnFootAlignmentSmoothTime;

            if (MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) is EnablerAngle180 maxYaw)
            {
                maxYaw.IsEnabled = this.m_MaxYawPerSide > 0f;
                maxYaw.Value = Mathf.Min(179f, this.m_MaxYawPerSide * 2f);
            }

            if (this.m_Zoom != null)
            {
                this.m_Zoom.MinDistance = 0f;
                this.m_Zoom.SmoothTime = 0f;
            }

            CLIPPING_FIELD?.SetValue(
                this.m_ActiveShot,
                ShotCamera.Clipping.ClipThrough
            );

            this.UpdateHeadingPivot();
            if (resetRotation)
            {
                float headingYaw = this.m_Pivot.eulerAngles.y;
                this.m_ThirdPerson.SetRotation(
                    new Vector2(this.m_Pitch, headingYaw + this.m_Yaw)
                );
            }

            this.m_MainCamera.Viewport.SetFieldOfView(
                this.m_FieldOfView,
                0f,
                Easing.Type.Linear
            );
            this.m_MainCamera.Viewport.SetClipPlaneNear(
                this.m_NearClipPlane,
                0f,
                Easing.Type.Linear
            );
            this.UpdateAlignmentGate();
        }

        private void RestoreSnapshot(bool restoreViewport, float duration)
        {
            if (this.m_HasSnapshot && this.m_ThirdPerson != null)
            {
                PIVOT_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Pivot);
                SHOULDER_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Shoulder);
                LIFT_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Lift);
                RADIUS_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Radius);
                SENSITIVITY_X_FIELD?.SetValue(
                    this.m_ThirdPerson,
                    this.m_Snapshot.SensitivityX
                );
                SENSITIVITY_Y_FIELD?.SetValue(
                    this.m_ThirdPerson,
                    this.m_Snapshot.SensitivityY
                );
                SMOOTH_TIME_FIELD?.SetValue(
                    this.m_ThirdPerson,
                    this.m_Snapshot.SmoothTime
                );
                this.m_ThirdPerson.MaxPitch = this.m_Snapshot.MaxPitch;
                this.m_ThirdPerson.Alignment.AutoAlign = this.m_Snapshot.AutoAlign;
                this.m_ThirdPerson.Alignment.Delay = this.m_Snapshot.AlignDelay;
                this.m_ThirdPerson.Alignment.SmoothTime =
                    this.m_Snapshot.AlignSmoothTime;
                this.m_ThirdPerson.SetRotation(this.m_Snapshot.Rotation);

                if (MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) is
                    EnablerAngle180 maxYaw)
                {
                    maxYaw.IsEnabled = this.m_Snapshot.MaxYawEnabled;
                    maxYaw.Value = this.m_Snapshot.MaxYaw;
                }

                if (this.m_Zoom != null)
                {
                    this.m_Zoom.MinDistance = this.m_Snapshot.ZoomMinDistance;
                    this.m_Zoom.SmoothTime = this.m_Snapshot.ZoomSmoothTime;
                }

                if (this.m_ActiveShot != null &&
                    this.m_Snapshot.Clipping != null)
                {
                    CLIPPING_FIELD?.SetValue(this.m_ActiveShot, this.m_Snapshot.Clipping);
                }

                if (restoreViewport && this.m_MainCamera != null)
                {
                    this.CancelFieldOfViewTween();
                    this.m_MainCamera.Viewport.SetFieldOfView(
                        this.m_Snapshot.FieldOfView,
                        duration,
                        Easing.Type.QuadInOut
                    );
                    this.m_MainCamera.Viewport.SetClipPlaneNear(
                        this.m_Snapshot.NearClipPlane,
                        duration,
                        Easing.Type.QuadInOut
                    );
                }
            }
            if (this.m_SprintYaw != null)
                this.m_SprintYaw.enabled = this.m_SprintYawWasEnabled;
            this.m_SprintYaw = null;
            this.m_SprintYawWasEnabled = false;
        }

        private void CancelFieldOfViewTween()
        {
            if (this.m_MainCamera == null) return;
            Tween.Cancel(
                this.m_MainCamera.gameObject,
                FIELD_OF_VIEW_TWEEN_HASH
            );
        }

        private void UpdateHeadingPivot()
        {
            if (this.m_Pivot == null || this.m_AlignmentTarget == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(
                this.m_AlignmentTarget.forward,
                Vector3.up
            );
            if (forward.sqrMagnitude <= 0.0001f) return;
            this.m_Pivot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void ApplyInspectorPreviewPosition()
        {
            if (!this.IsActive || this.m_Pivot == null || this.m_Player == null)
                return;

            if (this.m_RefreshPreviewPose != null)
            {
                this.m_RefreshPreviewPose.Invoke();
                return;
            }

            Animator animator = this.m_Player.Animim?.Animator;
            Transform heading = this.m_AlignmentTarget != null
                ? this.m_AlignmentTarget
                : this.m_Player.transform;
            this.m_Pivot.position = this.GetEyeWorldPosition(
                this.m_Context,
                animator,
                heading,
                this.m_Player.transform
            );
        }

        private void ApplyInspectorPreviewRotation(bool pitchChanged, bool yawChanged)
        {
            if (!this.IsActive || this.m_Pivot == null || this.m_ThirdPerson == null)
                return;

            if (pitchChanged) this.m_ThirdPerson.Pitch = this.m_Pitch;
            if (yawChanged)
            {
                this.UpdateHeadingPivot();
                this.m_ThirdPerson.Yaw = this.m_Pivot.eulerAngles.y + this.m_Yaw;
            }
        }

        private void CaptureInspectorPreviewState()
        {
            this.m_InspectorPreviewPosition = this.m_Position;
            this.m_InspectorPreviewFallbackPosition = this.m_FallbackPosition;
            this.m_InspectorPreviewBikePosition = this.m_BikePosition;
            this.m_InspectorPreviewBikeShooterPosition =
                this.m_BikeShooterPosition;
            this.m_InspectorPreviewCarPosition = this.m_CarPosition;
            this.m_InspectorPreviewCarRearPosition = this.m_CarRearPosition;
            this.m_InspectorPreviewPitch = this.m_Pitch;
            this.m_InspectorPreviewYaw = this.m_Yaw;
            this.m_HasInspectorPreviewState = true;
        }

        private void UpdateAlignmentGate()
        {
            if (this.m_ThirdPerson == null) return;

            // A captured gameplay touch and a pressed HUD control both own the
            // pointer until their real PointerUp. Zero input delta is not a
            // release signal, so GC2 must not align during either hold.
            bool orbitHeld = FranklinFirstPersonCameraInput.IsOrbitGestureHeld();
            bool held = this.m_OrbitSuppressions > 0 || orbitHeld;
            if (held)
            {
                bool alignmentWasActive =
                    this.m_ThirdPerson.Alignment.AutoAlign;
                if (!this.m_OrbitHeld && alignmentWasActive)
                    this.m_AlignmentDampingResetPending = true;

                this.m_ThirdPerson.Alignment.AutoAlign = false;

                if (this.m_AlignmentDampingResetPending && orbitHeld &&
                    FranklinFirstPersonCameraInput.HasOrbitInputThisFrame())
                {
                    this.CancelAlignmentDampingAtCurrentRotation();
                    this.m_AlignmentDampingResetPending = false;
                }

                this.m_OrbitHeld = true;
                return;
            }

            this.m_AlignmentDampingResetPending = false;
            if (this.m_OrbitHeld)
            {
                this.m_OrbitHeld = false;
                this.m_AlignmentResumeAt = Time.unscaledTime +
                                           this.m_AlignmentReleaseDelay;
            }

            this.m_ThirdPerson.Alignment.AutoAlign =
                !this.m_AlignmentSuspended &&
                Time.unscaledTime >= this.m_AlignmentResumeAt;
        }

        private void CancelAlignmentDampingAtCurrentRotation()
        {
            if (this.m_ThirdPerson == null) return;

            // GC2 exposes SetRotation as the native way to synchronize current
            // and target rotation and clear its X/Y damping velocities. It does
            // not expose the current (already rendered) base rotation, so read
            // that value once instead of baking Shot/Aim offsets into the angle.
            if (CURRENT_ROTATION_FIELD?.GetValue(this.m_ThirdPerson) is
                not Vector2 currentRotation)
            {
                return;
            }

            this.m_ThirdPerson.SetRotation(currentRotation);
        }

        private void UpdateOrbitSensitivityGate()
        {
            if (!this.m_OrbitSensitivityResumePending ||
                this.m_OrbitSuppressions > 0 ||
                Time.unscaledTime < this.m_OrbitSensitivityResumeAt)
            {
                return;
            }

            this.m_OrbitSensitivityResumePending = false;
            this.ApplySensitivity(false);
        }

        private void ApplySensitivity(bool suppressed)
        {
            if (this.m_ThirdPerson == null) return;
            this.m_ThirdPerson.Sensitivity = suppressed
                ? Vector2.zero
                : this.m_Sensitivity;
        }

        private void CaptureAngularSpeed()
        {
            this.m_RotationMotion = this.m_Player != null
                ? this.m_Player.Motion
                : null;
            if (this.m_RotationMotion == null) return;
            this.m_RotationBase = this.m_RotationMotion.AngularSpeed;
            this.m_HasRotationSnapshot = true;
            this.m_RotationMotion.AngularSpeed = this.m_FpsAngularSpeed;
            this.m_RotationLastApplied = this.m_RotationMotion.AngularSpeed;
        }

        private void UpdateAngularSpeed()
        {
            IUnitMotion currentMotion = this.m_Player != null
                ? this.m_Player.Motion
                : null;
            if (currentMotion == null) return;

            if (currentMotion != this.m_RotationMotion)
            {
                // A GC2 Kernel motion swap must not leave the old unit carrying
                // the FPS override if that unit is reused later.
                if (this.m_HasRotationSnapshot && this.m_RotationMotion != null)
                    this.m_RotationMotion.AngularSpeed = this.m_RotationBase;

                this.m_RotationMotion = currentMotion;
                this.m_RotationBase = currentMotion.AngularSpeed;
                this.m_HasRotationSnapshot = true;
            }
            else if (!Mathf.Approximately(
                         currentMotion.AngularSpeed,
                         this.m_RotationLastApplied
                     ))
            {
                // GC2 locomotion states can author another rotation speed. Keep
                // that newest base value so exit restores the correct state.
                this.m_RotationBase = currentMotion.AngularSpeed;
            }

            currentMotion.AngularSpeed = this.m_FpsAngularSpeed;
            this.m_RotationLastApplied = currentMotion.AngularSpeed;
        }

        private void RestoreAngularSpeed()
        {
            if (this.m_HasRotationSnapshot && this.m_RotationMotion != null)
                this.m_RotationMotion.AngularSpeed = this.m_RotationBase;
            this.m_RotationMotion = null;
            this.m_RotationBase = 0f;
            this.m_RotationLastApplied = 0f;
            this.m_HasRotationSnapshot = false;
        }

        private void AcquireIdleAnimationSuppression()
        {
            if (this.m_OwnsIdleVariationSuppression || this.m_Player == null) return;

            if (this.m_AnimationBridge == null)
            {
                this.m_AnimationBridge = this.m_Player
                    .GetComponentInChildren<FranklinAnimationBridge>(true);
            }
            if (this.m_AnimationBridge == null) return;

            this.m_AnimationBridge.AcquireIdleVariationsSuppression(this);
            this.m_OwnsIdleVariationSuppression = true;
        }

        private void ReleaseIdleAnimationSuppression()
        {
            if (!this.m_OwnsIdleVariationSuppression) return;

            this.m_AnimationBridge?.ReleaseIdleVariationsSuppression(this);
            this.m_OwnsIdleVariationSuppression = false;
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
                this.m_Player = this.GetComponentInParent<Character>();
            return this.m_Player != null;
        }

        private bool ResolveMainCamera()
        {
            if (this.m_MainCamera == null)
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            return this.m_MainCamera != null;
        }

        private bool IsVehicleContext =>
            this.m_Context == Context.Bike || this.m_Context == Context.Car;

        private bool CanRestoreViewport()
        {
            return this.m_MainCamera != null &&
                   this.m_ActiveShot != null &&
                   this.m_MainCamera.Transition.CurrentShotCamera ==
                   this.m_ActiveShot;
        }

        private void SetDecimal(FieldInfo field, float value)
        {
            field?.SetValue(this.m_ThirdPerson, new PropertyGetDecimal(value));
        }

        private static bool TryGetThirdPerson(
            ShotCamera shot,
            out ShotSystemThirdPerson thirdPerson)
        {
            thirdPerson = null;
            if (shot?.ShotType is not ShotTypeThirdPerson shotType) return false;
            thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            return thirdPerson != null;
        }

        private void OnDisable()
        {
            if (this.IsActive)
                this.DeactivateInternal(this.CanRestoreViewport(), 0f);
        }

        private void OnDestroy()
        {
            if (this.IsActive)
                this.DeactivateInternal(this.CanRestoreViewport(), 0f);
        }
    }
}
