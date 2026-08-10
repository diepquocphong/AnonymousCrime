using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

public interface ICarOccupiedEntryHandler
{
    bool TryEnterOccupiedCar(Character character, CarEntrySideMode requestedSide);
}

public enum CarEntrySideMode
{
    Automatic,
    DriverDoor,
    PassengerDoor
}

[AddComponentMenu("Game Creator/Mechanics/CarEntry")]
public class CarEntry : MonoBehaviour
{
    [Header("Animation Settings")]
    public AnimationClip entryAnimation;
    public AnimationClip exitAnimation;
    public AvatarMask animationMask;

    [Header("Passenger Door Entry")]
    [Tooltip("Automatic chooses the nearest configured front-door standing point.")]
    public CarEntrySideMode entrySideMode = CarEntrySideMode.Automatic;
    [Tooltip("Humanoid-mirrored enter clip used only when entering through the passenger door.")]
    public AnimationClip mirroredEntryAnimation;
    public Transform passengerEntryStandingPoint;
    public Transform passengerEntryStepPoint;
    [Tooltip("Root waypoint on the passenger seat before the same animation reaches the driver seat.")]
    public Transform passengerEntryCabinPoint;
    [Range(0.55f, 0.95f)] public float passengerCabinNormalizedTime = 0.78f;
    public Transform passengerDoorTransform;
    public Vector3 passengerDoorOpenRotation;
    public Transform passengerDoorHandleTarget;
    public AudioSource passengerDoorAudioSource;
    [Range(0.15f, 0.55f)] public float occupiedDoorOpenGestureNormalizedTime = 0.34f;
    [Min(0f)] public float occupiedDoorReachLeadTime = 0.12f;
    [Min(0.05f)] public float passengerToDriverTransferDuration = 0.22f;

    [Header("Animation Transitions")]
    public float entryAnimationTransitionIn = 0.1f;
    public float entryAnimationTransitionOut = 0.25f;
    public float exitAnimationTransitionIn = 0.1f;
    public float exitAnimationTransitionOut = 0.25f;
    [Range(0.5f, 2f)] public float entryAnimationSpeed = 1.35f;
    [Range(0.5f, 2f)] public float exitAnimationSpeed = 1.35f;
    public bool useRootMotion = true;

