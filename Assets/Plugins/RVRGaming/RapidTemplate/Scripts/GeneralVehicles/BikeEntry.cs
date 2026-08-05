using UnityEngine;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using System.Reflection;
using System.Collections;

[AddComponentMenu("Game Creator/Mechanics/BikeEntry")]
public class BikeEntry : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Animation clip to play when entering the bike.")]
    public AnimationClip entryAnimation;
    [Tooltip("Animation clip to play when exiting the bike.")]
    public AnimationClip exitAnimation;
    [Tooltip("Avatar mask to use for the entry/exit animations.")]
    public AvatarMask animationMask;

    [Header("Animation Transitions")]
    [Tooltip("Time for the entry animation to ease in.")]
    public float entryAnimationTransitionIn = 0.1f;
    [Tooltip("Time for the entry animation to ease out.")]
    public float entryAnimationTransitionOut = 0.25f;
    [Tooltip("Time for the exit animation to ease in.")]
    public float exitAnimationTransitionIn = 0.1f;
    [Tooltip("Time for the exit animation to ease out.")]
    public float exitAnimationTransitionOut = 0.25f;

    [Tooltip("If true, the animation uses root motion.")]
    public bool useRootMotion = true;

    [Header("Driving State")]
    [Tooltip("State that represents the driving mode.")]
    public StateData drivingState = new StateData(StateData.StateType.State);
    [Tooltip("Layer for the driving state.")]
    public int drivingStateLayer = 0;
    [Tooltip("Transition time when entering driving state.")]
    public float drivingStateTransitionIn = 0.1f;
    [Tooltip("Transition time when exiting driving state.")]
    public float drivingStateTransitionOut = 0.25f;

    [Header("Entry Parent Settings")]
    [Tooltip("A fixed transform defining the seat position. It should be a child of the bike's visual mesh so it rotates with the bike.")]
    public Transform entryParent;

    [Header("IK Settings")]
    [Tooltip("Target transform for the left hand on the bike's handlebar.")]
    public Transform steeringWheelLeftHandTarget;
    [Tooltip("Target transform for the right hand on the bike's handlebar.")]
    public Transform steeringWheelRightHandTarget;
    [Tooltip("IK weight for the left hand.")]
    public float leftHandIKWeight = 1f;
    [Tooltip("IK weight for the right hand.")]
    public float rightHandIKWeight = 1f;
    [Tooltip("Where the left foot should go when planted on the ground.")]
    public Transform groundLeftFootTarget;
    [Tooltip("How long, in seconds, to move the left foot from ground back to peg.")]
    public float footSmoothingTime = 0.5f;
    [Tooltip("Target transform for the left foot on the bike's footpeg.")]
    public Transform leftFootTarget;
    [Tooltip("Target transform for the right foot on the bike's footpeg.")]
    public Transform rightFootTarget;
    [Tooltip("IK weight for the left foot.")]
    public float leftFootIKWeight = 1f;
    [Tooltip("IK weight for the right foot.")]
    public float rightFootIKWeight = 1f;

    [Header("On Enter Instructions")]
    [Space]
    [Tooltip("Instructions to run when the character finishes entering.")]
    [SerializeField] public InstructionList onEnter = new InstructionList();
    [Header("On Exit Instructions")]
    [Space]
    [Tooltip("Instructions to run when the character finishes exiting.")]
    [SerializeField] public InstructionList onExit = new InstructionList();

    private PhysicsBikeController bikeController;
    private HoverVehicleController hoverController;
    private bool isEntering = false;
    private bool isExiting = false;
    private Vector3 _origLeftFootLocalPos;
    private Quaternion _origLeftFootLocalRot;
    private bool _inBike = false;
    private bool _footOnGround = false;
    private Coroutine _footRoutine;
    private Character _mountedCharacter = null;

    private void Awake()
    {
        bikeController = GetComponent<PhysicsBikeController>();
        hoverController = GetComponent<HoverVehicleController>();

        if (entryParent != null
         && bikeController != null
         && bikeController.bikeBody != null)
        {
            entryParent.SetParent(bikeController.bikeBody, true);
        }

        if (bikeController != null
         && bikeController.bikeBody != null
         && leftFootTarget != null)
        {
            _origLeftFootLocalPos = leftFootTarget.localPosition;
            _origLeftFootLocalRot = leftFootTarget.localRotation;
        }
    }


    private void Update()
    {
        if (!_inBike || bikeController == null) return;

        float speed = bikeController.GetComponent<Rigidbody>().linearVelocity.magnitude;
        bool shouldBeGrounded = speed < 0.01f;

        if (shouldBeGrounded != _footOnGround)
        {
            _footOnGround = shouldBeGrounded;

            if (_footRoutine != null) StopCoroutine(_footRoutine);

            Vector3 targetPos;
            Quaternion targetRot;

            if (_footOnGround && groundLeftFootTarget != null)
            {
                targetPos = bikeController.bikeBody.InverseTransformPoint(
                    groundLeftFootTarget.position
                );
                targetRot = Quaternion.Inverse(bikeController.bikeBody.rotation)
                          * groundLeftFootTarget.rotation;
            }
            else
            {
                targetPos = _origLeftFootLocalPos;
                targetRot = _origLeftFootLocalRot;
            }

            _footRoutine = StartCoroutine(
                BlendFootTo(leftFootTarget, targetPos, targetRot, footSmoothingTime)
            );
        }
    }

    private IEnumerator BlendFootTo(Transform foot, Vector3 toLocalPos,
                                     Quaternion toLocalRot, float duration)
    {
        float elapsed = 0f;
        Vector3 fromPos = foot.localPosition;
        Quaternion fromRot = foot.localRotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            foot.localPosition = Vector3.Lerp(fromPos, toLocalPos, t);
            foot.localRotation = Quaternion.Slerp(fromRot, toLocalRot, t);
            yield return null;
        }

        foot.localPosition = toLocalPos;
        foot.localRotation = toLocalRot;
    }

    private void LateUpdate()
    {
        if (_mountedCharacter != null)
        {
            _mountedCharacter.transform.localPosition = Vector3.zero;
        }
    }

    public async void EnterBike(Character character)
    {
        if (isEntering || character == null) return;
        isEntering = true;

        if (hoverController != null)
        {
            hoverController.FXActive = true;

            Vector3 startPos = hoverController.transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.3f;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                hoverController.transform.position = Vector3.Lerp(startPos, endPos, t);
                elapsed += Time.deltaTime;
                await Task.Yield();
            }
            hoverController.transform.position = endPos;

            await Task.Delay(100);
        }

        Collider vehicleCollider = GetComponent<Collider>();
        Rigidbody vehicleRigidbody = GetComponent<Rigidbody>();
        Collider playerCollider = character.GetComponent<Collider>();

        if (vehicleCollider != null)
        {
            await Task.Yield();
            vehicleCollider.isTrigger = true;

            if (vehicleRigidbody != null)
                vehicleRigidbody.isKinematic = true;
        }

        var drivingConfig = new ConfigState(
            0f, 1f, 1f,
            drivingStateTransitionIn,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState, drivingStateLayer,
            BlendMode.Blend, drivingConfig
        );

        Task animationTask = PlayAnimation(
            character,
            entryAnimation,
            entryAnimationTransitionIn,
            entryAnimationTransitionOut
        );
        await animationTask;

        await Task.Delay(10);
        if (vehicleCollider != null)
        {
            vehicleCollider.isTrigger = false;

            if (vehicleRigidbody != null)
            {
                vehicleRigidbody.isKinematic = false;
                await Task.Yield();
            }
        }

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (entryParent != null)
        {
            character.transform.SetParent(entryParent);
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
        }

        if (character.Player != null)
            character.Player.IsControllable = false;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            var ikSetter = animator.GetComponent<CharacterIKSetter>()
                           ?? animator.gameObject.AddComponent<CharacterIKSetter>();

            ikSetter.SetIKTargets(
                steeringWheelLeftHandTarget, steeringWheelRightHandTarget,
                leftHandIKWeight, rightHandIKWeight
            );

            ikSetter.SetFootIKTargets(
                leftFootTarget, rightFootTarget,
                leftFootIKWeight, rightFootIKWeight
            );
        }

        _inBike = true;
        _footOnGround = false;

        if (hoverController != null)
        {
            hoverController.isVehicleEnabled = true;
        }
        else if (bikeController != null)
        {
            var setMethod = bikeController.GetType()
                .GetMethod("SetBikeEnabled", BindingFlags.Public | BindingFlags.Instance);

            if (setMethod != null)
                setMethod.Invoke(bikeController, new object[] { true });
            else
                bikeController.enabled = true;
        }

        _ = this.onEnter.Run(new Args(this.gameObject));
        isEntering = false;
    }

    public async void ExitBike(Character character)
    {
        if (isExiting || character == null) return;
        isExiting = true;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            CharacterIKSetter ikSetter = animator.GetComponent<CharacterIKSetter>();
            if (ikSetter != null)
            {
                ikSetter.SetIKTargets(null, null, 0f, 0f);
                ikSetter.SetFootIKTargets(null, null, 0f, 0f);
            }
        }

        BoxCollider bikeCollider = GetComponent<BoxCollider>();
        Rigidbody bikeRigidbody = GetComponent<Rigidbody>();
        Collider playerCollider = character.GetComponent<Collider>();

        if (bikeRigidbody != null)
        {
            await Task.Yield();
            bikeRigidbody.isKinematic = true;
        }

        if (bikeCollider != null)
        {
            bikeCollider.isTrigger = true;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        character.States.Stop(drivingStateLayer, 0f, drivingStateTransitionOut);

        Task animationTask = PlayAnimation(character, exitAnimation, exitAnimationTransitionIn, exitAnimationTransitionOut);
        await animationTask;

        if (character.Player != null)
        {
            character.Player.IsControllable = true;
        }
        if (bikeCollider != null)
        {
            bikeCollider.isTrigger = false;
        }
        if (bikeRigidbody != null)
        {
            bikeRigidbody.isKinematic = false;
        }

        if (hoverController != null)
        {
            hoverController.isVehicleEnabled = false;
        }
        else if (bikeController != null)
        {
            MethodInfo setMethod = bikeController.GetType()
                .GetMethod("SetBikeEnabled", BindingFlags.Public | BindingFlags.Instance);

            if (setMethod != null)
                setMethod.Invoke(bikeController, new object[] { false });
            else
                bikeController.enabled = false;
        }

        _mountedCharacter = null;

        character.transform.SetParent(null);

        _ = this.onExit.Run(new Args(this.gameObject));

        isExiting = false;
    }

    private async Task PlayAnimation(Character character, AnimationClip clip, float transitionIn, float transitionOut)
    {
        if (clip == null) return;
        float speed = 1f;
        ConfigGesture gestureConfig = new ConfigGesture(0f, clip.length, speed, useRootMotion, transitionIn, transitionOut);
        character.Gestures.CrossFade(clip, animationMask, BlendMode.Blend, gestureConfig, true);
        await Task.Delay((int)(clip.length * 1000 / speed));
    }
}