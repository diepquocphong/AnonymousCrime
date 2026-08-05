using UnityEngine;

public class CharacterIKSetter : MonoBehaviour
{
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
        animator = GetComponent<Animator>();
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
        this.leftTarget = leftTarget;
        this.rightTarget = rightTarget;
        this.leftWeight = leftIKWeight;
        this.rightWeight = rightIKWeight;
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
        if (animator == null) return;

        // Left Hand IK
        if (leftTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftWeight);
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
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightWeight);
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
}
