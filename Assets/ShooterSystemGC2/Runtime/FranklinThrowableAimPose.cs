using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Adds the missing mobile grenade charge pose on top of GC2's native center-camera
    /// aim. GC2 still owns projectile direction and the authored throw gesture; this
    /// component only places the humanoid arms/head while the Fire pointer is held.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class FranklinThrowableAimPose : MonoBehaviour
    {
        [Header("Blend")]
        [SerializeField, Min(0.01f)] private float m_EnterSmoothTime = 0.11f;
        [SerializeField, Min(0.01f)] private float m_ExitSmoothTime = 0.14f;

        [Header("One-Hand Throw Hold")]
        [Tooltip("Right throwing hand offset from the Head in Player local axes.")]
        [SerializeField] private Vector3 m_ThrowHandOffset = new(0.28f, 0.08f, -0.07f);
        [Tooltip("Right elbow guide offset from the Head in Player local axes.")]
        [SerializeField] private Vector3 m_RightElbowHintOffset = new(0.43f, -0.1f, -0.18f);
        [SerializeField, Range(0f, 1f)] private float m_RightElbowHintWeight = 0.85f;

        [Header("Center Camera Look")]
        [SerializeField, Range(0f, 1f)] private float m_UpperChestLookWeight = 0.14f;
        [SerializeField, Range(0f, 1f)] private float m_NeckLookWeight = 0.28f;
        [SerializeField, Range(0f, 1f)] private float m_HeadLookWeight = 0.72f;
        [SerializeField, Range(0f, 89f)] private float m_MaxLookPitch = 55f;
        [SerializeField, Range(0f, 120f)] private float m_MaxLookYaw = 70f;
        [SerializeField, Min(1f)] private float m_AimDistance = 12f;

        private Character m_Player;
        private Animator m_Animator;
        private MainCamera m_MainCamera;
        private bool m_Requested;
        private float m_Weight;
        private float m_WeightVelocity;

        private Transform m_Head;
        private Transform m_Neck;
        private Transform m_UpperChest;
        private Transform m_RightUpperArm;
        private Transform m_RightLowerArm;
        private Transform m_RightHand;
        private Transform m_RightElbowHint;

        public void Initialize(Character player)
        {
            if (this.m_Player == player && this.m_Animator == player?.Animim?.Animator)
                return;

            this.m_Requested = false;
            this.m_Weight = 0f;
            this.m_WeightVelocity = 0f;
            this.m_Player = player;
            this.BindAnimator(player?.Animim?.Animator);
        }

        public void SetRequested(bool requested)
        {
            this.m_Requested = requested && this.m_Player != null;
        }

        private void OnValidate()
        {
            this.m_EnterSmoothTime = Mathf.Max(0.01f, this.m_EnterSmoothTime);
            this.m_ExitSmoothTime = Mathf.Max(0.01f, this.m_ExitSmoothTime);
            this.m_MaxLookPitch = Mathf.Clamp(this.m_MaxLookPitch, 0f, 89f);
            this.m_MaxLookYaw = Mathf.Clamp(this.m_MaxLookYaw, 0f, 120f);
            this.m_AimDistance = Mathf.Max(1f, this.m_AimDistance);
            this.m_RightElbowHintWeight = Mathf.Clamp01(this.m_RightElbowHintWeight);
        }

        private void OnDisable()
        {
            this.m_Requested = false;
            this.m_Weight = 0f;
            this.m_WeightVelocity = 0f;
        }

        private void LateUpdate()
        {
            Animator currentAnimator = this.m_Player?.Animim?.Animator;
            if (currentAnimator != this.m_Animator) this.BindAnimator(currentAnimator);

            float targetWeight = this.m_Requested && this.HasRequiredBones() ? 1f : 0f;
            float smoothTime = targetWeight > this.m_Weight
                ? this.m_EnterSmoothTime
                : this.m_ExitSmoothTime;
            this.m_Weight = Mathf.SmoothDamp(
                this.m_Weight,
                targetWeight,
                ref this.m_WeightVelocity,
                smoothTime,
                Mathf.Infinity,
                Mathf.Max(0f, Time.deltaTime)
            );

            if (targetWeight <= 0f && this.m_Weight < 0.001f)
            {
                this.m_Weight = 0f;
                this.m_WeightVelocity = 0f;
                return;
            }

            if (!this.HasRequiredBones()) return;

            // Capture GC2's camera-aligned hand rotation before the custom head/chest look
            // changes any parent bones. The solved arm restores this exact world rotation,
            // so the grenade muzzle and trajectory keep pointing at the center-camera aim.
            Quaternion gc2HandAimRotation = this.m_RightHand.rotation;
            Vector3 aimDirection = this.GetCenterCameraAimDirection();
            this.ApplyLookBone(this.m_UpperChest, aimDirection, this.m_UpperChestLookWeight);
            this.ApplyLookBone(this.m_Neck, aimDirection, this.m_NeckLookWeight);
            this.ApplyLookBone(this.m_Head, aimDirection, this.m_HeadLookWeight);

            float humanScale = Mathf.Max(0.5f, this.m_Animator.humanScale);
            Transform playerTransform = this.m_Player.transform;
            Vector3 throwTarget = this.m_Head.position +
                                  playerTransform.right *
                                  (this.m_ThrowHandOffset.x * humanScale) +
                                  playerTransform.up *
                                  (this.m_ThrowHandOffset.y * humanScale) +
                                  playerTransform.forward *
                                  (this.m_ThrowHandOffset.z * humanScale);

            this.EnsureRightElbowHint();
            this.m_RightElbowHint.position = this.m_Head.position +
                playerTransform.right * (this.m_RightElbowHintOffset.x * humanScale) +
                playerTransform.up * (this.m_RightElbowHintOffset.y * humanScale) +
                playerTransform.forward * (this.m_RightElbowHintOffset.z * humanScale);

            this.SolveArm(
                this.m_RightUpperArm,
                this.m_RightLowerArm,
                this.m_RightHand,
                throwTarget,
                gc2HandAimRotation,
                this.m_RightElbowHint,
                this.m_RightElbowHintWeight,
                this.m_Weight
            );
        }

        private void EnsureRightElbowHint()
        {
            if (this.m_RightElbowHint != null) return;

            GameObject hint = new("Franklin Throwable Right Elbow Hint")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hint.transform.SetParent(this.transform, false);
            this.m_RightElbowHint = hint.transform;
        }

        private Vector3 GetCenterCameraAimDirection()
        {
            this.m_MainCamera ??= ShortcutMainCamera.Get<MainCamera>();
            if (this.m_MainCamera == null) return this.m_Player.transform.forward;

            Transform cameraTransform = this.m_MainCamera.transform;
            Vector3 aimPoint = cameraTransform.position +
                               cameraTransform.forward * this.m_AimDistance;
            Vector3 direction = aimPoint - this.m_Head.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return this.m_Player.transform.forward;

            direction.Normalize();
            Vector3 localDirection = this.m_Player.transform.InverseTransformDirection(direction);
            float horizontal = Mathf.Sqrt(
                localDirection.x * localDirection.x +
                localDirection.z * localDirection.z
            );
            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -this.m_MaxLookYaw, this.m_MaxLookYaw);
            pitch = Mathf.Clamp(pitch, -this.m_MaxLookPitch, this.m_MaxLookPitch);

            return this.m_Player.transform.rotation *
                   (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);
        }

        private void ApplyLookBone(Transform bone, Vector3 direction, float boneWeight)
        {
            if (bone == null || boneWeight <= 0f) return;
            Quaternion source = bone.rotation;
            Quaternion target = Quaternion.FromToRotation(bone.forward, direction) * source;
            bone.rotation = Quaternion.Slerp(
                source,
                target,
                Mathf.Clamp01(this.m_Weight * boneWeight)
            );
        }

        private static void ApplySolvedRotation(
            Transform bone,
            Quaternion target,
            float weight)
        {
            bone.rotation = Quaternion.Slerp(bone.rotation, target, Mathf.Clamp01(weight));
        }

        private void SolveArm(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Transform hint,
            float hintWeight,
            float weight)
        {
            if (upperArm == null || lowerArm == null || hand == null || weight <= 0f)
                return;

            TwoBoneData source = new(upperArm, lowerArm, hand);
            TwoBoneData solved = TwoBoneSolver.Run(
                source,
                targetPosition,
                // Preserve GC2's world-space hand/muzzle aim so the center-camera
                // trajectory remains authoritative. The lower target and rearward elbow
                // guide remove the extreme wrist bend without changing throw direction.
                targetRotation,
                hint,
                hintWeight
            );
            ApplySolvedRotation(upperArm, solved.RootRotation, weight);
            ApplySolvedRotation(lowerArm, solved.BodyRotation, weight);
            // Parent blending changes the hand world rotation even at partial pose weight.
            // Restore GC2's cached aim exactly so releasing during the blend cannot skew
            // the grenade trajectory away from the center-camera target.
            hand.rotation = targetRotation;
        }

        private bool HasRequiredBones()
        {
            return this.m_Animator != null &&
                   this.m_Animator.isHuman &&
                   this.m_Head != null &&
                   this.m_RightUpperArm != null &&
                   this.m_RightLowerArm != null &&
                   this.m_RightHand != null;
        }

        private void BindAnimator(Animator animator)
        {
            this.m_Animator = animator;
            this.m_Head = null;
            this.m_Neck = null;
            this.m_UpperChest = null;
            this.m_RightUpperArm = null;
            this.m_RightLowerArm = null;
            this.m_RightHand = null;
            if (animator == null || !animator.isHuman) return;

            this.m_Head = animator.GetBoneTransform(HumanBodyBones.Head);
            this.m_Neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            this.m_UpperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                                animator.GetBoneTransform(HumanBodyBones.Chest);
            this.m_RightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            this.m_RightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            this.m_RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }
}