    [Header("Speed Aware Exit")]
    public AnimationClip movingExitAnimation;
    public AnimationClip movingExitLandingAnimation;
    [Tooltip("Number of source frames sampled from CarGetKickedOutL before ragdoll takes over.")]
    [Min(1)] public int movingExitLaunchFrameCount = 50;
    [Min(1f)] public float fastExitSpeedKph = 50f;
    [Min(0f)] public float stoppedExitSpeedKph = 0.8f;
    [Min(0.1f)] public float exitStopTimeout = 6f;
    [Range(0.5f, 2.5f)] public float movingExitAnimationSpeed = 1.45f;
    [Range(0.5f, 3f)] public float movingExitLandingSpeed = 1.6f;
    [Min(0f)] public float movingExitDoorLeadTime = 0.35f;
    [Tooltip("Driver-door hand reach used while the rest of the body remains seated.")]
    public AnimationCurve movingExitDoorReachIKCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1f),
        new Keyframe(0.8f, 1f),
        new Keyframe(1f, 0f)
    );
    [Min(0.1f)] public float movingExitLandingClipDuration = 2f;
    [Range(0f, 1f)] public float movingExitInheritedVelocity = 0.55f;
    [Min(0f)] public float movingExitLateralSpeed = 2.8f;
    [Min(0f)] public float movingExitUpwardSpeed = 1.2f;
    [Min(0f)] public float movingExitTransientDuration = 0.45f;
    [Min(0f)] public float movingExitTransientFade = 0.75f;
    [Min(0f)] public float movingExitClearance = 0.25f;
    [Header("Moving Exit Ragdoll")]
    [Min(0.25f)] public float movingExitRagdollDuration = 2.25f;
    [Min(0f)] public float movingExitRagdollTumbleVelocity = 5.5f;
    public bool movingExitAutoRecover = true;
    [Tooltip("Seconds the Sim-Cade chase camera remains active after bailout ragdoll begins.")]
    [Min(0f)] public float movingExitPlayerCameraDelay = 2f;
    [Header("Moving Exit Door")]
    [Min(0.1f)] public float movingExitDoorPartialCloseDuration = 2f;
    [Tooltip("Fraction of the fully-open angle retained after a high-speed bailout. Zero would latch the door completely.")]
    [Range(0.05f, 0.5f)] public float movingExitDoorRemainingOpen = 0.15f;

    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 doorOpenRotation;
    public float doorRotationDuration = 0.5f;
    public float doorRotationStartDelay = 0.2f;
    public float doorResetDelay = 0.5f;

    [Header("Door Audio")]
    [Tooltip("Dedicated 3D source used for door sounds so engine audio remains untouched.")]
    public AudioSource doorAudioSource;
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;
    [Range(0f, 1f)] public float doorSoundVolume = 0.7f;

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

    [Header("Entry Approach")]
    [Min(0.15f)] public float entryApproachStopDistance = 0.15f;
    [Min(0.1f)] public float entryApproachTimeout = 4f;
    [Min(0f)] public float entryApproachAlignmentDuration = 0.12f;
    [Min(1)] public int entryApproachMotionPriority = 10;

    [Header("IK Settings")]
    public Transform steeringWheelLeftHandTarget;
    public Transform steeringWheelRightHandTarget;
    public float leftHandIKWeight = 1f;
    public float rightHandIKWeight = 1f;

    [Header("External Controller Seat Alignment")]
    [Range(0f, 0.95f)] public float entrySeatAlignmentStart = 0.6f;
    [Min(0f)] public float entrySeatAlignmentSharpness = 14f;

    [Header("Seated Skeleton Handoff")]
    [Tooltip("Seconds before the Enter clip ends when its seated skeleton pose is captured.")]
    [Min(0f)] public float seatedPoseGuardLeadTime = 0.16f;
    [Tooltip("Seconds to retain that pose after the vehicle camera becomes active.")]
    [Min(0f)] public float seatedPoseGuardReleaseDelay = 0.18f;

    [Header("On Enter Instructions")]
    [SerializeField] public InstructionList onEnter = new InstructionList();
    [Header("On Exit Instructions")]
    [SerializeField] public InstructionList onExit = new InstructionList();

    private PhysicsCarController carController;
    private HoverVehicleController hoverController;
    private IRvrVehicleDriveController externalDriveController;
    private ICarOccupiedEntryHandler occupiedEntryHandler;
    private BoxCollider cachedCarCollider;
    private Rigidbody cachedCarRigidbody;

    private bool isEntering = false;
    private bool isExiting = false;

    private Character _seatedChar;
    private CharacterPhysicsSnapshot _seatedPhysics;
    private CharacterPhysicsSnapshot _passengerCarjackingPhysics;
    private readonly CharacterPhysicsSnapshot _physicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly CharacterPhysicsSnapshot _passengerPhysicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly List<Collider> _colliderBuffer = new List<Collider>(8);
    private readonly List<Rigidbody> _rigidbodyBuffer = new List<Rigidbody>(4);
    private readonly List<MonoBehaviour> _behaviourBuffer = new List<MonoBehaviour>(16);
    private Character _entryAlignCharacter;
    private float _entryAlignStartedAt;
    private float _entryAlignDuration;
    private CharacterIKSetter _doorHandleIKSetter;
    private AvatarIKGoal _activeDoorHandleHand;
    private AnimationCurve _activeDoorHandleIKCurve;
    private float _doorHandleIKStartedAt;
    private float _doorHandleIKDuration;
    private bool _doorHandleIKIsEntry;
    private Vector3 _entryPathStartPosition;
    private Quaternion _entryPathStartRotation;
    private Character _approachCharacter;
    private bool _approachFinished;
    private bool _approachSucceeded;
    private System.Action<Character, bool> _approachCallback;
    private SeatedSkeletonPoseGuard _seatedPoseGuard;
    private bool _activePassengerEntry;
    private bool _entryStopsAtPassengerCabin;
    private bool _passengerCarjackingSeatOccupied;
    private Quaternion _driverDoorClosedRotation;
    private Quaternion _passengerDoorClosedRotation;
    private bool _closedDoorRotationsCached;
    private CharacterModelSnapshot _lastBailoutModelSnapshot;

    private const int SEATED_GRAVITY_LOCK_KEY = 0x53454154;

    private struct ColliderSnapshot
    {
        public Collider collider;
        public bool enabled;
    }

    private struct RigidbodySnapshot
    {
        public Rigidbody rigidbody;
        public bool isKinematic;
        public bool useGravity;
        public bool detectCollisions;
        public RigidbodyConstraints constraints;
    }

    private struct CharacterModelSnapshot
    {
        public Character character;
        public Transform model;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    private sealed class CharacterPhysicsSnapshot
    {
        public Character character;
        public bool driverCollision;
        public bool driverUpdateKinematics;
        public Character.MovementType movementType;
        public readonly List<ColliderSnapshot> colliders = new List<ColliderSnapshot>(8);
        public readonly List<RigidbodySnapshot> rigidbodies = new List<RigidbodySnapshot>(4);
    }

    public Character SeatedCharacter => _seatedChar;
    public bool IsTransitioning => isEntering || isExiting;
    public bool IsUsingMirroredEntry => _activePassengerEntry &&
        mirroredEntryAnimation != null;
    public bool IsPassengerCarjackingSeatOccupied =>
        _passengerCarjackingSeatOccupied;

    /// <summary>
    /// Single entry API owned by the car. The caller only supplies the GC2
    /// Character; this component chooses normal entry or occupied-car stealing.
    /// </summary>
    public bool RequestEnter(Character character)
    {
        return RequestEnter(character, CarEntrySideMode.Automatic);
    }

    public bool RequestEnter(Character character, CarEntrySideMode requestedSide)
    {
        if (character == null || IsTransitioning) return false;
        if (_seatedChar == null)
        {
            _ = EnterCarAsync(character, true, false, requestedSide);
            return true;
        }

        if (_seatedChar == character) return false;
        if (occupiedEntryHandler == null) CacheVehicleBehaviours();
        return occupiedEntryHandler != null &&
            occupiedEntryHandler.TryEnterOccupiedCar(character, requestedSide);
    }

    public bool RequestExit(Character character)
    {
        if (character == null || character != _seatedChar || IsTransitioning) return false;
        ExitCar(character);
        return true;
    }

    /// <summary>
    /// Entry continuation used by the carjacking controller while the same door
    /// is already open. It deliberately does not start another door sequence.
    /// </summary>
    public Task<bool> EnterThroughOpenDoorAsync(Character character)
    {
        // Carjacking always continues through the already-open driver door.
        return EnterCarAsync(
            character,
            false,
            true,
            CarEntrySideMode.DriverDoor
        );
    }

    public Task<bool> EnterThroughOpenDoorAsync(
        Character character,
        CarEntrySideMode requestedSide)
    {
        return EnterCarAsync(
            character,
            false,
            requestedSide == CarEntrySideMode.DriverDoor,
            requestedSide
        );
    }

    public void PrepareOccupiedEntrySide(
        Character character,
        CarEntrySideMode requestedSide)
    {
        SelectEntrySide(character, false, requestedSide);
    }

    public void ClearPreparedEntrySide()
    {
        if (!isEntering) _activePassengerEntry = false;
        if (_seatedChar == null) _passengerCarjackingSeatOccupied = false;
        if (_passengerCarjackingPhysics != null &&
            _seatedChar != _passengerCarjackingPhysics.character)
        {
            RestorePassengerCarjackingPhysics();
        }
    }

    public bool IsPassengerEntryActive => _activePassengerEntry;
    public Transform ActiveEntryStandingPoint => GetActiveEntryStandingPoint();
    public Transform ActiveEntryDoor => GetActiveEntryDoor();
    public Vector3 ActiveEntryDoorOpenRotation => GetActiveEntryDoorOpenRotation();

    public void PlayActiveEntryDoorSound(bool opening)
    {
        PlayDoorSound(GetActiveEntryDoorAudioSource(), opening);
    }

    public void PlayEntryDoorSound(
        CarEntrySideMode side,
        bool opening)
    {
        AudioSource source = side == CarEntrySideMode.PassengerDoor &&
            passengerDoorAudioSource != null
                ? passengerDoorAudioSource
                : doorAudioSource;
        PlayDoorSound(source, opening);
    }

    public async Task<bool> OpenPreparedEntryDoorWithCharacterAsync(
        Character character)
    {
        Transform door = GetActiveEntryDoor();
        AnimationClip clip = GetActiveEntryAnimation();
        if (character == null || door == null || clip == null) return false;

        float clipDuration = clip.length * Mathf.Clamp(
            occupiedDoorOpenGestureNormalizedTime,
            0.15f,
            0.55f
        );
        BeginDoorHandleIK(
            character,
            clip,
            entryDoorHandleIKCurve,
            true,
            entryAnimationSpeed
        );

        try
        {
            Task gestureTask = PlayAnimation(
                character,
                clip,
                0.05f,
                0.05f,
                entryAnimationSpeed,
                false,
                clipDuration
            );
            await WaitUnscaledSecondsAsync(occupiedDoorReachLeadTime);
            PlayActiveEntryDoorSound(true);
            Task doorTask = RotateDoor(
                door,
                GetActiveEntryDoorOpenRotation(),
                doorRotationDuration
            );
            await Task.WhenAll(gestureTask, doorTask);
            return character != null && door != null;
        }
        finally
        {
            EndDoorHandleIK();
        }
    }

    public async Task<bool> EnterPassengerSeatForCarjackingAsync(
        Character character)
    {
        if (character == null || !_activePassengerEntry ||
            mirroredEntryAnimation == null || passengerEntryCabinPoint == null)
        {
            return false;
        }

        if (character.Player != null) character.Player.IsControllable = false;
        _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
            ? SeatedSkeletonPoseGuard.Prepare(character)
            : null;
        _entryStopsAtPassengerCabin = true;
        LockPassengerCarjackingPhysics(character);

        try
        {
            BeginDoorHandleIK(
                character,
                mirroredEntryAnimation,
                entryDoorHandleIKCurve,
                true,
                entryAnimationSpeed
            );
            BeginEntrySeatAlignment(
                character,
                mirroredEntryAnimation,
                entryAnimationSpeed
            );
            float animationStartedAt = Time.time;
            Task animationTask = PlayAnimation(
                character,
                mirroredEntryAnimation,
                0.04f,
                0.04f,
                entryAnimationSpeed
            );
            await WaitUnscaledSecondsAsync(occupiedDoorReachLeadTime);
            Transform door = GetActiveEntryDoor();
            if (door == null) return false;
            Task passengerStateWarmupTask = WarmPassengerDrivingStateNearEntryEndAsync(
                character,
                mirroredEntryAnimation,
                entryAnimationSpeed,
                animationStartedAt
            );
            PlayActiveEntryDoorSound(true);
            await RotateDoor(
                door,
                GetActiveEntryDoorOpenRotation(),
                doorRotationDuration
            );
            await animationTask;
            await passengerStateWarmupTask;
            EndDoorHandleIK();
            CompleteEntrySeatAlignment(character);

            // Unlike the final driver attachment, this intermediate passenger
            // stage is not parented to a seat yet. Hand the skeleton to the real
            // Driving state while the separately captured passenger physics stays
            // locked and the root remains on the cabin anchor.
            character.Gestures.Stop(0f, 0f);
            _seatedPoseGuard?.Cancel();

            // Let GC2 evaluate the seated state once, then capture that exact
            // skeleton. The guard retains only this correct passenger-seat pose
            // while the NPC is pushed out.
            await Task.Yield();
            await Task.Yield();
            if (character == null || passengerEntryCabinPoint == null)
            {
                _passengerCarjackingSeatOccupied = false;
                return false;
            }
            character.transform.SetPositionAndRotation(
                passengerEntryCabinPoint.position,
                passengerEntryCabinPoint.rotation
            );
            _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
                ? SeatedSkeletonPoseGuard.Prepare(character)
                : null;
            _seatedPoseGuard?.Capture();
            return character != null;
        }
        finally
        {
            EndDoorHandleIK();
            _entryStopsAtPassengerCabin = false;
        }
    }

    public void SetPassengerPushArmFree(bool free)
    {
        _seatedPoseGuard?.SetLeftArmFree(free);
        _seatedPoseGuard?.SetRightArmFree(free);
        if (!free) _seatedPoseGuard?.ClearPassengerPushPose();
    }

    public void ConfigurePassengerPushPose(
        Transform lookTarget,
        float weight,
        float maxTorsoYaw,
        float maxTorsoLean,
        float headLookWeight)
    {
        _seatedPoseGuard?.ConfigurePassengerPushPose(
            lookTarget,
            weight,
            maxTorsoYaw,
            maxTorsoLean,
            headLookWeight
        );
    }

    public void ConfigurePassengerPushHands(
        Transform leftTarget,
        Transform rightTarget,
        float leftWeight,
        float rightWeight)
    {
        _seatedPoseGuard?.ConfigurePassengerPushHands(
            leftTarget,
            rightTarget,
            leftWeight,
            rightWeight
        );
    }

    public void ApplyPassengerPushPoseBeforeIK()
    {
        _seatedPoseGuard?.ApplyPassengerPushPoseBeforeIK();
    }

    public async Task<bool> CompletePassengerCarjackingEntryAsync(
        Character character)
    {
        if (character == null || entryParent == null || _seatedChar != null)
        {
            _passengerCarjackingSeatOccupied = false;
            return false;
        }

        isEntering = true;
        _seatedPoseGuard?.SetLeftArmFree(false);
        var drivingConfig = new ConfigState(
            0f, 1f, 1f,
            0f,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState,
            drivingStateLayer,
            BlendMode.Blend,
            drivingConfig
        );

        Vector3 startPosition = character.transform.position;
        Quaternion startRotation = character.transform.rotation;
        float duration = Mathf.Max(0.05f, passengerToDriverTransferDuration);
        float elapsed = 0f;
        while (elapsed < duration && character != null && entryParent != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, entryParent.position, progress),
                Quaternion.Slerp(startRotation, entryParent.rotation, progress)
            );
            await Task.Yield();
        }

        if (character == null || entryParent == null)
        {
            _passengerCarjackingSeatOccupied = false;
            isEntering = false;
            return false;
        }

        character.transform.SetPositionAndRotation(
            entryParent.position,
            entryParent.rotation
        );
        // The interaction manager suppresses Driving while the passenger-to-
        // driver transfer is still in progress. Reassert it in the attachment
        // frame so releasing the skeleton guard cannot expose an upright pose.
        _ = character.States.SetState(
            drivingState,
            drivingStateLayer,
            BlendMode.Blend,
            drivingConfig
        );
        AttachCharacterToSeat(character);
        _passengerCarjackingSeatOccupied = false;
        await Task.Yield();
        await Task.Yield();

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(true);
        else if (hoverController != null)
            hoverController.isVehicleEnabled = true;
        else if (carController != null)
        {
            carController.enabled = true;
            carController.SetCarEnabled(true);
        }

        _seatedPoseGuard?.ReleaseAfter(seatedPoseGuardReleaseDelay);
        _seatedPoseGuard = null;
        _ = this.onEnter.Run(new Args(this.gameObject));
        _activePassengerEntry = false;
        isEntering = false;
        return true;
    }

    /// <summary>
    /// Places a GC2 NPC in the driver seat without enabling player vehicle input.
    /// Used by the Sim-Cade carjacking setup; normal player entry still goes
    /// through EnterCar and keeps its original door/entry sequence.
    /// </summary>
    public bool SeatOccupantInstant(Character character)
    {
        if (character == null || IsTransitioning || _seatedChar != null || entryParent == null)
            return false;

        var drivingConfig = new ConfigState(
            0f, 1f, 1f,
            drivingStateTransitionIn,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState, drivingStateLayer, BlendMode.Blend, drivingConfig
        );

        AttachCharacterToSeat(character);

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null) hoverController.isVehicleEnabled = false;
        else if (carController != null) carController.SetCarEnabled(false);
        return true;
    }

    /// <summary>
    /// Detaches the current NPC while keeping GC2 locomotion and collision locked
    /// until the paired kicked-out animation reaches its authored landing point.
    /// </summary>
    public bool ReleaseOccupantForCarjacking(Character character)
    {
        if (character == null || _seatedChar != character || IsTransitioning) return false;

        _seatedChar = null;
        character.transform.SetParent(null, true);
        ClearSteeringWheelIK(character);

        if (character.Player != null) character.Player.IsControllable = false;

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null) hoverController.isVehicleEnabled = false;
        else if (carController != null) carController.SetCarEnabled(false);
        return true;
    }

    /// <summary>
    /// Places the pulled-out NPC exactly at the authored landing anchor before
    /// restoring GC2 movement, gravity, colliders and rigidbodies.
    /// </summary>
    public async Task CompleteOccupantReleaseAsync(
        Character character,
        Vector3 position,
        Quaternion rotation)
    {
        if (character == null) return;
        character.transform.SetPositionAndRotation(position, rotation);
        // The victim gesture is still at full weight here, so the seated state
        // can be removed without exposing an upright locomotion pose in the car.
        character.States.Stop(drivingStateLayer, 0f, 0f);
        RestoreCharacterPhysics(character, true);
        if (character.Player != null) character.Player.IsControllable = false;
        Physics.SyncTransforms();

        // Keep GC2 grounded while its CharacterController/colliders receive the
        // landing transform. This prevents the base graph from entering Air
        // between the victim gesture and its grounded locomotion state.
        await Task.Yield();
        if (character == null) return;
        character.transform.SetPositionAndRotation(position, rotation);
        character.Driver?.ResetVerticalVelocity();
        Physics.SyncTransforms();

        await Task.Yield();
        if (character?.Driver != null)
        {
            character.Driver.ResetVerticalVelocity();
            character.Driver.ForceGrounded(false);
        }
    }

    private void Awake()
    {
        carController = GetComponent<PhysicsCarController>();
        hoverController = GetComponent<HoverVehicleController>();
        cachedCarCollider = GetComponent<BoxCollider>();
        cachedCarRigidbody = GetComponent<Rigidbody>();
        _approachCallback = OnEntryApproachFinished;
        CacheVehicleBehaviours();
        CacheClosedDoorRotations();

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

    public void EnterCar(Character character)
    {
        _ = EnterCarAsync(character, true);
    }

    private async Task<bool> EnterCarAsync(
        Character character,
        bool runDoorSequence,
        bool forceDriverDoor = false,
        CarEntrySideMode requestedSide = CarEntrySideMode.Automatic)
    {
        if (isEntering || character == null ||
            (_seatedChar != null && _seatedChar != character)) return false;
        RestoreBailoutModelTransform(character);
        isEntering = true;
        SelectEntrySide(character, forceDriverDoor, requestedSide);

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null) hoverController.isVehicleEnabled = false;
        else if (carController != null) carController.enabled = false;

        if (character.Player != null) character.Player.IsControllable = false;
        _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
            ? SeatedSkeletonPoseGuard.Prepare(character)
            : null;
        if (!await MoveCharacterToEntryStandingPointAsync(character))
        {
            if (character != null && character.Player != null)
                character.Player.IsControllable = true;
            _seatedPoseGuard = null;
            _activePassengerEntry = false;
            isEntering = false;
            return false;
        }

        var carCollider = cachedCarCollider;
        var carRigidbody = cachedCarRigidbody;
        var playerCollider = character.GetComponent<Collider>();

        if (carCollider != null)
        {
            // Empty-car entry keeps the original safety frame. During a
            // carjacking handoff the door is already open, so starting the
            // entry gesture in this same frame avoids an idle standing pose.
            if (runDoorSequence) await Task.Yield();
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

        Transform activeDoor = GetActiveEntryDoor();
        if (runDoorSequence && activeDoor != null)
        {
            _ = DoorRotationSequence(
                activeDoor,
                GetActiveEntryDoorOpenRotation(),
                GetActiveEntryDoorAudioSource()
            );
        }

        AnimationClip activeEntryAnimation = GetActiveEntryAnimation();
        BeginDoorHandleIK(
            character,
            activeEntryAnimation,
            entryDoorHandleIKCurve,
            true,
            entryAnimationSpeed
        );
        BeginEntrySeatAlignment(character, activeEntryAnimation, entryAnimationSpeed);
        // Normal entry blends from locomotion. An already-open door means this
        // is the carjacking handoff: blending for 0.2s would keep the upright
        // kick-out gesture while the authored path is already moving into the
        // seat. Switch the full-body gesture immediately in that branch.
        float entryTransitionIn = runDoorSequence
            ? entryAnimationTransitionIn
            : 0f;
        var animTask = PlayAnimation(
            character, activeEntryAnimation,
            entryTransitionIn,
            entryAnimationTransitionOut,
            entryAnimationSpeed
        );
        await CaptureSeatedPoseNearAnimationEndAsync(
            character,
            activeEntryAnimation,
            entryAnimationSpeed
        );
        await animTask;
        EndDoorHandleIK();
        CompleteEntrySeatAlignment(character);

        // Reassert the persistent seated clip after the entry gesture has been
        // removed from GC2's graph. This closes the one-frame window where the
        // base locomotion pose could be evaluated at the seat position.
        var finalDrivingConfig = new ConfigState(
            0f, 1f, 1f,
            0f,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState, drivingStateLayer, BlendMode.Blend, finalDrivingConfig
        );

        if (carCollider != null) carCollider.isTrigger = false;
        if (carRigidbody != null) carRigidbody.isKinematic = false;
        if (playerCollider != null) playerCollider.enabled = false;
        AttachCharacterToSeat(character);

        // Let the Animator evaluate the seated state for a complete rendered
        // frame before Sim-Cade activates its chase camera.
        await Task.Yield();
        await Task.Yield();

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(true);
        else if (hoverController != null)
            hoverController.isVehicleEnabled = true;
        else if (carController != null)
        {
            carController.enabled = true;
            carController.SetCarEnabled(true);
        }

        _seatedPoseGuard?.ReleaseAfter(seatedPoseGuardReleaseDelay);
        _seatedPoseGuard = null;

        _ = this.onEnter.Run(new Args(this.gameObject));
        _activePassengerEntry = false;
        isEntering = false;
        return true;
    }

    public void ExitCar(Character character)
    {
        if (isExiting || character == null || character != _seatedChar) return;
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;
        isExiting = true;
        _ = ExitCarBySpeedAsync(character);
    }

    private async Task ExitCarBySpeedAsync(Character character)
    {
        bool exitCompleted = false;
        try
        {
            // Only the exact Sim-Cade car exposes speed-aware braking and
            // momentum preservation. Other RVR vehicle prefabs keep their
            // original immediate exit path.
            if (externalDriveController == null)
            {
                await ExitStoppedCarAsync(character);
                exitCompleted = true;
                return;
            }

            float speedKph = GetVehicleSpeedMetersPerSecond() * 3.6f;
            bool useMovingExit = speedKph > fastExitSpeedKph;

            if (useMovingExit)
            {
                await ExitMovingCarAsync(character);
                exitCompleted = true;
                return;
            }

            if (!await WaitForVehicleStopAsync(character)) return;
            await ExitStoppedCarAsync(character);
            exitCompleted = true;
        }
        finally
        {
            if (!exitCompleted && externalDriveController != null)
                externalDriveController.CancelExitStop();
            isExiting = false;
        }
    }

    private async Task<bool> WaitForVehicleStopAsync(Character character)
    {
        if (character == null || character != _seatedChar) return false;
        if (GetVehicleSpeedMetersPerSecond() * 3.6f <= stoppedExitSpeedKph)
            return true;

        externalDriveController?.BeginExitStop();
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, exitStopTimeout);
        while (character != null && character == _seatedChar &&
               GetVehicleSpeedMetersPerSecond() * 3.6f > stoppedExitSpeedKph)
        {
            if (Time.unscaledTime >= deadline) return false;
            await Task.Yield();
        }

        return character != null && character == _seatedChar;
    }

    private async Task ExitStoppedCarAsync(Character character)
    {
        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(false, false);

        var carCollider = cachedCarCollider;
        var carRigidbody = cachedCarRigidbody;
        var playerCollider = character.GetComponent<Collider>();

        // The passenger-carjacking snapshot remembers the Player's colliders in
        // their original enabled state. Make the vehicle non-blocking before that
        // snapshot is restored; otherwise Unity resolves one frame of overlap by
        // lifting the still-seated Player onto the roof.
        if (carCollider != null) carCollider.isTrigger = true;
        if (carRigidbody != null) carRigidbody.isKinematic = true;
        Physics.SyncTransforms();

        _seatedChar = null;
        character.transform.SetParent(null, true);
        RestoreCharacterPhysics(character);
        ClearSteeringWheelIK(character);

        if (playerCollider != null) playerCollider.enabled = true;
        if (character.Driver != null) character.Driver.Collision = true;
        Physics.SyncTransforms();

        character.States.Stop(drivingStateLayer, 0f, drivingStateTransitionOut);

        if (doorTransform != null) _ = DoorRotationSequence();

        BeginDoorHandleIK(
            character,
            exitAnimation,
            exitDoorHandleIKCurve,
            false,
            exitAnimationSpeed
        );
        await PlayAnimation(
            character, exitAnimation,
            exitAnimationTransitionIn,
            exitAnimationTransitionOut,
            exitAnimationSpeed
        );
        EndDoorHandleIK();

        if (character.Player != null)
            character.Player.IsControllable = true;

        if (carCollider != null) carCollider.isTrigger = false;
        if (carRigidbody != null) carRigidbody.isKinematic = false;

        if (hoverController != null)
            hoverController.isVehicleEnabled = false;
        else if (carController != null)
            carController.SetCarEnabled(false);

        _ = this.onExit.Run(new Args(this.gameObject));
    }

    private async Task ExitMovingCarAsync(Character character)
    {
        Vector3 vehicleVelocity = cachedCarRigidbody != null
            ? cachedCarRigidbody.linearVelocity
            : Vector3.zero;

        SimcadeCarDriver bailoutCameraDriver =
            externalDriveController as SimcadeCarDriver;
        bailoutCameraDriver?.BeginBailoutCameraHold();
        bailoutCameraDriver?.KeepEngineRunningAfterBailout();
        externalDriveController?.SetVehicleEnabled(false, true);
        ClearSteeringWheelIK(character);
        _lastBailoutModelSnapshot = CaptureCharacterModelTransform(character);

        // Keep a real seated skeleton while the door starts opening. Stopping
        // Driving here used to expose the upright locomotion graph before the
        // moving-exit gesture had taken ownership of the body.
        _seatedPoseGuard = SeatedSkeletonPoseGuard.Prepare(character);
        _seatedPoseGuard?.Capture();
        AvatarIKGoal bailoutReachHand = GetDoorHandleHand(false);
        float bailoutDoorReachDuration = Mathf.Max(
            0.1f,
            Mathf.Max(doorRotationStartDelay, movingExitDoorLeadTime) +
                doorRotationDuration
        );
        _seatedPoseGuard?.ConfigureSeatedDoorReach(
            GetActiveDoorHandleTarget(false),
            bailoutReachHand == AvatarIKGoal.LeftHand,
            bailoutDoorReachDuration,
            movingExitDoorReachIKCurve,
            doorHandleIKWeight
        );

        Transform bailoutDoor = doorTransform;
        Quaternion bailoutDoorClosedRotation = bailoutDoor != null
            ? GetClosedDoorRotation(bailoutDoor)
            : Quaternion.identity;
        Task bailoutDoorOpenTask = bailoutDoor != null
            ? OpenMovingExitDoorAsync(
                bailoutDoor,
                doorOpenRotation,
                doorAudioSource
            )
            : Task.CompletedTask;
        // The whole skeleton remains in the captured driving pose while only
        // the door-side arm reaches out. Do not give the exit root path control
        // until the door has actually cleared the body.
        await bailoutDoorOpenTask;
        if (character == null)
        {
            bailoutCameraDriver?.EndBailoutCameraHold();
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            return;
        }

        Vector3 sideDirection = GetDriverDoorSideDirection();
        Transform bailoutReleaseAnchor = entryStepPoint != null
            ? entryStepPoint
            : entryStandingPoint;
        Vector3 releasePosition = bailoutReleaseAnchor != null
            ? bailoutReleaseAnchor.position
            : character.transform.position;
        Quaternion releaseRotation = bailoutReleaseAnchor != null
            ? bailoutReleaseAnchor.rotation
            : character.transform.rotation;
        Vector3 bailoutWorldOffset =
            sideDirection * movingExitClearance + Vector3.up * 0.05f;
        Vector3 bailoutAnchorLocalOffset = bailoutReleaseAnchor != null
            ? bailoutReleaseAnchor.InverseTransformVector(bailoutWorldOffset)
            : bailoutWorldOffset;

        // CarGetKickedOutL frames 0-49 contain the authored seated launch. The
        // car owns the root path and the gesture is cut exactly at frame 50;
        // later upright/recovery frames are never sampled.
        await PlayMovingExitLaunchSegmentAsync(
            character,
            bailoutReleaseAnchor,
            bailoutAnchorLocalOffset,
            releasePosition + bailoutWorldOffset,
            releaseRotation
        );
        if (character == null)
        {
            bailoutCameraDriver?.EndBailoutCameraHold();
            return;
        }

        Vector3 bailoutLaunchPosition = bailoutReleaseAnchor != null
            ? bailoutReleaseAnchor.TransformPoint(bailoutAnchorLocalOffset)
            : releasePosition + bailoutWorldOffset;
        releaseRotation = bailoutReleaseAnchor != null
            ? bailoutReleaseAnchor.rotation
            : releaseRotation;

        character.transform.SetParent(null, true);
        RestoreCharacterPhysics(character);
        character.transform.SetPositionAndRotation(
            bailoutLaunchPosition,
            releaseRotation
        );
        if (character.Driver != null) character.Driver.Collision = true;
        Physics.SyncTransforms();
        Task bailoutCameraReleaseTask = Task.CompletedTask;

        // Only start the wind/inertia close once the body has cleared the
        // cabin. A bailout door never reaches the latch and therefore must not
        // play the regular close sound.
        Task bailoutDoorPartialCloseTask = bailoutDoor != null
            ? PartiallyCloseMovingExitDoorAsync(
                bailoutDoor,
                bailoutDoorClosedRotation,
                doorOpenRotation,
                bailoutDoorOpenTask
            )
            : Task.CompletedTask;

        Vector3 inheritedVelocity = Vector3.ProjectOnPlane(
            vehicleVelocity,
            Vector3.up
        ) * movingExitInheritedVelocity;
        Vector3 bailoutVelocity = inheritedVelocity +
            sideDirection * movingExitLateralSpeed +
            Vector3.up * movingExitUpwardSpeed;
        bool useRagdoll = character.Ragdoll.Get<RagdollDefault>() != null;
        if (useRagdoll)
        {
            if (character.Player != null)
                character.Player.IsControllable = false;

            character.States.Stop(drivingStateLayer, 0f, 0f);
            Task ragdollStartTask = character.Ragdoll.StartRagdoll();
            // StartRagdoll enables the bone rigidbodies synchronously before its
            // first yield. Cancel immediately afterwards so the seated guard
            // seeds the ragdoll pose but never fights its physics in LateUpdate.
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            await ragdollStartTask;
            if (bailoutCameraDriver != null)
            {
                bailoutCameraReleaseTask =
                    ReleaseBailoutCameraAfterDelayAsync(bailoutCameraDriver);
            }
            if (character != null)
            {
                ApplyMovingExitRagdollVelocity(character, bailoutVelocity);
                // Ragdoll ignores locomotion input. The Player flag may safely
                // unlock interaction state now; Sim-Cade retains camera ownership
                // independently until the configured two-second hold expires.
                if (character.Player != null)
                    character.Player.IsControllable = true;

                await WaitUnscaledSecondsAsync(movingExitRagdollDuration);
                if (character != null && movingExitAutoRecover &&
                    character.Ragdoll.IsRagdoll)
                {
                    await character.Ragdoll.StartRecover();
                    RestoreBailoutModelTransform(character);
                }
            }
        }
        else
        {
            if (bailoutCameraDriver != null)
            {
                bailoutCameraReleaseTask =
                    ReleaseBailoutCameraAfterDelayAsync(bailoutCameraDriver);
            }
            character.States.Stop(drivingStateLayer, 0f, 0f);
            _seatedPoseGuard?.ReleaseAfter(movingExitTransientDuration);
            _seatedPoseGuard = null;
            if (character.Motion != null && bailoutVelocity.sqrMagnitude > 0.001f)
            {
                character.Motion.SetMotionTransient(
                    bailoutVelocity.normalized,
                    bailoutVelocity.magnitude,
                    movingExitTransientDuration,
                    movingExitTransientFade
                );
            }

        }

        if (character.Player != null)
            character.Player.IsControllable = true;

        await bailoutCameraReleaseTask;
        await bailoutDoorPartialCloseTask;
        _ = this.onExit.Run(new Args(this.gameObject));
    }

    private async Task ReleaseBailoutCameraAfterDelayAsync(
        SimcadeCarDriver driver)
    {
        await WaitUnscaledSecondsAsync(movingExitPlayerCameraDelay);
        if (driver != null) driver.EndBailoutCameraHold();
    }

    private async Task PlayMovingExitLaunchSegmentAsync(
        Character character,
        Transform targetAnchor,
        Vector3 targetAnchorLocalOffset,
        Vector3 fallbackTargetPosition,
        Quaternion fallbackTargetRotation)
    {
        if (character == null) return;

        Vector3 GetTargetPosition()
        {
            return targetAnchor != null
                ? targetAnchor.TransformPoint(targetAnchorLocalOffset)
                : fallbackTargetPosition;
        }

        Quaternion GetTargetRotation()
        {
            return targetAnchor != null
                ? targetAnchor.rotation
                : fallbackTargetRotation;
        }

        AnimationClip clip = movingExitAnimation;
        if (clip == null || clip.frameRate <= 0f)
        {
            _seatedChar = null;
            character.transform.SetPositionAndRotation(
                GetTargetPosition(),
                GetTargetRotation()
            );
            return;
        }

        float sourceDuration = Mathf.Min(
            clip.length,
            movingExitLaunchFrameCount / clip.frameRate
        );
        float speed = Mathf.Max(0.01f, movingExitAnimationSpeed);
        float runtimeDuration = sourceDuration / speed;
        Vector3 fallbackStartPosition = character.transform.position;
        Quaternion fallbackStartRotation = character.transform.rotation;
        float startedAt = Time.time;

        Task launchGestureTask = PlayAnimation(
            character,
            clip,
            0f,
            0f,
            speed,
            false,
            sourceDuration
        );

        // Let GC2 install frame zero underneath the seated guard. Releasing on
        // the next continuation prevents a base-locomotion/upright frame while
        // preserving the animation's authored seated launch pose.
        await Task.Yield();
        if (character == null) return;
        _seatedChar = null;
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;

        float progress = 0f;
        while (progress < 1f && character != null)
        {
            progress = Mathf.Clamp01(
                (Time.time - startedAt) / Mathf.Max(0.01f, runtimeDuration)
            );
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 currentStartPosition = entryParent != null
                ? entryParent.position
                : fallbackStartPosition;
            Quaternion currentStartRotation = entryParent != null
                ? entryParent.rotation
                : fallbackStartRotation;
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(currentStartPosition, GetTargetPosition(), eased),
                Quaternion.Slerp(
                    currentStartRotation,
                    GetTargetRotation(),
                    eased
                )
            );
            await Task.Yield();
        }

        if (character != null)
        {
            float finalSourceFrameTime = Mathf.Min(
                clip.length,
                Mathf.Max(0, movingExitLaunchFrameCount - 1) /
                    clip.frameRate
            );
            _seatedPoseGuard = SeatedSkeletonPoseGuard.Prepare(character);
            _seatedPoseGuard?.CaptureAnimationPose(
                clip,
                finalSourceFrameTime
            );
        }

        await launchGestureTask;
        if (character != null)
            character.transform.SetPositionAndRotation(
                GetTargetPosition(),
                GetTargetRotation()
            );
    }

    private void ApplyMovingExitRagdollVelocity(
        Character character,
        Vector3 velocity)
    {
        Animator animator = character?.Animim?.Animator;
        if (animator == null) return;

        Rigidbody[] bodies = animator.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in bodies)
        {
            if (body == null || body.isKinematic) continue;
            body.linearVelocity = velocity;
        }

        Transform hips = animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Hips)
            : animator.transform;
        Rigidbody hipsBody = hips != null ? hips.GetComponent<Rigidbody>() : null;
        if (hipsBody == null || hipsBody.isKinematic) return;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 tumbleAxis = planarVelocity.sqrMagnitude > 0.001f
            ? Vector3.Cross(Vector3.up, planarVelocity.normalized)
            : transform.right;
        hipsBody.AddTorque(
            tumbleAxis.normalized * movingExitRagdollTumbleVelocity,
            ForceMode.VelocityChange
        );
    }

    private void AttachCharacterToSeat(Character character)
    {
        if (character == null || entryParent == null) return;

        RestoreBailoutModelTransform(character);
        LockCharacterPhysics(character);

        character.transform.SetParent(entryParent);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        _seatedChar = character;
        if (character.Player != null) character.Player.IsControllable = false;
        ConfigureSteeringWheelIK(character);
    }

    private static CharacterModelSnapshot CaptureCharacterModelTransform(
        Character character)
    {
        Animator animator = character?.Animim?.Animator;
        Transform model = animator != null ? animator.transform : null;
        if (model == null) return default;

        return new CharacterModelSnapshot
        {
            character = character,
            model = model,
            parent = model.parent,
            localPosition = model.localPosition,
            localRotation = model.localRotation,
            localScale = model.localScale
        };
    }

    private void RestoreBailoutModelTransform(Character character)
    {
        CharacterModelSnapshot snapshot = _lastBailoutModelSnapshot;
        Animator animator = character?.Animim?.Animator;
        if (snapshot.character != character || snapshot.model == null ||
            animator == null || animator.transform != snapshot.model)
        {
            return;
        }

        Transform model = snapshot.model;
        if (snapshot.parent != null && model.parent != snapshot.parent)
            model.SetParent(snapshot.parent, false);

        model.SetLocalPositionAndRotation(
            snapshot.localPosition,
            snapshot.localRotation
        );
        model.localScale = snapshot.localScale;
        Physics.SyncTransforms();
    }

    private void LockCharacterPhysics(Character character)
    {
        if (character == null) return;
        if (_passengerCarjackingPhysics != null &&
            _passengerCarjackingPhysics.character == character)
        {
            // Promote the original pre-entry snapshot. Recapturing here would
            // remember already-disabled colliders and make them stay disabled
            // after the Player exits the car.
            if (_seatedPhysics != null)
                RestoreCharacterPhysics(_seatedPhysics.character);
            _seatedPhysics = _passengerCarjackingPhysics;
            _passengerCarjackingPhysics = null;
            return;
        }

        if (_seatedPhysics != null)
            RestoreCharacterPhysics(_seatedPhysics.character);

        CharacterPhysicsSnapshot snapshot = _physicsSnapshotBuffer;
        CaptureAndLockCharacterPhysics(character, snapshot);
        _seatedPhysics = snapshot;
    }

    public void LockPassengerCarjackingPhysics(Character character)
    {
        if (character == null) return;
        if (_passengerCarjackingPhysics?.character == character) return;
        if (_passengerCarjackingPhysics != null)
            RestorePassengerCarjackingPhysics();

        CharacterPhysicsSnapshot snapshot = _passengerPhysicsSnapshotBuffer;
        CaptureAndLockCharacterPhysics(character, snapshot);
        _passengerCarjackingPhysics = snapshot;
    }

    private void CaptureAndLockCharacterPhysics(
        Character character,
        CharacterPhysicsSnapshot snapshot)
    {
        snapshot.colliders.Clear();
        snapshot.rigidbodies.Clear();
        snapshot.character = character;
        snapshot.driverCollision = character.Driver?.Collision ?? false;
        snapshot.driverUpdateKinematics = character.Driver?.UpdateKinematics ?? false;
        snapshot.movementType = character.Motion?.MovementType ?? Character.MovementType.None;

        character.Motion?.StopToDirection();
        character.Motion?.StopFollowingTarget();
        if (character.Motion != null)
            character.Motion.MovementType = Character.MovementType.None;

        if (character.Driver != null)
        {
            character.Driver.ResetVerticalVelocity();
            character.Driver.ForceGrounded(true);
            character.Driver.SetGravityInfluence(SEATED_GRAVITY_LOCK_KEY, 0f);
            character.Driver.UpdateKinematics = false;
            character.Driver.Collision = false;
        }

        _colliderBuffer.Clear();
        character.GetComponentsInChildren(true, _colliderBuffer);
        foreach (Collider collider in _colliderBuffer)
        {
            snapshot.colliders.Add(new ColliderSnapshot
            {
                collider = collider,
                enabled = collider.enabled
            });
            collider.enabled = false;
        }

        _colliderBuffer.Clear();

        _rigidbodyBuffer.Clear();
        character.GetComponentsInChildren(true, _rigidbodyBuffer);
        foreach (Rigidbody rigidbody in _rigidbodyBuffer)
        {
            snapshot.rigidbodies.Add(new RigidbodySnapshot
            {
                rigidbody = rigidbody,
                isKinematic = rigidbody.isKinematic,
                useGravity = rigidbody.useGravity,
                detectCollisions = rigidbody.detectCollisions,
                constraints = rigidbody.constraints
            });
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
            rigidbody.isKinematic = true;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
        _rigidbodyBuffer.Clear();

        Physics.SyncTransforms();
    }

    private void RestoreCharacterPhysics(Character character, bool keepGrounded = false)
    {
        CharacterPhysicsSnapshot snapshot = _seatedPhysics;
        if (snapshot == null || snapshot.character != character) return;

        RestoreCharacterPhysicsSnapshot(snapshot, keepGrounded);
        _seatedPhysics = null;
    }

    private void RestorePassengerCarjackingPhysics()
    {
        CharacterPhysicsSnapshot snapshot = _passengerCarjackingPhysics;
        if (snapshot == null) return;

        RestoreCharacterPhysicsSnapshot(snapshot, false);
        _passengerCarjackingPhysics = null;
    }

    private static void RestoreCharacterPhysicsSnapshot(
        CharacterPhysicsSnapshot snapshot,
        bool keepGrounded)
    {
        Character character = snapshot.character;
        if (character == null) return;

        foreach (RigidbodySnapshot state in snapshot.rigidbodies)
        {
            if (state.rigidbody == null) continue;
            state.rigidbody.constraints = state.constraints;
            state.rigidbody.useGravity = state.useGravity;
            state.rigidbody.detectCollisions = state.detectCollisions;
            state.rigidbody.isKinematic = state.isKinematic;
            if (!state.rigidbody.isKinematic)
            {
                state.rigidbody.linearVelocity = Vector3.zero;
                state.rigidbody.angularVelocity = Vector3.zero;
            }
        }

        foreach (ColliderSnapshot state in snapshot.colliders)
        {
            if (state.collider != null) state.collider.enabled = state.enabled;
        }

        if (character.Driver != null)
        {
            character.Driver.RemoveGravityInfluence(SEATED_GRAVITY_LOCK_KEY);
            character.Driver.UpdateKinematics = snapshot.driverUpdateKinematics;
            character.Driver.Collision = snapshot.driverCollision;
            character.Driver.ResetVerticalVelocity();
            if (!keepGrounded) character.Driver.ForceGrounded(false);
        }

        if (character.Motion != null)
            character.Motion.MovementType = snapshot.movementType;

        snapshot.character = null;
        snapshot.colliders.Clear();
        snapshot.rigidbodies.Clear();
        Physics.SyncTransforms();
    }

    private void ConfigureSteeringWheelIK(Character character)
    {
        if (character == null || steeringWheelLeftHandTarget == null ||
            steeringWheelRightHandTarget == null) return;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator == null) return;

        CharacterIKSetter ikSetter = animator.GetComponent<CharacterIKSetter>();
        if (ikSetter == null)
            ikSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

        ikSetter.SetIKTargets(
            steeringWheelLeftHandTarget,
            steeringWheelRightHandTarget,
            leftHandIKWeight,
            rightHandIKWeight
        );
    }

    private static void ClearSteeringWheelIK(Character character)
    {
        if (character == null) return;
        Animator animator = character.GetComponentInChildren<Animator>();
        CharacterIKSetter ikSetter = animator != null
            ? animator.GetComponent<CharacterIKSetter>()
            : null;
        if (ikSetter == null) return;
        ikSetter.SetIKTargets(null, null, 0f, 0f);
        ikSetter.SetFootIKTargets(null, null, 0f, 0f);
    }

    public async Task<bool> MoveCharacterToEntryStandingPointAsync(Character character)
    {
        Transform standingPoint = GetActiveEntryStandingPoint();
        if (character == null || character.Motion == null || standingPoint == null)
            return false;

        _approachCharacter = character;
        _approachFinished = false;
        _approachSucceeded = false;

        Vector3 targetFeet = standingPoint.position -
            Vector3.up * (character.Motion.Height * 0.5f);
        Location target = new Location(targetFeet, standingPoint.rotation);
        character.Motion.MoveToLocation(
            target,
            Mathf.Max(0.15f, entryApproachStopDistance),
            _approachCallback,
            Mathf.Max(1, entryApproachMotionPriority)
        );

        float deadline = Time.unscaledTime + Mathf.Max(0.1f, entryApproachTimeout);
        while (!_approachFinished && character != null && Time.unscaledTime < deadline)
        {
            await Task.Yield();
        }

        if (character == null) return false;
        if (!_approachFinished)
        {
            character.Motion.MoveToDirection(
                Vector3.zero,
                Space.World,
                Mathf.Max(1, entryApproachMotionPriority)
            );
            _approachCharacter = null;
            return false;
        }

        if (!_approachSucceeded)
        {
            _approachCharacter = null;
            return false;
        }

        if (alignCharacterToStandingPoint)
        {
            await SmoothAlignCharacterToStandingPoint(character);
        }

        _approachCharacter = null;
        return character != null;
    }

    private void OnEntryApproachFinished(Character character, bool success)
    {
        if (character != _approachCharacter) return;
        _approachSucceeded = success;
        _approachFinished = true;
    }

    private async Task SmoothAlignCharacterToStandingPoint(Character character)
    {
        Transform standingPoint = GetActiveEntryStandingPoint();
        if (character == null || standingPoint == null) return;

        float duration = Mathf.Max(0f, entryApproachAlignmentDuration);
        if (duration <= 0f) return;

        Vector3 startPosition = character.transform.position;
        Quaternion startRotation = character.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration && character != null && standingPoint != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, standingPoint.position, progress),
                Quaternion.Slerp(startRotation, standingPoint.rotation, progress)
            );
            await Task.Yield();
        }

        if (character != null && standingPoint != null)
            character.transform.SetPositionAndRotation(
                standingPoint.position,
                standingPoint.rotation
            );
    }

    private void BeginDoorHandleIK(
        Character character,
        AnimationClip clip,
        AnimationCurve weightCurve,
        bool isEntry,
        float speed)
    {
        EndDoorHandleIK();
        Transform handleTarget = GetActiveDoorHandleTarget(isEntry);
        if (character == null || clip == null || handleTarget == null) return;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;

        _doorHandleIKSetter = animator.GetComponent<CharacterIKSetter>();
        if (_doorHandleIKSetter == null)
            _doorHandleIKSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

        _activeDoorHandleIKCurve = weightCurve;
        _doorHandleIKIsEntry = isEntry;
        _activeDoorHandleHand = GetDoorHandleHand(isEntry);
        _doorHandleIKStartedAt = Time.time;
        _doorHandleIKDuration = Mathf.Max(
            0.01f,
            clip.length / Mathf.Max(0.01f, speed)
        );
        UpdateDoorHandleIK();
    }

    private void UpdateDoorHandleIK()
    {
        Transform handleTarget = GetActiveDoorHandleTarget(_doorHandleIKIsEntry);
        if (_doorHandleIKSetter == null || handleTarget == null) return;

        float normalized = Mathf.Clamp01(
            (Time.time - _doorHandleIKStartedAt) / _doorHandleIKDuration
        );
        float curveWeight = _activeDoorHandleIKCurve != null
            ? _activeDoorHandleIKCurve.Evaluate(normalized)
            : 1f;
        if (_doorHandleIKIsEntry && GetActiveEntryStepPoint() != null)
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

        if (_activeDoorHandleHand == AvatarIKGoal.LeftHand)
            _doorHandleIKSetter.SetIKTargets(handleTarget, null, weight, 0f);
        else
            _doorHandleIKSetter.SetIKTargets(null, handleTarget, 0f, weight);
    }

    private void EndDoorHandleIK()
    {
        if (_doorHandleIKSetter != null)
            _doorHandleIKSetter.SetIKTargets(null, null, 0f, 0f);

        _doorHandleIKSetter = null;
        _activeDoorHandleIKCurve = null;
        _doorHandleIKIsEntry = false;
        _activeDoorHandleHand = doorHandleHand;
    }

    private AnimationClip GetActiveEntryAnimation()
    {
        return IsUsingMirroredEntry ? mirroredEntryAnimation : entryAnimation;
    }

    public AvatarIKGoal GetDoorHandleHand(bool entering)
    {
        if (!entering || !_activePassengerEntry)
            return doorHandleHand;

        return doorHandleHand == AvatarIKGoal.LeftHand
            ? AvatarIKGoal.RightHand
            : AvatarIKGoal.LeftHand;
    }

    private void SelectEntrySide(
        Character character,
        bool forceDriverDoor,
        CarEntrySideMode requestedSide)
    {
        _activePassengerEntry = false;
        if (forceDriverDoor || !HasPassengerEntrySetup()) return;

        CarEntrySideMode resolvedSide = requestedSide != CarEntrySideMode.Automatic
            ? requestedSide
            : entrySideMode;
        if (resolvedSide == CarEntrySideMode.PassengerDoor)
        {
            _activePassengerEntry = true;
            return;
        }
        if (resolvedSide == CarEntrySideMode.DriverDoor || character == null) return;

        Vector3 characterPosition = character.transform.position;
        Vector3 driverOffset = Vector3.ProjectOnPlane(
            entryStandingPoint.position - characterPosition,
            Vector3.up
        );
        Vector3 passengerOffset = Vector3.ProjectOnPlane(
            passengerEntryStandingPoint.position - characterPosition,
            Vector3.up
        );
        _activePassengerEntry = passengerOffset.sqrMagnitude < driverOffset.sqrMagnitude;
    }

    private bool HasPassengerEntrySetup()
    {
        return mirroredEntryAnimation != null &&
            passengerEntryStandingPoint != null &&
            passengerEntryStepPoint != null &&
            passengerEntryCabinPoint != null &&
            passengerDoorTransform != null &&
            passengerDoorHandleTarget != null &&
            entryParent != null && entryStandingPoint != null;
    }

    private Transform GetActiveEntryStandingPoint()
    {
        return _activePassengerEntry && passengerEntryStandingPoint != null
            ? passengerEntryStandingPoint
            : entryStandingPoint;
    }

    private Transform GetActiveEntryStepPoint()
    {
        return _activePassengerEntry && passengerEntryStepPoint != null
            ? passengerEntryStepPoint
            : entryStepPoint;
    }

    private Transform GetActiveEntryDoor()
    {
        return _activePassengerEntry && passengerDoorTransform != null
            ? passengerDoorTransform
            : doorTransform;
    }

    private Vector3 GetActiveEntryDoorOpenRotation()
    {
        return _activePassengerEntry
            ? passengerDoorOpenRotation
            : doorOpenRotation;
    }

    private AudioSource GetActiveEntryDoorAudioSource()
    {
        return _activePassengerEntry && passengerDoorAudioSource != null
            ? passengerDoorAudioSource
            : doorAudioSource;
    }

    private Transform GetActiveDoorHandleTarget(bool entering)
    {
        return entering && _activePassengerEntry && passengerDoorHandleTarget != null
            ? passengerDoorHandleTarget
            : doorHandleTarget;
    }

    private void CacheVehicleBehaviours()
    {
        _behaviourBuffer.Clear();
        GetComponents(_behaviourBuffer);
        foreach (MonoBehaviour behaviour in _behaviourBuffer)
        {
            if (externalDriveController == null &&
                behaviour is IRvrVehicleDriveController controller)
                externalDriveController = controller;

            if (occupiedEntryHandler == null &&
                behaviour is ICarOccupiedEntryHandler handler)
                occupiedEntryHandler = handler;
        }
        _behaviourBuffer.Clear();
    }

    private void BeginEntrySeatAlignment(
        Character character,
        AnimationClip clip,
        float speed)
    {
        if (externalDriveController?.UseSeatEntryAlignment != true ||
            character == null || clip == null || entryParent == null)
        {
            _entryAlignCharacter = null;
            return;
        }

        _entryAlignCharacter = character;
        _entryAlignStartedAt = Time.time;
        _entryAlignDuration = Mathf.Max(
            0.01f,
            clip.length / Mathf.Max(0.01f, speed)
        );
        Transform standingPoint = GetActiveEntryStandingPoint();
        _entryPathStartPosition = standingPoint != null
            ? standingPoint.position
            : character.transform.position;
        _entryPathStartRotation = standingPoint != null
            ? standingPoint.rotation
            : character.transform.rotation;
    }

    private void UpdateEntrySeatAlignment()
    {
        if (_entryAlignCharacter == null || entryParent == null) return;

        float normalized = Mathf.Clamp01(
            (Time.time - _entryAlignStartedAt) / _entryAlignDuration
        );

        if (useAuthoredEntryPath && GetActiveEntryStepPoint() != null)
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

        Transform alignmentEnd = GetEntryAlignmentEnd();
        if (alignmentEnd == null) return;
        Transform characterTransform = _entryAlignCharacter.transform;
        characterTransform.position = Vector3.Lerp(
            characterTransform.position,
            alignmentEnd.position,
            blend
        );
        characterTransform.rotation = Quaternion.Slerp(
            characterTransform.rotation,
            alignmentEnd.rotation,
            blend
        );
    }

    private void CompleteEntrySeatAlignment(Character character)
    {
        if (_entryAlignCharacter != character) return;

        Transform alignmentEnd = GetEntryAlignmentEnd();
        if (character != null && alignmentEnd != null)
        {
            character.transform.SetPositionAndRotation(
                alignmentEnd.position,
                alignmentEnd.rotation
            );
        }

        _entryAlignCharacter = null;
    }

    private Transform GetEntryAlignmentEnd()
    {
        if (_entryStopsAtPassengerCabin && passengerEntryCabinPoint != null)
            return passengerEntryCabinPoint;
        return entryParent;
    }

    private float GetVehicleSpeedMetersPerSecond()
    {
        if (externalDriveController != null)
            return externalDriveController.SpeedMetersPerSecond;
        if (cachedCarRigidbody == null) return 0f;
        return Vector3.ProjectOnPlane(
            cachedCarRigidbody.linearVelocity,
            Vector3.up
        ).magnitude;
    }

    private Vector3 GetDriverDoorSideDirection()
    {
        Vector3 direction = entryStandingPoint != null
            ? entryStandingPoint.position - transform.position
            : -transform.right;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        return direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : -transform.right;
    }

    private static async Task WaitUnscaledSecondsAsync(float seconds)
    {
        if (seconds <= 0f) return;
        float deadline = Time.unscaledTime + seconds;
        while (Time.unscaledTime < deadline) await Task.Yield();
    }

    private async Task WarmPassengerDrivingStateNearEntryEndAsync(
        Character character,
        AnimationClip clip,
        float speed,
        float animationStartedAt)
    {
        if (character == null || clip == null) return;

        float duration = clip.length / Mathf.Max(0.01f, speed);
        float warmupLead = Mathf.Max(0.12f, seatedPoseGuardLeadTime);
        float warmupAt = animationStartedAt + Mathf.Max(0f, duration - warmupLead);
        while (character != null && Time.time < warmupAt)
            await Task.Yield();
        if (character == null) return;

        // Mark the intermediate passenger seat before setting the state so the
        // interaction manager does not suppress it in the same LateUpdate.
        // The full-body Enter gesture still owns the visible pose while GC2
        // evaluates the Driving playable graph underneath it.
        _passengerCarjackingSeatOccupied = true;
        var passengerStateConfig = new ConfigState(
            0f, 1f, 1f,
            0f,
            0f
        );
        _ = character.States.SetState(
            drivingState,
            drivingStateLayer,
            BlendMode.Blend,
            passengerStateConfig
        );
    }

    private async Task CaptureSeatedPoseNearAnimationEndAsync(
        Character character,
        AnimationClip clip,
        float speed)
    {
        if (_seatedPoseGuard == null || character == null || clip == null) return;

        float duration = clip.length / Mathf.Max(0.01f, speed);
        float captureAt = Mathf.Max(0f, duration - seatedPoseGuardLeadTime);
        float startedAt = Time.time;
        while (character != null && Time.time - startedAt < captureAt)
        {
            await Task.Yield();
        }

        if (character != null) _seatedPoseGuard?.Capture();
    }

    public void EvaluateAuthoredEntryPath(
        float normalized,
        out Vector3 position,
        out Quaternion rotation)
    {
        normalized = Mathf.Clamp01(normalized);
        Transform standingPoint = GetActiveEntryStandingPoint();
        Transform stepPoint = GetActiveEntryStepPoint();
        Vector3 startPosition = _entryAlignCharacter != null
            ? _entryPathStartPosition
            : standingPoint != null
                ? standingPoint.position
                : transform.position;
        Quaternion startRotation = _entryAlignCharacter != null
            ? _entryPathStartRotation
            : standingPoint != null
                ? standingPoint.rotation
                : transform.rotation;

        if (_activePassengerEntry && passengerEntryCabinPoint != null &&
            entryParent != null)
        {
            if (_entryStopsAtPassengerCabin)
            {
                EvaluateQuadraticEntrySegment(
                    normalized,
                    startPosition,
                    startRotation,
                    stepPoint,
                    passengerEntryCabinPoint.position,
                    passengerEntryCabinPoint.rotation,
                    entryStepNormalizedTime,
                    out position,
                    out rotation
                );
                return;
            }

            float cabinTime = Mathf.Clamp(passengerCabinNormalizedTime, 0.55f, 0.95f);
            if (normalized <= cabinTime)
            {
                float cabinProgress = Mathf.InverseLerp(0f, cabinTime, normalized);
                float localStepTime = Mathf.Clamp01(
                    entryStepNormalizedTime / cabinTime
                );
                EvaluateQuadraticEntrySegment(
                    cabinProgress,
                    startPosition,
                    startRotation,
                    stepPoint,
                    passengerEntryCabinPoint.position,
                    passengerEntryCabinPoint.rotation,
                    localStepTime,
                    out position,
                    out rotation
                );
                return;
            }

            float seatProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(cabinTime, 1f, normalized)
            );
            position = Vector3.Lerp(
                passengerEntryCabinPoint.position,
                entryParent.position,
                seatProgress
            );
            rotation = Quaternion.Slerp(
                passengerEntryCabinPoint.rotation,
                entryParent.rotation,
                seatProgress
            );
            return;
        }

        Vector3 endPosition = entryParent != null
            ? entryParent.position
            : startPosition;
        Quaternion endRotation = entryParent != null
            ? entryParent.rotation
            : startRotation;
        EvaluateQuadraticEntrySegment(
            normalized,
            startPosition,
            startRotation,
            stepPoint,
            endPosition,
            endRotation,
            entryStepNormalizedTime,
            out position,
            out rotation
        );
    }

    private static void EvaluateQuadraticEntrySegment(
        float normalized,
        Vector3 startPosition,
        Quaternion startRotation,
        Transform stepPoint,
        Vector3 endPosition,
        Quaternion endRotation,
        float requestedWaypointTime,
        out Vector3 position,
        out Quaternion rotation)
    {
        normalized = Mathf.Clamp01(normalized);
        if (stepPoint == null)
        {
            position = Vector3.Lerp(startPosition, endPosition, normalized);
            rotation = Quaternion.Slerp(startRotation, endRotation, normalized);
            return;
        }

        float waypointTime = Mathf.Clamp(requestedWaypointTime, 0.1f, 0.9f);
        float inverse = 1f - waypointTime;
        float denominator = Mathf.Max(0.0001f, 2f * inverse * waypointTime);
        Vector3 control = (
            stepPoint.position -
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
                stepPoint.rotation,
                Mathf.SmoothStep(0f, 1f, stepProgress)
            );
        }
        else
        {
            float seatProgress = Mathf.InverseLerp(waypointTime, 1f, normalized);
            rotation = Quaternion.Slerp(
                stepPoint.rotation,
                endRotation,
                Mathf.SmoothStep(0f, 1f, seatProgress)
            );
        }
    }

    private async Task DoorRotationSequence()
    {
        await DoorRotationSequence(doorTransform, doorOpenRotation, doorAudioSource);
    }

    private async Task DoorRotationSequence(
        Transform activeDoor,
        Vector3 activeOpenRotation,
        AudioSource activeAudioSource)
    {
        if (activeDoor == null) return;
        Quaternion originalRotation = GetClosedDoorRotation(activeDoor);

        await Task.Delay((int)(doorRotationStartDelay * 1000));
        PlayDoorSound(activeAudioSource, true);
        await RotateDoor(activeDoor, activeOpenRotation, doorRotationDuration);

        await Task.Delay((int)(doorResetDelay * 1000));
        PlayDoorSound(activeAudioSource, false);
        await RotateDoor(activeDoor, originalRotation.eulerAngles, doorRotationDuration);
    }

    private void CacheClosedDoorRotations()
    {
        if (_closedDoorRotationsCached) return;
        if (doorTransform != null)
            _driverDoorClosedRotation = doorTransform.localRotation;
        if (passengerDoorTransform != null)
            _passengerDoorClosedRotation = passengerDoorTransform.localRotation;
        _closedDoorRotationsCached = true;
    }

    private Quaternion GetClosedDoorRotation(Transform activeDoor)
    {
        CacheClosedDoorRotations();
        if (activeDoor == doorTransform) return _driverDoorClosedRotation;
        if (activeDoor == passengerDoorTransform)
            return _passengerDoorClosedRotation;
        return activeDoor != null ? activeDoor.localRotation : Quaternion.identity;
    }

    private async Task OpenMovingExitDoorAsync(
        Transform activeDoor,
        Vector3 activeOpenRotation,
        AudioSource activeAudioSource)
    {
        await WaitUnscaledSecondsAsync(
            Mathf.Max(doorRotationStartDelay, movingExitDoorLeadTime)
        );
        if (activeDoor == null) return;

        PlayDoorSound(activeAudioSource, true);
        await RotateDoor(activeDoor, activeOpenRotation, doorRotationDuration);
    }

    private async Task PartiallyCloseMovingExitDoorAsync(
        Transform activeDoor,
        Quaternion closedRotation,
        Vector3 activeOpenRotation,
        Task openDoorTask)
    {
        if (openDoorTask != null) await openDoorTask;
        if (activeDoor == null) return;

        Quaternion retainedOpenRotation = Quaternion.Slerp(
            closedRotation,
            Quaternion.Euler(activeOpenRotation),
            movingExitDoorRemainingOpen
        );
        await RotateDoorNaturally(
            activeDoor,
            retainedOpenRotation,
            movingExitDoorPartialCloseDuration
        );
    }

    /// <summary>
    /// Shared by normal enter/exit and the occupied-car sequence so one door
    /// action produces exactly one sound without adding another runtime loop.
    /// </summary>
    public void PlayDoorSound(bool opening)
    {
        PlayDoorSound(doorAudioSource, opening);
    }

    private void PlayDoorSound(AudioSource source, bool opening)
    {
        if (source == null || doorSoundVolume <= 0f) return;

        AudioClip clip = opening ? doorOpenSound : doorCloseSound;
        if (clip != null) source.PlayOneShot(clip, doorSoundVolume);
    }

    private Task PlayAnimation(
        Character character,
        AnimationClip clip,
        float transitionIn,
        float transitionOut,
        float speed
    )
    {
        return PlayAnimation(
            character,
            clip,
            transitionIn,
            transitionOut,
            speed,
            useRootMotion,
            clip != null ? clip.length : 0f
        );
    }

    private async Task PlayAnimation(
        Character character,
        AnimationClip clip,
        float transitionIn,
        float transitionOut,
        float speed,
        bool rootMotion,
        float duration
    )
    {
        if (character == null || clip == null) return;

        speed = Mathf.Max(0.01f, speed);
        var gestureConfig = new ConfigGesture(
            0f, Mathf.Clamp(duration, 0.01f, clip.length), speed,
            rootMotion,
            transitionIn, transitionOut
        );

        await character.Gestures.CrossFade(
            clip, animationMask,
            BlendMode.Blend,
            gestureConfig,
            true
        );
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

    private static async Task RotateDoorNaturally(
        Transform door,
        Quaternion targetRotation,
        float duration)
    {
        if (door == null) return;

        Quaternion startRotation = door.localRotation;
        float safeDuration = Mathf.Max(0.1f, duration);
        float elapsedTime = 0f;

        while (elapsedTime < safeDuration && door != null)
        {
            float normalized = Mathf.Clamp01(elapsedTime / safeDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            door.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                eased
            );
            elapsedTime += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        if (door != null) door.localRotation = targetRotation;
    }
}
