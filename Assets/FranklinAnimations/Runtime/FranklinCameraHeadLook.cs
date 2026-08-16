using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Characters.IK;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Makes the on-foot character naturally glance towards the centre of the camera while
    /// it orbits. Uses GC2's public Look-To rig without modifying the GC2 package.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2)]
    public sealed class FranklinCameraHeadLook : MonoBehaviour
    {
        private const float CAMERA_LOOKUP_RETRY_SECONDS = 0.5f;
        private const float MIN_DELTA_TIME = 0.0001f;
        private const float MIN_PLANAR_DIRECTION_SQR = 0.0025f;
        private const float MAX_CAMERA_SAMPLE_DELTA_TIME = 0.12f;
        private const float MAX_CAMERA_SAMPLE_ANGLE = 55f;

        [Header("Character and camera")]
        [SerializeField]
        [Tooltip("GC2 Character to animate. Resolved from a parent when omitted.")]
        private Character m_Player;
        [SerializeField]
        [Tooltip("Optional camera override. By default the active GC2 Main Camera is used.")]
        private Transform m_CameraOverride;

        [Header("Camera orbit detection")]
        [SerializeField]
        [Tooltip("Only activate the head look after the camera starts orbiting.")]
        private bool m_OnlyWhileCameraOrbiting = true;
        [SerializeField, Min(0f)]
        [Tooltip("Minimum camera rotation speed in degrees/second that counts as orbit input.")]
        private float m_MinOrbitSpeed = 3f;
        [SerializeField, Min(0f)]
        [Tooltip("How long the character keeps looking after the camera stops orbiting.")]
        private float m_KeepLookingTime = 1.15f;

        [Header("Natural head movement")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Distance of the virtual look target from the character's eyes.")]
        private float m_LookDistance = 8f;
        [SerializeField, Min(0.01f)]
        [Tooltip("Higher values make the head follow the camera more slowly.")]
        private float m_FollowSmoothTime = 0.38f;
        [SerializeField, Range(0f, 85f)] private float m_MaxYaw = 55f;
        [SerializeField, Range(0f, 45f)] private float m_MaxUpPitch = 18f;
        [SerializeField, Range(0f, 45f)] private float m_MaxDownPitch = 12f;
        [SerializeField, Range(60f, 175f)]
        [Tooltip("Stops head tracking when the camera points too far behind the character. This prevents the yaw sign from flipping at 180 degrees.")]
        private float m_MaxTrackYaw = 100f;
        [SerializeField, Range(0f, 30f)]
        [Tooltip("Degrees the camera must return inside Max Track Yaw before head tracking resumes.")]
        private float m_TrackYawHysteresis = 10f;
        [SerializeField, Min(1f)]
        [Tooltip("Maximum local head-target turn speed in degrees/second.")]
        private float m_MaxFollowSpeed = 140f;
        [SerializeField]
        [Tooltip("Lets GC2 Shooter exclusively own the neck/head while a Shooter weapon is equipped.")]
        private bool m_DisableWhileShooterWeaponEquipped = true;
        [SerializeField]
        [Tooltip("Lower GC2 Look-To layers are evaluated first. This target is removed when idle.")]
        private int m_LookPriority = -10;

        private Transform m_ActiveCamera;
        private Transform m_LookTargetTransform;
        private FranklinAnimationBridge m_MovementBridge;
        private RigLookTo m_LookRig;
        private LookToTransform m_LookTarget;
        private Vector3 m_LastCameraForward;
        private Vector3 m_LastStablePlanarForward;
        private float m_SmoothedYaw;
        private float m_SmoothedPitch;
        private float m_YawVelocity;
        private float m_PitchVelocity;
        private float m_NextCameraLookupTime;
        private float m_OrbitActiveUntil = float.NegativeInfinity;
        private bool m_HasCameraSample;
        private bool m_HasStablePlanarForward;
        private bool m_HasSmoothedAngles;
        private bool m_IsInsideTrackingCone = true;
        private bool m_IsTargetRegistered;
        private bool m_HasWarnedNonHumanoid;

        private void Awake()
        {
            this.ResolveCharacter();
            this.m_MovementBridge = this.GetComponent<FranklinAnimationBridge>();
        }

        private void Start()
        {
            this.EnsureLookRig();
        }

        private void OnValidate()
        {
            this.m_MinOrbitSpeed = Mathf.Max(0f, this.m_MinOrbitSpeed);
            this.m_KeepLookingTime = Mathf.Max(0f, this.m_KeepLookingTime);
            this.m_LookDistance = Mathf.Max(0.1f, this.m_LookDistance);
            this.m_FollowSmoothTime = Mathf.Max(0.01f, this.m_FollowSmoothTime);
            this.m_MaxYaw = Mathf.Clamp(this.m_MaxYaw, 0f, 85f);
            this.m_MaxUpPitch = Mathf.Clamp(this.m_MaxUpPitch, 0f, 45f);
            this.m_MaxDownPitch = Mathf.Clamp(this.m_MaxDownPitch, 0f, 45f);
            this.m_MaxTrackYaw = Mathf.Clamp(
                this.m_MaxTrackYaw,
                Mathf.Max(60f, this.m_MaxYaw),
                175f
            );
            this.m_TrackYawHysteresis = Mathf.Clamp(
                this.m_TrackYawHysteresis,
                0f,
                Mathf.Min(30f, this.m_MaxTrackYaw - this.m_MaxYaw)
            );
            this.m_MaxFollowSpeed = Mathf.Max(1f, this.m_MaxFollowSpeed);
        }

        private void Update()
        {
            if (!this.CanUseHeadLook() || !this.TryResolveCamera(out Transform cameraTransform))
            {
                this.DeactivateLookTarget();
                this.m_HasCameraSample = false;
                return;
            }

            Vector3 cameraForward = cameraTransform.forward;
            if (!TryNormalizeFinite(ref cameraForward))
            {
                this.DeactivateLookTarget();
                this.m_HasCameraSample = false;
                return;
            }

            bool isOrbiting = this.DetectCameraOrbit(
                cameraForward,
                out bool cameraSampleIsValid
            );
            if (!cameraSampleIsValid)
            {
                this.DeactivateLookTarget();
                this.m_OrbitActiveUntil = float.NegativeInfinity;
                return;
            }

            if (isOrbiting)
            {
                this.m_OrbitActiveUntil = Time.unscaledTime + this.m_KeepLookingTime;
            }

            bool shouldLook = !this.m_OnlyWhileCameraOrbiting ||
                              isOrbiting ||
                              Time.unscaledTime <= this.m_OrbitActiveUntil;
            if (!shouldLook || !this.EnsureLookRig())
            {
                this.DeactivateLookTarget();
                return;
            }

            if (!this.TryCalculateTargetAngles(
                    cameraForward,
                    out float targetYaw,
                    out float targetPitch
                ))
            {
                this.DeactivateLookTarget();
                return;
            }

            this.UpdateSmoothedAngles(targetYaw, targetPitch);
            Vector3 targetPosition = this.CalculateTargetPosition(
                this.m_SmoothedYaw,
                this.m_SmoothedPitch
            );
            this.ActivateLookTarget(targetPosition);
            this.m_LookTargetTransform.position = targetPosition;
        }

        private bool ResolveCharacter()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            return this.m_Player != null;
        }

        private bool CanUseHeadLook()
        {
            if (!this.ResolveCharacter() || !this.m_Player.isActiveAndEnabled) return false;
            if (this.m_MovementBridge != null &&
                this.m_MovementBridge.IsExternalAnimationLocked) return false;
            if (this.m_Player.Ragdoll?.IsRagdoll == true) return false;
            if (this.m_Player.Busy?.IsBusy == true) return false;
            if (this.m_DisableWhileShooterWeaponEquipped &&
                this.m_Player.Combat?.GetActiveWeapon<ShooterWeapon>() != null)
            {
                return false;
            }
            if (this.m_Player.IsDead || this.m_Player.Player?.IsControllable != true) return false;

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator != null && animator.isActiveAndEnabled && animator.isHuman) return true;

            if (!this.m_HasWarnedNonHumanoid)
            {
                this.m_HasWarnedNonHumanoid = true;
                Debug.LogWarning(
                    "Franklin camera head look requires a Humanoid Animator.",
                    this
                );
            }

            return false;
        }

        private bool EnsureLookRig()
        {
            if (this.m_LookRig != null && this.m_LookTargetTransform != null) return true;
            if (!this.ResolveCharacter() || this.m_Player.IK == null) return false;

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator == null || !animator.isHuman) return false;

            this.m_LookRig = this.m_Player.IK.RequireRig<RigLookTo>();
            if (this.m_LookRig == null) return false;

            if (this.m_LookTargetTransform == null)
            {
                GameObject lookTarget = new GameObject("Franklin Camera Head Look Target");
                lookTarget.hideFlags = HideFlags.HideAndDontSave;
                this.m_LookTargetTransform = lookTarget.transform;
                this.m_LookTarget = new LookToTransform(
                    this.m_LookPriority,
                    this.m_LookTargetTransform,
                    Vector3.zero
                );
            }

            return true;
        }

        private bool TryResolveCamera(out Transform cameraTransform)
        {
            if (this.m_CameraOverride != null)
            {
                cameraTransform = this.m_CameraOverride;
                return true;
            }

            if (this.m_ActiveCamera == null &&
                Time.unscaledTime >= this.m_NextCameraLookupTime)
            {
                this.m_ActiveCamera = ShortcutMainCamera.Transform;
                if (this.m_ActiveCamera == null) this.m_ActiveCamera = ShortcutMainShot.Transform;
                if (this.m_ActiveCamera == null && Camera.main != null)
                {
                    this.m_ActiveCamera = Camera.main.transform;
                }

                if (this.m_ActiveCamera == null)
                {
                    this.m_NextCameraLookupTime =
                        Time.unscaledTime + CAMERA_LOOKUP_RETRY_SECONDS;
                }
            }

            cameraTransform = this.m_ActiveCamera;
            return cameraTransform != null;
        }

        private bool DetectCameraOrbit(
            Vector3 cameraForward,
            out bool sampleIsValid)
        {
            if (!this.m_HasCameraSample)
            {
                this.m_LastCameraForward = cameraForward;
                this.m_HasCameraSample = true;
                sampleIsValid = false;
                return false;
            }

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, MIN_DELTA_TIME);
            float angle = Vector3.Angle(this.m_LastCameraForward, cameraForward);
            this.m_LastCameraForward = cameraForward;
            sampleIsValid = deltaTime <= MAX_CAMERA_SAMPLE_DELTA_TIME &&
                            angle <= MAX_CAMERA_SAMPLE_ANGLE;
            if (!sampleIsValid) return false;

            float angularSpeed = angle / deltaTime;
            return angularSpeed >= this.m_MinOrbitSpeed;
        }

        private bool TryCalculateTargetAngles(
            Vector3 cameraForward,
            out float yaw,
            out float pitch)
        {
            Transform characterTransform = this.m_Player.transform;
            Vector3 localDirection = characterTransform.InverseTransformDirection(cameraForward);

            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (planarForward.sqrMagnitude >= MIN_PLANAR_DIRECTION_SQR)
            {
                planarForward.Normalize();
                this.m_LastStablePlanarForward = planarForward;
                this.m_HasStablePlanarForward = true;
            }
            else if (this.m_HasStablePlanarForward)
            {
                planarForward = this.m_LastStablePlanarForward;
            }
            else
            {
                planarForward = Vector3.ProjectOnPlane(
                    characterTransform.forward,
                    Vector3.up
                ).normalized;
            }

            Vector3 localPlanar = characterTransform.InverseTransformDirection(planarForward);
            float rawYaw = Mathf.Atan2(localPlanar.x, localPlanar.z) * Mathf.Rad2Deg;
            float absoluteYaw = Mathf.Abs(rawYaw);
            float reacquireYaw = Mathf.Max(
                this.m_MaxYaw,
                this.m_MaxTrackYaw - this.m_TrackYawHysteresis
            );

            if (this.m_IsInsideTrackingCone)
            {
                if (absoluteYaw > this.m_MaxTrackYaw)
                {
                    this.m_IsInsideTrackingCone = false;
                    yaw = 0f;
                    pitch = 0f;
                    return false;
                }
            }
            else if (absoluteYaw > reacquireYaw)
            {
                yaw = 0f;
                pitch = 0f;
                return false;
            }
            else
            {
                this.m_IsInsideTrackingCone = true;
            }

            float horizontal = Mathf.Sqrt(
                localDirection.x * localDirection.x + localDirection.z * localDirection.z
            );
            float rawPitch = Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(rawYaw, -this.m_MaxYaw, this.m_MaxYaw);
            pitch = Mathf.Clamp(rawPitch, -this.m_MaxDownPitch, this.m_MaxUpPitch);
            return IsFinite(yaw) && IsFinite(pitch);
        }

        private void UpdateSmoothedAngles(float targetYaw, float targetPitch)
        {
            if (!this.m_HasSmoothedAngles)
            {
                this.m_SmoothedYaw = 0f;
                this.m_SmoothedPitch = 0f;
                this.m_YawVelocity = 0f;
                this.m_PitchVelocity = 0f;
                this.m_HasSmoothedAngles = true;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, MIN_DELTA_TIME);
            this.m_SmoothedYaw = Mathf.SmoothDampAngle(
                this.m_SmoothedYaw,
                targetYaw,
                ref this.m_YawVelocity,
                this.m_FollowSmoothTime,
                this.m_MaxFollowSpeed,
                deltaTime
            );
            this.m_SmoothedPitch = Mathf.SmoothDamp(
                this.m_SmoothedPitch,
                targetPitch,
                ref this.m_PitchVelocity,
                this.m_FollowSmoothTime,
                this.m_MaxFollowSpeed,
                deltaTime
            );

            this.m_SmoothedYaw = Mathf.Clamp(
                this.m_SmoothedYaw,
                -this.m_MaxYaw,
                this.m_MaxYaw
            );
            this.m_SmoothedPitch = Mathf.Clamp(
                this.m_SmoothedPitch,
                -this.m_MaxDownPitch,
                this.m_MaxUpPitch
            );
        }

        private Vector3 CalculateTargetPosition(float yaw, float pitch)
        {
            Transform characterTransform = this.m_Player.transform;

            float yawRadians = yaw * Mathf.Deg2Rad;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            Vector3 clampedLocalDirection = new Vector3(
                Mathf.Sin(yawRadians) * Mathf.Cos(pitchRadians),
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * Mathf.Cos(pitchRadians)
            );

            Vector3 worldDirection = characterTransform.TransformDirection(
                clampedLocalDirection.normalized
            );
            return this.m_Player.Eyes + worldDirection * this.m_LookDistance;
        }

        private static bool TryNormalizeFinite(ref Vector3 direction)
        {
            if (!IsFinite(direction.x) || !IsFinite(direction.y) ||
                !IsFinite(direction.z) || direction.sqrMagnitude <= float.Epsilon)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ActivateLookTarget(Vector3 initialPosition)
        {
            if (this.m_LookTargetTransform == null || this.m_LookRig == null) return;
            if (this.m_IsTargetRegistered) return;

            this.m_LookTargetTransform.position = initialPosition;
            this.m_LookRig.SetTarget(this.m_LookTarget);
            this.m_IsTargetRegistered = true;
        }

        private void DeactivateLookTarget()
        {
            if (!this.m_IsTargetRegistered) return;

            this.m_LookRig?.RemoveTarget(this.m_LookTarget);
            this.m_IsTargetRegistered = false;
            this.m_YawVelocity = 0f;
            this.m_PitchVelocity = 0f;
            this.m_HasSmoothedAngles = false;
        }

        private void OnDisable()
        {
            this.DeactivateLookTarget();
            this.m_HasCameraSample = false;
            this.m_HasStablePlanarForward = false;
            this.m_IsInsideTrackingCone = true;
        }

        private void OnDestroy()
        {
            this.DeactivateLookTarget();
            if (this.m_LookTargetTransform != null)
            {
                Destroy(this.m_LookTargetTransform.gameObject);
            }
        }
    }
}
