using UnityEngine;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

[AddComponentMenu("Game Creator/Mechanics/CarEntry")]
public class CarEntry : MonoBehaviour
{
    [Header("Animation Settings")]
    public AnimationClip entryAnimation;
    public AnimationClip exitAnimation;
    public AvatarMask animationMask;

    [Header("Animation Transitions")]
    public float entryAnimationTransitionIn = 0.1f;
    public float entryAnimationTransitionOut = 0.25f;
    public float exitAnimationTransitionIn = 0.1f;
    public float exitAnimationTransitionOut = 0.25f;
    public bool useRootMotion = true;

    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 doorOpenRotation;
    public float doorRotationDuration = 0.5f;
    public float doorRotationStartDelay = 0.2f;
    public float doorResetDelay = 0.5f;

    [Header("Door Handle IK")]
    [Tooltip("Hand target on the exterior door handle during entry and exit animations.")]
    public Transform doorHandleTarget;
    [Tooltip("Choose which hand reaches for the exterior door handle.")]
    public AvatarIKGoal doorHandleHand = AvatarIKGoal.RightHand;
    [Range(0f, 1f)] public float doorHandleIKWeight = 1f;
    [Tooltip("Door-handle IK weight over the normalized entry animation time.")]
    public AnimationCurve entryDoorHandleIKCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.08f, 1f),
        new Keyframe(0.45f, 1f),
        new Keyframe(0.65f, 0f)
    );
    [Tooltip("Door-handle IK weight over the normalized exit animation time.")]
    public AnimationCurve exitDoorHandleIKCurve = new AnimationCurve(
        new Keyframe(0.2f, 0f),
        new Keyframe(0.4f, 1f),
        new Keyframe(0.78f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Driving State")]
    public StateData drivingState = new StateData(StateData.StateType.State);
    public int drivingStateLayer = 0;
    public float drivingStateTransitionIn = 0.1f;
    public float drivingStateTransitionOut = 0.25f;

    [Header("Entry Parent Settings")]
    public Transform entryParent;
    [Tooltip("Exact character root position and facing direction before the entry animation starts.")]
    public Transform entryStandingPoint;
    [Tooltip("Character root waypoint at the door threshold while stepping into the vehicle.")]
    public Transform entryStepPoint;
    public bool alignCharacterToStandingPoint = true;
    [Tooltip("Warp the animation root through Standing, Step and Seat anchors.")]
    public bool useAuthoredEntryPath = true;
    [Range(0.1f, 0.9f)] public float entryStepNormalizedTime = 0.52f;

    [Header("IK Settings")]
    public Transform steeringWheelLeftHandTarget;
    public Transform steeringWheelRightHandTarget;
    public float leftHandIKWeight = 1f;
    public float rightHandIKWeight = 1f;

    [Header("External Controller Seat Alignment")]
    [Range(0f, 0.95f)] public float entrySeatAlignmentStart = 0.6f;
    [Min(0f)] public float entrySeatAlignmentSharpness = 14f;

    [Header("On Enter Instructions")]
    [SerializeField] public InstructionList onEnter = new InstructionList();
    [Header("On Exit Instructions")]
    [SerializeField] public InstructionList onExit = new InstructionList();

    private PhysicsCarController carController;
    private HoverVehicleController hoverController;
    private IRvrVehicleDriveController externalDriveController;

    private bool isEntering = false;
    private bool isExiting = false;

    private Rigidbody _charRb;
    private bool _rbWasKinematic;
    private RigidbodyConstraints _origConstraints;
    private Character _seatedChar;
    private Character _entryAlignCharacter;
    private float _entryAlignStartedAt;
    private float _entryAlignDuration;
    private CharacterIKSetter _doorHandleIKSetter;
    private AnimationCurve _activeDoorHandleIKCurve;
    private float _doorHandleIKStartedAt;
    private float _doorHandleIKDuration;
    private bool _doorHandleIKIsEntry;
    private Vector3 _entryPathStartPosition;
    private Quaternion _entryPathStartRotation;

    public Character SeatedCharacter => _seatedChar;
    public bool IsTransitioning => isEntering || isExiting;

    private void Awake()
    {
        carController = GetComponent<PhysicsCarController>();
        hoverController = GetComponent<HoverVehicleController>();
        externalDriveController = ResolveExternalDriveController();

        Transform vehicleBody = externalDriveController != null
            ? externalDriveController.VehicleBody
            : carController != null
                ? carController.carBody
                : null;

        if (entryParent != null && vehicleBody != null)
        {
            entryParent.SetParent(vehicleBody, true);
        }
    }

    private void LateUpdate()
    {
        UpdateEntrySeatAlignment();
        UpdateDoorHandleIK();

        if (_seatedChar != null)
        {
            _seatedChar.transform.localPosition = Vector3.zero;
            _seatedChar.transform.localRotation = Quaternion.identity;
        }
    }

    public async void EnterCar(Character character)
    {
        if (isEntering || character == null) return;
        isEntering = true;

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null) hoverController.isVehicleEnabled = false;
        else if (carController != null) carController.enabled = false;

        AlignCharacterToStandingPoint(character);

        var carCollider = GetComponent<BoxCollider>();
        var carRigidbody = GetComponent<Rigidbody>();
        var playerCollider = character.GetComponent<Collider>();

        if (carCollider != null)
        {
            await Task.Yield();
            carCollider.isTrigger = true;
            if (carRigidbody != null) carRigidbody.isKinematic = true;
        }

        var drivingConfig = new ConfigState(
            0f, 1f, 1f,
            drivingStateTransitionIn,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState, drivingStateLayer, BlendMode.Blend, drivingConfig
        );

        if (doorTransform != null) _ = DoorRotationSequence();

        BeginDoorHandleIK(character, entryAnimation, entryDoorHandleIKCurve, true);
        BeginEntrySeatAlignment(character, entryAnimation);
        var animTask = PlayAnimation(
            character, entryAnimation,
            entryAnimationTransitionIn,
            entryAnimationTransitionOut
        );
        await animTask;
        EndDoorHandleIK();
        CompleteEntrySeatAlignment(character);

        await Task.Delay(10);
        if (carCollider != null) carCollider.isTrigger = false;
        if (carRigidbody != null) carRigidbody.isKinematic = false;
        if (playerCollider != null) playerCollider.enabled = false;

        character.transform.SetParent(entryParent);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        _charRb = character.GetComponent<Rigidbody>();
        if (_charRb != null)
        {
            _rbWasKinematic = _charRb.isKinematic;
            _origConstraints = _charRb.constraints;

            _charRb.isKinematic = true;
            _charRb.constraints = RigidbodyConstraints.FreezeAll;
        }

        _seatedChar = character;

        if (character.Player != null)
            character.Player.IsControllable = false;

        if (steeringWheelLeftHandTarget != null && steeringWheelRightHandTarget != null)
        {
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var ikSetter = animator.GetComponent<CharacterIKSetter>();
                if (ikSetter == null)
                    ikSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

                ikSetter.SetIKTargets(
                    steeringWheelLeftHandTarget,
                    steeringWheelRightHandTarget,
                    leftHandIKWeight,
                    rightHandIKWeight
                );
            }
        }

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(true);
        else if (hoverController != null)
            hoverController.isVehicleEnabled = true;
        else if (carController != null)
        {
            carController.enabled = true;
            carController.SetCarEnabled(true);
        }

        _ = this.onEnter.Run(new Args(this.gameObject));
        isEntering = false;
    }

    public async void ExitCar(Character character)
    {
        if (isExiting || character == null) return;
        isExiting = true;

        if (_charRb != null)
        {
            _charRb.isKinematic = _rbWasKinematic;
            _charRb.constraints = _origConstraints;
            _charRb = null;
        }

        _seatedChar = null;

        character.transform.SetParent(null);

        var animator = character.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            var ikSetter = animator.GetComponent<CharacterIKSetter>();
            if (ikSetter != null)
                ikSetter.SetIKTargets(null, null, 0f, 0f);
        }

        var carCollider = GetComponent<BoxCollider>();
        var carRigidbody = GetComponent<Rigidbody>();
        var playerCollider = character.GetComponent<Collider>();

        if (carRigidbody != null)
        {
            await Task.Yield();
            carRigidbody.isKinematic = true;
        }
        if (carCollider != null) carCollider.isTrigger = true;
        if (playerCollider != null) playerCollider.enabled = true;

        character.States.Stop(drivingStateLayer, 0f, drivingStateTransitionOut);

        if (doorTransform != null) _ = DoorRotationSequence();

        BeginDoorHandleIK(character, exitAnimation, exitDoorHandleIKCurve, false);
        var animTask = PlayAnimation(
            character, exitAnimation,
            exitAnimationTransitionIn,
            exitAnimationTransitionOut
        );
        await animTask;
        EndDoorHandleIK();

        if (character.Player != null)
            character.Player.IsControllable = true;

        if (carCollider != null) carCollider.isTrigger = false;
        if (carRigidbody != null) carRigidbody.isKinematic = false;

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null)
            hoverController.isVehicleEnabled = false;
        else if (carController != null)
            carController.SetCarEnabled(false);

        _ = this.onExit.Run(new Args(this.gameObject));
        isExiting = false;
    }

    private void AlignCharacterToStandingPoint(Character character)
    {
        if (!alignCharacterToStandingPoint || entryStandingPoint == null || character == null)
            return;

        character.transform.SetPositionAndRotation(
            entryStandingPoint.position,
            entryStandingPoint.rotation
        );
    }

    private void BeginDoorHandleIK(
        Character character,
        AnimationClip clip,
        AnimationCurve weightCurve,
        bool isEntry)
    {
        EndDoorHandleIK();
        if (character == null || clip == null || doorHandleTarget == null) return;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;

        _doorHandleIKSetter = animator.GetComponent<CharacterIKSetter>();
        if (_doorHandleIKSetter == null)
            _doorHandleIKSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

        _activeDoorHandleIKCurve = weightCurve;
        _doorHandleIKIsEntry = isEntry;
        _doorHandleIKStartedAt = Time.time;
        _doorHandleIKDuration = Mathf.Max(0.01f, clip.length);
        UpdateDoorHandleIK();
    }

    private void UpdateDoorHandleIK()
    {
        if (_doorHandleIKSetter == null || doorHandleTarget == null) return;

        float normalized = Mathf.Clamp01(
            (Time.time - _doorHandleIKStartedAt) / _doorHandleIKDuration
        );
        float curveWeight = _activeDoorHandleIKCurve != null
            ? _activeDoorHandleIKCurve.Evaluate(normalized)
            : 1f;
        if (_doorHandleIKIsEntry && entryStepPoint != null)
        {
            float releaseAt = Mathf.Clamp01(entryStepNormalizedTime);
            float releaseFrom = Mathf.Max(0f, releaseAt - 0.15f);
            curveWeight *= 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(releaseFrom, releaseAt, normalized)
            );
        }
        float weight = Mathf.Clamp01(curveWeight * doorHandleIKWeight);

        if (doorHandleHand == AvatarIKGoal.LeftHand)
            _doorHandleIKSetter.SetIKTargets(doorHandleTarget, null, weight, 0f);
        else
            _doorHandleIKSetter.SetIKTargets(null, doorHandleTarget, 0f, weight);
    }

    private void EndDoorHandleIK()
    {
        if (_doorHandleIKSetter != null)
            _doorHandleIKSetter.SetIKTargets(null, null, 0f, 0f);

        _doorHandleIKSetter = null;
        _activeDoorHandleIKCurve = null;
        _doorHandleIKIsEntry = false;
    }

    private IRvrVehicleDriveController ResolveExternalDriveController()
    {
        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour is IRvrVehicleDriveController controller)
            {
                return controller;
            }
        }

        return null;
    }

    private void BeginEntrySeatAlignment(Character character, AnimationClip clip)
    {
        if (externalDriveController?.UseSeatEntryAlignment != true ||
            character == null || clip == null || entryParent == null)
        {
            _entryAlignCharacter = null;
            return;
        }

        _entryAlignCharacter = character;
        _entryAlignStartedAt = Time.time;
        _entryAlignDuration = Mathf.Max(0.01f, clip.length);
        _entryPathStartPosition = entryStandingPoint != null
            ? entryStandingPoint.position
            : character.transform.position;
        _entryPathStartRotation = entryStandingPoint != null
            ? entryStandingPoint.rotation
            : character.transform.rotation;
    }

    private void UpdateEntrySeatAlignment()
    {
        if (_entryAlignCharacter == null || entryParent == null) return;

        float normalized = Mathf.Clamp01(
            (Time.time - _entryAlignStartedAt) / _entryAlignDuration
        );

        if (useAuthoredEntryPath && entryStepPoint != null)
        {
            EvaluateAuthoredEntryPath(
                normalized,
                out Vector3 pathPosition,
                out Quaternion pathRotation
            );
            _entryAlignCharacter.transform.SetPositionAndRotation(
                pathPosition,
                pathRotation
            );
            return;
        }

        if (normalized < entrySeatAlignmentStart) return;

        float window = Mathf.Max(0.01f, 1f - entrySeatAlignmentStart);
        float progress = Mathf.Clamp01((normalized - entrySeatAlignmentStart) / window);
        progress = progress * progress * (3f - 2f * progress);
        float blend = 1f - Mathf.Exp(
            -entrySeatAlignmentSharpness * progress * Time.deltaTime
        );

        Transform characterTransform = _entryAlignCharacter.transform;
        characterTransform.position = Vector3.Lerp(
            characterTransform.position,
            entryParent.position,
            blend
        );
        characterTransform.rotation = Quaternion.Slerp(
            characterTransform.rotation,
            entryParent.rotation,
            blend
        );
    }

    private void CompleteEntrySeatAlignment(Character character)
    {
        if (_entryAlignCharacter != character) return;

        if (character != null && entryParent != null)
        {
            character.transform.SetPositionAndRotation(
                entryParent.position,
                entryParent.rotation
            );
        }

        _entryAlignCharacter = null;
    }

    public void EvaluateAuthoredEntryPath(
        float normalized,
        out Vector3 position,
        out Quaternion rotation)
    {
        normalized = Mathf.Clamp01(normalized);
        Vector3 startPosition = _entryAlignCharacter != null
            ? _entryPathStartPosition
            : entryStandingPoint != null
                ? entryStandingPoint.position
                : transform.position;
        Quaternion startRotation = _entryAlignCharacter != null
            ? _entryPathStartRotation
            : entryStandingPoint != null
                ? entryStandingPoint.rotation
                : transform.rotation;
        Vector3 endPosition = entryParent != null
            ? entryParent.position
            : startPosition;
        Quaternion endRotation = entryParent != null
            ? entryParent.rotation
            : startRotation;

        if (entryStepPoint == null)
        {
            position = Vector3.Lerp(startPosition, endPosition, normalized);
            rotation = Quaternion.Slerp(startRotation, endRotation, normalized);
            return;
        }

        float waypointTime = Mathf.Clamp(entryStepNormalizedTime, 0.1f, 0.9f);
        float inverse = 1f - waypointTime;
        float denominator = Mathf.Max(0.0001f, 2f * inverse * waypointTime);
        Vector3 control = (
            entryStepPoint.position -
            inverse * inverse * startPosition -
            waypointTime * waypointTime * endPosition
        ) / denominator;

        float oneMinus = 1f - normalized;
        position = oneMinus * oneMinus * startPosition +
            2f * oneMinus * normalized * control +
            normalized * normalized * endPosition;

        if (normalized <= waypointTime)
        {
            float stepProgress = Mathf.InverseLerp(0f, waypointTime, normalized);
            rotation = Quaternion.Slerp(
                startRotation,
                entryStepPoint.rotation,
                Mathf.SmoothStep(0f, 1f, stepProgress)
            );
        }
        else
        {
            float seatProgress = Mathf.InverseLerp(waypointTime, 1f, normalized);
            rotation = Quaternion.Slerp(
                entryStepPoint.rotation,
                endRotation,
                Mathf.SmoothStep(0f, 1f, seatProgress)
            );
        }
    }

    private async Task DoorRotationSequence()
    {
        if (doorTransform == null) return;
        Quaternion originalRotation = doorTransform.localRotation;

        await Task.Delay((int)(doorRotationStartDelay * 1000));
        await RotateDoor(doorTransform, doorOpenRotation, doorRotationDuration);

        await Task.Delay((int)(doorResetDelay * 1000));
        await RotateDoor(doorTransform, originalRotation.eulerAngles, doorRotationDuration);
    }

    private async Task PlayAnimation(
        Character character,
        AnimationClip clip,
        float transitionIn,
        float transitionOut
    )
    {
        if (clip == null) return;

        float speed = 1f;
        var gestureConfig = new ConfigGesture(
            0f, clip.length, speed,
            useRootMotion,
            transitionIn, transitionOut
        );

        character.Gestures.CrossFade(
            clip, animationMask,
            BlendMode.Blend,
            gestureConfig,
            true
        );

        await Task.Delay((int)(clip.length * 1000 / speed));
    }

    private async Task RotateDoor(
        Transform door,
        Vector3 targetEulerAngles,
        float duration
    )
    {
        Quaternion startRotation = door.localRotation;
        Quaternion targetRotation = Quaternion.Euler(targetEulerAngles);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            door.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            await Task.Yield();
        }

        door.localRotation = targetRotation;
    }
}
