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
    private float reverseLookWeight;
    private Vector3 reverseLookUpperChestEuler;
    private Vector3 reverseLookNeckEuler;
    private Vector3 reverseLookHeadEuler;
    private HumanPoseHandler reverseLookPoseHandler;
    private Animator reverseLookPoseAnimator;
    private HumanPose reverseLookHumanPose;
    private int reverseLookAppliedUpperMuscle = -1;
    private float reverseLookBaseUpperTwist;
    private float reverseLookBaseNeckTurn;
    private float reverseLookBaseHeadTurn;
    private float reverseLookBaseHeadNod;
    private bool reverseLookPoseApplied;
    private float reverseLeftHandReleaseWeight;
    private Transform reverseLeftHandReleaseTarget;
    private Vector3 reverseLeftHandReleaseOffset;
    private Transform reverseLeftUpperLeg;
    private Transform reverseLeftLowerLeg;
    private Transform reverseHips;

    private static bool s_ReverseMusclesResolved;
    private static int s_ChestTwistMuscle = -1;
    private static int s_UpperChestTwistMuscle = -1;
    private static int s_NeckTurnMuscle = -1;
    private static int s_HeadTurnMuscle = -1;
    private static int s_HeadNodMuscle = -1;

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
        RefreshRuntimeActivity();
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
        RefreshRuntimeActivity();
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
        RefreshRuntimeActivity();
    }

    public void ConfigureManualSpineAdjustment(
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        spinePositionOffset = localPositionOffset;
        spineRotationOffset = localRotationOffset;
        RefreshRuntimeActivity();
    }

    /// <summary>
    /// Adds a lightweight seated reverse-look pose after the Animator and torso
    /// adjustment, but before hand IK locks both hands back onto the grips.
    /// </summary>
    public void SetReverseLookPose(
        float weight,
        Vector3 upperChestEuler,
        Vector3 neckEuler,
        Vector3 headEuler)
    {
        float clampedWeight = Mathf.Clamp01(weight);
        if (clampedWeight <= 0.001f && reverseLookPoseApplied)
            RestoreReverseLookHumanPose();
        reverseLookWeight = clampedWeight;
        reverseLookUpperChestEuler = upperChestEuler;
        reverseLookNeckEuler = neckEuler;
        reverseLookHeadEuler = headEuler;
        RefreshRuntimeActivity();
    }

    /// <summary>
    /// Moves the left hand from its normal grip toward an authored release target
    /// without changing the captured base hand-IK state used by Bike Shooter.
    /// </summary>
    public void SetReverseLeftHandRelease(
        float weight,
        Transform target,
        Vector3 localOffset)
    {
        reverseLeftHandReleaseWeight = Mathf.Clamp01(weight);
        reverseLeftHandReleaseTarget = target;
        reverseLeftHandReleaseOffset = localOffset;
        RefreshRuntimeActivity();
    }

    /// <summary>
    /// Immediately releases every vehicle-owned IK target and manual torso offset.
    /// This is used before ragdoll captures the current skeleton pose, so a rider
    /// cannot carry the steering/footpeg pose into death or crash physics.
    /// </summary>
    public void ResetVehiclePose()
    {
        RestoreReverseLookHumanPose();
        SetIKTargets(null, null, 0f, 0f, 0f, 0f);
        SetFootIKTargets(null, null, 0f, 0f);
        beforeHandIK = null;
        spinePositionOffset = Vector3.zero;
        spineRotationOffset = Vector3.zero;
        reverseLookWeight = 0f;
        reverseLookUpperChestEuler = Vector3.zero;
        reverseLookNeckEuler = Vector3.zero;
        reverseLookHeadEuler = Vector3.zero;
        reverseLeftHandReleaseWeight = 0f;
        reverseLeftHandReleaseTarget = null;
        reverseLeftHandReleaseOffset = Vector3.zero;

        if (cachedSpine != null)
        {
            cachedSpine.localPosition = baseSpineLocalPosition;
        }

        ReleaseReverseLookPoseHandler();
        RefreshRuntimeActivity();
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
        RefreshRuntimeActivity();
    }

    private void RefreshRuntimeActivity()
    {
        if (!Application.isPlaying) return;

        bool hasHandIk = (leftTarget != null && leftWeight > 0.001f) ||
                         (rightTarget != null && rightWeight > 0.001f);
        bool hasFootIk = (leftFootTarget != null && leftFootWeight > 0.001f) ||
                         (rightFootTarget != null && rightFootWeight > 0.001f);
        bool hasSpineAdjustment = spinePositionOffset.sqrMagnitude > 0.000001f ||
                                  spineRotationOffset.sqrMagnitude > 0.000001f;
        bool hasReverseLook = reverseLookWeight > 0.001f;
        bool hasReverseLeftHandRelease =
            reverseLeftHandReleaseWeight > 0.001f;
        bool shouldRun = beforeHandIK != null || hasHandIk || hasFootIk ||
                         hasSpineAdjustment || hasReverseLook ||
                         hasReverseLeftHandRelease;
        if (enabled != shouldRun) enabled = shouldRun;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        EnsureSetup();
        if (animator == null) return;

        // Torso authoring and manual Spine offsets are evaluated before limb IK.
        // Keeping this order prevents the live pose preview from overwriting
        // the solved hand and foot transforms later in the frame.
        beforeHandIK?.Invoke();
        ApplyReverseLookPose();
        ApplyManualSpineAdjustment();

        // Left Hand IK
        Vector3 releasePosition = default;
        bool hasReverseReleasePose =
            reverseLeftHandReleaseWeight > 0.001f &&
            TryGetReverseLeftHandReleasePosition(out releasePosition);
        if (leftTarget != null || hasReverseReleasePose)
        {
            float releaseWeight = hasReverseReleasePose
                ? reverseLeftHandReleaseWeight
                : 0f;
            Vector3 gripPosition = leftTarget != null
                ? leftTarget.position
                : leftHandBone != null
                    ? leftHandBone.position
                    : releasePosition;
            if (!hasReverseReleasePose) releasePosition = gripPosition;
            float positionWeight = Mathf.Lerp(
                leftWeight,
                1f,
                releaseWeight
            );
            animator.SetIKPositionWeight(
                AvatarIKGoal.LeftHand,
                positionWeight
            );
            animator.SetIKRotationWeight(
                AvatarIKGoal.LeftHand,
                leftRotationWeight * (1f - releaseWeight)
            );
            animator.SetIKPosition(
                AvatarIKGoal.LeftHand,
                Vector3.Lerp(gripPosition, releasePosition, releaseWeight)
            );
            if (leftTarget != null)
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

    private bool TryGetReverseLeftHandReleasePosition(out Vector3 position)
    {
        if (reverseLeftHandReleaseTarget != null)
        {
            position = reverseLeftHandReleaseTarget.TransformPoint(
                reverseLeftHandReleaseOffset
            );
            return true;
        }

        if (animator == null || !animator.isHuman)
        {
            position = default;
            return false;
        }

        reverseLeftUpperLeg ??=
            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        reverseLeftLowerLeg ??=
            animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        reverseHips ??= animator.GetBoneTransform(HumanBodyBones.Hips);

        Vector3 riderOffset = new(-0.04f, 0.03f, 0.08f);
        riderOffset += reverseLeftHandReleaseOffset;
        if (reverseLeftUpperLeg != null && reverseLeftLowerLeg != null)
        {
            Vector3 upperThigh = Vector3.Lerp(
                reverseLeftUpperLeg.position,
                reverseLeftLowerLeg.position,
                0.3f
            );
            position = upperThigh + animator.transform.TransformVector(riderOffset);
            return true;
        }

        if (reverseHips != null)
        {
            position = reverseHips.position + animator.transform.TransformVector(
                new Vector3(-0.18f, -0.1f, 0.08f) +
                reverseLeftHandReleaseOffset
            );
            return true;
        }

        position = default;
        return false;
    }

    private void ApplyReverseLookPose()
    {
        if (reverseLookWeight <= 0.001f || !EnsureReverseLookPoseHandler())
            return;

        try
        {
            reverseLookPoseHandler.GetHumanPose(ref reverseLookHumanPose);
            int upperMuscle = s_UpperChestTwistMuscle >= 0
                ? s_UpperChestTwistMuscle
                : s_ChestTwistMuscle;
            reverseLookAppliedUpperMuscle = upperMuscle;
            reverseLookBaseUpperTwist = GetMuscle(upperMuscle);
            reverseLookBaseNeckTurn = GetMuscle(s_NeckTurnMuscle);
            reverseLookBaseHeadTurn = GetMuscle(s_HeadTurnMuscle);
            reverseLookBaseHeadNod = GetMuscle(s_HeadNodMuscle);

            AddMuscleDegrees(
                upperMuscle,
                reverseLookUpperChestEuler.y * reverseLookWeight
            );
            AddMuscleDegrees(
                s_NeckTurnMuscle,
                reverseLookNeckEuler.y * reverseLookWeight
            );
            AddMuscleDegrees(
                s_HeadTurnMuscle,
                reverseLookHeadEuler.y * reverseLookWeight
            );
            SetMuscleDegreesTarget(
                s_HeadNodMuscle,
                reverseLookHeadEuler.x,
                reverseLookWeight
            );
            reverseLookPoseHandler.SetHumanPose(ref reverseLookHumanPose);
            reverseLookPoseApplied = true;
        }
        catch (Exception)
        {
            ReleaseReverseLookPoseHandler();
        }
    }

    private bool EnsureReverseLookPoseHandler()
    {
        if (animator == null || !animator.isHuman || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            return false;
        }

        ResolveReverseLookMuscles();
        if (reverseLookPoseHandler != null && reverseLookPoseAnimator == animator)
            return true;

        ReleaseReverseLookPoseHandler();
        reverseLookPoseAnimator = animator;
        reverseLookPoseHandler = new HumanPoseHandler(
            animator.avatar,
            animator.transform
        );
        return true;
    }

    private void RestoreReverseLookHumanPose()
    {
        if (!reverseLookPoseApplied || reverseLookPoseHandler == null) return;
        try
        {
            reverseLookPoseHandler.GetHumanPose(ref reverseLookHumanPose);
            SetMuscle(reverseLookAppliedUpperMuscle, reverseLookBaseUpperTwist);
            SetMuscle(s_NeckTurnMuscle, reverseLookBaseNeckTurn);
            SetMuscle(s_HeadTurnMuscle, reverseLookBaseHeadTurn);
            SetMuscle(s_HeadNodMuscle, reverseLookBaseHeadNod);
            reverseLookPoseHandler.SetHumanPose(ref reverseLookHumanPose);
        }
        catch (Exception)
        {
            ReleaseReverseLookPoseHandler();
            return;
        }

        reverseLookPoseApplied = false;
    }

    private float GetMuscle(int muscleIndex)
    {
        return muscleIndex >= 0 && reverseLookHumanPose.muscles != null &&
               muscleIndex < reverseLookHumanPose.muscles.Length
            ? reverseLookHumanPose.muscles[muscleIndex]
            : 0f;
    }

    private void SetMuscle(int muscleIndex, float value)
    {
        if (muscleIndex < 0 || reverseLookHumanPose.muscles == null ||
            muscleIndex >= reverseLookHumanPose.muscles.Length)
        {
            return;
        }

        reverseLookHumanPose.muscles[muscleIndex] = Mathf.Clamp(value, -1f, 1f);
    }

    private void AddMuscleDegrees(int muscleIndex, float degrees)
    {
        if (muscleIndex < 0) return;
        float degreeRange = degrees >= 0f
            ? HumanTrait.GetMuscleDefaultMax(muscleIndex)
            : -HumanTrait.GetMuscleDefaultMin(muscleIndex);
        if (degreeRange <= 0.0001f) return;
        SetMuscle(
            muscleIndex,
            GetMuscle(muscleIndex) + degrees / degreeRange
        );
    }

    private void SetMuscleDegreesTarget(
        int muscleIndex,
        float targetDegrees,
        float weight)
    {
        if (muscleIndex < 0) return;
        float degreeRange = targetDegrees >= 0f
            ? HumanTrait.GetMuscleDefaultMax(muscleIndex)
            : -HumanTrait.GetMuscleDefaultMin(muscleIndex);
        if (degreeRange <= 0.0001f) return;
        float targetMuscle = Mathf.Clamp(targetDegrees / degreeRange, -1f, 1f);
        SetMuscle(
            muscleIndex,
            Mathf.Lerp(GetMuscle(muscleIndex), targetMuscle, Mathf.Clamp01(weight))
        );
    }

    private static void ResolveReverseLookMuscles()
    {
        if (s_ReverseMusclesResolved) return;
        s_ReverseMusclesResolved = true;
        s_ChestTwistMuscle = FindMuscle("Chest Twist Left-Right");
        s_UpperChestTwistMuscle = FindMuscle("UpperChest Twist Left-Right");
        s_NeckTurnMuscle = FindMuscle("Neck Turn Left-Right");
        s_HeadTurnMuscle = FindMuscle("Head Turn Left-Right");
        s_HeadNodMuscle = FindMuscle("Head Nod Down-Up");
    }

    private static int FindMuscle(string muscleName)
    {
        string[] names = HumanTrait.MuscleName;
        for (int index = 0; index < names.Length; index++)
        {
            if (names[index] == muscleName) return index;
        }

        return -1;
    }

    private void ReleaseReverseLookPoseHandler()
    {
        reverseLookPoseHandler?.Dispose();
        reverseLookPoseHandler = null;
        reverseLookPoseAnimator = null;
        reverseLookHumanPose = default;
        reverseLookAppliedUpperMuscle = -1;
        reverseLookPoseApplied = false;
    }

    private void OnDestroy()
    {
        ResetVehiclePose();
        ReleaseReverseLookPoseHandler();
    }

}
