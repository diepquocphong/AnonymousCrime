using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FranklinGame.Animations;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using GameCreator.Runtime.VisualScripting;

public interface ICarOccupiedEntryHandler
{
    bool TryEnterOccupiedCar(Character character, CarEntrySideMode requestedSide);
}

public enum CarEntrySideMode
{
    Automatic,
    DriverDoor,
    PassengerDoor,
    RearLeftDoor,
    RearRightDoor
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
    [Tooltip("Optional local-position offset for sliding passenger doors. Leave zero for hinged doors.")]
    public Vector3 passengerDoorOpenLocalOffset;
    public Transform passengerDoorHandleTarget;
    public AudioSource passengerDoorAudioSource;
    [Range(0.15f, 0.55f)] public float occupiedDoorOpenGestureNormalizedTime = 0.34f;
    [Min(0f)] public float occupiedDoorReachLeadTime = 0.12f;
    [Min(0.05f)] public float passengerToDriverTransferDuration = 0.22f;

    [Header("Rear Seat Entry (4-door cars)")]
    [Tooltip("Mirrored exit clip used by doors on the right side of the car.")]
    public AnimationClip mirroredExitAnimation;
    public Transform rearLeftEntryStandingPoint;
    public Transform rearLeftEntryStepPoint;
    public Transform rearLeftSeatParent;
    public Transform rearLeftDoorTransform;
    public Vector3 rearLeftDoorOpenRotation;
    public Vector3 rearLeftDoorOpenLocalOffset;
    public Transform rearLeftDoorHandleTarget;
    public AudioSource rearLeftDoorAudioSource;
    public Transform rearRightEntryStandingPoint;
    public Transform rearRightEntryStepPoint;
    public Transform rearRightSeatParent;
    public Transform rearRightDoorTransform;
    public Vector3 rearRightDoorOpenRotation;
    public Vector3 rearRightDoorOpenLocalOffset;
    public Transform rearRightDoorHandleTarget;
    public AudioSource rearRightDoorAudioSource;
    public Transform rearLeftLapLeftHandTarget;
    public Transform rearLeftLapRightHandTarget;
    public Transform rearRightLapLeftHandTarget;
    public Transform rearRightLapRightHandTarget;
    [Range(0f, 1f)] public float rearLapHandIKWeight = 0.92f;

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
    [Header("Moving Exit Traits Damage")]
    [Tooltip("GC2 Player Traits Attribute reduced once a high-speed bailout starts ragdoll.")]
    public string movingExitHealthAttributeId = "hp";
    [Min(0f)] public float movingExitBaseDamage = 10f;
    [Min(0f)] public float movingExitDamagePerKphAboveThreshold = 0.5f;
    [Min(0f)] public float movingExitMaximumDamage = 60f;
    [Header("Moving Exit Door")]
    [Min(0.1f)] public float movingExitDoorPartialCloseDuration = 2f;
    [Tooltip("Fraction of the fully-open door pose retained after a high-speed bailout. Applies to both hinged rotation and an optional slide offset.")]
    [Range(0.05f, 0.5f)] public float movingExitDoorRemainingOpen = 0.15f;

    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 doorOpenRotation;
    [Tooltip("Optional local-position offset for sliding driver doors. Leave zero for hinged doors.")]
    public Vector3 doorOpenLocalOffset;
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

    [Header("Automatic Seated Visual Fit")]
    [Tooltip("Fits only the animated character model below the cabin ceiling. Character physics and vehicle scale are never changed.")]
    public bool autoFitSeatedModel = false;
    [Tooltip("Authored interior ceiling above the driver seat. This is more accurate than whole-body renderer bounds.")]
    public Transform driverSeatedCeilingAnchor;
    [Tooltip("Authored interior ceiling above the front passenger/cabin transfer point.")]
    public Transform passengerSeatedCeilingAnchor;
    public Transform rearLeftSeatedCeilingAnchor;
    public Transform rearRightSeatedCeilingAnchor;
    [Tooltip("Visible roof/body renderer used as the cabin ceiling. Falls back to the root BoxCollider when empty.")]
    public Renderer seatedCabinCeilingRenderer;
    [Min(0f)] public float seatedHeadTopOffset = 0.1f;
    [Min(0f)] public float seatedHeadClearance = 0.025f;
    [Min(0f)] public float seatedRoofLiningInset = 0.025f;
    [Range(0.75f, 1f)] public float minimumSeatedModelScale = 0.9f;
    [Min(0f)] public float maximumSeatedModelDownOffset = 0.16f;

    [Header("On Enter Instructions")]
    [SerializeField] public InstructionList onEnter = new InstructionList();
    [Header("On Exit Instructions")]
    [SerializeField] public InstructionList onExit = new InstructionList();

    private PhysicsCarController carController;
    private IRvrVehicleDriveController externalDriveController;
    private IRvrVehicleOccupantCollisionPolicy occupantCollisionPolicy;
    private ICarOccupiedEntryHandler occupiedEntryHandler;
    private SimcadeCarDestruction destruction;
    private SimcadeCarDoorDamage doorDamage;
    private BoxCollider cachedCarCollider;
    private Rigidbody cachedCarRigidbody;

    private bool isEntering = false;
    private bool isExiting = false;
    private bool _entryRagdollInterrupted;
    private int _lifecycleVersion;
    private int _driverTransitionVersion;
    private Character _driverTransitionCharacter;
    private Character _activeExitCharacter;

    private Character _seatedChar;
    private Character _rearLeftSeatedChar;
    private Character _rearRightSeatedChar;
    private CharacterPhysicsSnapshot _seatedPhysics;
    private CharacterPhysicsSnapshot _rearLeftPhysics;
    private CharacterPhysicsSnapshot _rearRightPhysics;
    private CharacterPhysicsSnapshot _passengerCarjackingPhysics;
    private readonly CharacterPhysicsSnapshot _physicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly CharacterPhysicsSnapshot _passengerPhysicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly CharacterPhysicsSnapshot _rearLeftPhysicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly CharacterPhysicsSnapshot _rearRightPhysicsSnapshotBuffer =
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
    private int _approachVersion;
    private SeatedSkeletonPoseGuard _seatedPoseGuard;
    private bool _activePassengerEntry;
    private CarEntrySideMode _activeEntrySide = CarEntrySideMode.DriverDoor;
    private bool _entryStopsAtPassengerCabin;
    private bool _passengerCarjackingSeatOccupied;
    private Quaternion _driverDoorClosedRotation;
    private Quaternion _passengerDoorClosedRotation;
    private Quaternion _rearLeftDoorClosedRotation;
    private Quaternion _rearRightDoorClosedRotation;
    private Vector3 _driverDoorClosedPosition;
    private Vector3 _passengerDoorClosedPosition;
    private Vector3 _rearLeftDoorClosedPosition;
    private Vector3 _rearRightDoorClosedPosition;
    private bool _closedDoorPosesCached;
    private CharacterModelSnapshot _lastBailoutModelSnapshot;
    private bool _hasWarnedMissingMovingExitHealth;
    private readonly SeatVisualFitSnapshot _driverVisualFit =
        new SeatVisualFitSnapshot();
    private readonly SeatVisualFitSnapshot _rearLeftVisualFit =
        new SeatVisualFitSnapshot();
    private readonly SeatVisualFitSnapshot _rearRightVisualFit =
        new SeatVisualFitSnapshot();
    private readonly SeatVisualFitSnapshot _passengerVisualFit =
        new SeatVisualFitSnapshot();
    private Character _pendingDriverVisualFit;
    private Character _pendingRearLeftVisualFit;
    private Character _pendingRearRightVisualFit;
    private bool _hasWarnedCabinFitLimit;

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

    private sealed class SeatVisualFitSnapshot
    {
        public Character character;
        public Transform model;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool active;

        public void Clear()
        {
            this.character = null;
            this.model = null;
            this.parent = null;
            this.localPosition = Vector3.zero;
            this.localRotation = Quaternion.identity;
            this.localScale = Vector3.one;
            this.active = false;
        }
    }

    private sealed class CharacterPhysicsSnapshot
    {
        public Character character;
        public bool driverCollision;
        public bool driverUpdateKinematics;
        public Character.MovementType movementType;
        public Animator animator;
        public AnimatorCullingMode animatorCullingMode;
        public bool hasAnimatorCullingSnapshot;
        public readonly List<ColliderSnapshot> colliders = new List<ColliderSnapshot>(8);
        public readonly List<RigidbodySnapshot> rigidbodies = new List<RigidbodySnapshot>(4);
    }

    public Character SeatedCharacter => _seatedChar;
    public Character RearLeftSeatedCharacter => _rearLeftSeatedChar;
    public Character RearRightSeatedCharacter => _rearRightSeatedChar;
    public Character RearPassengerCharacter =>
        _rearLeftSeatedChar?.Player != null
            ? _rearLeftSeatedChar
            : _rearRightSeatedChar?.Player != null
                ? _rearRightSeatedChar
                : _rearLeftSeatedChar ?? _rearRightSeatedChar;
    public bool IsTransitioning => isEntering || isExiting;
    public bool IsUsingMirroredEntry => IsRightSideEntry() &&
        mirroredEntryAnimation != null;
    public bool IsPassengerCarjackingSeatOccupied =>
        _passengerCarjackingSeatOccupied;

    public bool CanAnimateDoor(CarEntrySideMode side)
    {
        if (doorDamage == null) doorDamage = GetComponent<SimcadeCarDoorDamage>();
        return doorDamage == null || doorDamage.CanAnimateDoor(side);
    }

    public bool IsDoorMissing(CarEntrySideMode side)
    {
        if (doorDamage == null) doorDamage = GetComponent<SimcadeCarDoorDamage>();
        return doorDamage != null && doorDamage.IsDoorMissing(side);
    }

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
        if (character == null || character.Ragdoll.IsRagdoll ||
            IsTransitioning || IsCarDestroyed()) return false;
        CarEntrySideMode resolvedSide = ResolveRequestedEntrySide(
            character,
            requestedSide
        );
        if (resolvedSide == CarEntrySideMode.Automatic) return false;
        if (IsRearEntrySide(resolvedSide))
        {
            if (!CanUseRearSeat(resolvedSide)) return false;
            PreparePlayerVehiclePresentation(character);
            _ = EnterRearSeatAsync(character, resolvedSide);
            return true;
        }

        if (_seatedChar == null)
        {
            PreparePlayerVehiclePresentation(character);
            _ = EnterCarAsync(character, true, false, resolvedSide);
            return true;
        }

