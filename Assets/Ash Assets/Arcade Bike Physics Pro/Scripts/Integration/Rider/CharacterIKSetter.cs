using System;
using UnityEngine;

public class CharacterIKSetter : MonoBehaviour
{
    public readonly struct HandIKState
    {
        public Transform LeftTarget { get; }
        public Transform RightTarget { get; }
        public float LeftWeight { get; }
        public float RightWeight { get; }
        public float LeftRotationWeight { get; }
        public float RightRotationWeight { get; }

        public HandIKState(
            Transform leftTarget,
            Transform rightTarget,
            float leftWeight,
            float rightWeight,
            float leftRotationWeight,
            float rightRotationWeight)
        {
            LeftTarget = leftTarget;
            RightTarget = rightTarget;
            LeftWeight = leftWeight;
            RightWeight = rightWeight;
            LeftRotationWeight = leftRotationWeight;
            RightRotationWeight = rightRotationWeight;
        }
    }

    private Animator animator;

    [Header("Hand IK Target References")]
    [Tooltip("The target transform for the left hand (e.g., a position on the steering wheel).")]
    public Transform leftTarget;
    [Tooltip("The target transform for the right hand (e.g., a position on the steering wheel).")]
    public Transform rightTarget;
    [Tooltip("IK weight for the left hand.")]
    public float leftWeight = 1f;
    [Tooltip("IK weight for the right hand.")]
    public float rightWeight = 1f;

    private float leftRotationWeight = 1f;
    private float rightRotationWeight = 1f;

    private Action beforeHandIK;
    private Vector3 spinePositionOffset;
    private Vector3 spineRotationOffset;
    private Transform cachedSpine;
    private Vector3 baseSpineLocalPosition;

    [Header("Foot IK Target References")]
    [Tooltip("The target transform for the left foot (e.g., a position on the bike's footpeg).")]
    public Transform leftFootTarget;
    [Tooltip("The target transform for the right foot (e.g., a position on the bike's footpeg).")]
    public Transform rightFootTarget;
    [Tooltip("IK weight for the left foot.")]
    public float leftFootWeight = 1f;
    [Tooltip("IK weight for the right foot.")]
    public float rightFootWeight = 1f;

    [Header("Bone References (Optional)")]
    [Tooltip("Reference to the left hand bone. Automatically assigned if not set.")]
    public Transform leftHandBone;
    [Tooltip("Reference to the right hand bone. Automatically assigned if not set.")]
    public Transform rightHandBone;


    private void Awake()
    {
        EnsureSetup();
    }

    private void EnsureSetup()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            if (leftHandBone == null)
            {
                leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            if (rightHandBone == null)
            {
                rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

        }
    }

    /// <summary>
    /// Sets the IK targets for the left and right hands.
    /// </summary>
    public void SetIKTargets(Transform leftTarget, Transform rightTarget, float leftIKWeight, float rightIKWeight)
    {
        SetIKTargets(
            leftTarget,
            rightTarget,
            leftIKWeight,
            rightIKWeight,
            leftIKWeight,
            rightIKWeight
        );
    }

    public void SetIKTargets(
        Transform leftTarget,
        Transform rightTarget,
        float leftIKWeight,
        float rightIKWeight,
        float leftIKRotationWeight,
        float rightIKRotationWeight
    )
    {
        this.leftTarget = leftTarget;
        this.rightTarget = rightTarget;
        this.leftWeight = leftIKWeight;
        this.rightWeight = rightIKWeight;
        this.leftRotationWeight = leftIKRotationWeight;
        this.rightRotationWeight = rightIKRotationWeight;
    }

    public HandIKState CaptureHandIKState()
    {
        return new HandIKState(
            leftTarget,
            rightTarget,
            leftWeight,
            rightWeight,
            leftRotationWeight,
            rightRotationWeight
        );
    }

    public void RestoreHandIKState(HandIKState state)
    {
        SetIKTargets(
            state.LeftTarget,
            state.RightTarget,
            state.LeftWeight,
            state.RightWeight,
            state.LeftRotationWeight,
            state.RightRotationWeight
        );
    }

    public void SetBeforeHandIK(Action callback)
    {
        beforeHandIK = callback;
    }

    public void ConfigureManualSpineAdjustment(
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        spinePositionOffset = localPositionOffset;
        spineRotationOffset = localRotationOffset;
    }

    /// <summary>
    /// Immediately releases every vehicle-owned IK target and manual torso offset.
    /// This is used before ragdoll captures the current skeleton pose, so a rider
    /// cannot carry the steering/footpeg pose into death or crash physics.
    /// </summary>
    public void ResetVehiclePose()
    {
        SetIKTargets(null, null, 0f, 0f, 0f, 0f);
        SetFootIKTargets(null, null, 0f, 0f);
        beforeHandIK = null;
        spinePositionOffset = Vector3.zero;
        spineRotationOffset = Vector3.zero;

        if (cachedSpine != null)
        {
            cachedSpine.localPosition = baseSpineLocalPosition;
        }
    }

    /// <summary>
    /// Sets the IK targets for the left and right feet.
    /// </summary>
    public void SetFootIKTargets(Transform leftFoot, Transform rightFoot, float leftFootIKWeight, float rightFootIKWeight)
    {
        this.leftFootTarget = leftFoot;
        this.rightFootTarget = rightFoot;
        this.leftFootWeight = leftFootIKWeight;
        this.rightFootWeight = rightFootIKWeight;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        EnsureSetup();
        if (animator == null) return;

        // Torso authoring and manual Spine offsets are evaluated before limb IK.
        // Keeping this order prevents the live pose preview from overwriting
        // the solved hand and foot transforms later in the frame.
        beforeHandIK?.Invoke();
        ApplyManualSpineAdjustment();

        // Left Hand IK
        if (leftTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftRotationWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftTarget.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
        }

        // Right Hand IK
        if (rightTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightRotationWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightTarget.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
        }

        // Left Foot IK
        if (leftFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootTarget.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0);
        }

        // Right Foot IK
        if (rightFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootTarget.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0);
        }
    }

    private void ApplyManualSpineAdjustment()
    {
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (spine == null) return;
        if (cachedSpine != spine)
        {
            cachedSpine = spine;
            baseSpineLocalPosition = spine.localPosition;
        }

        spine.localPosition = baseSpineLocalPosition + spinePositionOffset;
        animator.SetBoneLocalRotation(
            HumanBodyBones.Spine,
            spine.localRotation * Quaternion.Euler(spineRotationOffset)
        );
    }

    private void OnDestroy()
    {
        ResetVehiclePose();
    }

}
