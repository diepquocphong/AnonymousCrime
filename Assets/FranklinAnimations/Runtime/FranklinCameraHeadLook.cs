using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Characters.IK;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Makes the on-foot character naturally glance towards the centre of the camera while
    /// it orbits. Uses GC2's public Look-To rig without modifying the GC2 package.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(90)]
    public sealed class FranklinCameraHeadLook : MonoBehaviour
    {
        private const float CAMERA_LOOKUP_RETRY_SECONDS = 0.5f;
        private const float MIN_DELTA_TIME = 0.0001f;

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
        private float m_MinOrbitSpeed = 1f;
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
        [SerializeField]
        [Tooltip("Lower GC2 Look-To layers are evaluated first. This target is removed when idle.")]
        private int m_LookPriority = -10;

        private Transform m_ActiveCamera;
        private Transform m_LookTargetTransform;
        private FranklinAnimationBridge m_MovementBridge;
        private RigLookTo m_LookRig;
        private LookToTransform m_LookTarget;
        private Vector3 m_TargetVelocity;
        private Vector3 m_LastCameraForward;
        private float m_NextCameraLookupTime;
        private float m_OrbitActiveUntil = float.NegativeInfinity;
        private bool m_HasCameraSample;
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
        }

        private void Update()
        {
            if (!this.CanUseHeadLook() || !this.TryResolveCamera(out Transform cameraTransform))
            {
                this.DeactivateLookTarget();
                this.m_HasCameraSample = false;
                return;
            }

            Vector3 cameraForward = cameraTransform.forward.normalized;
            bool isOrbiting = this.DetectCameraOrbit(cameraForward);
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

            Vector3 targetPosition = this.CalculateClampedTarget(cameraForward);
            this.ActivateLookTarget(targetPosition);

            this.m_LookTargetTransform.position = Vector3.SmoothDamp(
                this.m_LookTargetTransform.position,
                targetPosition,
                ref this.m_TargetVelocity,
                this.m_FollowSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
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
            if (this.m_Player.IsDead || this.m_Player.Player?.IsControllable != true) return false;

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator != null && animator.isHuman) return true;

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

        private bool DetectCameraOrbit(Vector3 cameraForward)
        {
            if (!this.m_HasCameraSample)
            {
                this.m_LastCameraForward = cameraForward;
                this.m_HasCameraSample = true;
                return false;
            }

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, MIN_DELTA_TIME);
            float angularSpeed = Vector3.Angle(this.m_LastCameraForward, cameraForward) / deltaTime;
            this.m_LastCameraForward = cameraForward;
            return angularSpeed >= this.m_MinOrbitSpeed;
        }

        private Vector3 CalculateClampedTarget(Vector3 cameraForward)
        {
            Transform characterTransform = this.m_Player.transform;
            Vector3 localDirection = characterTransform.InverseTransformDirection(cameraForward);

            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                localDirection.x * localDirection.x + localDirection.z * localDirection.z
            );
            float pitch = Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -this.m_MaxYaw, this.m_MaxYaw);
            pitch = Mathf.Clamp(pitch, -this.m_MaxDownPitch, this.m_MaxUpPitch);

            Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 flatDirection = yawRotation * Vector3.forward;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            Vector3 clampedLocalDirection = new Vector3(
                flatDirection.x * Mathf.Cos(pitchRadians),
                Mathf.Sin(pitchRadians),
                flatDirection.z * Mathf.Cos(pitchRadians)
            );

            Vector3 worldDirection = characterTransform.TransformDirection(
                clampedLocalDirection.normalized
            );
            return this.m_Player.Eyes + worldDirection * this.m_LookDistance;
        }

        private void ActivateLookTarget(Vector3 initialPosition)
        {
            if (this.m_LookTargetTransform == null || this.m_LookRig == null) return;
            if (this.m_IsTargetRegistered) return;

            this.m_LookTargetTransform.position = initialPosition;
            this.m_TargetVelocity = Vector3.zero;
            this.m_LookRig.SetTarget(this.m_LookTarget);
            this.m_IsTargetRegistered = true;
        }

        private void DeactivateLookTarget()
        {
            if (!this.m_IsTargetRegistered) return;

            this.m_LookRig?.RemoveTarget(this.m_LookTarget);
            this.m_IsTargetRegistered = false;
            this.m_TargetVelocity = Vector3.zero;
        }

        private void OnDisable()
        {
            this.DeactivateLookTarget();
            this.m_HasCameraSample = false;
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