        if (_seatedChar == character) return false;
        if (occupiedEntryHandler == null) CacheVehicleBehaviours();
        if (occupiedEntryHandler == null) return false;
        if (character.IsPlayer)
        {
            (externalDriveController as SimcadeCarDriver)
                ?.PrepareGroundingForEntry();
        }
        bool accepted = occupiedEntryHandler.TryEnterOccupiedCar(
            character,
            resolvedSide
        );
        if (accepted) PreparePlayerVehiclePresentation(character);
        return accepted;
    }

    private void PreparePlayerVehiclePresentation(Character character)
    {
        if (character == null || !character.IsPlayer) return;
        (externalDriveController as SimcadeCarDriver)
            ?.PreparePresentationForEntry();
    }

    public bool RequestExit(Character character)
    {
        if (character == null || !IsCharacterSeated(character) || IsTransitioning)
            return false;
        ExitCar(character);
        return true;
    }

    public bool IsCharacterSeated(Character character)
    {
        return character != null &&
            (character == _seatedChar || character == _rearLeftSeatedChar ||
             character == _rearRightSeatedChar);
    }

    /// <summary>
    /// True only when this Car currently owns the Character's enter/exit or
    /// carjacking presentation. Unrelated pedestrians must remain valid impact
    /// targets even while another occupant is leaving a moving Car.
    /// </summary>
    public bool IsCharacterInTransition(Character character)
    {
        if (character == null) return false;
        if (character == _driverTransitionCharacter ||
            character == _activeExitCharacter ||
            character == _entryAlignCharacter ||
            character == _approachCharacter ||
            character == _passengerCarjackingPhysics?.character)
        {
            return true;
        }

        if (!isEntering && !isExiting) return false;
        return character == _seatedPhysics?.character ||
               character == _rearLeftPhysics?.character ||
               character == _rearRightPhysics?.character;
    }

    /// <summary>
    /// Animation-free driver release used before the project's death sequence
    /// snapshots the Humanoid skeleton for ragdoll. This mirrors BikeEntry's
    /// crash release while keeping the shared GC2 Character state authoritative.
    /// </summary>
    public bool ReleaseDriverForDeath(Character character)
    {
        return ReleaseDriverImmediately(character, true, false, true, false);
    }

    /// <summary>
    /// Detaches the driver when its external vehicle controller becomes
    /// unavailable. The overload without a Character also covers the short
    /// async window after a stopped exit has detached the seat reference.
    /// Teardown does not execute the normal onExit instruction list.
    /// </summary>
    public bool ReleaseDriverForUnavailableVehicle()
    {
        Character character = _seatedChar != null
            ? _seatedChar
            : _driverTransitionCharacter;
        return ReleaseDriverForUnavailableVehicle(character);
    }

    public bool ReleaseDriverForUnavailableVehicle(Character character)
    {
        return ReleaseDriverImmediately(character, false, true, true, false);
    }

    private bool ReleaseDriverImmediately(
        Character character,
        bool preserveVehicleMomentum,
        bool restoreCharacterControl,
        bool disableVehicleController,
        bool runExitInstructions)
    {
        if (character == null) return false;

        bool wasSeated = character == _seatedChar;
        bool ownsTrackedTransition = character == _driverTransitionCharacter;
        bool hasActiveRagdoll = character.Ragdoll.IsRagdoll;
        // Moving-exit owns its own launch/ragdoll sequence. The guarded
        // transition reference is installed only for entry and stopped exit.
        if (!wasSeated && !ownsTrackedTransition) return false;
        if (isExiting && !ownsTrackedTransition) return false;

        occupantCollisionPolicy?.BeginOccupantCollisionIgnore(character);
        try
        {
        InvalidateDriverTransition(character);
        if (_approachCharacter == character)
        {
            _approachCharacter = null;
            _approachFinished = true;
            _approachSucceeded = false;
        }
        if (_entryAlignCharacter == character) _entryAlignCharacter = null;

        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;
        EndDoorHandleIK();
        character.Gestures.Stop(0f, 0f);
        if (wasSeated) _seatedChar = null;

        // A death release keeps world momentum and gravity. An unavailable
        // controller is parked by its own disabled-state contract.
        if (disableVehicleController)
        {
            bool keepWorldMomentum = preserveVehicleMomentum &&
                externalDriveController?.IsVehicleEnabled == true;
            externalDriveController?.SetVehicleEnabled(
                false,
                keepWorldMomentum
            );
        }
        character.transform.SetParent(null, true);
        Transform exteriorPoint = GetActiveEntryStandingPoint();
        if (!hasActiveRagdoll &&
            (wasSeated || (ownsTrackedTransition && isExiting)) &&
            exteriorPoint != null)
        {
            character.transform.SetPositionAndRotation(
                exteriorPoint.position,
                exteriorPoint.rotation
            );
        }

        if (hasActiveRagdoll)
            RestoreCharacterPhysicsForActiveRagdoll(character);
        else
            RestoreCharacterPhysics(character);
        ReleaseEntryApproachMotion(character);
        if (_passengerCarjackingPhysics?.character == character)
            RestorePassengerCarjackingPhysics();
        if (!hasActiveRagdoll)
        {
            RestoreBailoutModelTransform(character);
            RestoreSharedCharacterModelBaseline(character);
        }
        ClearSteeringWheelIK(character);
        character.States.Stop(drivingStateLayer, 0f, 0f);

        if (cachedCarCollider != null) cachedCarCollider.isTrigger = false;
        if (externalDriveController == null && cachedCarRigidbody != null)
            cachedCarRigidbody.isKinematic = false;
        if (carController != null) carController.SetCarEnabled(false);

        if (character.Player != null)
        {
            character.Player.IsControllable = restoreCharacterControl &&
                !character.IsDead;
        }

        _passengerCarjackingSeatOccupied = false;
        if (_activeExitCharacter == character) _activeExitCharacter = null;
        isEntering = false;
        isExiting = false;
        ResetActiveEntrySide();
        Physics.SyncTransforms();
        if (runExitInstructions)
            _ = this.onExit.Run(new Args(this.gameObject));
        return true;
        }
        finally
        {
            occupantCollisionPolicy?.EndOccupantCollisionIgnore(character);
        }
    }

    private int BeginDriverTransition(Character character)
    {
        unchecked { _driverTransitionVersion++; }
        _driverTransitionCharacter = character;
        return _driverTransitionVersion;
    }

    private bool IsDriverTransitionCurrent(
        Character character,
        int version)
    {
        return character != null &&
            character == _driverTransitionCharacter &&
            version == _driverTransitionVersion;
    }

    private void CompleteDriverTransition(Character character, int version)
    {
        if (!IsDriverTransitionCurrent(character, version)) return;
        _driverTransitionCharacter = null;
    }

    private void InvalidateDriverTransition(Character character)
    {
        if (character != _driverTransitionCharacter) return;
        unchecked { _driverTransitionVersion++; }
        _driverTransitionCharacter = null;
    }

    private bool IsCarDestroyed()
    {
        if (destruction == null) destruction = GetComponent<SimcadeCarDestruction>();
        return destruction != null && destruction.IsDestroyed;
    }

    private bool IsLifecycleCurrent(int version)
    {
        return this != null && isActiveAndEnabled &&
               version == _lifecycleVersion;
    }

    private void AbortEntryForDestroyedCar(
        Character character,
        bool restorePassengerCarjackingPhysics)
    {
        // Stop every presentation owner before moving the non-seated Character
        // back outside. Collider/GC2 physics is restored only after that move so
        // an explosion cannot leave the Character overlapping the wreck body.
        EndDoorHandleIK();
        if (_entryAlignCharacter == character) _entryAlignCharacter = null;
        _entryStopsAtPassengerCabin = false;
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;

        Transform exteriorPoint = GetActiveEntryStandingPoint();
        if (character != null)
        {
            character.Gestures.Stop(0f, 0f);
            character.States.Stop(drivingStateLayer, 0f, 0f);
            character.transform.SetParent(null, true);
            if (exteriorPoint != null)
            {
                character.transform.SetPositionAndRotation(
                    exteriorPoint.position,
                    exteriorPoint.rotation
                );
            }
        }

        if (restorePassengerCarjackingPhysics)
            RestorePassengerCarjackingPhysics();
        if (character != null && character.Player != null)
            character.Player.IsControllable = !character.IsDead;

        _passengerCarjackingSeatOccupied = false;
        ResetActiveEntrySide();
        isEntering = false;
        Physics.SyncTransforms();
    }

    private void AbortEntryForRagdoll(Character character)
    {
        // GC2 already owns the detached Animator and bone physics. Only release
        // the Car presentation state; restoring its cached colliders here would
        // fight RagdollDefault's own collision lifecycle.
        EndDoorHandleIK();
        if (_entryAlignCharacter == character) _entryAlignCharacter = null;
        _entryStopsAtPassengerCabin = false;
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;
        if (character != null)
        {
            character.Gestures.Stop(0f, 0f);
            character.States.Stop(drivingStateLayer, 0f, 0f);
            if (character.Player != null)
                character.Player.IsControllable = !character.IsDead;
        }
        _passengerCarjackingSeatOccupied = false;
        ResetActiveEntrySide();
        isEntering = false;
    }

    private void AbortPassengerCarjackingForRagdoll(Character character)
    {
        if (character != null && character.Ragdoll.IsRagdoll)
            ReleasePassengerCarjackingPhysicsForRagdoll(character);
        else
            RestorePassengerCarjackingPhysics();
        AbortEntryForRagdoll(character);
    }

    private bool WasEntryRagdollInterrupted(Character character)
    {
        return _entryRagdollInterrupted || character == null ||
               character.Ragdoll.IsRagdoll;
    }

    /// <summary>
    /// Terminal, animation-free release used only when the car explodes. Physics
    /// is restored before GC2 ragdoll starts, and no automatic recovery is queued.
    /// </summary>
    public async Task<bool> ForceEjectForDestructionAsync(
        Character character,
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity)
    {
        if (character == null || !IsCharacterSeated(character)) return false;

        InvalidateDriverTransition(character);
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;
        EndDoorHandleIK();

        if (character == _seatedChar)
        {
            _seatedChar = null;
            character.transform.SetParent(null, true);
            RestoreCharacterPhysics(character);
            RestoreBailoutModelTransform(character);
            RestoreSharedCharacterModelBaseline(character);
        }
        else if (character == _rearLeftSeatedChar)
        {
            DetachRearPassenger(character, CarEntrySideMode.RearLeftDoor);
        }
        else if (character == _rearRightSeatedChar)
        {
            DetachRearPassenger(character, CarEntrySideMode.RearRightDoor);
        }

        ClearSteeringWheelIK(character);
        character.States.Stop(drivingStateLayer, 0f, 0f);
        character.transform.SetPositionAndRotation(position, rotation);
        if (character.Driver != null)
        {
            character.Driver.Collision = true;
            character.Driver.ForceGrounded(false);
        }
        if (character.Player != null) character.Player.IsControllable = false;
        isEntering = false;
        isExiting = false;
        Physics.SyncTransforms();

        bool hasRagdoll = character.Ragdoll.Get<RagdollDefault>() != null;
        if (!hasRagdoll) return true;
        if (!character.Ragdoll.IsRagdoll)
            await character.Ragdoll.StartRagdoll();

        if (character == null) return false;
        ApplyMovingExitRagdollVelocity(character, velocity);
        if (character.Player != null) character.Player.IsControllable = false;
        return true;
    }

    public bool IsRearPassenger(Character character)
    {
        return character != null &&
            (character == _rearLeftSeatedChar || character == _rearRightSeatedChar);
    }

    public bool CanRequestEnter(Character character, CarEntrySideMode requestedSide)
    {
        if (character == null || character.Ragdoll.IsRagdoll ||
            IsTransitioning || IsCharacterSeated(character) || IsCarDestroyed())
            return false;

        CarEntrySideMode resolved = ResolveRequestedEntrySide(character, requestedSide);
        if (IsRearEntrySide(resolved)) return CanUseRearSeat(resolved);
        if (resolved != CarEntrySideMode.DriverDoor &&
            resolved != CarEntrySideMode.PassengerDoor) return false;
        if (_seatedChar == null) return true;
        if (occupiedEntryHandler == null) CacheVehicleBehaviours();
        return occupiedEntryHandler != null;
    }

    public CarEntrySideMode ResolveEntrySide(
        Character character,
        CarEntrySideMode requestedSide = CarEntrySideMode.Automatic)
    {
        return ResolveRequestedEntrySide(character, requestedSide);
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
        _entryRagdollInterrupted = false;
        SelectEntrySide(character, false, requestedSide);
    }

    public void ClearPreparedEntrySide()
    {
        if (!isEntering)
        {
            _activePassengerEntry = false;
            _activeEntrySide = CarEntrySideMode.DriverDoor;
        }
        if (_seatedChar == null)
        {
            _passengerCarjackingSeatOccupied = false;
            _entryStopsAtPassengerCabin = false;
            _entryAlignCharacter = null;
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            EndDoorHandleIK();
        }
        if (_passengerCarjackingPhysics != null &&
            _seatedChar != _passengerCarjackingPhysics.character)
        {
            Character passengerCharacter = _passengerCarjackingPhysics.character;
            if (passengerCharacter != null &&
                passengerCharacter.Ragdoll.IsRagdoll)
            {
                ReleasePassengerCarjackingPhysicsForRagdoll(
                    passengerCharacter
                );
            }
            else
            {
                RestorePassengerCarjackingPhysics();
            }
        }
    }

    public bool IsPassengerEntryActive => _activePassengerEntry;
    public Transform ActiveEntryStandingPoint => GetActiveEntryStandingPoint();
    public Transform ActiveEntryDoor => GetActiveEntryDoor();
    public Vector3 ActiveEntryDoorOpenRotation => GetActiveEntryDoorOpenRotation();
    public Vector3 ActiveEntryDoorOpenLocalOffset =>
        GetActiveEntryDoorOpenLocalOffset();

    public void PlayActiveEntryDoorSound(bool opening)
    {
        PlayDoorSound(GetActiveEntryDoorAudioSource(), opening);
    }

    public void PlayEntryDoorSound(
        CarEntrySideMode side,
        bool opening)
    {
        AudioSource source = side switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorAudioSource,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorAudioSource,
            CarEntrySideMode.RearRightDoor => rearRightDoorAudioSource,
            _ => doorAudioSource
        };
        source ??= doorAudioSource;
        PlayDoorSound(source, opening);
    }

    public async Task<bool> OpenPreparedEntryDoorWithCharacterAsync(
        Character character)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (WasEntryRagdollInterrupted(character) ||
            IsCarDestroyed() || !IsLifecycleCurrent(lifecycleVersion)) return false;
        if (!CanAnimateDoor(_activeEntrySide)) return true;

        Transform door = GetActiveEntryDoor();
        AnimationClip clip = GetActiveEntryAnimation();
        if (door == null || clip == null) return false;

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
            if (WasEntryRagdollInterrupted(character) ||
                IsCarDestroyed() || !IsLifecycleCurrent(lifecycleVersion)) return false;
            PlayActiveEntryDoorSound(true);
            Task doorTask = AnimateDoorPose(
                door,
                GetActiveEntryDoorOpenRotation(),
                GetOpenDoorPosition(door),
                doorRotationDuration
            );
            await Task.WhenAll(gestureTask, doorTask);
            return !WasEntryRagdollInterrupted(character) && door != null &&
                   !IsCarDestroyed() && IsLifecycleCurrent(lifecycleVersion);
        }
        finally
        {
            if (IsLifecycleCurrent(lifecycleVersion)) EndDoorHandleIK();
        }
    }

    public async Task<bool> EnterPassengerSeatForCarjackingAsync(
        Character character)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (character == null || !_activePassengerEntry ||
            mirroredEntryAnimation == null || passengerEntryCabinPoint == null ||
            WasEntryRagdollInterrupted(character) || IsCarDestroyed() ||
            !IsLifecycleCurrent(lifecycleVersion))
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
            if (!IsLifecycleCurrent(lifecycleVersion)) return false;
            if (WasEntryRagdollInterrupted(character))
            {
                AbortPassengerCarjackingForRagdoll(character);
                return false;
            }
            if (IsCarDestroyed())
            {
                AbortEntryForDestroyedCar(character, true);
                return false;
            }
            Transform door = GetActiveEntryDoor();
            Task passengerStateWarmupTask = WarmPassengerDrivingStateNearEntryEndAsync(
                character,
                mirroredEntryAnimation,
                entryAnimationSpeed,
                animationStartedAt,
                lifecycleVersion
            );
            if (door != null && CanAnimateDoor(_activeEntrySide))
            {
                PlayActiveEntryDoorSound(true);
                await AnimateDoorPose(
                    door,
                    GetActiveEntryDoorOpenRotation(),
                    GetOpenDoorPosition(door),
                    doorRotationDuration
                );
            }
            if (!IsLifecycleCurrent(lifecycleVersion)) return false;
            if (WasEntryRagdollInterrupted(character))
            {
                AbortPassengerCarjackingForRagdoll(character);
                return false;
            }
            if (IsCarDestroyed())
            {
                AbortEntryForDestroyedCar(character, true);
                return false;
            }
            await animationTask;
            await passengerStateWarmupTask;
            if (!IsLifecycleCurrent(lifecycleVersion)) return false;
            EndDoorHandleIK();
            if (WasEntryRagdollInterrupted(character))
            {
                AbortPassengerCarjackingForRagdoll(character);
                return false;
            }
            if (IsCarDestroyed())
            {
                AbortEntryForDestroyedCar(character, true);
                return false;
            }
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
            if (character == null || passengerEntryCabinPoint == null ||
                WasEntryRagdollInterrupted(character) || IsCarDestroyed() ||
                !IsLifecycleCurrent(lifecycleVersion))
            {
                if (WasEntryRagdollInterrupted(character))
                    AbortPassengerCarjackingForRagdoll(character);
                if (IsCarDestroyed())
                    AbortEntryForDestroyedCar(character, true);
                _passengerCarjackingSeatOccupied = false;
                return false;
            }
            character.transform.SetPositionAndRotation(
                passengerEntryCabinPoint.position,
                passengerEntryCabinPoint.rotation
            );
            ApplySeatVisualFit(
                character,
                _passengerVisualFit,
                passengerSeatedCeilingAnchor
            );
            _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
                ? SeatedSkeletonPoseGuard.Prepare(character)
                : null;
            _seatedPoseGuard?.Capture();
            return !WasEntryRagdollInterrupted(character) && !IsCarDestroyed() &&
                   IsLifecycleCurrent(lifecycleVersion);
        }
        finally
        {
            if (IsLifecycleCurrent(lifecycleVersion))
            {
                EndDoorHandleIK();
                _entryStopsAtPassengerCabin = false;
            }
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
        int lifecycleVersion = _lifecycleVersion;
        if (character == null || entryParent == null || _seatedChar != null ||
            WasEntryRagdollInterrupted(character) || IsCarDestroyed() ||
            !IsLifecycleCurrent(lifecycleVersion))
        {
            if (WasEntryRagdollInterrupted(character))
                AbortPassengerCarjackingForRagdoll(character);
            if (IsCarDestroyed())
                AbortEntryForDestroyedCar(character, true);
            _passengerCarjackingSeatOccupied = false;
            return false;
        }

        RestoreSeatVisualFit(_passengerVisualFit);
        isEntering = true;
        int transitionVersion = BeginDriverTransition(character);
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
        while (elapsed < duration &&
               IsDriverTransitionCurrent(character, transitionVersion) &&
               IsLifecycleCurrent(lifecycleVersion) &&
               entryParent != null &&
               !WasEntryRagdollInterrupted(character) && !IsCarDestroyed())
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

        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            !IsLifecycleCurrent(lifecycleVersion))
            return false;

        if (character == null || entryParent == null ||
            WasEntryRagdollInterrupted(character) || IsCarDestroyed())
        {
            if (WasEntryRagdollInterrupted(character))
                AbortPassengerCarjackingForRagdoll(character);
            if (IsCarDestroyed())
                AbortEntryForDestroyedCar(character, true);
            _passengerCarjackingSeatOccupied = false;
            isEntering = false;
            CompleteDriverTransition(character, transitionVersion);
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
        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            !IsLifecycleCurrent(lifecycleVersion) ||
            _seatedChar != character)
        {
            return false;
        }
        if (character.IsDead)
        {
            ReleaseDriverForDeath(character);
            return false;
        }
        if (character.Ragdoll.IsRagdoll)
        {
            ReleaseDriverForUnavailableVehicle(character);
            return false;
        }

        if (IsCarDestroyed())
        {
            InvalidateDriverTransition(character);
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            ResetActiveEntrySide();
            isEntering = false;
            return false;
        }

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(true);
        else if (carController != null)
        {
            carController.enabled = true;
            carController.SetCarEnabled(true);
        }

        _seatedPoseGuard?.ReleaseAfter(seatedPoseGuardReleaseDelay);
        _seatedPoseGuard = null;
        _ = this.onEnter.Run(new Args(this.gameObject));
        ResetActiveEntrySide();
        isEntering = false;
        CompleteDriverTransition(character, transitionVersion);
        return true;
    }

    /// <summary>
    /// Places a GC2 NPC in the driver seat without enabling player vehicle input.
    /// Used by the Sim-Cade carjacking setup; normal player entry still goes
    /// through EnterCar and keeps its original door/entry sequence.
    /// </summary>
    public bool SeatOccupantInstant(Character character)
    {
        if (character == null || IsTransitioning || IsCarDestroyed() ||
            _seatedChar != null || entryParent == null)
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
        RestoreSeatVisualFit(_driverVisualFit);
        ClearSteeringWheelIK(character);

        if (character.Player != null) character.Player.IsControllable = false;

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
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
        if (character.Ragdoll.IsRagdoll)
        {
            AbortOccupantReleaseForRagdoll(character);
            return;
        }
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
        if (character.Ragdoll.IsRagdoll)
        {
            AbortOccupantReleaseForRagdoll(character);
            return;
        }
        character.transform.SetPositionAndRotation(position, rotation);
        character.Driver?.ResetVerticalVelocity();
        Physics.SyncTransforms();

        await Task.Yield();
        if (character == null || character.Ragdoll.IsRagdoll) return;
        if (character.Driver != null)
        {
            character.Driver.ResetVerticalVelocity();
            character.Driver.ForceGrounded(false);
        }
    }

    public void AbortOccupantReleaseForRagdoll(Character character)
    {
        CharacterPhysicsSnapshot snapshot = _seatedPhysics;
        if (snapshot == null || snapshot.character != character) return;

        if (_driverVisualFit.character == character)
            RestoreSeatVisualFit(_driverVisualFit);
        RestoreCharacterPhysicsSnapshot(snapshot, false, true);
        _seatedPhysics = null;
        character?.States.Stop(drivingStateLayer, 0f, 0f);
        if (character?.Player != null)
            character.Player.IsControllable = true;
    }

    private void Awake()
    {
        carController = GetComponent<PhysicsCarController>();
        cachedCarCollider = GetComponent<BoxCollider>();
        cachedCarRigidbody = GetComponent<Rigidbody>();
        destruction = GetComponent<SimcadeCarDestruction>();
        doorDamage = GetComponent<SimcadeCarDoorDamage>();
        CacheVehicleBehaviours();
        CacheClosedDoorPoses();

        Transform vehicleBody = externalDriveController != null
            ? externalDriveController.VehicleBody
            : carController != null
                ? carController.carBody
                : null;

        if (entryParent != null && vehicleBody != null)
        {
            entryParent.SetParent(vehicleBody, true);
        }
        if (rearLeftSeatParent != null && vehicleBody != null)
            rearLeftSeatParent.SetParent(vehicleBody, true);
        if (rearRightSeatParent != null && vehicleBody != null)
            rearRightSeatParent.SetParent(vehicleBody, true);
    }

    private void OnDisable()
    {
        unchecked { _lifecycleVersion++; }
        EndDoorHandleIK();
        RestoreClosedDoorPoseIfAnimatable(doorTransform);
        RestoreClosedDoorPoseIfAnimatable(passengerDoorTransform);
        RestoreClosedDoorPoseIfAnimatable(rearLeftDoorTransform);
        RestoreClosedDoorPoseIfAnimatable(rearRightDoorTransform);
        Character transitionCharacter = _driverTransitionCharacter;
        if (transitionCharacter != null)
        {
            // Pooling/streaming may disable a Car between two awaits. Invalidate
            // that operation and release the non-terminal Character immediately;
            // otherwise its continuation could attach to an inactive vehicle.
            ReleaseDriverForUnavailableVehicle(transitionCharacter);
        }
        else if (isEntering || _entryAlignCharacter != null ||
                 _approachCharacter != null ||
                 _passengerCarjackingPhysics != null)
        {
            Character rearEnteringCharacter = isEntering
                ? _activeEntrySide == CarEntrySideMode.RearLeftDoor
                    ? _rearLeftSeatedChar
                    : _activeEntrySide == CarEntrySideMode.RearRightDoor
                        ? _rearRightSeatedChar
                        : null
                : null;
            Character enteringCharacter = _entryAlignCharacter ??
                _approachCharacter ?? _passengerCarjackingPhysics?.character ??
                rearEnteringCharacter;
            if (_approachCharacter != null)
            {
                Character approachCharacter = _approachCharacter;
                _approachCharacter = null;
                _approachFinished = true;
                _approachSucceeded = false;
                ReleaseEntryApproachMotion(approachCharacter);
            }
            _entryAlignCharacter = null;
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            if (rearEnteringCharacter != null)
            {
                DetachRearPassenger(
                    rearEnteringCharacter,
                    _activeEntrySide
                );
            }
            if (_passengerCarjackingPhysics != null)
            {
                Character passenger = _passengerCarjackingPhysics.character;
                if (passenger != null && passenger.Ragdoll.IsRagdoll)
                    ReleasePassengerCarjackingPhysicsForRagdoll(passenger);
                else
                    RestorePassengerCarjackingPhysics();
            }
            if (enteringCharacter != null)
            {
                enteringCharacter.Gestures.Stop(0f, 0f);
                enteringCharacter.States.Stop(drivingStateLayer, 0f, 0f);
                if (enteringCharacter.Player != null)
                {
                    enteringCharacter.Player.IsControllable =
                        !enteringCharacter.IsDead &&
                        !enteringCharacter.Ragdoll.IsRagdoll;
                }
            }
            if (cachedCarCollider != null) cachedCarCollider.isTrigger = false;
            if (cachedCarRigidbody != null) cachedCarRigidbody.isKinematic = false;
            _passengerCarjackingSeatOccupied = false;
            isEntering = false;
            isExiting = false;
            ResetActiveEntrySide();
            Physics.SyncTransforms();
        }

        _entryRagdollInterrupted = false;
        _entryStopsAtPassengerCabin = false;
        _activeExitCharacter = null;
        _pendingDriverVisualFit = null;
        _pendingRearLeftVisualFit = null;
        _pendingRearRightVisualFit = null;
        RestoreSeatVisualFit(_driverVisualFit);
        RestoreSeatVisualFit(_rearLeftVisualFit);
        RestoreSeatVisualFit(_rearRightVisualFit);
        RestoreSeatVisualFit(_passengerVisualFit);
    }

    private void OnEnable()
    {
        // Pool/stream reactivation may keep an occupied Car instance. OnDisable
        // restores the avatar's authored model transform; queue the one-shot fit
        // again so the first visible seated frame still respects this cabin.
        if (_seatedChar != null) _pendingDriverVisualFit = _seatedChar;
        if (_rearLeftSeatedChar != null)
            _pendingRearLeftVisualFit = _rearLeftSeatedChar;
        if (_rearRightSeatedChar != null)
            _pendingRearRightVisualFit = _rearRightSeatedChar;
    }

    private void LateUpdate()
    {
        if (!isEntering && !isExiting &&
            _entryAlignCharacter == null && _doorHandleIKSetter == null &&
            _seatedChar == null && _rearLeftSeatedChar == null &&
            _rearRightSeatedChar == null &&
            _pendingDriverVisualFit == null &&
            _pendingRearLeftVisualFit == null &&
            _pendingRearRightVisualFit == null)
        {
            return;
        }

        UpdateEntrySeatAlignment();
        UpdateDoorHandleIK();
        ApplyPendingSeatVisualFits();

        if (_seatedChar != null)
        {
            KeepPassengerOnSeat(_seatedChar);
        }
        KeepPassengerOnSeat(_rearLeftSeatedChar);
        KeepPassengerOnSeat(_rearRightSeatedChar);
    }

    public void EnterCar(Character character)
    {
        RequestEnter(character, CarEntrySideMode.Automatic);
    }

    private async Task<bool> EnterCarAsync(
        Character character,
        bool runDoorSequence,
        bool forceDriverDoor = false,
        CarEntrySideMode requestedSide = CarEntrySideMode.Automatic)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (isEntering || character == null || character.IsDead ||
            character.Ragdoll.IsRagdoll ||
            (_seatedChar != null && _seatedChar != character) ||
            IsCarDestroyed()) return false;
        if (IsRearEntrySide(requestedSide)) return false;
        _entryRagdollInterrupted = false;
        RestoreSharedCharacterModelBaseline(character);
        RestoreBailoutModelTransform(character);
        isEntering = true;
        int transitionVersion = BeginDriverTransition(character);
        SelectEntrySide(character, forceDriverDoor, requestedSide);

        occupantCollisionPolicy?.BeginOccupantCollisionIgnore(character);
        try
        {

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (carController != null) carController.enabled = false;

        if (character.Player != null) character.Player.IsControllable = false;
        _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
            ? SeatedSkeletonPoseGuard.Prepare(character)
            : null;
        bool reachedStandingPoint =
            await MoveCharacterToEntryStandingPointAsync(character);
        if (!IsDriverTransitionCurrent(character, transitionVersion))
            return false;
        if (!reachedStandingPoint)
        {
            if (character != null && character.Player != null)
                character.Player.IsControllable = !character.IsDead &&
                    !character.Ragdoll.IsRagdoll;
            _seatedPoseGuard = null;
            ResetActiveEntrySide();
            isEntering = false;
            CompleteDriverTransition(character, transitionVersion);
            return false;
        }
        if (character.IsDead)
        {
            ReleaseDriverForDeath(character);
            return false;
        }
        if (character.Ragdoll.IsRagdoll)
        {
            ReleaseDriverForUnavailableVehicle(character);
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
            if (runDoorSequence)
            {
                await Task.Yield();
                if (!IsDriverTransitionCurrent(character, transitionVersion))
                    return false;
                if (IsCarDestroyed())
                {
                    _seatedPoseGuard?.Cancel();
                    _seatedPoseGuard = null;
                    if (character != null && character.Player != null)
                        character.Player.IsControllable = !character.IsDead &&
                            !character.Ragdoll.IsRagdoll;
                    ResetActiveEntrySide();
                    isEntering = false;
                    CompleteDriverTransition(character, transitionVersion);
                    return false;
                }
            }
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
        if (runDoorSequence && activeDoor != null &&
            CanAnimateDoor(_activeEntrySide))
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
            entryAnimationSpeed,
            lifecycleVersion
        );
        await animTask;
        if (!IsDriverTransitionCurrent(character, transitionVersion))
            return false;
        EndDoorHandleIK();
        if (character.IsDead)
        {
            ReleaseDriverForDeath(character);
            return false;
        }

        // The car can reach terminal health while this async animation is
        // waiting. Never attach a Character to a wreck after its one-shot
        // destruction/ejection sequence has already completed.
        if (IsCarDestroyed())
        {
            InvalidateDriverTransition(character);
            if (carCollider != null) carCollider.isTrigger = false;
            if (carRigidbody != null) carRigidbody.isKinematic = false;
            if (playerCollider != null) playerCollider.enabled = true;
            AbortEntryForDestroyedCar(character, false);
            return false;
        }
        if (_entryRagdollInterrupted || character.Ragdoll.IsRagdoll)
        {
            InvalidateDriverTransition(character);
            if (carCollider != null) carCollider.isTrigger = false;
            if (carRigidbody != null) carRigidbody.isKinematic = false;
            AbortEntryForRagdoll(character);
            return false;
        }
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
        AttachCharacterToSeat(character);

        // Let the Animator evaluate the seated state for a complete rendered
        // frame before Sim-Cade activates its chase camera.
        await Task.Yield();
        await Task.Yield();
        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            _seatedChar != character)
        {
            return false;
        }
        if (character.IsDead)
        {
            ReleaseDriverForDeath(character);
            return false;
        }
        if (character.Ragdoll.IsRagdoll)
        {
            ReleaseDriverForUnavailableVehicle(character);
            return false;
        }

        // If destruction happened after attachment, ForceEject already detached
        // the occupant synchronously. Only suppress this stale continuation so
        // it cannot re-enable the terminal vehicle controller.
        if (IsCarDestroyed())
        {
            InvalidateDriverTransition(character);
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            ResetActiveEntrySide();
            isEntering = false;
            return false;
        }

        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(true);
        else if (carController != null)
        {
            carController.enabled = true;
            carController.SetCarEnabled(true);
        }

        _seatedPoseGuard?.ReleaseAfter(seatedPoseGuardReleaseDelay);
        _seatedPoseGuard = null;

        _ = this.onEnter.Run(new Args(this.gameObject));
        ResetActiveEntrySide();
        isEntering = false;
        CompleteDriverTransition(character, transitionVersion);
        return true;
        }
        finally
        {
            occupantCollisionPolicy?.EndOccupantCollisionIgnore(character);
        }
    }

    private async Task<bool> EnterRearSeatAsync(
        Character character,
        CarEntrySideMode side)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (isEntering || character == null || character.Ragdoll.IsRagdoll ||
            !IsRearEntrySide(side) ||
            !CanUseRearSeat(side) || IsCarDestroyed()) return false;

        _entryRagdollInterrupted = false;
        RestoreSharedCharacterModelBaseline(character);
        RestoreBailoutModelTransform(character);
        SelectEntrySide(character, false, side);
        Transform seat = GetActiveSeatParent();
        AnimationClip clip = GetActiveEntryAnimation();
        if (seat == null || clip == null)
        {
            ResetActiveEntrySide();
            return false;
        }

        isEntering = true;
        if (character.Player != null) character.Player.IsControllable = false;
        _seatedPoseGuard = externalDriveController?.UseSeatEntryAlignment == true
            ? SeatedSkeletonPoseGuard.Prepare(character)
            : null;

        if (!await MoveCharacterToEntryStandingPointAsync(character))
        {
            if (character != null && character.Player != null)
                character.Player.IsControllable = true;
            _seatedPoseGuard = null;
            ResetActiveEntrySide();
            isEntering = false;
            return false;
        }
        if (!IsLifecycleCurrent(lifecycleVersion)) return false;

        BoxCollider carCollider = cachedCarCollider;
        Rigidbody carRigidbody = cachedCarRigidbody;
        bool madeCarTrigger = carCollider != null && !carCollider.isTrigger;
        bool madeCarKinematic = carRigidbody != null && !carRigidbody.isKinematic;
        if (madeCarTrigger) carCollider.isTrigger = true;
        if (madeCarKinematic) carRigidbody.isKinematic = true;
        Physics.SyncTransforms();

        var seatedConfig = new ConfigState(
            0f, 1f, 1f,
            drivingStateTransitionIn,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState,
            drivingStateLayer,
            BlendMode.Blend,
            seatedConfig
        );

        Transform activeDoor = GetActiveEntryDoor();
        if (activeDoor != null && CanAnimateDoor(_activeEntrySide))
        {
            _ = DoorRotationSequence(
                activeDoor,
                GetActiveEntryDoorOpenRotation(),
                GetActiveEntryDoorAudioSource()
            );
        }

        BeginDoorHandleIK(
            character,
            clip,
            entryDoorHandleIKCurve,
            true,
            entryAnimationSpeed
        );
        BeginEntrySeatAlignment(character, clip, entryAnimationSpeed);
        Task animationTask = PlayAnimation(
            character,
            clip,
            entryAnimationTransitionIn,
            entryAnimationTransitionOut,
            entryAnimationSpeed
        );
        await CaptureSeatedPoseNearAnimationEndAsync(
            character,
            clip,
            entryAnimationSpeed,
            lifecycleVersion
        );
        await animationTask;
        if (!IsLifecycleCurrent(lifecycleVersion)) return false;
        EndDoorHandleIK();

        if (IsCarDestroyed())
        {
            if (madeCarTrigger && carCollider != null)
                carCollider.isTrigger = false;
            if (madeCarKinematic && carRigidbody != null)
                carRigidbody.isKinematic = false;
            AbortEntryForDestroyedCar(character, false);
            return false;
        }
        if (_entryRagdollInterrupted || character.Ragdoll.IsRagdoll)
        {
            if (madeCarTrigger && carCollider != null)
                carCollider.isTrigger = false;
            if (madeCarKinematic && carRigidbody != null)
                carRigidbody.isKinematic = false;
            AbortEntryForRagdoll(character);
            return false;
        }
        CompleteEntrySeatAlignment(character);

        var finalSeatedConfig = new ConfigState(
            0f, 1f, 1f,
            0f,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            drivingState,
            drivingStateLayer,
            BlendMode.Blend,
            finalSeatedConfig
        );
        AttachRearPassengerToSeat(character, side);

        if (madeCarTrigger && carCollider != null) carCollider.isTrigger = false;
        if (madeCarKinematic && carRigidbody != null) carRigidbody.isKinematic = false;
        (externalDriveController as SimcadeCarDriver)
            ?.FinalizeParkedPoseAfterKinematicTransition();
        Physics.SyncTransforms();

        await Task.Yield();
        await Task.Yield();
        if (!IsLifecycleCurrent(lifecycleVersion)) return false;
        if (IsCarDestroyed())
        {
            _seatedPoseGuard?.Cancel();
            _seatedPoseGuard = null;
            ResetActiveEntrySide();
            isEntering = false;
            return false;
        }
        // Re-sample after GC2 has evaluated the persistent seated state. The
        // Player model uses a -1 Y offset, so fixed Character-root coordinates
        // are not reliable lap positions across Humanoid avatars.
        ConfigureRearLapIK(character, side);
        (externalDriveController as SimcadeCarDriver)
            ?.SetPassengerPresentation(true);

        _seatedPoseGuard?.ReleaseAfter(seatedPoseGuardReleaseDelay);
        _seatedPoseGuard = null;
        _ = this.onEnter.Run(new Args(this.gameObject));
        ResetActiveEntrySide();
        isEntering = false;
        return true;
    }

    private async Task ExitRearSeatAsync(Character character)
    {
        CarEntrySideMode side = character == _rearLeftSeatedChar
            ? CarEntrySideMode.RearLeftDoor
            : CarEntrySideMode.RearRightDoor;
        SelectEntrySide(character, false, side);

        try
        {
            float deadline = Time.unscaledTime + Mathf.Max(0.1f, exitStopTimeout);
            while (character != null && IsRearPassenger(character) &&
                   GetVehicleSpeedMetersPerSecond() * 3.6f > stoppedExitSpeedKph)
            {
                if (Time.unscaledTime >= deadline) return;
                await Task.Yield();
            }
            if (character == null || !IsRearPassenger(character)) return;

            (externalDriveController as SimcadeCarDriver)
                ?.SetPassengerPresentation(false);

            BoxCollider carCollider = cachedCarCollider;
            Rigidbody carRigidbody = cachedCarRigidbody;
            bool madeCarTrigger = carCollider != null && !carCollider.isTrigger;
            bool madeCarKinematic = carRigidbody != null && !carRigidbody.isKinematic;
            if (madeCarTrigger) carCollider.isTrigger = true;
            if (madeCarKinematic) carRigidbody.isKinematic = true;
            Physics.SyncTransforms();

            DetachRearPassenger(character, side);
            character.States.Stop(
                drivingStateLayer,
                0f,
                drivingStateTransitionOut
            );

            Transform activeDoor = GetActiveEntryDoor();
            if (activeDoor != null && CanAnimateDoor(_activeEntrySide))
            {
                _ = DoorRotationSequence(
                    activeDoor,
                    GetActiveEntryDoorOpenRotation(),
                    GetActiveEntryDoorAudioSource()
                );
            }

            AnimationClip clip = GetActiveExitAnimation();
            BeginDoorHandleIK(
                character,
                clip,
                exitDoorHandleIKCurve,
                false,
                exitAnimationSpeed
            );
            await PlayAnimation(
                character,
                clip,
                exitAnimationTransitionIn,
                exitAnimationTransitionOut,
                exitAnimationSpeed
            );
            EndDoorHandleIK();

            if (character.Player != null)
                character.Player.IsControllable = true;
            if (madeCarTrigger && carCollider != null) carCollider.isTrigger = false;
            if (madeCarKinematic && carRigidbody != null)
                carRigidbody.isKinematic = false;
            (externalDriveController as SimcadeCarDriver)
                ?.FinalizeParkedPoseAfterKinematicTransition();
            Physics.SyncTransforms();
            _ = this.onExit.Run(new Args(this.gameObject));
        }
        finally
        {
            if (_activeExitCharacter == character) _activeExitCharacter = null;
            ResetActiveEntrySide();
            isExiting = false;
        }
    }

    public void ExitCar(Character character)
    {
        if (isExiting || character == null || !IsCharacterSeated(character)) return;
        _seatedPoseGuard?.Cancel();
        _seatedPoseGuard = null;
        isExiting = true;
        _activeExitCharacter = character;
        if (character == _rearLeftSeatedChar || character == _rearRightSeatedChar)
        {
            _ = ExitRearSeatAsync(character);
            return;
        }
        _ = ExitCarBySpeedAsync(character);
    }

    private async Task ExitCarBySpeedAsync(Character character)
    {
        bool exitCompleted = false;
        int stoppedExitVersion = 0;
        try
        {
            // Only the exact Sim-Cade car exposes speed-aware braking and
            // momentum preservation. Other RVR vehicle prefabs keep their
            // original immediate exit path.
            if (externalDriveController == null)
            {
                stoppedExitVersion = BeginDriverTransition(character);
                exitCompleted = await ExitStoppedCarAsync(
                    character,
                    stoppedExitVersion
                );
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

            stoppedExitVersion = BeginDriverTransition(character);
            if (!await WaitForVehicleStopAsync(character, stoppedExitVersion))
                return;
            exitCompleted = await ExitStoppedCarAsync(
                character,
                stoppedExitVersion
            );
        }
        finally
        {
            bool ownsStoppedExit = stoppedExitVersion == 0 ||
                IsDriverTransitionCurrent(character, stoppedExitVersion);
            if (!exitCompleted && externalDriveController != null &&
                ownsStoppedExit)
            {
                externalDriveController.CancelExitStop();
            }
            if (stoppedExitVersion != 0 && ownsStoppedExit)
                CompleteDriverTransition(character, stoppedExitVersion);
            if (ownsStoppedExit)
            {
                if (_activeExitCharacter == character)
                    _activeExitCharacter = null;
                isExiting = false;
            }
        }
    }

    private async Task<bool> WaitForVehicleStopAsync(
        Character character,
        int transitionVersion)
    {
        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            character != _seatedChar)
        {
            return false;
        }
        if (GetVehicleSpeedMetersPerSecond() * 3.6f <= stoppedExitSpeedKph)
            return true;

        externalDriveController?.BeginExitStop();
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, exitStopTimeout);
        while (IsDriverTransitionCurrent(character, transitionVersion) &&
               character == _seatedChar &&
               GetVehicleSpeedMetersPerSecond() * 3.6f > stoppedExitSpeedKph)
        {
            if (Time.unscaledTime >= deadline) return false;
            await Task.Yield();
        }

        return IsDriverTransitionCurrent(character, transitionVersion) &&
            character == _seatedChar;
    }

    private async Task<bool> ExitStoppedCarAsync(
        Character character,
        int transitionVersion)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            !IsLifecycleCurrent(lifecycleVersion) || character != _seatedChar)
        {
            return false;
        }
        occupantCollisionPolicy?.BeginOccupantCollisionIgnore(character);
        try
        {
        if (externalDriveController != null)
            externalDriveController.SetVehicleEnabled(false, false);

        var carCollider = cachedCarCollider;
        var carRigidbody = cachedCarRigidbody;
        var playerCollider = character.GetComponent<Collider>();
        bool useAuthoredExitPath = ShouldUseAuthoredStoppedExitPath();

        // The passenger-carjacking snapshot remembers the Player's colliders in
        // their original enabled state. Make the vehicle non-blocking before that
        // snapshot is restored; otherwise Unity resolves one frame of overlap by
        // lifting the still-seated Player onto the roof.
        if (carCollider != null) carCollider.isTrigger = true;
        if (carRigidbody != null) carRigidbody.isKinematic = true;
        Physics.SyncTransforms();

        _seatedChar = null;
        character.transform.SetParent(null, true);
        if (useAuthoredExitPath)
        {
            // The no-root-motion rotorcraft path keeps GC2 collision, gravity
            // and kinematics locked until the Character is fully outside. If
            // these are restored at the interior seat, PhysX depenetrates the
            // Player onto the roof/rotor as soon as the collision lease ends.
            if (_driverVisualFit.character == character)
                RestoreSeatVisualFit(_driverVisualFit);
        }
        else
        {
            RestoreCharacterPhysics(character);
        }
        ClearSteeringWheelIK(character);

        if (!useAuthoredExitPath)
        {
            if (playerCollider != null) playerCollider.enabled = true;
            if (character.Driver != null) character.Driver.Collision = true;
            Physics.SyncTransforms();
        }

        character.States.Stop(drivingStateLayer, 0f, drivingStateTransitionOut);

        if (doorTransform != null && CanAnimateDoor(CarEntrySideMode.DriverDoor))
            _ = DoorRotationSequence();

        BeginDoorHandleIK(
            character,
            exitAnimation,
            exitDoorHandleIKCurve,
            false,
            exitAnimationSpeed
        );
        Task exitAnimationTask = PlayAnimation(
            character, exitAnimation,
            exitAnimationTransitionIn,
            exitAnimationTransitionOut,
            exitAnimationSpeed
        );
        bool exitPathCompleted = !useAuthoredExitPath ||
            await FollowAuthoredStoppedExitPathAsync(
                character,
                exitAnimation,
                exitAnimationSpeed,
                transitionVersion,
                lifecycleVersion
            );
        await exitAnimationTask;
        if (!IsDriverTransitionCurrent(character, transitionVersion) ||
            !IsLifecycleCurrent(lifecycleVersion))
            return false;
        if (character.IsDead)
        {
            ReleaseDriverForDeath(character);
            return false;
        }
        if (character.Ragdoll.IsRagdoll)
        {
            ReleaseDriverForUnavailableVehicle(character);
            return false;
        }
        if (!exitPathCompleted)
        {
            // A destroyed or otherwise invalidated no-root-motion path can
            // still own the current transition. Route it through the same
            // deterministic teardown instead of leaving the seated physics
            // snapshot locked on the detached Character.
            ReleaseDriverForUnavailableVehicle(character);
            return false;
        }
        EndDoorHandleIK();

        if (useAuthoredExitPath)
        {
            // The root is now exactly at the exterior anchor. Restore the
            // authoritative GC2 snapshot while collision is still ignored.
            RestoreCharacterPhysics(character);
            ReleaseEntryApproachMotion(character);
            // Older live sessions may already contain a snapshot captured
            // after the runtime CharacterController had been disabled. Make
            // the exterior hand-off authoritative so hot reload does not leave
            // an otherwise controllable Player unable to move.
            if (playerCollider != null) playerCollider.enabled = true;
            if (character.Driver != null) character.Driver.Collision = true;
            character.Driver?.ResetVerticalVelocity();
            Physics.SyncTransforms();
        }

        if (carCollider != null) carCollider.isTrigger = false;
        if (carRigidbody != null) carRigidbody.isKinematic = false;
        (externalDriveController as SimcadeCarDriver)
            ?.FinalizeParkedPoseAfterKinematicTransition();
        Physics.SyncTransforms();

        if (character.Player != null)
            character.Player.IsControllable = true;

        if (carController != null)
            carController.SetCarEnabled(false);

        _ = this.onExit.Run(new Args(this.gameObject));
        return true;
        }
        finally
        {
            occupantCollisionPolicy?.EndOccupantCollisionIgnore(character);
        }
    }

    private bool ShouldUseAuthoredStoppedExitPath()
    {
        // Root-motion Car prefabs keep their existing behavior. The
        // Gyrocopter is the only current no-root-motion prefab and uses its
        // deterministic entry anchors in reverse for exit.
        return !useRootMotion && useAuthoredEntryPath &&
               externalDriveController?.UseSeatEntryAlignment == true &&
               GetActiveSeatParent() != null &&
               GetActiveEntryStepPoint() != null &&
               GetActiveEntryStandingPoint() != null;
    }

    private async Task<bool> FollowAuthoredStoppedExitPathAsync(
        Character character,
        AnimationClip clip,
        float speed,
        int transitionVersion,
        int lifecycleVersion)
    {
        if (!CanContinueAuthoredStoppedExit(
                character,
                transitionVersion,
                lifecycleVersion))
        {
            return false;
        }

        float duration = clip != null
            ? clip.length / Mathf.Max(0.01f, speed)
            : 0f;
        float startedAt = Time.time;
        float progress = duration <= 0f ? 1f : 0f;

        while (progress < 1f)
        {
            if (!CanContinueAuthoredStoppedExit(
                    character,
                    transitionVersion,
                    lifecycleVersion))
            {
                return false;
            }

            progress = Mathf.Clamp01(
                (Time.time - startedAt) / Mathf.Max(0.01f, duration)
            );
            // Entry evaluates Standing -> Step -> Seat. Reading the same
            // authored curve backwards gives Seat -> Step -> Standing.
            EvaluateAuthoredEntryPath(
                1f - progress,
                out Vector3 position,
                out Quaternion rotation
            );
            character.transform.SetPositionAndRotation(position, rotation);
            if (progress < 1f) await Task.Yield();
        }

        if (!CanContinueAuthoredStoppedExit(
                character,
                transitionVersion,
                lifecycleVersion))
        {
            return false;
        }

        Transform exteriorPoint = GetActiveEntryStandingPoint();
        character.transform.SetPositionAndRotation(
            exteriorPoint.position,
            exteriorPoint.rotation
        );
        Physics.SyncTransforms();
        return true;
    }

    private bool CanContinueAuthoredStoppedExit(
        Character character,
        int transitionVersion,
        int lifecycleVersion)
    {
        return character != null && !character.IsDead &&
               !character.Ragdoll.IsRagdoll && !IsCarDestroyed() &&
               IsLifecycleCurrent(lifecycleVersion) &&
               IsDriverTransitionCurrent(character, transitionVersion);
    }

    private async Task ExitMovingCarAsync(Character character)
    {
        Vector3 vehicleVelocity = cachedCarRigidbody != null
            ? cachedCarRigidbody.linearVelocity
            : Vector3.zero;
        float bailoutSpeedKph = Vector3.ProjectOnPlane(
            vehicleVelocity,
            Vector3.up
        ).magnitude * 3.6f;

        SimcadeCarDriver bailoutCameraDriver =
            externalDriveController as SimcadeCarDriver;
        bailoutCameraDriver?.BeginBailoutCameraHold();
        bailoutCameraDriver?.KeepEngineRunningAfterBailout();
        externalDriveController?.SetVehicleEnabled(false, true);
        ClearSteeringWheelIK(character);
        _lastBailoutModelSnapshot = CaptureBailoutModelTransform(character);

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

        Transform bailoutDoor = CanAnimateDoor(CarEntrySideMode.DriverDoor)
            ? doorTransform
            : null;
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
                ApplyMovingExitTraitsDamage(character, bailoutSpeedKph);
                ApplyMovingExitRagdollVelocity(character, bailoutVelocity);
                // Ragdoll ignores locomotion input. The Player flag may safely
                // unlock interaction state now; Sim-Cade retains camera ownership
                // independently until the configured two-second hold expires.
                if (character.Player != null)
                    character.Player.IsControllable = true;

                await WaitUnscaledSecondsAsync(movingExitRagdollDuration);
                if (character != null && !character.IsDead && movingExitAutoRecover &&
                    character.Ragdoll.IsRagdoll)
                {
                    await character.Ragdoll.StartRecover();
                    RestoreSharedCharacterModelBaseline(character);
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
            ApplyMovingExitTraitsDamage(character, bailoutSpeedKph);
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

    private void ApplyMovingExitTraitsDamage(Character character, float speedKph)
    {
        if (character?.Player == null || speedKph <= fastExitSpeedKph) return;

        float damage = movingExitBaseDamage + Mathf.Max(
            0f,
            speedKph - fastExitSpeedKph
        ) * movingExitDamagePerKphAboveThreshold;
        damage = Mathf.Clamp(damage, 0f, movingExitMaximumDamage);
        if (damage <= 0f) return;

        Traits traits = character.GetComponent<Traits>();
        if (traits == null)
        {
            WarnMissingMovingExitHealth(character, "Traits component is missing");
            return;
        }

        try
        {
            RuntimeAttributeData health = traits.RuntimeAttributes.Get(
                movingExitHealthAttributeId
            );
            if (health == null)
            {
                WarnMissingMovingExitHealth(
                    character,
                    $"Attribute '{movingExitHealthAttributeId}' is missing"
                );
                return;
            }
            health.Value -= damage;
        }
        catch (Exception exception)
        {
            WarnMissingMovingExitHealth(character, exception.Message);
        }
    }

    private void WarnMissingMovingExitHealth(Character character, string reason)
    {
        if (_hasWarnedMissingMovingExitHealth) return;
        _hasWarnedMissingMovingExitHealth = true;
        Debug.LogWarning(
            $"High-speed bailout could not damage Player Traits " +
            $"'{movingExitHealthAttributeId}': {reason}",
            character
        );
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(movingExitHealthAttributeId))
            movingExitHealthAttributeId = "hp";
        movingExitBaseDamage = Mathf.Max(0f, movingExitBaseDamage);
        movingExitDamagePerKphAboveThreshold = Mathf.Max(
            0f,
            movingExitDamagePerKphAboveThreshold
        );
        movingExitMaximumDamage = Mathf.Max(
            movingExitBaseDamage,
            movingExitMaximumDamage
        );
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

        RestoreSeatVisualFitForCharacter(character);
        RestoreBailoutModelTransform(character);
        RestoreSharedCharacterModelBaseline(character);
        LockCharacterPhysics(character);

        character.transform.SetParent(entryParent);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        _seatedChar = character;
        _pendingDriverVisualFit = character;
        if (character.Player != null) character.Player.IsControllable = false;
        ConfigureSteeringWheelIK(character);
    }

    private void AttachRearPassengerToSeat(
        Character character,
        CarEntrySideMode side)
    {
        Transform seat = side == CarEntrySideMode.RearLeftDoor
            ? rearLeftSeatParent
            : rearRightSeatParent;
        if (character == null || seat == null) return;

        RestoreSeatVisualFitForCharacter(character);
        RestoreSharedCharacterModelBaseline(character);
        LockRearPassengerPhysics(character, side);
        character.transform.SetParent(seat);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        if (side == CarEntrySideMode.RearLeftDoor)
        {
            _rearLeftSeatedChar = character;
            _pendingRearLeftVisualFit = character;
        }
        else
        {
            _rearRightSeatedChar = character;
            _pendingRearRightVisualFit = character;
        }

        if (character.Player != null) character.Player.IsControllable = false;
        ConfigureRearLapIK(character, side);
    }

    private void DetachRearPassenger(
        Character character,
        CarEntrySideMode side)
    {
        if (character == null) return;
        if (side == CarEntrySideMode.RearLeftDoor)
            _rearLeftSeatedChar = null;
        else
            _rearRightSeatedChar = null;

        character.transform.SetParent(null, true);
        if (side == CarEntrySideMode.RearLeftDoor)
            RestoreSeatVisualFit(_rearLeftVisualFit);
        else
            RestoreSeatVisualFit(_rearRightVisualFit);
        RestoreRearPassengerPhysics(character, side);
        ClearSteeringWheelIK(character);
        if (character.Driver != null) character.Driver.Collision = true;
        Physics.SyncTransforms();
    }

    private static void KeepPassengerOnSeat(Character character)
    {
        if (character == null) return;
        Transform characterTransform = character.transform;
        if (characterTransform.localPosition.sqrMagnitude > 0.000001f)
            characterTransform.localPosition = Vector3.zero;
        if (Quaternion.Angle(
                characterTransform.localRotation,
                Quaternion.identity
            ) > 0.01f)
        {
            characterTransform.localRotation = Quaternion.identity;
        }
    }

    private void ApplyPendingSeatVisualFits()
    {
        if (_pendingDriverVisualFit != null)
        {
            Character character = _pendingDriverVisualFit;
            _pendingDriverVisualFit = null;
            if (character == _seatedChar)
                ApplySeatVisualFit(
                    character,
                    _driverVisualFit,
                    driverSeatedCeilingAnchor
                );
        }

        if (_pendingRearLeftVisualFit != null)
        {
            Character character = _pendingRearLeftVisualFit;
            _pendingRearLeftVisualFit = null;
            if (character == _rearLeftSeatedChar)
                ApplySeatVisualFit(
                    character,
                    _rearLeftVisualFit,
                    rearLeftSeatedCeilingAnchor
                );
        }

        if (_pendingRearRightVisualFit != null)
        {
            Character character = _pendingRearRightVisualFit;
            _pendingRearRightVisualFit = null;
            if (character == _rearRightSeatedChar)
                ApplySeatVisualFit(
                    character,
                    _rearRightVisualFit,
                    rearRightSeatedCeilingAnchor
                );
        }
    }

    private void ApplySeatVisualFit(
        Character character,
        SeatVisualFitSnapshot snapshot,
        Transform ceilingAnchor)
    {
        RestoreSeatVisualFit(snapshot);
        if (!autoFitSeatedModel || character == null || snapshot == null) return;

        Animator animator = character.Animim?.Animator;
        if (animator == null || !animator.isHuman) return;
        if (!animator.enabled || !animator.gameObject.activeInHierarchy) return;

        // Seated NPC physics deliberately uses CullCompletely while offscreen.
        // Force one zero-delta evaluation so cabin fitting reads the actual
        // Driving pose instead of a stale standing pose, then restore culling.
        AnimatorCullingMode previousCulling = animator.cullingMode;
        if (previousCulling == AnimatorCullingMode.CullCompletely)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);
            animator.cullingMode = previousCulling;
        }

        Transform model = animator.transform;
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (model == null || hips == null || head == null) return;
        if (!TryGetCabinCeilingCoordinate(
                ceilingAnchor,
                out float ceilingCoordinate
            ))
        {
            return;
        }

        Vector3 vehicleUp = transform.up.normalized;
        float hipsCoordinate = Vector3.Dot(hips.position, vehicleUp);
        float headCoordinate = Vector3.Dot(head.position, vehicleUp) +
            Mathf.Max(0f, seatedHeadTopOffset);
        float availableHeight = ceilingCoordinate -
            Mathf.Max(0f, seatedRoofLiningInset) -
            Mathf.Max(0f, seatedHeadClearance) - hipsCoordinate;
        float currentHeight = headCoordinate - hipsCoordinate;
        if (currentHeight <= 0.01f || availableHeight >= currentHeight) return;

        snapshot.character = character;
        snapshot.model = model;
        snapshot.parent = model.parent;
        snapshot.localPosition = model.localPosition;
        snapshot.localRotation = model.localRotation;
        snapshot.localScale = model.localScale;
        snapshot.active = true;

        Vector3 hipsWorldPosition = hips.position;
        float scale = Mathf.Clamp(
            availableHeight / currentHeight,
            Mathf.Clamp(minimumSeatedModelScale, 0.75f, 1f),
            1f
        );
        model.localScale = Vector3.Scale(
            snapshot.localScale,
            Vector3.one * scale
        );
        model.position += hipsWorldPosition - hips.position;

        float fittedHeadCoordinate = Vector3.Dot(head.position, vehicleUp) +
            Mathf.Max(0f, seatedHeadTopOffset);
        float ceilingLimit = ceilingCoordinate -
            Mathf.Max(0f, seatedRoofLiningInset) -
            Mathf.Max(0f, seatedHeadClearance);
        float remainingOverflow = fittedHeadCoordinate - ceilingLimit;
        if (remainingOverflow > 0f)
        {
            float downOffset = Mathf.Min(
                remainingOverflow,
                Mathf.Max(0f, maximumSeatedModelDownOffset)
            );
            model.position -= vehicleUp * downOffset;
            remainingOverflow -= downOffset;
        }

        if (remainingOverflow > 0.01f && !_hasWarnedCabinFitLimit)
        {
            _hasWarnedCabinFitLimit = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"{name}: cabin is still {remainingOverflow:0.000}m too low for " +
                $"{character.name}. Author a lower seated pose or a taller cabin profile.",
                this
            );
