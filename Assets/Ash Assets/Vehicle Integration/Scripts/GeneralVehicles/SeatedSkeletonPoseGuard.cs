using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Holds the final seated humanoid pose across the one-frame GC2 gesture-to-state
    /// handoff. It disables itself after release, so there is no steady-state cost.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    [DisallowMultipleComponent]
    public sealed class SeatedSkeletonPoseGuard : MonoBehaviour
    {
        private static readonly int BONE_COUNT = (int)HumanBodyBones.LastBone;

        private readonly Transform[] m_Bones = new Transform[BONE_COUNT];
        private readonly Vector3[] m_LocalPositions = new Vector3[BONE_COUNT];
        private readonly Quaternion[] m_LocalRotations = new Quaternion[BONE_COUNT];

        private Animator m_Animator;
        private float m_ReleaseAt = float.PositiveInfinity;
        private bool m_IsHolding;
        private bool m_LeftArmFree;
        private bool m_RightArmFree;
        private Transform m_PushLookTarget;
        private float m_PushPoseWeight;
        private float m_PushTorsoYaw;
        private float m_PushTorsoLean;
        private float m_PushHeadLookWeight;
        private Transform m_PushLeftHandTarget;
        private Transform m_PushRightHandTarget;
        private float m_PushLeftHandWeight;
        private float m_PushRightHandWeight;
        private Transform m_SeatedReachTarget;
        private bool m_SeatedReachLeft;
        private float m_SeatedReachStartedAt;
        private float m_SeatedReachDuration;
        private AnimationCurve m_SeatedReachCurve;
        private float m_SeatedReachMaxWeight;

        public static SeatedSkeletonPoseGuard Prepare(Character character)
        {
            Animator animator = character?.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return null;

            SeatedSkeletonPoseGuard guard =
                animator.GetComponent<SeatedSkeletonPoseGuard>();
            if (guard == null) guard = animator.gameObject.AddComponent<SeatedSkeletonPoseGuard>();
            guard.m_Animator = animator;
            guard.Cancel();
            return guard;
        }

        public void Capture()
        {
            if (this.m_Animator == null || !this.m_Animator.isHuman) return;

            for (int i = 0; i < BONE_COUNT; ++i)
            {
                Transform bone = this.m_Animator.GetBoneTransform((HumanBodyBones)i);
                this.m_Bones[i] = bone;
                if (bone == null) continue;

                this.m_LocalPositions[i] = bone.localPosition;
                this.m_LocalRotations[i] = bone.localRotation;
            }

            this.m_ReleaseAt = float.PositiveInfinity;
            this.m_IsHolding = true;
            this.m_LeftArmFree = false;
            this.m_RightArmFree = false;
            this.ClearPassengerPushPose();
            this.ClearSeatedDoorReach();
            this.enabled = true;
        }

        /// <summary>
        /// Samples an exact Humanoid source frame and holds its bone pose while
        /// preserving the model-root offset. Used to hand a deterministic final
        /// launch pose to ragdoll without exposing the underlying Driving state.
        /// </summary>
        public void CaptureAnimationPose(AnimationClip clip, float sampleTime)
        {
            if (this.m_Animator == null || !this.m_Animator.isHuman || clip == null)
                return;

            Transform model = this.m_Animator.transform;
            Vector3 modelLocalPosition = model.localPosition;
            Quaternion modelLocalRotation = model.localRotation;
            Vector3 modelLocalScale = model.localScale;

            clip.SampleAnimation(
                this.m_Animator.gameObject,
                Mathf.Clamp(sampleTime, 0f, clip.length)
            );
            model.SetLocalPositionAndRotation(
                modelLocalPosition,
                modelLocalRotation
            );
            model.localScale = modelLocalScale;
            this.Capture();
        }

        public void SetLeftArmFree(bool free)
        {
            this.m_LeftArmFree = free;
        }

        public void SetRightArmFree(bool free)
        {
            this.m_RightArmFree = free;
        }

        /// <summary>
        /// Keeps the pelvis and legs on the captured passenger-seat pose while
        /// allowing a small, additive upper-body turn toward the driver. This is
        /// evaluated immediately before Humanoid hand IK so both arms solve from
        /// the leaned torso instead of being dragged after IK has already run.
        /// </summary>
        public void ConfigurePassengerPushPose(
            Transform lookTarget,
            float weight,
            float maxTorsoYaw,
            float maxTorsoLean,
            float headLookWeight)
        {
            this.m_PushLookTarget = lookTarget;
            this.m_PushPoseWeight = Mathf.Clamp01(weight);
            this.m_PushTorsoYaw = Mathf.Max(0f, maxTorsoYaw);
            this.m_PushTorsoLean = Mathf.Max(0f, maxTorsoLean);
            this.m_PushHeadLookWeight = Mathf.Clamp01(headLookWeight);
        }

        public void ClearPassengerPushPose()
        {
            this.m_PushLookTarget = null;
            this.m_PushPoseWeight = 0f;
            this.m_PushTorsoYaw = 0f;
            this.m_PushTorsoLean = 0f;
            this.m_PushHeadLookWeight = 0f;
            this.m_PushLeftHandTarget = null;
            this.m_PushRightHandTarget = null;
            this.m_PushLeftHandWeight = 0f;
            this.m_PushRightHandWeight = 0f;
        }

        public void ConfigurePassengerPushHands(
            Transform leftTarget,
            Transform rightTarget,
            float leftWeight,
            float rightWeight)
        {
            this.m_PushLeftHandTarget = leftTarget;
            this.m_PushRightHandTarget = rightTarget;
            this.m_PushLeftHandWeight = Mathf.Clamp01(leftWeight);
            this.m_PushRightHandWeight = Mathf.Clamp01(rightWeight);
        }

        /// <summary>
        /// Solves only the door-side arm from the captured driving pose. This
        /// does not depend on Animator IK Pass, so the pelvis and legs cannot
        /// inherit an upright frame while opening the door for a bailout.
        /// </summary>
        public void ConfigureSeatedDoorReach(
            Transform target,
            bool leftHand,
            float duration,
            AnimationCurve curve,
            float maxWeight)
        {
            this.m_SeatedReachTarget = target;
            this.m_SeatedReachLeft = leftHand;
            this.m_SeatedReachStartedAt = Time.time;
            this.m_SeatedReachDuration = Mathf.Max(0.01f, duration);
            this.m_SeatedReachCurve = curve;
            this.m_SeatedReachMaxWeight = Mathf.Clamp01(maxWeight);
        }

        public void ClearSeatedDoorReach()
        {
            this.m_SeatedReachTarget = null;
            this.m_SeatedReachLeft = false;
            this.m_SeatedReachStartedAt = 0f;
            this.m_SeatedReachDuration = 0f;
            this.m_SeatedReachCurve = null;
            this.m_SeatedReachMaxWeight = 0f;
        }

        public void ApplyPassengerPushPoseBeforeIK()
        {
            if (!this.m_IsHolding || this.m_Animator == null ||
                this.m_PushLookTarget == null || this.m_PushPoseWeight <= 0f)
            {
                return;
            }

            Transform hips = this.GetBone(HumanBodyBones.Hips);
            Transform head = this.GetBone(HumanBodyBones.Head);
            Vector3 origin = head != null
                ? head.position
                : hips != null
                    ? hips.position
                    : this.m_Animator.transform.position;
            Vector3 direction = this.m_PushLookTarget.position - origin;
            Vector3 planar = Vector3.ProjectOnPlane(
                direction,
                this.m_Animator.transform.up
            );
            if (planar.sqrMagnitude < 0.0001f) return;

            Vector3 localDirection = this.m_Animator.transform.InverseTransformDirection(
                planar.normalized
            );
            float signedYaw = Mathf.Atan2(localDirection.x, localDirection.z) *
                Mathf.Rad2Deg;
            float yaw = Mathf.Clamp(
                signedYaw,
                -this.m_PushTorsoYaw,
                this.m_PushTorsoYaw
            ) * this.m_PushPoseWeight;
            float roll = -Mathf.Sign(yaw) * this.m_PushTorsoLean *
                this.m_PushPoseWeight;
            float forwardLean = this.m_PushTorsoLean * 0.65f *
                this.m_PushPoseWeight;

            this.ApplyAdditiveLocalRotation(
                HumanBodyBones.Spine,
                new Vector3(forwardLean * 0.2f, yaw * 0.2f, roll * 0.2f)
            );
            this.ApplyAdditiveLocalRotation(
                HumanBodyBones.Chest,
                new Vector3(forwardLean * 0.4f, yaw * 0.4f, roll * 0.4f)
            );
            this.ApplyAdditiveLocalRotation(
                HumanBodyBones.UpperChest,
                new Vector3(forwardLean * 0.4f, yaw * 0.4f, roll * 0.4f)
            );
            Transform spine = this.GetBone(HumanBodyBones.Spine);
            if (spine != null && direction.sqrMagnitude > 0.0001f)
            {
                // A small upper-body translation makes the push read as body
                // weight over the wheel instead of only a waist rotation.
                float reachOffset = this.m_PushTorsoLean * 0.004f *
                    this.m_PushPoseWeight;
                spine.position += direction.normalized * reachOffset;
            }
            this.RestoreLocalPose(HumanBodyBones.Neck);

            head = this.GetBone(HumanBodyBones.Head);
            if (head == null || direction.sqrMagnitude < 0.0001f) return;
            Vector3 lookDirection = (this.m_PushLookTarget.position - head.position)
                .normalized;
            Quaternion lookDelta = Quaternion.FromToRotation(
                head.forward,
                lookDirection
            );
            Quaternion desiredHead = lookDelta * head.rotation;
            desiredHead = Quaternion.RotateTowards(
                head.rotation,
                desiredHead,
                55f
            );
            head.rotation = Quaternion.Slerp(
                head.rotation,
                desiredHead,
                this.m_PushPoseWeight * this.m_PushHeadLookWeight
            );
        }

        public void ReleaseAfter(float seconds)
        {
            if (!this.m_IsHolding) return;
            this.m_ReleaseAt = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        public void Cancel()
        {
            this.m_IsHolding = false;
            this.m_ReleaseAt = float.PositiveInfinity;
            this.m_LeftArmFree = false;
            this.m_RightArmFree = false;
            this.ClearPassengerPushPose();
            this.ClearSeatedDoorReach();
            this.enabled = false;
        }

        private void LateUpdate()
        {
            if (!this.m_IsHolding || Time.unscaledTime >= this.m_ReleaseAt)
            {
                this.Cancel();
                return;
            }

            bool proceduralPush = this.m_PushLookTarget != null;
            bool seatedDoorReach = this.m_SeatedReachTarget != null;
            for (int i = 0; i < BONE_COUNT; ++i)
            {
                if (!proceduralPush && !seatedDoorReach && this.m_LeftArmFree &&
                    IsLeftArmBone((HumanBodyBones)i)) continue;
                if (!proceduralPush && !seatedDoorReach && this.m_RightArmFree &&
                    IsRightArmBone((HumanBodyBones)i)) continue;
                Transform bone = this.m_Bones[i];
                if (bone == null) continue;
                bone.SetLocalPositionAndRotation(
                    this.m_LocalPositions[i],
                    this.m_LocalRotations[i]
                );
            }

            if (seatedDoorReach && !proceduralPush)
            {
                float normalized = Mathf.Clamp01(
                    (Time.time - this.m_SeatedReachStartedAt) /
                    this.m_SeatedReachDuration
                );
                float curveWeight = this.m_SeatedReachCurve != null
                    ? this.m_SeatedReachCurve.Evaluate(normalized)
                    : 1f;
                this.SolveArmIK(
                    this.m_SeatedReachLeft,
                    this.m_SeatedReachTarget,
                    Mathf.Clamp01(curveWeight * this.m_SeatedReachMaxWeight)
                );
            }

            if (!proceduralPush) return;

            // GC2 does not guarantee Animator IK Pass on every transient graph
            // layer. Apply the body first and solve both arms here so this pose is
            // deterministic regardless of the active Animator Controller layers.
            this.ApplyPassengerPushPoseBeforeIK();
            this.SolveArmIK(
                true,
                this.m_PushLeftHandTarget,
                this.m_PushLeftHandWeight
            );
            this.SolveArmIK(
                false,
                this.m_PushRightHandTarget,
                this.m_PushRightHandWeight
            );
        }

        private static bool IsLeftArmBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftShoulder ||
                bone == HumanBodyBones.LeftUpperArm ||
                bone == HumanBodyBones.LeftLowerArm ||
                bone == HumanBodyBones.LeftHand;
        }

        private static bool IsRightArmBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.RightShoulder ||
                bone == HumanBodyBones.RightUpperArm ||
                bone == HumanBodyBones.RightLowerArm ||
                bone == HumanBodyBones.RightHand;
        }

        private Transform GetBone(HumanBodyBones bone)
        {
            int index = (int)bone;
            return index >= 0 && index < BONE_COUNT ? this.m_Bones[index] : null;
        }

        private void ApplyAdditiveLocalRotation(
            HumanBodyBones bone,
            Vector3 euler)
        {
            int index = (int)bone;
            if (index < 0 || index >= BONE_COUNT) return;
            Transform transformBone = this.m_Bones[index];
            if (transformBone == null) return;
            transformBone.localPosition = this.m_LocalPositions[index];
            transformBone.localRotation = this.m_LocalRotations[index] *
                Quaternion.Euler(euler);
        }

        private void RestoreLocalPose(HumanBodyBones bone)
        {
            int index = (int)bone;
            if (index < 0 || index >= BONE_COUNT) return;
            Transform transformBone = this.m_Bones[index];
            if (transformBone == null) return;
            transformBone.SetLocalPositionAndRotation(
                this.m_LocalPositions[index],
                this.m_LocalRotations[index]
            );
        }

        private void SolveArmIK(
            bool left,
            Transform target,
            float weight)
        {
            if (target == null || weight <= 0f) return;

            Transform upperArm = this.GetBone(
                left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm
            );
            Transform lowerArm = this.GetBone(
                left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm
            );
            Transform hand = this.GetBone(
                left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
            );
            if (upperArm == null || lowerArm == null || hand == null) return;

            Vector3 desiredPosition = Vector3.Lerp(
                hand.position,
                target.position,
                weight
            );
            // Two compact CCD passes are enough for a Humanoid arm and avoid the
            // allocations/dependencies of an Animation Rigging graph on mobile.
            for (int iteration = 0; iteration < 2; ++iteration)
            {
                RotateJointTowardTarget(lowerArm, hand, desiredPosition);
                RotateJointTowardTarget(upperArm, hand, desiredPosition);
            }

            hand.rotation = Quaternion.Slerp(
                hand.rotation,
                target.rotation,
                weight
            );
        }

        private static void RotateJointTowardTarget(
            Transform joint,
            Transform end,
            Vector3 target)
        {
            Vector3 toEnd = end.position - joint.position;
            Vector3 toTarget = target - joint.position;
            if (toEnd.sqrMagnitude < 0.000001f ||
                toTarget.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Quaternion correction = Quaternion.FromToRotation(toEnd, toTarget);
            joint.rotation = correction * joint.rotation;
        }
    }
}