#endif
        }

        Physics.SyncTransforms();
    }

    private bool TryGetCabinCeilingCoordinate(
        Transform ceilingAnchor,
        out float coordinate)
    {
        Vector3 vehicleUp = transform.up.normalized;
        if (ceilingAnchor != null)
        {
            coordinate = Vector3.Dot(ceilingAnchor.position, vehicleUp);
            return true;
        }

        coordinate = float.NegativeInfinity;

        Renderer ceiling = seatedCabinCeilingRenderer;
        MeshFilter filter = ceiling != null
            ? ceiling.GetComponent<MeshFilter>()
            : null;
        if (ceiling != null && filter != null && filter.sharedMesh != null)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 local = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z
                );
                coordinate = Mathf.Max(
                    coordinate,
                    Vector3.Dot(ceiling.transform.TransformPoint(local), vehicleUp)
                );
            }
            return true;
        }

        BoxCollider body = cachedCarCollider;
        if (body == null) return false;
        Vector3 half = body.size * 0.5f;
        for (int corner = 0; corner < 8; ++corner)
        {
            Vector3 local = body.center + new Vector3(
                (corner & 1) == 0 ? -half.x : half.x,
                (corner & 2) == 0 ? -half.y : half.y,
                (corner & 4) == 0 ? -half.z : half.z
            );
            coordinate = Mathf.Max(
                coordinate,
                Vector3.Dot(body.transform.TransformPoint(local), vehicleUp)
            );
        }
        return true;
    }

    private void RestoreSeatVisualFitForCharacter(Character character)
    {
        if (character == null) return;
        if (_driverVisualFit.character == character)
            RestoreSeatVisualFit(_driverVisualFit);
        if (_rearLeftVisualFit.character == character)
            RestoreSeatVisualFit(_rearLeftVisualFit);
        if (_rearRightVisualFit.character == character)
            RestoreSeatVisualFit(_rearRightVisualFit);
        if (_passengerVisualFit.character == character)
            RestoreSeatVisualFit(_passengerVisualFit);
    }

    private static void RestoreSeatVisualFit(SeatVisualFitSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.active || snapshot.model == null)
        {
            snapshot?.Clear();
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
        snapshot.Clear();
        Physics.SyncTransforms();
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

    private CharacterModelSnapshot CaptureBailoutModelTransform(
        Character character)
    {
        // The moving-exit launch starts while the cabin fit is still visible.
        // Persist the authored pre-fit transform for ragdoll recovery, otherwise
        // a later Car/Bike entry would inherit the temporary smaller avatar.
        SeatVisualFitSnapshot fit = _driverVisualFit;
        if (fit.active && fit.character == character && fit.model != null)
        {
            return new CharacterModelSnapshot
            {
                character = character,
                model = fit.model,
                parent = fit.parent,
                localPosition = fit.localPosition,
                localRotation = fit.localRotation,
                localScale = fit.localScale
            };
        }

        return CaptureCharacterModelTransform(character);
    }

    private static void RestoreSharedCharacterModelBaseline(Character character)
    {
        character?.GetComponentInChildren<FranklinAnimationBridge>(true)
            ?.RestoreModelRootBaseline();
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

    private void LockRearPassengerPhysics(
        Character character,
        CarEntrySideMode side)
    {
        if (character == null) return;
        CharacterPhysicsSnapshot snapshot = side == CarEntrySideMode.RearLeftDoor
            ? _rearLeftPhysicsSnapshotBuffer
            : _rearRightPhysicsSnapshotBuffer;
        CaptureAndLockCharacterPhysics(character, snapshot);
        if (side == CarEntrySideMode.RearLeftDoor)
            _rearLeftPhysics = snapshot;
        else
            _rearRightPhysics = snapshot;
    }

    private void RestoreRearPassengerPhysics(
        Character character,
        CarEntrySideMode side)
    {
        if (side == CarEntrySideMode.RearLeftDoor)
            RestoreSeatVisualFit(_rearLeftVisualFit);
        else
            RestoreSeatVisualFit(_rearRightVisualFit);
        CharacterPhysicsSnapshot snapshot = side == CarEntrySideMode.RearLeftDoor
            ? _rearLeftPhysics
            : _rearRightPhysics;
        if (snapshot == null || snapshot.character != character) return;
        RestoreCharacterPhysicsSnapshot(snapshot, false);
        if (side == CarEntrySideMode.RearLeftDoor)
            _rearLeftPhysics = null;
        else
            _rearRightPhysics = null;
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
        // Entry navigation has higher priority than normal Player input. Clear
        // it before taking the snapshot so a timed-out approach cannot survive
        // the ride and reject every priority-zero joystick command after exit.
        ReleaseEntryApproachMotion(character);

        snapshot.colliders.Clear();
        snapshot.rigidbodies.Clear();
        snapshot.character = character;
        snapshot.driverCollision = character.Driver?.Collision ?? false;
        snapshot.driverUpdateKinematics = character.Driver?.UpdateKinematics ?? false;
        snapshot.movementType = character.Motion?.MovementType ?? Character.MovementType.None;
        snapshot.animator = null;
        snapshot.hasAnimatorCullingSnapshot = false;

        if (!character.IsPlayer)
        {
            Animator animator = character.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                snapshot.animator = animator;
                snapshot.animatorCullingMode = animator.cullingMode;
                snapshot.hasAnimatorCullingSnapshot = true;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
            }
        }

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
        if (_driverVisualFit.character == character)
            RestoreSeatVisualFit(_driverVisualFit);
        CharacterPhysicsSnapshot snapshot = _seatedPhysics;
        if (snapshot == null || snapshot.character != character) return;

        RestoreCharacterPhysicsSnapshot(snapshot, keepGrounded);
        _seatedPhysics = null;
    }

    private void RestoreCharacterPhysicsForActiveRagdoll(Character character)
    {
        if (_driverVisualFit.character == character)
            RestoreSeatVisualFit(_driverVisualFit);
        CharacterPhysicsSnapshot snapshot = _seatedPhysics;
        if (snapshot == null || snapshot.character != character) return;

        // GC2 owns the root and bone bodies once ragdoll is active. Restore
        // only non-ragdoll state so release cannot make those bodies kinematic
        // again or disable their colliders.
        RestoreCharacterPhysicsSnapshot(snapshot, false, true);
        _seatedPhysics = null;
    }

    private void RestorePassengerCarjackingPhysics()
    {
        CharacterPhysicsSnapshot snapshot = _passengerCarjackingPhysics;
        if (snapshot == null) return;

        if (_passengerVisualFit.character == snapshot.character)
            RestoreSeatVisualFit(_passengerVisualFit);
        RestoreCharacterPhysicsSnapshot(snapshot, false);
        _passengerCarjackingPhysics = null;
    }

    private void ReleasePassengerCarjackingPhysicsForRagdoll(
        Character character)
    {
        CharacterPhysicsSnapshot snapshot = _passengerCarjackingPhysics;
        if (snapshot == null || snapshot.character != character) return;

        if (_passengerVisualFit.character == character)
            RestoreSeatVisualFit(_passengerVisualFit);
        RestoreCharacterPhysicsSnapshot(snapshot, false, true);
        _passengerCarjackingPhysics = null;
    }

    private static void RestoreCharacterPhysicsSnapshot(
        CharacterPhysicsSnapshot snapshot,
        bool keepGrounded,
        bool preserveActiveRagdoll = false)
    {
        Character character = snapshot.character;
        if (character == null) return;

        Transform ragdollModel = preserveActiveRagdoll
            ? character.Animim?.Animator?.transform
            : null;

        foreach (RigidbodySnapshot state in snapshot.rigidbodies)
        {
            if (state.rigidbody == null) continue;
            if (preserveActiveRagdoll &&
                (state.rigidbody.transform == character.transform ||
                 ragdollModel != null &&
                 state.rigidbody.transform.IsChildOf(ragdollModel)))
            {
                continue;
            }
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
            if (state.collider == null) continue;
            if (preserveActiveRagdoll &&
                (state.collider.transform == character.transform ||
                 ragdollModel != null &&
                 state.collider.transform.IsChildOf(ragdollModel)))
            {
                continue;
            }
            state.collider.enabled = state.enabled;
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

        if (snapshot.hasAnimatorCullingSnapshot && snapshot.animator != null)
            snapshot.animator.cullingMode = snapshot.animatorCullingMode;

        snapshot.character = null;
        snapshot.animator = null;
        snapshot.hasAnimatorCullingSnapshot = false;
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

    private void ConfigureRearLapIK(
        Character character,
        CarEntrySideMode side)
    {
        if (character == null) return;
        Transform leftTarget = side == CarEntrySideMode.RearLeftDoor
            ? rearLeftLapLeftHandTarget
            : rearRightLapLeftHandTarget;
        Transform rightTarget = side == CarEntrySideMode.RearLeftDoor
            ? rearLeftLapRightHandTarget
            : rearRightLapRightHandTarget;
        if (leftTarget == null || rightTarget == null) return;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator == null) return;
        AlignLapTargetToThigh(
            animator,
            leftTarget,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            character.transform.up
        );
        AlignLapTargetToThigh(
            animator,
            rightTarget,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            character.transform.up
        );
        CharacterIKSetter ikSetter = animator.GetComponent<CharacterIKSetter>();
        if (ikSetter == null)
            ikSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

        // Position the palms on the thighs but retain the animation's authored
        // wrist orientation, avoiding twisted hands on differently proportioned
        // Humanoid avatars.
        ikSetter.SetIKTargets(
            leftTarget,
            rightTarget,
            rearLapHandIKWeight,
            rearLapHandIKWeight,
            0f,
            0f
        );
    }

    private static void AlignLapTargetToThigh(
        Animator animator,
        Transform target,
        HumanBodyBones upperLegBone,
        HumanBodyBones lowerLegBone,
        Vector3 up)
    {
        if (animator == null || !animator.isHuman || target == null) return;
        Transform upperLeg = animator.GetBoneTransform(upperLegBone);
        Transform lowerLeg = animator.GetBoneTransform(lowerLegBone);
        if (upperLeg == null || lowerLeg == null) return;

        // The middle/front of each thigh is a stable avatar-relative lap point.
        // A small lift prevents the palm from clipping into trousers while the
        // wrist rotation remains authored by the seated animation.
        target.position = Vector3.Lerp(
            upperLeg.position,
            lowerLeg.position,
            0.52f
        ) + up.normalized * 0.035f;
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

    private void ReleaseEntryApproachMotion(Character character)
    {
        if (character == null || character.Motion == null) return;

        int priority = Mathf.Max(1, entryApproachMotionPriority);
        character.Motion.MoveToDirection(Vector3.zero, Space.World, priority);
        character.Motion.StopToDirection(priority);
    }

    public async Task<bool> MoveCharacterToEntryStandingPointAsync(Character character)
    {
        Transform standingPoint = GetActiveEntryStandingPoint();
        if (character == null || character.Motion == null || standingPoint == null)
            return false;

        _approachCharacter = character;
        _approachFinished = false;
        _approachSucceeded = false;
        int approachVersion = ++_approachVersion;

        Vector3 targetFeet = standingPoint.position -
            Vector3.up * (character.Motion.Height * 0.5f);
        Location target = new Location(targetFeet, standingPoint.rotation);
        character.Motion.MoveToLocation(
            target,
            Mathf.Max(0.15f, entryApproachStopDistance),
            (callbackCharacter, success) => OnEntryApproachFinished(
                callbackCharacter,
                success,
                approachVersion
            ),
            Mathf.Max(1, entryApproachMotionPriority)
        );

        float deadline = Time.unscaledTime + Mathf.Max(0.1f, entryApproachTimeout);
        while (!_approachFinished && character != null &&
               !character.Ragdoll.IsRagdoll && !IsCarDestroyed() &&
               Time.unscaledTime < deadline)
        {
            await Task.Yield();
        }

        if (character == null) return false;
        if (IsCarDestroyed() || character.Ragdoll.IsRagdoll)
        {
            character.Motion.MoveToDirection(
                Vector3.zero,
                Space.World,
                Mathf.Max(1, entryApproachMotionPriority)
            );
            character.Motion.StopToDirection(
                Mathf.Max(1, entryApproachMotionPriority)
            );
            _approachCharacter = null;
            return false;
        }

        Vector3 horizontalToStanding = Vector3.ProjectOnPlane(
            character.transform.position - standingPoint.position,
            Vector3.up
        );
        float allowedHorizontalError =
            Mathf.Max(0.15f, entryApproachStopDistance) + 0.05f;
        bool reachedStandingArea = horizontalToStanding.sqrMagnitude <=
            allowedHorizontalError * allowedHorizontalError;

        // GC2 can report a blocked/stale navigation callback even when the
        // Character has already reached the authored door area. Use the same
        // world-space fallback as BikeEntry so a previous blocked approach
        // cannot prevent the door sequence on the next attempt.
        if ((!_approachFinished || !_approachSucceeded) &&
            !reachedStandingArea)
        {
            character.Motion.MoveToDirection(
                Vector3.zero,
                Space.World,
                Mathf.Max(1, entryApproachMotionPriority)
            );
            character.Motion.StopToDirection(
                Mathf.Max(1, entryApproachMotionPriority)
            );
            _approachCharacter = null;
            return false;
        }

        // Clear the callback owner first so replacing a still-running
        // MotionToLocation cannot overwrite the accepted fallback result.
        _approachCharacter = null;
        ReleaseEntryApproachMotion(character);

        if (alignCharacterToStandingPoint)
        {
            await SmoothAlignCharacterToStandingPoint(character);
        }

        return character != null && !character.Ragdoll.IsRagdoll &&
               !IsCarDestroyed();
    }

    private void OnEntryApproachFinished(
        Character character,
        bool success,
        int approachVersion)
    {
        if (approachVersion != _approachVersion ||
            character != _approachCharacter)
        {
            return;
        }
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
        while (elapsed < duration && character != null && standingPoint != null &&
               !character.Ragdoll.IsRagdoll && !IsCarDestroyed())
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

        if (character != null && standingPoint != null &&
            !character.Ragdoll.IsRagdoll && !IsCarDestroyed())
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

    private AnimationClip GetActiveExitAnimation()
    {
        return IsRightSideEntry() && mirroredExitAnimation != null
            ? mirroredExitAnimation
            : exitAnimation;
    }

    public AvatarIKGoal GetDoorHandleHand(bool entering)
    {
        if (!IsRightSideEntry()) return doorHandleHand;

        return doorHandleHand == AvatarIKGoal.LeftHand
            ? AvatarIKGoal.RightHand
            : AvatarIKGoal.LeftHand;
    }

    private void SelectEntrySide(
        Character character,
        bool forceDriverDoor,
        CarEntrySideMode requestedSide)
    {
        if (forceDriverDoor)
        {
            SetActiveEntrySide(CarEntrySideMode.DriverDoor);
            return;
        }

        CarEntrySideMode resolved = ResolveRequestedEntrySide(
            character,
            requestedSide
        );
        if (resolved == CarEntrySideMode.Automatic)
            resolved = CarEntrySideMode.DriverDoor;
        SetActiveEntrySide(resolved);
    }

    private CarEntrySideMode ResolveRequestedEntrySide(
        Character character,
        CarEntrySideMode requestedSide)
    {
        if (requestedSide != CarEntrySideMode.Automatic)
            return IsEntrySideConfigured(requestedSide)
                ? requestedSide
                : CarEntrySideMode.Automatic;

        if (entrySideMode != CarEntrySideMode.Automatic &&
            IsEntrySideAvailable(entrySideMode))
        {
            return entrySideMode;
        }
        if (character == null) return CarEntrySideMode.Automatic;

        CarEntrySideMode closest = CarEntrySideMode.Automatic;
        float closestDistance = float.PositiveInfinity;
        ConsiderNearestEntrySide(
            character,
            CarEntrySideMode.DriverDoor,
            entryStandingPoint,
            ref closest,
            ref closestDistance
        );
        ConsiderNearestEntrySide(
            character,
            CarEntrySideMode.PassengerDoor,
            passengerEntryStandingPoint,
            ref closest,
            ref closestDistance
        );
        ConsiderNearestEntrySide(
            character,
            CarEntrySideMode.RearLeftDoor,
            rearLeftEntryStandingPoint,
            ref closest,
            ref closestDistance
        );
        ConsiderNearestEntrySide(
            character,
            CarEntrySideMode.RearRightDoor,
            rearRightEntryStandingPoint,
            ref closest,
            ref closestDistance
        );
        return closest;
    }

    private void ConsiderNearestEntrySide(
        Character character,
        CarEntrySideMode side,
        Transform standingPoint,
        ref CarEntrySideMode closest,
        ref float closestDistance)
    {
        if (standingPoint == null || !IsEntrySideAvailable(side)) return;
        Vector3 offset = Vector3.ProjectOnPlane(
            standingPoint.position - character.transform.position,
            Vector3.up
        );
        float distance = offset.sqrMagnitude;
        if (distance >= closestDistance) return;
        closestDistance = distance;
        closest = side;
    }

    private bool IsEntrySideAvailable(CarEntrySideMode side)
    {
        if (!IsEntrySideConfigured(side)) return false;
        if (IsRearEntrySide(side)) return CanUseRearSeat(side);
        if (_seatedChar == null) return true;
        if (occupiedEntryHandler == null) CacheVehicleBehaviours();
        return occupiedEntryHandler != null;
    }

    private bool IsEntrySideConfigured(CarEntrySideMode side)
    {
        return side switch
        {
            CarEntrySideMode.DriverDoor => entryStandingPoint != null &&
                entryStepPoint != null && doorTransform != null && entryParent != null,
            CarEntrySideMode.PassengerDoor => HasPassengerEntrySetup(),
            CarEntrySideMode.RearLeftDoor => HasRearSeatSetup() &&
                rearLeftEntryStandingPoint != null,
            CarEntrySideMode.RearRightDoor => HasRearSeatSetup() &&
                rearRightEntryStandingPoint != null,
            _ => false
        };
    }

    private bool CanUseRearSeat(CarEntrySideMode side)
    {
        if (!HasRearSeatSetup()) return false;
        return side switch
        {
            CarEntrySideMode.RearLeftDoor => _rearLeftSeatedChar == null,
            CarEntrySideMode.RearRightDoor => _rearRightSeatedChar == null,
            _ => false
        };
    }

    public bool HasRearSeatSetup()
    {
        return mirroredEntryAnimation != null && mirroredExitAnimation != null &&
            rearLeftEntryStandingPoint != null && rearLeftEntryStepPoint != null &&
            rearLeftSeatParent != null && rearLeftDoorTransform != null &&
            rearLeftDoorHandleTarget != null && rearLeftDoorAudioSource != null &&
            rearRightEntryStandingPoint != null && rearRightEntryStepPoint != null &&
            rearRightSeatParent != null && rearRightDoorTransform != null &&
            rearRightDoorHandleTarget != null && rearRightDoorAudioSource != null &&
            rearLeftLapLeftHandTarget != null &&
            rearLeftLapRightHandTarget != null &&
            rearRightLapLeftHandTarget != null &&
            rearRightLapRightHandTarget != null;
    }

    private static bool IsRearEntrySide(CarEntrySideMode side)
    {
        return side == CarEntrySideMode.RearLeftDoor ||
            side == CarEntrySideMode.RearRightDoor;
    }

    private bool IsRightSideEntry()
    {
        return _activeEntrySide == CarEntrySideMode.PassengerDoor ||
            _activeEntrySide == CarEntrySideMode.RearRightDoor;
    }

    private void SetActiveEntrySide(CarEntrySideMode side)
    {
        _activeEntrySide = side;
        _activePassengerEntry = side == CarEntrySideMode.PassengerDoor;
    }

    private void ResetActiveEntrySide()
    {
        SetActiveEntrySide(CarEntrySideMode.DriverDoor);
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
        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerEntryStandingPoint,
            CarEntrySideMode.RearLeftDoor => rearLeftEntryStandingPoint,
            CarEntrySideMode.RearRightDoor => rearRightEntryStandingPoint,
            _ => entryStandingPoint
        };
    }

    private Transform GetActiveEntryStepPoint()
    {
        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerEntryStepPoint,
            CarEntrySideMode.RearLeftDoor => rearLeftEntryStepPoint,
            CarEntrySideMode.RearRightDoor => rearRightEntryStepPoint,
            _ => entryStepPoint
        };
    }

    private Transform GetActiveEntryDoor()
    {
        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorTransform,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorTransform,
            CarEntrySideMode.RearRightDoor => rearRightDoorTransform,
            _ => doorTransform
        };
    }

    private Vector3 GetActiveEntryDoorOpenRotation()
    {
        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorOpenRotation,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorOpenRotation,
            CarEntrySideMode.RearRightDoor => rearRightDoorOpenRotation,
            _ => doorOpenRotation
        };
    }

    private Vector3 GetActiveEntryDoorOpenLocalOffset()
    {
        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorOpenLocalOffset,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorOpenLocalOffset,
            CarEntrySideMode.RearRightDoor => rearRightDoorOpenLocalOffset,
            _ => doorOpenLocalOffset
        };
    }

    private AudioSource GetActiveEntryDoorAudioSource()
    {
        AudioSource source = _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorAudioSource,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorAudioSource,
            CarEntrySideMode.RearRightDoor => rearRightDoorAudioSource,
            _ => doorAudioSource
        };
        return source != null ? source : doorAudioSource;
    }

    private Transform GetActiveDoorHandleTarget(bool entering)
    {
        if (!CanAnimateDoor(_activeEntrySide)) return null;

        return _activeEntrySide switch
        {
            CarEntrySideMode.PassengerDoor => passengerDoorHandleTarget,
            CarEntrySideMode.RearLeftDoor => rearLeftDoorHandleTarget,
            CarEntrySideMode.RearRightDoor => rearRightDoorHandleTarget,
            _ => doorHandleTarget
        };
    }

    private Transform GetActiveSeatParent()
    {
        return _activeEntrySide switch
        {
            CarEntrySideMode.RearLeftDoor => rearLeftSeatParent,
            CarEntrySideMode.RearRightDoor => rearRightSeatParent,
            _ => entryParent
        };
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

            if (occupantCollisionPolicy == null &&
                behaviour is IRvrVehicleOccupantCollisionPolicy collisionPolicy)
                occupantCollisionPolicy = collisionPolicy;

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
        Transform seatParent = GetActiveSeatParent();
        if (externalDriveController?.UseSeatEntryAlignment != true ||
            character == null || clip == null || seatParent == null)
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
        if (_entryAlignCharacter == null || GetActiveSeatParent() == null) return;
        if (IsCarDestroyed() || _entryAlignCharacter.Ragdoll.IsRagdoll)
        {
            if (_entryAlignCharacter.Ragdoll.IsRagdoll)
                _entryRagdollInterrupted = true;
            _entryAlignCharacter = null;
            return;
        }

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
        return GetActiveSeatParent();
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
        float animationStartedAt,
        int lifecycleVersion)
    {
        if (character == null || clip == null ||
            !IsLifecycleCurrent(lifecycleVersion)) return;

        float duration = clip.length / Mathf.Max(0.01f, speed);
        float warmupLead = Mathf.Max(0.12f, seatedPoseGuardLeadTime);
        float warmupAt = animationStartedAt + Mathf.Max(0f, duration - warmupLead);
        while (character != null && IsLifecycleCurrent(lifecycleVersion) &&
               !WasEntryRagdollInterrupted(character) &&
               !IsCarDestroyed() && Time.time < warmupAt)
            await Task.Yield();
        if (WasEntryRagdollInterrupted(character) ||
            IsCarDestroyed() || !IsLifecycleCurrent(lifecycleVersion)) return;

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
        float speed,
        int lifecycleVersion)
    {
        if (_seatedPoseGuard == null || character == null || clip == null ||
            !IsLifecycleCurrent(lifecycleVersion)) return;

        float duration = clip.length / Mathf.Max(0.01f, speed);
        float captureAt = Mathf.Max(0f, duration - seatedPoseGuardLeadTime);
        float startedAt = Time.time;
        while (character != null && IsLifecycleCurrent(lifecycleVersion) &&
               !character.Ragdoll.IsRagdoll &&
               !IsCarDestroyed() && Time.time - startedAt < captureAt)
        {
            await Task.Yield();
        }

        if (character == null || IsCarDestroyed() ||
            !IsLifecycleCurrent(lifecycleVersion)) return;
        if (character.Ragdoll.IsRagdoll)
        {
            _entryRagdollInterrupted = true;
            return;
        }
        _seatedPoseGuard?.Capture();
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

        Transform activeSeat = GetActiveSeatParent();
        Vector3 endPosition = activeSeat != null
            ? activeSeat.position
            : startPosition;
        Quaternion endRotation = activeSeat != null
            ? activeSeat.rotation
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
        int lifecycleVersion = _lifecycleVersion;
        if (activeDoor == null || !CanAnimateDoorTransform(activeDoor)) return;
        Quaternion originalRotation = GetClosedDoorRotation(activeDoor);
        Vector3 originalPosition = GetClosedDoorPosition(activeDoor);

        await Task.Delay((int)(doorRotationStartDelay * 1000));
        if (!IsLifecycleCurrent(lifecycleVersion) || activeDoor == null ||
            !CanAnimateDoorTransform(activeDoor)) return;
        PlayDoorSound(activeAudioSource, true);
        await AnimateDoorPose(
            activeDoor,
            activeOpenRotation,
            GetOpenDoorPosition(activeDoor),
            doorRotationDuration
        );

        await Task.Delay((int)(doorResetDelay * 1000));
        if (!IsLifecycleCurrent(lifecycleVersion) || activeDoor == null ||
            !CanAnimateDoorTransform(activeDoor)) return;
        PlayDoorSound(activeAudioSource, false);
        await AnimateDoorPose(
            activeDoor,
            originalRotation.eulerAngles,
            originalPosition,
            doorRotationDuration
        );
    }

    private void CacheClosedDoorPoses()
    {
        if (_closedDoorPosesCached) return;
        if (doorTransform != null)
        {
            _driverDoorClosedRotation = doorTransform.localRotation;
            _driverDoorClosedPosition = doorTransform.localPosition;
        }
        if (passengerDoorTransform != null)
        {
            _passengerDoorClosedRotation = passengerDoorTransform.localRotation;
            _passengerDoorClosedPosition = passengerDoorTransform.localPosition;
        }
        if (rearLeftDoorTransform != null)
        {
            _rearLeftDoorClosedRotation = rearLeftDoorTransform.localRotation;
            _rearLeftDoorClosedPosition = rearLeftDoorTransform.localPosition;
        }
        if (rearRightDoorTransform != null)
        {
            _rearRightDoorClosedRotation = rearRightDoorTransform.localRotation;
            _rearRightDoorClosedPosition = rearRightDoorTransform.localPosition;
        }
        _closedDoorPosesCached = true;
    }

    private Quaternion GetClosedDoorRotation(Transform activeDoor)
    {
        CacheClosedDoorPoses();
        if (activeDoor == doorTransform) return _driverDoorClosedRotation;
        if (activeDoor == passengerDoorTransform)
            return _passengerDoorClosedRotation;
        if (activeDoor == rearLeftDoorTransform)
            return _rearLeftDoorClosedRotation;
        if (activeDoor == rearRightDoorTransform)
            return _rearRightDoorClosedRotation;
        return activeDoor != null ? activeDoor.localRotation : Quaternion.identity;
    }

    private Vector3 GetClosedDoorPosition(Transform activeDoor)
    {
        CacheClosedDoorPoses();
        if (activeDoor == doorTransform) return _driverDoorClosedPosition;
        if (activeDoor == passengerDoorTransform)
            return _passengerDoorClosedPosition;
        if (activeDoor == rearLeftDoorTransform)
            return _rearLeftDoorClosedPosition;
        if (activeDoor == rearRightDoorTransform)
            return _rearRightDoorClosedPosition;
        return activeDoor != null ? activeDoor.localPosition : Vector3.zero;
    }

    private Vector3 GetDoorOpenLocalOffset(Transform activeDoor)
    {
        if (activeDoor == passengerDoorTransform)
            return passengerDoorOpenLocalOffset;
        if (activeDoor == rearLeftDoorTransform)
            return rearLeftDoorOpenLocalOffset;
        if (activeDoor == rearRightDoorTransform)
            return rearRightDoorOpenLocalOffset;
        return doorOpenLocalOffset;
    }

    private Vector3 GetOpenDoorPosition(Transform activeDoor)
    {
        return GetClosedDoorPosition(activeDoor) +
            GetDoorOpenLocalOffset(activeDoor);
    }

    private void RestoreClosedDoorPoseIfAnimatable(Transform activeDoor)
    {
        if (activeDoor == null || !CanAnimateDoorTransform(activeDoor)) return;
        activeDoor.SetLocalPositionAndRotation(
            GetClosedDoorPosition(activeDoor),
            GetClosedDoorRotation(activeDoor)
        );
    }

    private async Task OpenMovingExitDoorAsync(
        Transform activeDoor,
        Vector3 activeOpenRotation,
        AudioSource activeAudioSource)
    {
        int lifecycleVersion = _lifecycleVersion;
        await WaitUnscaledSecondsAsync(
            Mathf.Max(doorRotationStartDelay, movingExitDoorLeadTime)
        );
        if (!IsLifecycleCurrent(lifecycleVersion) || activeDoor == null ||
            !CanAnimateDoorTransform(activeDoor)) return;

        PlayDoorSound(activeAudioSource, true);
        await AnimateDoorPose(
            activeDoor,
            activeOpenRotation,
            GetOpenDoorPosition(activeDoor),
            doorRotationDuration
        );
    }

    private async Task PartiallyCloseMovingExitDoorAsync(
        Transform activeDoor,
        Quaternion closedRotation,
        Vector3 activeOpenRotation,
        Task openDoorTask)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (openDoorTask != null) await openDoorTask;
        if (!IsLifecycleCurrent(lifecycleVersion) || activeDoor == null) return;

        Quaternion retainedOpenRotation = Quaternion.Slerp(
            closedRotation,
            Quaternion.Euler(activeOpenRotation),
            movingExitDoorRemainingOpen
        );
        Vector3 closedPosition = GetClosedDoorPosition(activeDoor);
        Vector3 retainedOpenPosition = Vector3.Lerp(
            closedPosition,
            closedPosition + GetDoorOpenLocalOffset(activeDoor),
            movingExitDoorRemainingOpen
        );
        await AnimateDoorPoseNaturally(
            activeDoor,
            retainedOpenRotation,
            retainedOpenPosition,
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

    private async Task AnimateDoorPose(
        Transform door,
        Vector3 targetEulerAngles,
        Vector3 targetLocalPosition,
        float duration
    )
    {
        int lifecycleVersion = _lifecycleVersion;
        if (door == null || !CanAnimateDoorTransform(door)) return;
        Quaternion startRotation = door.localRotation;
        Vector3 startPosition = door.localPosition;
        Quaternion targetRotation = Quaternion.Euler(targetEulerAngles);
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsedTime = 0f;

        while (elapsedTime < safeDuration && IsLifecycleCurrent(lifecycleVersion) &&
               door != null &&
               CanAnimateDoorTransform(door))
        {
            float normalized = Mathf.Clamp01(elapsedTime / safeDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            door.SetLocalPositionAndRotation(
                Vector3.Lerp(startPosition, targetLocalPosition, eased),
                Quaternion.Slerp(startRotation, targetRotation, eased)
            );
            elapsedTime += Time.deltaTime;
            await Task.Yield();
        }

        if (IsLifecycleCurrent(lifecycleVersion) && door != null &&
            CanAnimateDoorTransform(door))
        {
            door.SetLocalPositionAndRotation(
                targetLocalPosition,
                targetRotation
            );
        }
    }

    private bool CanAnimateDoorTransform(Transform door)
    {
        if (door == null) return false;
        if (doorDamage == null) doorDamage = GetComponent<SimcadeCarDoorDamage>();
        return doorDamage == null || doorDamage.CanAnimateDoor(door);
    }

    private async Task AnimateDoorPoseNaturally(
        Transform door,
        Quaternion targetRotation,
        Vector3 targetLocalPosition,
        float duration)
    {
        int lifecycleVersion = _lifecycleVersion;
        if (door == null || !CanAnimateDoorTransform(door)) return;

        Quaternion startRotation = door.localRotation;
        Vector3 startPosition = door.localPosition;
        float safeDuration = Mathf.Max(0.1f, duration);
        float elapsedTime = 0f;

        while (elapsedTime < safeDuration &&
               IsLifecycleCurrent(lifecycleVersion) && door != null &&
               CanAnimateDoorTransform(door))
        {
            float normalized = Mathf.Clamp01(elapsedTime / safeDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            door.SetLocalPositionAndRotation(
                Vector3.Lerp(startPosition, targetLocalPosition, eased),
                Quaternion.Slerp(startRotation, targetRotation, eased)
            );
            elapsedTime += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        if (IsLifecycleCurrent(lifecycleVersion) && door != null &&
            CanAnimateDoorTransform(door))
        {
            door.SetLocalPositionAndRotation(
                targetLocalPosition,
                targetRotation
            );
        }
    }
}
