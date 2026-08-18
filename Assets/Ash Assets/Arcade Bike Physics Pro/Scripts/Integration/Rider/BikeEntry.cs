using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using FranklinGame.Animations;
using FranklinGame.Shooter;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using System.Collections;

public enum RiderFitStyle
{
    Upright,
    Standard,
    Sport,
    Racing,
    Custom
}

public enum BikeEntrySideMode
{
    Automatic,
    Original,
    Mirrored
}

[AddComponentMenu("Game Creator/Mechanics/BikeEntry")]
public class BikeEntry : MonoBehaviour
{
    private const int REVERSE_LOOK_CONFIGURATION_VERSION = 2;

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
    [Tooltip("Normalized point where the rider root begins blending onto the authored seat so the hips arrive without a final snap.")]
    [Range(0.35f, 0.95f)] public float enterSeatPositionBlendStart = 0.72f;
    [Tooltip("Time for the exit animation to ease in.")]
    public float exitAnimationTransitionIn = 0.1f;
    [Tooltip("Time for the exit animation to ease out.")]
    public float exitAnimationTransitionOut = 0.25f;

    [Tooltip("If true, the animation uses root motion.")]
    public bool useRootMotion = true;

    [Header("Exit Stop")]
    [Tooltip("The exit animation starts only after planar bike speed falls below this threshold.")]
    [Min(0f)] public float stoppedExitSpeedKph = 1.25f;
    [Tooltip("Maximum braking time before the bike is forced to a complete safe stop.")]
    [Min(0.1f)] public float exitStopTimeout = 5f;

    [Header("Enter Alignment")]
    [Tooltip("Time used to turn the character parallel with the bike and blend the nearest hand onto the handlebar.")]
    [Min(0.05f)] public float enterAlignmentDuration = 0.35f;

    [Header("Driving State")]
    [Tooltip("State that represents the driving mode.")]
    public StateData drivingState = new StateData(StateData.StateType.State);
    [Tooltip("Layer for the driving state.")]
    public int drivingStateLayer = 1;
    [Tooltip("Transition time when entering driving state.")]
    public float drivingStateTransitionIn = 0.1f;
    [Tooltip("Transition time when exiting driving state.")]
    public float drivingStateTransitionOut = 0.25f;

    [Header("Entry Parent Settings")]
    [Tooltip("A fixed transform defining the seat position. It should be a child of the bike's visual mesh so it rotates with the bike.")]
    public Transform entryParent;

    [Header("Entry Approach")]
    [Tooltip("Exact character root position and facing direction before the bike entry animation starts.")]
    public Transform entryStandingPoint;
    [Tooltip("Automatically uses the nearest side, or forces the original/mirrored enter side.")]
    public BikeEntrySideMode entrySideMode = BikeEntrySideMode.Automatic;
    [Tooltip("Humanoid-mirrored version of Entry Animation for mounting from the opposite side.")]
    public AnimationClip mirroredEntryAnimation;
    [Tooltip("Standing point mirrored across the bike-local X center plane.")]
    public Transform mirroredEntryStandingPoint;
    public bool alignCharacterToStandingPoint = true;
    [Min(0.15f)] public float entryApproachStopDistance = 0.15f;
    [Min(0.1f)] public float entryApproachTimeout = 4f;
    [Min(0f)] public float entryApproachAlignmentDuration = 0.12f;
    [Min(1)] public int entryApproachMotionPriority = 10;

    [Header("IK Settings")]
    [Tooltip("Target transform for the left hand on the bike's handlebar.")]
    public Transform steeringWheelLeftHandTarget;
    [Tooltip("Target transform for the right hand on the bike's handlebar.")]
    public Transform steeringWheelRightHandTarget;
    [Tooltip("IK weight for the left hand.")]
    public float leftHandIKWeight = 1f;
    [Tooltip("IK weight for the right hand.")]
    public float rightHandIKWeight = 1f;
    [Tooltip("Hand rotation IK weight. Position stays fully locked to the grips; lower values allow natural wrist rotation.")]
    [Range(0f, 1f)] public float handIKRotationWeight = 0.5f;

    [Tooltip("Where the left foot should go when planted on the ground.")]
    public Transform groundLeftFootTarget;
    [Tooltip("Optional right-foot ground target. When empty it is mirrored from Ground Left Foot across the bike center line.")]
    public Transform groundRightFootTarget;
    [Tooltip("The left foot moves to the ground target while bike speed is below this value in km/h.")]
    [Min(0f)] public float groundFootSpeedKph = 3f;
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

    [Header("Reverse Foot Push")]
    [Tooltip("Alternates both seated rider feet on the ground while the bike reverses.")]
    public bool reverseFootPushEnabled = true;
    [Tooltip("Duration of one complete left/right pushing cycle.")]
    [Min(0.35f)] public float reverseFootPushCycleDuration = 0.85f;
    [Tooltip("Fore/aft travel of each planted foot in bike-local metres.")]
    [Range(0.02f, 0.35f)] public float reverseFootPushStride = 0.27f;
    [Tooltip("Maximum lift of the returning foot in bike-local metres.")]
    [Range(0.01f, 0.2f)] public float reverseFootPushLift = 0.11f;
    [Tooltip("How quickly the IK targets follow the generated pushing pose.")]
    [Min(0.1f)] public float reverseFootPushResponse = 22f;
    [Tooltip("Stops the gesture above this speed. Keep it slightly above reverse max speed.")]
    [Min(0.5f)] public float reverseFootPushMaxSpeedKph = 8f;

    [Header("Reverse Look Back")]
    [Tooltip("Turns the seated rider's upper chest, neck and head toward the rear while reversing.")]
    public bool reverseLookEnabled = true;
    [Tooltip("Look over the left shoulder by default. Disable to look over the right shoulder.")]
    public bool reverseLookOverLeftShoulder = true;
    [Tooltip("Total horizontal look angle distributed across UpperChest, Neck and Head.")]
    [Range(45f, 145f)] public float reverseLookYaw = 85f;
    [Tooltip("Small downward head pitch that keeps the eyes toward the reverse path.")]
    [Range(-15f, 15f)] public float reverseLookHeadPitch = -5f;
    [Range(0f, 1f)] public float reverseLookUpperChestWeight = 0.22f;
    [Range(0f, 1f)] public float reverseLookNeckWeight = 0.35f;
    [Range(0f, 1f)] public float reverseLookHeadWeight = 0.43f;
    [Tooltip("Moves the left hand away from the handlebar while looking backward.")]
    public bool reverseLookReleaseLeftHand = false;
    [Tooltip("Optional authored hand target. When empty, a stable target is derived from the rider's left thigh.")]
    public Transform reverseLookLeftHandTarget;
    [Tooltip("Local fine adjustment relative to the resolved left-hand release target.")]
    public Vector3 reverseLookLeftHandOffset = Vector3.zero;
    [Tooltip("How strongly the left hand leaves the grip at the full reverse-look pose.")]
    [Range(0f, 1f)] public float reverseLookLeftHandReleaseWeight = 1f;
    [Tooltip("Seconds to blend from the normal riding pose into the reverse look.")]
    [Min(0.02f)] public float reverseLookBlendIn = 0.18f;
    [Tooltip("Seconds to return smoothly to the forward riding pose.")]
    [Min(0.02f)] public float reverseLookBlendOut = 0.24f;
    [SerializeField, HideInInspector]
    private int reverseLookConfigurationVersion;

    [Header("Rider Fit - Seat / Grips / Footpegs")]
    [Tooltip("Ergonomic family used by the editor Rider Fit tool.")]
    public RiderFitStyle riderFitStyle = RiderFitStyle.Standard;
    [Tooltip("Original bike idle clip used to build the generated pose clip.")]
    public AnimationClip riderPoseSourceClip;
    [Tooltip("Per-bike pose clip generated by the Rider Fit tool. This clip owns the torso pose.")]
    public AnimationClip riderPoseClip;
    [Tooltip("Fine adjustment of the rider root relative to the seat target, in bike-local space.")]
    public Vector3 riderSeatOffset = Vector3.zero;
    [Tooltip("Manual local-position offset for the Humanoid Spine bone. This never moves the rider parent or seat anchor.")]
    public Vector3 riderSpinePositionOffset = Vector3.zero;
    [Tooltip("Manual local Euler rotation offset, in degrees, for the Humanoid Spine bone.")]
    public Vector3 riderSpineRotationOffset = Vector3.zero;
    [Tooltip("Forward pitch of the pelvis/root in the generated pose clip.")]
    [Range(0f, 55f)] public float riderPelvisPitch = 10f;
    [Tooltip("Normalized lower-back curl written into the generated Humanoid clip.")]
    [Range(0f, 0.65f)] public float riderLowerBackCurl = 0.18f;
    [Tooltip("Normalized chest curl written into the generated Humanoid clip.")]
    [Range(0f, 0.55f)] public float riderChestCurl = 0.12f;
    [Tooltip("Normalized upper-chest curl used to form a gradual spinal arc.")]
    [Range(0f, 0.4f)] public float riderUpperChestCurl = 0.05f;
    [Tooltip("Counter rotation that keeps the rider looking forward.")]
    [Range(0f, 0.7f)] public float riderNeckLift = 0.18f;
    [Tooltip("Head counter rotation that keeps the eyes toward the road.")]
    [Range(0f, 0.7f)] public float riderHeadLift = 0.18f;

    [Header("Airborne Rider Lift")]
    [Tooltip("Local Y distance that raises the rider root when both bike wheels leave the ground.")]
    [Range(0f, 0.25f)] public float riderAirborneLift = 0.08f;
    [Tooltip("Short delay that filters single-frame wheel contact loss.")]
    [Range(0f, 0.25f)] public float riderAirborneLiftDelay = 0.06f;
    [Tooltip("Blend time used to raise and return the rider root without snapping.")]
    [Range(0.02f, 0.5f)] public float riderAirborneLiftSmoothTime = 0.1f;

    [Header("Fallen Bike Recovery")]
    [Tooltip("Full-body crouch/bend gesture used while the Player raises a fallen bike.")]
    public AnimationClip fallenBikeRecoveryAnimation;
    [Tooltip("Body grab target used when the bike is lying on its left surface.")]
    public Transform fallenBikeBodyGripLeft;
    [Tooltip("Mirrored body grab target used when the bike is lying on its right surface.")]
    public Transform fallenBikeBodyGripRight;
    [Min(0.2f)] public float fallenBikeReachDuration = 0.45f;
    [Min(0.3f)] public float fallenBikeRaiseDuration = 1.15f;
    [Min(0.4f)] public float fallenBikeStandDistance = 0.85f;
    [Tooltip("Horizontal reach from the Player root to the fallen-bike grip center before lifting.")]
    [Min(0.2f)] public float fallenBikeGripReach = 0.48f;
    [Tooltip("Maximum distance the parked bike may slide toward the Player to guarantee both hands can reach it.")]
    [Min(0f)] public float fallenBikeMaxAssistDistance = 0.75f;
    [Tooltip("If GC2 cannot reach the lift point, pull the parked fallen bike toward the Player and continue recovery.")]
    public bool fallenBikeMoveToPlayerWhenBlocked = true;
    [Tooltip("Maximum time GC2 may try the recovery point before the bike takes over.")]
    [Min(0.1f)] public float fallenBikeBlockedApproachTimeout = 1.25f;
    [Tooltip("Duration of the one-shot fallen-bike relocation toward the Player.")]
    [Min(0.05f)] public float fallenBikeRelocationDuration = 0.35f;
    [Tooltip("Safety limit for a single blocked-recovery relocation.")]
    [Min(0.5f)] public float fallenBikeMaxRelocationDistance = 3f;
    [Range(0f, 1f)] public float fallenBikeHandRotationWeight = 0.35f;
    public Vector3 fallenBikeSpinePositionOffset = new Vector3(0f, -0.06f, 0.04f);
    public Vector3 fallenBikeSpineRotationOffset = new Vector3(32f, 0f, 0f);
    [SerializeField, HideInInspector]
    private int fallenBikeBlockedRecoveryConfigurationVersion;

    // Legacy procedural lean data is kept only so existing prefabs deserialize
    // without losing user values. Rider Fit clips now own the torso pose.
    [HideInInspector]
    [Tooltip("Full-spine forward arc in degrees. Typical sport values are 30-45; the extended range supports custom poses.")]
    [Range(0f, 90f)] public float riderForwardLean = 0f;
    [HideInInspector]
    [Tooltip("How quickly the rider blends into and out of the configured forward lean.")]
    [Min(0.01f)] public float riderLeanResponse = 9f;
    [HideInInspector]
    [Tooltip("Influence multiplier for the hips portion of the default 10/60/20/10 reach curve. Set to 0 to keep the pelvis unchanged.")]
    [Range(0f, 2f)] public float riderHipsLeanWeight = 1f;
    [HideInInspector]
    [Tooltip("Influence multiplier for the lower-spine portion of the default 10/60/20/10 reach curve.")]
    [Range(0f, 2f)] public float riderSpineLeanWeight = 1f;
    [HideInInspector]
    [Tooltip("Influence multiplier for the chest portion of the default 10/60/20/10 reach curve.")]
    [Range(0f, 2f)] public float riderChestLeanWeight = 1f;
    [HideInInspector]
    [Tooltip("Influence multiplier for the upper-chest portion of the default 10/60/20/10 reach curve.")]
    [Range(0f, 2f)] public float riderUpperChestLeanWeight = 1f;

    [Header("On Enter Instructions")]
    [Space]
    [Tooltip("Instructions to run when the character finishes entering.")]
    [SerializeField] public InstructionList onEnter = new InstructionList();
    [Header("On Exit Instructions")]
    [Space]
    [Tooltip("Instructions to run when the character finishes exiting.")]
    [SerializeField] public InstructionList onExit = new InstructionList();

    private IRvrVehicleDriveController bikeController;
    private IRvrVehicleAirborneState airborneState;
    private FranklinArcadeBikeRagdoll arcadeBikeRagdoll;
    private FranklinBikeHealth bikeHealth;
    private FranklinBikePassengerSeat _passengerSeat;
    private Transform bikeBody;
    private Collider bikeCollider;
    private bool isEntering = false;
    private bool isExiting = false;
    private Vector3 _origLeftFootLocalPos;
    private Quaternion _origLeftFootLocalRot;
    private Vector3 _origRightFootLocalPos;
    private Quaternion _origRightFootLocalRot;
    private bool _inBike = false;
    private bool _footOnGround = false;
    private Coroutine _footRoutine;
    private Coroutine _rightFootRoutine;
    private bool _reverseFootPushActive;
    private float _reverseFootPushTime;
    private float _reverseLookWeight;
    private FranklinBikeHelmetController _riderHelmetController;
    private Character _mountedCharacter = null;
    private bool _liveRiderPosePreview;
    private Animator _livePoseAnimator;
    private HumanPoseHandler _livePoseHandler;
    private HumanPose _liveHumanPose;
    private CharacterIKSetter _activeIkSetter;
    private bool _shooterPoseActive;
    private Character _approachCharacter;
    private Character _transitionCharacter;
    private bool _approachFinished;
    private bool _approachSucceeded;
    private int _approachVersion;
    private int _operationVersion;
    private Transform _activeEntryStandingPoint;
    private bool _activeEntryMirrored;
    private CharacterPhysicsSnapshot _seatedPhysics;
    private readonly CharacterPhysicsSnapshot _physicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly List<Collider> _colliderBuffer = new List<Collider>(8);
    private readonly List<Rigidbody> _rigidbodyBuffer = new List<Rigidbody>(4);
    private readonly List<Collider> _entryCharacterColliderBuffer =
        new List<Collider>(12);
    private readonly List<Collider> _entryVehicleColliderBuffer =
        new List<Collider>(24);
    private readonly List<IgnoredCollisionPair> _ignoredEntryCollisionPairs =
        new List<IgnoredCollisionPair>(48);
    private readonly RaycastHit[] _recoveryGroundHits = new RaycastHit[16];
    private float _airborneDuration;
    private float _currentAirborneLift;
    private float _airborneLiftVelocity;

    private const int SEATED_GRAVITY_LOCK_KEY = 0x42494B45;
    private const int CURRENT_BLOCKED_RECOVERY_CONFIGURATION_VERSION = 1;

    private struct IgnoredCollisionPair
    {
        public Collider characterCollider;
        public Collider vehicleCollider;
    }

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

    private sealed class CharacterPhysicsSnapshot
    {
        public Character character;
        public bool driverCollision;
        public bool driverUpdateKinematics;
        public Character.MovementType movementType;
        public readonly List<ColliderSnapshot> colliders = new List<ColliderSnapshot>(8);
        public readonly List<RigidbodySnapshot> rigidbodies = new List<RigidbodySnapshot>(4);
    }

    private static bool s_HumanoidMusclesResolved;
    private static int s_SpineFrontBackMuscle = -1;
    private static int s_ChestFrontBackMuscle = -1;
    private static int s_UpperChestFrontBackMuscle = -1;
    private static int s_NeckNodMuscle = -1;
    private static int s_HeadNodMuscle = -1;

    public Character SeatedCharacter => _mountedCharacter;
    public bool IsTransitioning => isEntering || isExiting;
    public bool IsUsingMirroredEntry => _activeEntryMirrored;
    public bool HasCurrentBlockedRecoveryConfiguration =>
        fallenBikeBlockedRecoveryConfigurationVersion >=
        CURRENT_BLOCKED_RECOVERY_CONFIGURATION_VERSION;

    public void ConfigureBlockedFallenBikeRecovery()
    {
        fallenBikeMoveToPlayerWhenBlocked = true;
        fallenBikeBlockedApproachTimeout = 1.25f;
        fallenBikeRelocationDuration = 0.35f;
        fallenBikeMaxRelocationDistance = 3f;
        fallenBikeBlockedRecoveryConfigurationVersion =
            CURRENT_BLOCKED_RECOVERY_CONFIGURATION_VERSION;
    }

    /// <summary>
    /// Entry API used by the same Franklin interaction manager as cars.
    /// </summary>
    public bool RequestEnter(Character character)
    {
        if (!isActiveAndEnabled || character == null ||
            character.Ragdoll.IsRagdoll || IsTransitioning ||
            _mountedCharacter != null ||
            character.Motion == null || entryParent == null || entryStandingPoint == null ||
            (bikeHealth != null && bikeHealth.IsDestroyed))
        {
            return false;
        }
        if (arcadeBikeRagdoll != null && arcadeBikeRagdoll.IsRagdoll &&
            !arcadeBikeRagdoll.TryPrepareManualRecoveryForInteraction())
        {
            return false;
        }
        SelectEntrySide(character);
        EnterBike(character);
        return true;
    }

    /// <summary>
    /// Exit API used by Franklin's shared car-control HUD.
    /// </summary>
    public bool RequestExit(Character character)
    {
        if (!isActiveAndEnabled || character == null ||
            character != _mountedCharacter || IsTransitioning)
        {
            return false;
        }
        ExitBike(character);
        return true;
    }

    /// <summary>
    /// Immediately releases a seated rider for a physical crash. Unlike ExitBike,
    /// this does not play the left-side exit animation, move the character to the
    /// standing point, or park the bike. The caller can start GC2 ragdoll safely
    /// after the seated collider/rigidbody snapshot has been restored.
    /// </summary>
    public bool ReleaseForCrash(Character character)
    {
        if (character == null || character != _mountedCharacter || IsTransitioning)
            return false;

        SetShooterPoseActive(false);
        ClearActiveEntrySide();
        SetLiveRiderPosePreview(false);

        ResetFootTargetsImmediate();

        Animator animator = character.GetComponentInChildren<Animator>();
        CharacterIKSetter ikSetter = animator != null
            ? animator.GetComponent<CharacterIKSetter>()
            : null;
        if (ikSetter != null)
        {
            ikSetter.ResetVehiclePose();
        }

        _activeIkSetter = null;
        _riderHelmetController = null;
        _inBike = false;
        _footOnGround = false;
        ResetAirborneRiderLift();
        _mountedCharacter = null;

        character.transform.SetParent(null, true);
        EndEntryCollisionIgnore();
        RestoreCharacterPhysics(character);
        // A physical release must remove the rider clip immediately. In
        // particular, death ragdoll captures the current bones synchronously.
        character.States.Stop(drivingStateLayer, 0f, 0f);
        if (character.Player != null) character.Player.IsControllable = false;

        Physics.SyncTransforms();
        _ = this.onExit.Run(new Args(this.gameObject));
        return true;
    }

    private void Awake()
    {
        ApplyReverseLookDefaultsIfNeeded();
        ResolveHumanoidMuscles();

        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour is not IRvrVehicleDriveController controller) continue;
            bikeController = controller;
            airborneState = behaviour as IRvrVehicleAirborneState;
            break;
        }

        arcadeBikeRagdoll = GetComponent<FranklinArcadeBikeRagdoll>();
        bikeHealth = GetComponent<FranklinBikeHealth>();
        _passengerSeat = GetComponent<FranklinBikePassengerSeat>();
        bikeBody = bikeController?.VehicleBody;
        bikeCollider = ResolvePhysicsCollider();

        if (entryParent != null
         && bikeController != null
         && bikeBody != null)
        {
            entryParent.SetParent(bikeBody, true);
        }

        if (leftFootTarget != null)
        {
            _origLeftFootLocalPos = leftFootTarget.localPosition;
            _origLeftFootLocalRot = leftFootTarget.localRotation;
        }
        if (rightFootTarget != null)
        {
            _origRightFootLocalPos = rightFootTarget.localPosition;
            _origRightFootLocalRot = rightFootTarget.localRotation;
        }
    }

    private void OnDisable()
    {
        // Async/await transitions are not owned by Unity's coroutine lifecycle.
        // A pooled or unloaded Bike can therefore leave an old entry/exit loop,
        // collision-ignore pairs and a locked Character alive unless we cancel
        // the operation explicitly here.
        ++_operationVersion;
        ++_approachVersion;
        _approachFinished = true;

        Character character = _mountedCharacter ??
                              _seatedPhysics?.character ??
                              _transitionCharacter ??
                              _approachCharacter;
        if (_approachCharacter != null)
        {
            _approachCharacter.Motion?.MoveToDirection(
                Vector3.zero,
                Space.World,
                Mathf.Max(1, entryApproachMotionPriority)
            );
            _approachCharacter.Motion?.StopToDirection(
                Mathf.Max(1, entryApproachMotionPriority)
            );
        }

        StopFootRoutines();
        ResetAirborneRiderLift();
        ReleaseLivePoseHandler();
        if (_activeIkSetter != null) _activeIkSetter.ResetVehiclePose();

        if (character != null)
        {
            _ = FranklinShooterSystem.CancelBikeDriverEntry(character);
            if (character.transform.parent == entryParent)
                character.transform.SetParent(null, true);
            RestoreCharacterPhysics(character);
            character.States.Stop(drivingStateLayer, 0f, 0f);
            if (character.Player != null) character.Player.IsControllable = true;
        }

        EndEntryCollisionIgnore();
        _activeIkSetter = null;
        _riderHelmetController = null;
        _mountedCharacter = null;
        _transitionCharacter = null;
        _approachCharacter = null;
        _inBike = false;
        _footOnGround = false;
        _reverseFootPushActive = false;
        _reverseFootPushTime = 0f;
        _reverseLookWeight = 0f;
        _shooterPoseActive = false;
        isEntering = false;
        isExiting = false;
        ClearActiveEntrySide();
    }

    private bool IsOperationCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == _operationVersion;
    }

    private async Task<bool> AbortEntryForCharacterRagdollAsync(
        Character character,
        int operationVersion,
        Rigidbody vehicleRigidbody = null,
        bool restoreVehicleKinematic = false,
        bool previousVehicleKinematic = false,
        CharacterIKSetter ikSetter = null)
    {
        if (!IsOperationCurrent(operationVersion) || character == null ||
            !character.Ragdoll.IsRagdoll)
        {
            return false;
        }

        ++_operationVersion;
        ++_approachVersion;
        _approachFinished = true;
        character.Motion?.MoveToDirection(
            Vector3.zero,
            Space.World,
            Mathf.Max(1, entryApproachMotionPriority)
        );
        character.Motion?.StopToDirection(
            Mathf.Max(1, entryApproachMotionPriority)
        );
        character.Gestures.Stop(0f, 0f);
        character.States.Stop(drivingStateLayer, 0f, 0f);
        ikSetter?.ResetVehiclePose();
        EndEntryCollisionIgnore();
        if (restoreVehicleKinematic && vehicleRigidbody != null)
            vehicleRigidbody.isKinematic = previousVehicleKinematic;
        if (character.Player != null)
            character.Player.IsControllable = true;

        _activeIkSetter = null;
        _transitionCharacter = null;
        _approachCharacter = null;
        isEntering = false;
        ClearActiveEntrySide();
        await FranklinShooterSystem.CancelBikeDriverEntry(character);
        return true;
    }

    private void OnValidate()
    {
        ApplyReverseLookDefaultsIfNeeded();
        riderForwardLean = Mathf.Clamp(riderForwardLean, 0f, 90f);
        riderLeanResponse = Mathf.Max(0.01f, riderLeanResponse);
        riderHipsLeanWeight = Mathf.Clamp(riderHipsLeanWeight, 0f, 2f);
        riderSpineLeanWeight = Mathf.Clamp(riderSpineLeanWeight, 0f, 2f);
        riderChestLeanWeight = Mathf.Clamp(riderChestLeanWeight, 0f, 2f);
        riderUpperChestLeanWeight = Mathf.Clamp(riderUpperChestLeanWeight, 0f, 2f);

        riderPelvisPitch = Mathf.Clamp(riderPelvisPitch, 0f, 55f);
        riderLowerBackCurl = Mathf.Clamp(riderLowerBackCurl, 0f, 0.65f);
        riderChestCurl = Mathf.Clamp(riderChestCurl, 0f, 0.55f);
        riderUpperChestCurl = Mathf.Clamp(riderUpperChestCurl, 0f, 0.4f);
        riderNeckLift = Mathf.Clamp(riderNeckLift, 0f, 0.7f);
        riderHeadLift = Mathf.Clamp(riderHeadLift, 0f, 0.7f);
        riderAirborneLift = Mathf.Clamp(riderAirborneLift, 0f, 0.25f);
        riderAirborneLiftDelay = Mathf.Clamp(riderAirborneLiftDelay, 0f, 0.25f);
        riderAirborneLiftSmoothTime = Mathf.Clamp(
            riderAirborneLiftSmoothTime,
            0.02f,
            0.5f
        );
        fallenBikeReachDuration = Mathf.Max(0.2f, fallenBikeReachDuration);
        fallenBikeRaiseDuration = Mathf.Max(0.3f, fallenBikeRaiseDuration);
        fallenBikeStandDistance = Mathf.Max(0.4f, fallenBikeStandDistance);
        fallenBikeGripReach = Mathf.Max(0.2f, fallenBikeGripReach);
        fallenBikeMaxAssistDistance = Mathf.Max(0f, fallenBikeMaxAssistDistance);
        fallenBikeBlockedApproachTimeout = Mathf.Max(
            0.1f,
            fallenBikeBlockedApproachTimeout
        );
        fallenBikeRelocationDuration = Mathf.Max(0.05f, fallenBikeRelocationDuration);
        fallenBikeMaxRelocationDistance = Mathf.Max(
            0.5f,
            fallenBikeMaxRelocationDistance
        );
        fallenBikeHandRotationWeight = Mathf.Clamp01(fallenBikeHandRotationWeight);
        groundFootSpeedKph = Mathf.Max(0f, groundFootSpeedKph);
        footSmoothingTime = Mathf.Max(0.01f, footSmoothingTime);
        reverseFootPushCycleDuration = Mathf.Max(0.35f, reverseFootPushCycleDuration);
        reverseFootPushStride = Mathf.Clamp(reverseFootPushStride, 0.02f, 0.35f);
        reverseFootPushLift = Mathf.Clamp(reverseFootPushLift, 0.01f, 0.2f);
        reverseFootPushResponse = Mathf.Max(0.1f, reverseFootPushResponse);
        reverseFootPushMaxSpeedKph = Mathf.Max(0.5f, reverseFootPushMaxSpeedKph);
        reverseLookYaw = Mathf.Clamp(reverseLookYaw, 45f, 145f);
        reverseLookHeadPitch = Mathf.Clamp(reverseLookHeadPitch, -15f, 15f);
        reverseLookUpperChestWeight = Mathf.Clamp01(reverseLookUpperChestWeight);
        reverseLookNeckWeight = Mathf.Clamp01(reverseLookNeckWeight);
        reverseLookHeadWeight = Mathf.Clamp01(reverseLookHeadWeight);
        reverseLookLeftHandReleaseWeight =
            Mathf.Clamp01(reverseLookLeftHandReleaseWeight);
        reverseLookBlendIn = Mathf.Max(0.02f, reverseLookBlendIn);
        reverseLookBlendOut = Mathf.Max(0.02f, reverseLookBlendOut);
        stoppedExitSpeedKph = Mathf.Max(0f, stoppedExitSpeedKph);
        exitStopTimeout = Mathf.Max(0.1f, exitStopTimeout);
        enterAlignmentDuration = Mathf.Max(0.05f, enterAlignmentDuration);
        enterSeatPositionBlendStart = Mathf.Clamp(
            enterSeatPositionBlendStart,
            0.35f,
            0.95f
        );
        entryApproachStopDistance = Mathf.Max(0.15f, entryApproachStopDistance);
        entryApproachTimeout = Mathf.Max(0.1f, entryApproachTimeout);
        entryApproachAlignmentDuration = Mathf.Max(0f, entryApproachAlignmentDuration);
        entryApproachMotionPriority = Mathf.Max(1, entryApproachMotionPriority);

        // Limb IK, seat offset and the manual Spine offsets are safe to preview
        // live. The generated Humanoid torso values remain memory-only in Play.
        if (Application.isPlaying)
        {
            ApplyRiderHandIK();
        }
    }

    private void ApplyReverseLookDefaultsIfNeeded()
    {
        if (reverseLookConfigurationVersion >= REVERSE_LOOK_CONFIGURATION_VERSION)
            return;

        // Version 1 used raw local-bone yaw and a one-hand cinematic pose. On
        // Franklin's Humanoid axes this produced lateral spine bend and neck
        // extension. Version 2 uses the controlled backing posture instead.
        reverseLookYaw = 85f;
        reverseLookHeadPitch = -5f;
        reverseLookUpperChestWeight = 0.22f;
        reverseLookNeckWeight = 0.35f;
        reverseLookHeadWeight = 0.43f;
        reverseLookReleaseLeftHand = false;
        reverseLookConfigurationVersion = REVERSE_LOOK_CONFIGURATION_VERSION;
    }

    public void ApplyRiderHandIK()
    {
        if (_mountedCharacter == null) return;

        Animator riderAnimator = _mountedCharacter.GetComponentInChildren<Animator>();
        CharacterIKSetter ikSetter = riderAnimator != null
            ? riderAnimator.GetComponent<CharacterIKSetter>()
            : null;

        if (ikSetter != null)
        {
            ConfigureRiderIKSetter(ikSetter);
            ikSetter.SetIKTargets(
                steeringWheelLeftHandTarget,
                steeringWheelRightHandTarget,
                leftHandIKWeight,
                rightHandIKWeight,
                handIKRotationWeight,
                handIKRotationWeight
            );
        }
    }

    /// <summary>
    /// Enables a memory-only Humanoid preview. This intentionally does not
    /// modify or reimport riderPoseClip and does not rebuild Game Creator's
    /// Playable graph while it is being evaluated.
    /// </summary>
    public void SetLiveRiderPosePreview(bool enabled)
    {
        _liveRiderPosePreview = enabled && Application.isPlaying;
        if (_activeIkSetter != null)
        {
            _activeIkSetter.SetBeforeHandIK(
                _liveRiderPosePreview && !_shooterPoseActive
                    ? ApplyLiveRiderPosePreview
                    : null
            );
        }
        if (!_liveRiderPosePreview) ReleaseLivePoseHandler();
    }

    /// <summary>
    /// Gives the upper body to the active GC2 weapon Sight without disturbing the
    /// seated pelvis, legs or handlebar hand. The authored Bike spine offset is restored
    /// as soon as ADS/fire ends.
    /// </summary>
    public void SetShooterPoseActive(bool active)
    {
        if (_shooterPoseActive == active) return;

        _shooterPoseActive = active;
        if (active) ResetReverseLookPose();
        if (_activeIkSetter != null) ConfigureRiderIKSetter(_activeIkSetter);
        if (active) ReleaseLivePoseHandler();
    }


    private void Update()
    {
        if (!_inBike || bikeController == null || bikeBody == null) return;

        float speedKph = Mathf.Abs(bikeController.SpeedMetersPerSecond) * 3.6f;
        FranklinArcadeBikeDriver arcadeDriver =
            bikeController as FranklinArcadeBikeDriver;
        bool reverseRequested = arcadeDriver?.IsReverseInputActive == true;
        bool isAtReverseSideOfStop = arcadeDriver != null &&
                                     arcadeDriver.SignedForwardSpeedMetersPerSecond <= 0.05f;
        bool movingBackward = arcadeDriver != null &&
                              arcadeDriver.SignedForwardSpeedMetersPerSecond < -0.1f;
        bool reverseLookHardBlocked =
            _shooterPoseActive ||
            arcadeDriver?.IsFirstPersonViewActive == true ||
            _riderHelmetController?.IsTransitioning == true;
        bool shouldLookBack = reverseLookEnabled &&
                              !reverseLookHardBlocked &&
                              (reverseRequested || movingBackward) &&
                              isAtReverseSideOfStop &&
                              airborneState?.IsAirborne != true;
        if (reverseLookHardBlocked)
        {
            if (_reverseLookWeight > 0f) ResetReverseLookPose();
        }
        else
        {
            UpdateReverseLookPose(shouldLookBack);
        }
        bool shouldPushWithFeet = reverseFootPushEnabled &&
                                  reverseRequested &&
                                  isAtReverseSideOfStop &&
                                  speedKph <= reverseFootPushMaxSpeedKph &&
                                  airborneState?.IsAirborne != true &&
                                  leftFootTarget != null &&
                                  rightFootTarget != null;

        if (shouldPushWithFeet &&
            TryGetGroundFootPose(
                leftFootTarget,
                groundLeftFootTarget,
                false,
                out Vector3 leftGroundPosition,
                out Quaternion leftGroundRotation
            ) &&
            TryGetGroundFootPose(
                rightFootTarget,
                groundRightFootTarget,
                true,
                out Vector3 rightGroundPosition,
                out Quaternion rightGroundRotation
            ))
        {
            if (!_reverseFootPushActive)
            {
                StopFootRoutines();
                _reverseFootPushActive = true;
                _reverseFootPushTime = 0f;
                _footOnGround = false;
            }

            AnimateReverseFootPush(
                leftGroundPosition,
                leftGroundRotation,
                rightGroundPosition,
                rightGroundRotation
            );
            return;
        }

        if (_reverseFootPushActive)
        {
            _reverseFootPushActive = false;
            _reverseFootPushTime = 0f;
            BlendFeetAfterReversePush(speedKph);
            return;
        }

        bool shouldBeGrounded = speedKph < groundFootSpeedKph;

        if (shouldBeGrounded != _footOnGround)
        {
            _footOnGround = shouldBeGrounded;

            if (_footRoutine != null)
            {
                StopCoroutine(_footRoutine);
                _footRoutine = null;
            }

            Vector3 targetPos;
            Quaternion targetRot;

            if (_footOnGround && TryGetGroundFootPose(
                    leftFootTarget,
                    groundLeftFootTarget,
                    false,
                    out Vector3 groundedPosition,
                    out Quaternion groundedRotation
                ))
            {
                targetPos = groundedPosition;
                targetRot = groundedRotation;
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
        if (_mountedCharacter == null) return;

        bool isAirborne = _inBike && airborneState?.IsAirborne == true;
        _airborneDuration = isAirborne
            ? _airborneDuration + Time.deltaTime
            : 0f;

        float targetLift = isAirborne &&
                           _airborneDuration >= riderAirborneLiftDelay
            ? riderAirborneLift
            : 0f;
        _currentAirborneLift = Mathf.SmoothDamp(
            _currentAirborneLift,
            targetLift,
            ref _airborneLiftVelocity,
            riderAirborneLiftSmoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );
        _mountedCharacter.transform.localPosition =
            riderSeatOffset + Vector3.up * _currentAirborneLift;
    }

    private void ResetAirborneRiderLift()
    {
        _airborneDuration = 0f;
        _currentAirborneLift = 0f;
        _airborneLiftVelocity = 0f;
    }

    private void ApplyLiveRiderPosePreview()
    {
        if (!_liveRiderPosePreview || _mountedCharacter == null) return;

        Animator animator = _mountedCharacter.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isActiveAndEnabled ||
            animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            return;
        }

        if (_livePoseHandler == null || _livePoseAnimator != animator)
        {
            ReleaseLivePoseHandler();
            _livePoseAnimator = animator;
            _livePoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        }

        try
        {
            _livePoseHandler.GetHumanPose(ref _liveHumanPose);
            _liveHumanPose.bodyRotation = Quaternion.Euler(riderPelvisPitch, 0f, 0f);
            SetLiveMuscle(s_SpineFrontBackMuscle, -riderLowerBackCurl);
            SetLiveMuscle(s_ChestFrontBackMuscle, -riderChestCurl);
            SetLiveMuscle(s_UpperChestFrontBackMuscle, -riderUpperChestCurl);
            SetLiveMuscle(s_NeckNodMuscle, riderNeckLift);
            SetLiveMuscle(s_HeadNodMuscle, riderHeadLift);
            _livePoseHandler.SetHumanPose(ref _liveHumanPose);
        }
        catch (System.Exception)
        {
            // A character can rebuild its Animator during state transitions.
            // Drop this preview handler and retry safely on a later frame.
            ReleaseLivePoseHandler();
        }
    }

    private void SetLiveMuscle(int muscleIndex, float value)
    {
        if (muscleIndex < 0 || _liveHumanPose.muscles == null ||
            muscleIndex >= _liveHumanPose.muscles.Length)
        {
            return;
        }

        _liveHumanPose.muscles[muscleIndex] = value;
    }

    private static int FindHumanoidMuscle(string muscleName)
    {
        string[] muscleNames = HumanTrait.MuscleName;
        for (int index = 0; index < muscleNames.Length; index++)
        {
            if (muscleNames[index] == muscleName) return index;
        }

        return -1;
    }

    private static void ResolveHumanoidMuscles()
    {
        if (s_HumanoidMusclesResolved) return;
        s_HumanoidMusclesResolved = true;
        s_SpineFrontBackMuscle = FindHumanoidMuscle("Spine Front-Back");
        s_ChestFrontBackMuscle = FindHumanoidMuscle("Chest Front-Back");
        s_UpperChestFrontBackMuscle = FindHumanoidMuscle("UpperChest Front-Back");
        s_NeckNodMuscle = FindHumanoidMuscle("Neck Nod Down-Up");
        s_HeadNodMuscle = FindHumanoidMuscle("Head Nod Down-Up");
    }

    private void ReleaseLivePoseHandler()
    {
        _livePoseHandler?.Dispose();
        _livePoseHandler = null;
        _livePoseAnimator = null;
        _liveHumanPose = default;
    }

    private void OnDestroy()
    {
        _shooterPoseActive = false;
        if (_seatedPhysics?.character != null)
            RestoreCharacterPhysics(_seatedPhysics.character);
        if (_activeIkSetter != null)
        {
            _activeIkSetter.SetBeforeHandIK(null);
            _activeIkSetter.ConfigureManualSpineAdjustment(Vector3.zero, Vector3.zero);
        }
        EndEntryCollisionIgnore();
        ReleaseLivePoseHandler();
    }

    public async void EnterBike(Character character)
    {
        if (IsTransitioning || character == null || _mountedCharacter != null ||
            (bikeHealth != null && bikeHealth.IsDestroyed))
        {
            return;
        }
        int operationVersion = ++_operationVersion;
        _transitionCharacter = character;
        _shooterPoseActive = false;
        isEntering = true;
        await FranklinShooterSystem.PrepareForBikeDriverEntry(character);
        if (!IsOperationCurrent(operationVersion) ||
            character == null || character.Ragdoll.IsRagdoll ||
            _mountedCharacter != null)
        {
            await FranklinShooterSystem.CancelBikeDriverEntry(character);
            if (IsOperationCurrent(operationVersion))
            {
                _transitionCharacter = null;
                isEntering = false;
            }
            return;
        }
        character.GetComponentInChildren<FranklinAnimationBridge>(true)
            ?.RestoreModelRootBaseline();
        SelectEntrySide(character);

        if (bikeController != null) bikeController.SetVehicleEnabled(false);

        if (character.Player != null)
            character.Player.IsControllable = false;

        // A bike that is still physically tumbling cannot be mounted. If the
        // rendered side is already grounded and still, interaction can park that
        // pose immediately and hand it to the manual recovery sequence.
        if (arcadeBikeRagdoll != null && arcadeBikeRagdoll.IsRagdoll &&
            !arcadeBikeRagdoll.TryPrepareManualRecoveryForInteraction())
        {
            if (character.Player != null)
                character.Player.IsControllable = true;
            ClearActiveEntrySide();
            await FranklinShooterSystem.CancelBikeDriverEntry(character);
            if (!IsOperationCurrent(operationVersion)) return;
            _transitionCharacter = null;
            isEntering = false;
            return;
        }

        if (arcadeBikeRagdoll != null &&
            arcadeBikeRagdoll.RequiresManualRecovery)
        {
            // Ignore Player/Bike collision before approaching a fallen bike. The
            // parked mesh collider can otherwise block GC2 navigation before the
            // hands ever reach the recovery targets.
            BeginEntryCollisionIgnore(character);
            bool raised = await RaiseFallenBikeBeforeEntry(
                character,
                operationVersion
            );
            if (!IsOperationCurrent(operationVersion)) return;
            if (!raised)
            {
                if (await AbortEntryForCharacterRagdollAsync(
                        character,
                        operationVersion
                    )) return;
                if (character != null && character.Player != null)
                    character.Player.IsControllable = true;
                EndEntryCollisionIgnore();
                ClearActiveEntrySide();
                await FranklinShooterSystem.CancelBikeDriverEntry(character);
                if (!IsOperationCurrent(operationVersion)) return;
                _transitionCharacter = null;
                isEntering = false;
                return;
            }

            // Recovery never owns or offsets the normal enter targets. Resolve
            // the original/mirrored standing point again from its authored pose.
            ClearActiveEntrySide();
            SelectEntrySide(character);
        }

        bool reachedEntry = await MoveCharacterToEntryStandingPointAsync(
            character,
            operationVersion
        );
        if (!IsOperationCurrent(operationVersion)) return;
        if (!reachedEntry)
        {
            if (await AbortEntryForCharacterRagdollAsync(
                    character,
                    operationVersion
                )) return;
            if (character != null && character.Player != null)
                character.Player.IsControllable = true;
            ClearActiveEntrySide();
            await FranklinShooterSystem.CancelBikeDriverEntry(character);
            if (!IsOperationCurrent(operationVersion)) return;
            _transitionCharacter = null;
            isEntering = false;
            return;
        }

        Animator animator = character.GetComponentInChildren<Animator>();
        CharacterIKSetter ikSetter = animator != null
            ? animator.GetComponent<CharacterIKSetter>()
              ?? animator.gameObject.AddComponent<CharacterIKSetter>()
            : null;

        if (ikSetter != null) ConfigureRiderIKSetter(ikSetter);

        if (ikSetter != null)
        {
            // The hand nearest the selected enter side reaches for its grip.
            SetEntryHandIK(ikSetter, 0f);
        }

        Rigidbody vehicleRigidbody = GetComponent<Rigidbody>();
        bool previousVehicleKinematic = vehicleRigidbody != null &&
                                        vehicleRigidbody.isKinematic;

        await Task.Yield();
        if (!IsOperationCurrent(operationVersion)) return;
        if (await AbortEntryForCharacterRagdollAsync(
                character,
                operationVersion,
                vehicleRigidbody,
                false,
                previousVehicleKinematic,
                ikSetter
            )) return;
        if (vehicleRigidbody != null) vehicleRigidbody.isKinematic = true;
        BeginEntryCollisionIgnore(character);

        // The seated state must not animate the gun hand. Without this mask the
        // full-body rider clip keeps pulling RightArm back to the handlebar even
        // after Shooter releases RightHand IK, making the gun and grip share a hand.
        AvatarMask activeDrivingMask =
            FranklinShooterSystem.BikeDriverAnimationMask ?? animationMask;
        StateData activeDrivingState = riderPoseClip != null
            ? new StateData(riderPoseClip, activeDrivingMask)
            : drivingState;
        var drivingConfig = new ConfigState(
            0f, 1f, 1f,
            drivingStateTransitionIn,
            drivingStateTransitionOut
        );
        _ = character.States.SetState(
            activeDrivingState, drivingStateLayer,
            BlendMode.Blend, drivingConfig
        );

        AnimationClip activeEntryAnimation = _activeEntryMirrored &&
                                             mirroredEntryAnimation != null
            ? mirroredEntryAnimation
            : entryAnimation;
        await PlayEntryAnimationAligned(
            character,
            activeEntryAnimation,
            entryAnimationTransitionIn,
            entryAnimationTransitionOut,
            ikSetter,
            operationVersion
        );
        if (!IsOperationCurrent(operationVersion)) return;
        if (await AbortEntryForCharacterRagdollAsync(
                character,
                operationVersion,
                vehicleRigidbody,
                true,
                previousVehicleKinematic,
                ikSetter
            )) return;

        await Task.Delay(10);
        if (!IsOperationCurrent(operationVersion)) return;
        if (await AbortEntryForCharacterRagdollAsync(
                character,
                operationVersion,
                vehicleRigidbody,
                true,
                previousVehicleKinematic,
                ikSetter
            )) return;
        AttachCharacterToSeat(character);

        // The entry gesture temporarily suppresses the driving layer. Re-assert the
        // isolated per-bike clip only after the rider is seated so it cannot be lost
        // at the transition boundary or replaced by a shared car state.
        _ = character.States.SetState(
            activeDrivingState, drivingStateLayer,
            BlendMode.Blend, drivingConfig
        );

        if (character.Player != null)
            character.Player.IsControllable = false;

        if (animator != null)
        {
            ikSetter ??= animator.GetComponent<CharacterIKSetter>()
                         ?? animator.gameObject.AddComponent<CharacterIKSetter>();

            // This component is shared with cars, so it only owns hand/foot IK.
            // The generated per-bike clip owns the rider's pelvis and spine.
            ikSetter.SetIKTargets(
                steeringWheelLeftHandTarget, steeringWheelRightHandTarget,
                leftHandIKWeight, rightHandIKWeight,
                handIKRotationWeight, handIKRotationWeight
            );
            ikSetter.SetFootIKTargets(
                leftFootTarget, rightFootTarget,
                leftFootIKWeight, rightFootIKWeight
            );

        }

        _inBike = true;
        _footOnGround = false;
        _reverseFootPushActive = false;
        _reverseFootPushTime = 0f;
        _reverseLookWeight = 0f;

        if (bikeController != null)
        {
            bikeController.SetVehicleEnabled(true);
        }

        _ = this.onEnter.Run(new Args(this.gameObject));
        _transitionCharacter = null;
        isEntering = false;
    }

    private async Task<bool> RaiseFallenBikeBeforeEntry(
        Character character,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) ||
            character == null || character.Ragdoll.IsRagdoll ||
            arcadeBikeRagdoll == null)
        {
            return false;
        }
        if (!arcadeBikeRagdoll.RequiresManualRecovery &&
            !arcadeBikeRagdoll.TryPrepareManualRecoveryForInteraction())
        {
            return false;
        }
        if (!arcadeBikeRagdoll.TryGetManualRecoveryTarget(
                out Vector3 targetBikePosition,
                out Quaternion targetBikeRotation,
                out Vector3 groundNormal
            ))
        {
            return false;
        }

        int groundedSide = arcadeBikeRagdoll.ParkedGroundSide;
        if (groundedSide == 0)
            groundedSide = Vector3.Dot(transform.right, Vector3.up) >= 0f ? -1 : 1;

        Vector3 sideDirection = targetBikeRotation * Vector3.right * groundedSide;
        sideDirection = Vector3.ProjectOnPlane(sideDirection, groundNormal).normalized;
        if (sideDirection.sqrMagnitude < 0.0001f)
            sideDirection = Vector3.right * groundedSide;

        Vector3 standProbe = targetBikePosition +
                             sideDirection * fallenBikeStandDistance;
        if (!TryFindRecoveryStandingGround(standProbe, out Vector3 standingGround))
        {
            // The interaction button is already limited to a nearby Player. If
            // a complex MeshCollider or ramp hides the ground ray, preserve the
            // Player's current foot height and force a short alignment instead
            // of cancelling the entire lift sequence.
            standingGround = new Vector3(
                standProbe.x,
                character.transform.position.y - character.Motion.Height * 0.5f,
                standProbe.z
            );
        }

        Quaternion standingRotation = Quaternion.LookRotation(
            -sideDirection,
            Vector3.up
        );
        bool reachedRecoveryPoint = await MoveCharacterToRecoveryPoint(
            character,
            standingGround,
            standingRotation,
            operationVersion
        );
        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll) return false;

        bool mirroredRecovery = groundedSide > 0;
        Transform handleTarget = mirroredRecovery
            ? steeringWheelRightHandTarget
            : steeringWheelLeftHandTarget;
        Transform bodyTarget = mirroredRecovery
            ? fallenBikeBodyGripRight
            : fallenBikeBodyGripLeft;

        if (!reachedRecoveryPoint)
        {
            if (!fallenBikeMoveToPlayerWhenBlocked ||
                !await MoveFallenBikeToCharacter(
                    character,
                    handleTarget,
                    bodyTarget,
                    groundNormal,
                    operationVersion
                ))
            {
                return false;
            }

            // The bike now owns the fallback movement. Rebuild the upright target
            // from its new position and face the Player toward the reachable grips.
            if (!arcadeBikeRagdoll.TryGetManualRecoveryTarget(
                    out targetBikePosition,
                    out targetBikeRotation,
                    out groundNormal
                ))
            {
                return false;
            }
            standingRotation = GetRecoveryFacingRotation(
                character,
                handleTarget,
                bodyTarget,
                groundNormal
            );
            await SmoothAlignCharacterToPose(
                character,
                character.transform.position,
                standingRotation,
                operationVersion
            );
            if (!IsOperationCurrent(operationVersion) || character == null ||
                character.Ragdoll.IsRagdoll)
                return false;
        }

        Animator animator = character.GetComponentInChildren<Animator>();
        CharacterIKSetter ikSetter = animator != null
            ? animator.GetComponent<CharacterIKSetter>() ??
              animator.gameObject.AddComponent<CharacterIKSetter>()
            : null;

        _activeIkSetter = ikSetter;

        Vector3 fallenStartBikePosition = transform.position;
        Quaternion fallenStartBikeRotation = transform.rotation;
        Vector3 bikeAssist = CalculateFallenBikeAssistTranslation(
            character,
            handleTarget,
            bodyTarget,
            groundNormal
        );
        Vector3 assistedFallenPosition = fallenStartBikePosition + bikeAssist;
        targetBikePosition = arcadeBikeRagdoll.AlignManualRecoveryTargetToGround(
            targetBikePosition + bikeAssist,
            targetBikeRotation,
            groundNormal
        );

        float reachDuration = Mathf.Max(0.2f, fallenBikeReachDuration);
        float raiseDuration = Mathf.Max(0.3f, fallenBikeRaiseDuration);
        float totalDuration = reachDuration + raiseDuration;
        if (fallenBikeRecoveryAnimation != null)
        {
            var gestureConfig = new ConfigGesture(
                0f,
                totalDuration,
                1f,
                false,
                0.15f,
                0.25f
            );
            _ = character.Gestures.CrossFade(
                fallenBikeRecoveryAnimation,
                animationMask,
                BlendMode.Blend,
                gestureConfig,
                true
            );
        }

        Quaternion characterStartRotation = character.transform.rotation;
        float elapsed = 0f;
        while (elapsed < reachDuration && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion))
        {
            float blend = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / reachDuration)
            );
            character.transform.rotation = Quaternion.Slerp(
                characterStartRotation,
                standingRotation,
                blend
            );
            arcadeBikeRagdoll.SetManualRecoveryPose(
                Vector3.Lerp(fallenStartBikePosition, assistedFallenPosition, blend),
                fallenStartBikeRotation
            );
            if (ikSetter != null)
            {
                ikSetter.ConfigureManualSpineAdjustment(
                    Vector3.Lerp(Vector3.zero, fallenBikeSpinePositionOffset, blend),
                    Vector3.Lerp(Vector3.zero, fallenBikeSpineRotationOffset, blend)
                );
                SetFallenBikeRecoveryHands(
                    ikSetter,
                    mirroredRecovery,
                    handleTarget,
                    bodyTarget,
                    blend
                );
            }
            elapsed += Time.deltaTime;
            await Task.Yield();
        }

        Vector3 raiseStartBikePosition = assistedFallenPosition;
        Quaternion raiseStartBikeRotation = fallenStartBikeRotation;
        elapsed = 0f;
        while (elapsed < raiseDuration && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion))
        {
            float progress = Mathf.Clamp01(elapsed / raiseDuration);
            float smooth = progress * progress * (3f - 2f * progress);
            arcadeBikeRagdoll.SetManualRecoveryPose(
                Vector3.Lerp(raiseStartBikePosition, targetBikePosition, smooth),
                Quaternion.Slerp(raiseStartBikeRotation, targetBikeRotation, smooth)
            );
            character.transform.rotation = standingRotation;
            float standBlend = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(
                0.82f,
                1f,
                progress
            ));
            if (ikSetter != null)
            {
                ikSetter.ConfigureManualSpineAdjustment(
                    Vector3.Lerp(Vector3.zero, fallenBikeSpinePositionOffset, standBlend),
                    Vector3.Lerp(Vector3.zero, fallenBikeSpineRotationOffset, standBlend)
                );
                SetFallenBikeRecoveryHands(
                    ikSetter,
                    mirroredRecovery,
                    handleTarget,
                    bodyTarget,
                    standBlend
                );
            }
            elapsed += Time.deltaTime;
            await Task.Yield();
        }

        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll)
        {
            if (ikSetter != null)
            {
                ikSetter.SetIKTargets(null, null, 0f, 0f, 0f, 0f);
                ikSetter.ConfigureManualSpineAdjustment(Vector3.zero, Vector3.zero);
            }
            _activeIkSetter = null;
            return false;
        }
        arcadeBikeRagdoll.SetManualRecoveryPose(
            targetBikePosition,
            targetBikeRotation
        );
        arcadeBikeRagdoll.CompleteManualRecovery();
        if (ikSetter != null)
        {
            ikSetter.SetIKTargets(null, null, 0f, 0f, 0f, 0f);
            ikSetter.ConfigureManualSpineAdjustment(Vector3.zero, Vector3.zero);
        }
        _activeIkSetter = null;
        character.Gestures.Stop(0.1f, 0.2f);
        await Task.Yield();
        return IsOperationCurrent(operationVersion);
    }

    private Vector3 CalculateFallenBikeAssistTranslation(
        Character character,
        Transform handleTarget,
        Transform bodyTarget,
        Vector3 groundNormal)
    {
        if (character == null || fallenBikeMaxAssistDistance <= 0f)
            return Vector3.zero;

        Vector3 gripCenter = Vector3.zero;
        int gripCount = 0;
        if (handleTarget != null)
        {
            gripCenter += handleTarget.position;
            gripCount++;
        }
        if (bodyTarget != null)
        {
            gripCenter += bodyTarget.position;
            gripCount++;
        }
        gripCenter = gripCount > 0
            ? gripCenter / gripCount
            : transform.position;

        Vector3 normal = groundNormal.sqrMagnitude > 0.001f
            ? groundNormal.normalized
            : Vector3.up;
        Vector3 playerForward = Vector3.ProjectOnPlane(
            character.transform.forward,
            normal
        );
        if (playerForward.sqrMagnitude < 0.001f)
            playerForward = Vector3.ProjectOnPlane(-transform.right, normal);
        playerForward.Normalize();

        Vector3 desiredGripCenter = character.transform.position +
                                    playerForward * fallenBikeGripReach;
        Vector3 assist = Vector3.ProjectOnPlane(
            desiredGripCenter - gripCenter,
            normal
        );
        return Vector3.ClampMagnitude(assist, fallenBikeMaxAssistDistance);
    }

    private async Task<bool> MoveFallenBikeToCharacter(
        Character character,
        Transform handleTarget,
        Transform bodyTarget,
        Vector3 groundNormal,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) ||
            character == null || character.Ragdoll.IsRagdoll ||
            arcadeBikeRagdoll == null ||
            !arcadeBikeRagdoll.RequiresManualRecovery)
        {
            return false;
        }

        Vector3 gripCenter = GetRecoveryGripCenter(handleTarget, bodyTarget);
        Vector3 normal = groundNormal.sqrMagnitude > 0.001f
            ? groundNormal.normalized
            : Vector3.up;
        Vector3 playerToGrip = Vector3.ProjectOnPlane(
            gripCenter - character.transform.position,
            normal
        );
        if (playerToGrip.sqrMagnitude < 0.001f)
        {
            playerToGrip = Vector3.ProjectOnPlane(
                transform.position - character.transform.position,
                normal
            );
        }
        if (playerToGrip.sqrMagnitude < 0.001f)
            playerToGrip = Vector3.ProjectOnPlane(character.transform.forward, normal);
        if (playerToGrip.sqrMagnitude < 0.001f) playerToGrip = Vector3.forward;
        playerToGrip.Normalize();

        Vector3 desiredGripCenter = character.transform.position +
                                    playerToGrip * fallenBikeGripReach;
        Vector3 relocation = Vector3.ProjectOnPlane(
            desiredGripCenter - gripCenter,
            normal
        );
        relocation = Vector3.ClampMagnitude(
            relocation,
            Mathf.Max(0.5f, fallenBikeMaxRelocationDistance)
        );

        character.Motion?.MoveToDirection(
            Vector3.zero,
            Space.World,
            Mathf.Max(1, entryApproachMotionPriority)
        );
        character.Motion?.StopToDirection(Mathf.Max(1, entryApproachMotionPriority));

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + relocation;
        Quaternion parkedRotation = transform.rotation;
        Quaternion playerStartRotation = character.transform.rotation;
        Quaternion playerTargetRotation = Quaternion.LookRotation(
            playerToGrip,
            Vector3.up
        );
        float duration = Mathf.Max(0.05f, fallenBikeRelocationDuration);
        float elapsed = 0f;
        while (elapsed < duration && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion) &&
               arcadeBikeRagdoll.RequiresManualRecovery)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);
            arcadeBikeRagdoll.SetManualRecoveryPose(
                Vector3.Lerp(startPosition, targetPosition, progress),
                parkedRotation
            );
            character.transform.rotation = Quaternion.Slerp(
                playerStartRotation,
                playerTargetRotation,
                progress
            );
            await Task.Yield();
        }

        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll ||
            !arcadeBikeRagdoll.RequiresManualRecovery)
            return false;

        arcadeBikeRagdoll.SetManualRecoveryPose(targetPosition, parkedRotation);
        character.transform.rotation = GetRecoveryFacingRotation(
            character,
            handleTarget,
            bodyTarget,
            normal
        );
        Physics.SyncTransforms();
        return true;
    }

    private Vector3 GetRecoveryGripCenter(
        Transform handleTarget,
        Transform bodyTarget)
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        if (handleTarget != null)
        {
            center += handleTarget.position;
            count++;
        }
        if (bodyTarget != null)
        {
            center += bodyTarget.position;
            count++;
        }
        return count > 0 ? center / count : transform.position;
    }

    private Quaternion GetRecoveryFacingRotation(
        Character character,
        Transform handleTarget,
        Transform bodyTarget,
        Vector3 groundNormal)
    {
        if (character == null) return Quaternion.identity;
        Vector3 normal = groundNormal.sqrMagnitude > 0.001f
            ? groundNormal.normalized
            : Vector3.up;
        Vector3 facing = Vector3.ProjectOnPlane(
            GetRecoveryGripCenter(handleTarget, bodyTarget) -
            character.transform.position,
            normal
        );
        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.ProjectOnPlane(character.transform.forward, normal);
        return facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : character.transform.rotation;
    }

    private void SetFallenBikeRecoveryHands(
        CharacterIKSetter ikSetter,
        bool mirrored,
        Transform handleTarget,
        Transform bodyTarget,
        float weight)
    {
        weight = Mathf.Clamp01(weight);
        float rotationWeight = fallenBikeHandRotationWeight * weight;
        if (mirrored)
        {
            // True mirror: right hand takes the handlebar and left hand takes
            // the body when the bike rests on its right surface.
            float leftWeight = bodyTarget != null ? weight : 0f;
            float rightWeight = handleTarget != null ? weight : 0f;
            ikSetter.SetIKTargets(
                bodyTarget,
                handleTarget,
                leftWeight,
                rightWeight,
                bodyTarget != null ? rotationWeight : 0f,
                handleTarget != null ? rotationWeight : 0f
            );
        }
        else
        {
            float leftWeight = handleTarget != null ? weight : 0f;
            float rightWeight = bodyTarget != null ? weight : 0f;
            ikSetter.SetIKTargets(
                handleTarget,
                bodyTarget,
                leftWeight,
                rightWeight,
                handleTarget != null ? rotationWeight : 0f,
                bodyTarget != null ? rotationWeight : 0f
            );
        }
    }

    private void AnimateReverseFootPush(
        Vector3 leftGroundPosition,
        Quaternion leftGroundRotation,
        Vector3 rightGroundPosition,
        Quaternion rightGroundRotation)
    {
        _reverseFootPushTime += Time.deltaTime;
        float cycle = Mathf.Max(0.35f, reverseFootPushCycleDuration);
        float leftPhase = Mathf.Repeat(_reverseFootPushTime / cycle, 1f);
        float rightPhase = Mathf.Repeat(leftPhase + 0.5f, 1f);

        ApplyReverseFootPose(
            leftFootTarget,
            _origLeftFootLocalRot,
            leftGroundPosition,
            leftGroundRotation,
            leftPhase
        );
        ApplyReverseFootPose(
            rightFootTarget,
            _origRightFootLocalRot,
            rightGroundPosition,
            rightGroundRotation,
            rightPhase
        );
    }

    private void ApplyReverseFootPose(
        Transform foot,
        Quaternion footpegRotation,
        Vector3 groundPosition,
        Quaternion groundRotation,
        float phase)
    {
        if (foot == null) return;

        // Keep the foot planted for most of the cycle so the IK chain reads as
        // a deliberate push rather than a light stepping loop.
        const float stanceFraction = 0.66f;
        Vector3 localForward = foot.parent != null
            ? foot.parent.InverseTransformDirection(bikeBody.forward).normalized
            : bikeBody.forward;
        Vector3 localUp = foot.parent != null
            ? foot.parent.InverseTransformDirection(bikeBody.up).normalized
            : bikeBody.up;

        float forwardOffset;
        float lift = 0f;
        Quaternion targetRotation = groundRotation;

        if (phase < stanceFraction)
        {
            float stance = phase / stanceFraction;
            forwardOffset = Mathf.Lerp(
                -reverseFootPushStride * 0.5f,
                reverseFootPushStride * 0.5f,
                Mathf.SmoothStep(0f, 1f, stance)
            );
        }
        else
        {
            float swing = (phase - stanceFraction) / (1f - stanceFraction);
            forwardOffset = Mathf.Lerp(
                reverseFootPushStride * 0.5f,
                -reverseFootPushStride * 0.5f,
                Mathf.SmoothStep(0f, 1f, swing)
            );
            float swingArc = Mathf.Sin(swing * Mathf.PI);
            lift = reverseFootPushLift * swingArc;
            targetRotation = Quaternion.Slerp(
                groundRotation,
                footpegRotation,
                swingArc * 0.45f
            );
        }

        Vector3 targetPosition = groundPosition +
                                 localForward * forwardOffset +
                                 localUp * lift;
        float response = 1f - Mathf.Exp(
            -Mathf.Max(0.1f, reverseFootPushResponse) * Time.deltaTime
        );
        foot.localPosition = Vector3.Lerp(
            foot.localPosition,
            targetPosition,
            response
        );
        foot.localRotation = Quaternion.Slerp(
            foot.localRotation,
            targetRotation,
            response
        );
    }

    private bool TryGetGroundFootPose(
        Transform foot,
        Transform explicitGroundTarget,
        bool mirrorGroundLeft,
        out Vector3 localPosition,
        out Quaternion localRotation)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        if (foot == null || bikeBody == null) return false;

        Vector3 worldPosition;
        Quaternion worldRotation;
        if (explicitGroundTarget != null)
        {
            worldPosition = explicitGroundTarget.position;
            worldRotation = explicitGroundTarget.rotation;
        }
        else if (mirrorGroundLeft && groundLeftFootTarget != null)
        {
            Vector3 mirroredPosition = bikeBody.InverseTransformPoint(
                groundLeftFootTarget.position
            );
            mirroredPosition.x = -mirroredPosition.x;
            worldPosition = bikeBody.TransformPoint(mirroredPosition);

            Vector3 mirroredForward = bikeBody.InverseTransformDirection(
                groundLeftFootTarget.forward
            );
            Vector3 mirroredUp = bikeBody.InverseTransformDirection(
                groundLeftFootTarget.up
            );
            mirroredForward.x = -mirroredForward.x;
            mirroredUp.x = -mirroredUp.x;
            worldRotation = Quaternion.LookRotation(
                bikeBody.TransformDirection(mirroredForward),
                bikeBody.TransformDirection(mirroredUp)
            );
        }
        else
        {
            return false;
        }

        Transform parent = foot.parent;
        if (parent == null)
        {
            localPosition = worldPosition;
            localRotation = worldRotation;
        }
        else
        {
            localPosition = parent.InverseTransformPoint(worldPosition);
            localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
        }
        return true;
    }

    private void BlendFeetAfterReversePush(float speedKph)
    {
        StopFootRoutines();
        _footOnGround = speedKph < groundFootSpeedKph;

        Vector3 leftPosition = _origLeftFootLocalPos;
        Quaternion leftRotation = _origLeftFootLocalRot;
        if (_footOnGround && TryGetGroundFootPose(
                leftFootTarget,
                groundLeftFootTarget,
                false,
                out Vector3 groundedPosition,
                out Quaternion groundedRotation
            ))
        {
            leftPosition = groundedPosition;
            leftRotation = groundedRotation;
        }

        if (leftFootTarget != null)
        {
            _footRoutine = StartCoroutine(BlendFootTo(
                leftFootTarget,
                leftPosition,
                leftRotation,
                footSmoothingTime
            ));
        }
        if (rightFootTarget != null)
        {
            _rightFootRoutine = StartCoroutine(BlendFootTo(
                rightFootTarget,
                _origRightFootLocalPos,
                _origRightFootLocalRot,
                footSmoothingTime
            ));
        }
    }

    private void StopFootRoutines()
    {
        if (_footRoutine != null)
        {
            StopCoroutine(_footRoutine);
            _footRoutine = null;
        }
        if (_rightFootRoutine != null)
        {
            StopCoroutine(_rightFootRoutine);
            _rightFootRoutine = null;
        }
    }

    private void ResetFootTargetsImmediate()
    {
        StopFootRoutines();
        if (leftFootTarget != null)
        {
            leftFootTarget.localPosition = _origLeftFootLocalPos;
            leftFootTarget.localRotation = _origLeftFootLocalRot;
        }
        if (rightFootTarget != null)
        {
            rightFootTarget.localPosition = _origRightFootLocalPos;
            rightFootTarget.localRotation = _origRightFootLocalRot;
        }
        _reverseFootPushActive = false;
        _reverseFootPushTime = 0f;
        ResetReverseLookPose();
        _footOnGround = false;
    }

    private async Task<bool> MoveCharacterToRecoveryPoint(
        Character character,
        Vector3 groundPoint,
        Quaternion targetRotation,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) ||
            character == null || character.Ragdoll.IsRagdoll ||
            character.Motion == null)
        {
            return false;
        }

        Vector3 targetRootPosition = groundPoint +
                                     Vector3.up * (character.Motion.Height * 0.5f);
        Location motionTarget = new Location(groundPoint, targetRotation);
        _approachCharacter = character;
        _approachFinished = false;
        _approachSucceeded = false;
        int approachVersion = ++_approachVersion;
        character.Motion.MoveToLocation(
            motionTarget,
            Mathf.Max(0.15f, entryApproachStopDistance),
            (callbackCharacter, success) => OnEntryApproachFinished(
                callbackCharacter,
                success,
                approachVersion
            ),
            Mathf.Max(1, entryApproachMotionPriority)
        );

        float deadline = Time.unscaledTime + Mathf.Min(
            Mathf.Max(0.1f, entryApproachTimeout),
            Mathf.Max(0.1f, fallenBikeBlockedApproachTimeout)
        );
        while (!_approachFinished && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion) &&
               Time.unscaledTime < deadline)
        {
            await Task.Yield();
        }

        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll)
        {
            _approachCharacter = null;
            return false;
        }

        Vector3 horizontalError = Vector3.ProjectOnPlane(
            character.transform.position - targetRootPosition,
            Vector3.up
        );
        float allowedError = Mathf.Max(0.15f, entryApproachStopDistance) + 0.05f;
        bool reachedArea = horizontalError.sqrMagnitude <= allowedError * allowedError;
        // GC2 may report a completed path at the closest navigable point on the
        // opposite side of a wall. Only the real world-space distance is allowed
        // to start the lift; otherwise the bike performs the fallback relocation.
        bool succeeded = reachedArea;
        if (!succeeded)
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

        await SmoothAlignCharacterToPose(
            character,
            targetRootPosition,
            targetRotation,
            operationVersion
        );
        if (!IsOperationCurrent(operationVersion) ||
            character.Ragdoll.IsRagdoll) return false;
        _approachCharacter = null;
        return true;
    }

    private async Task SmoothAlignCharacterToPose(
        Character character,
        Vector3 targetPosition,
        Quaternion targetRotation,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll) return;

        float duration = Mathf.Max(0f, entryApproachAlignmentDuration);
        Vector3 startPosition = character.transform.position;
        Quaternion startRotation = character.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion))
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;
            progress = progress * progress * (3f - 2f * progress);
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, progress),
                Quaternion.Slerp(startRotation, targetRotation, progress)
            );
            await Task.Yield();
        }

        if (character != null && !character.Ragdoll.IsRagdoll &&
            IsOperationCurrent(operationVersion))
        {
            character.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );
            character.Driver?.ResetVerticalVelocity();
        }
    }

    private bool TryFindRecoveryStandingGround(
        Vector3 probePosition,
        out Vector3 groundPoint)
    {
        groundPoint = probePosition;
        int hitCount = Physics.RaycastNonAlloc(
            probePosition + Vector3.up * 2f,
            Vector3.down,
            _recoveryGroundHits,
            6f,
            ~0,
            QueryTriggerInteraction.Ignore
        );
        float closestDistance = float.PositiveInfinity;
        bool found = false;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = _recoveryGroundHits[index];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;
            Rigidbody support = hit.collider.attachedRigidbody;
            if (support != null && !support.isKinematic) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.35f) continue;
            if (hit.distance >= closestDistance) continue;
            closestDistance = hit.distance;
            groundPoint = hit.point;
            found = true;
        }

        return found;
    }

    public async void ExitBike(Character character)
    {
        if (IsTransitioning || character == null || character != _mountedCharacter) return;
        int operationVersion = ++_operationVersion;
        _transitionCharacter = character;
        SetShooterPoseActive(false);
        isExiting = true;

        Rigidbody bikeRigidbody = GetComponent<Rigidbody>();
        float stoppedSpeed = Mathf.Max(0f, stoppedExitSpeedKph) / 3.6f;
        if (bikeController != null && bikeController.IsVehicleEnabled &&
            bikeController.SpeedMetersPerSecond > stoppedSpeed)
        {
            // Keep the rider seated and ABP active while throttle/steering input
            // is suppressed. The driver applies mobile-safe planar deceleration
            // in FixedUpdate and keeps the suspension settled until the bike has
            // nearly stopped.
            bikeController.BeginExitStop();
            float deadline = Time.unscaledTime + Mathf.Max(0.1f, exitStopTimeout);
            while (character != null && character == _mountedCharacter &&
                   IsOperationCurrent(operationVersion) &&
                   bikeController.IsVehicleEnabled &&
                   bikeController.SpeedMetersPerSecond > stoppedSpeed &&
                   Time.unscaledTime < deadline)
            {
                await Task.Yield();
            }

            if (!IsOperationCurrent(operationVersion)) return;
            if (character == null || character != _mountedCharacter)
            {
                bikeController.CancelExitStop();
                _transitionCharacter = null;
                isExiting = false;
                return;
            }
        }

        // Remove the final sub-threshold creep (or force-stop after timeout)
        // before detaching the rider and starting the exit gesture.
        if (bikeRigidbody != null && !bikeRigidbody.isKinematic)
        {
            bikeRigidbody.linearVelocity = Vector3.zero;
            bikeRigidbody.angularVelocity = Vector3.zero;
        }

        // Entry may use the opposite side, but bike exit is intentionally never
        // mirrored. From this point onward the original left standing point owns
        // the exit animation landing pose and safe character placement.
        ClearActiveEntrySide();
        SetLiveRiderPosePreview(false);
        ResetFootTargetsImmediate();

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

        // Stop Arcade physics before the rider regains collision. A parked bike
        // remains kinematic so GC2's character collision cannot impart velocity.
        if (bikeController != null) bikeController.SetVehicleEnabled(false);

        if (bikeRigidbody != null)
        {
            if (!bikeRigidbody.isKinematic)
            {
                bikeRigidbody.linearVelocity = Vector3.zero;
                bikeRigidbody.angularVelocity = Vector3.zero;
            }
            bikeRigidbody.isKinematic = true;
        }

        _inBike = false;
        ResetAirborneRiderLift();
        _riderHelmetController = null;
        _mountedCharacter = null;
        character.transform.SetParent(null, true);

        character.States.Stop(drivingStateLayer, 0f, drivingStateTransitionOut);

        // The animation is authored against the exact standing point. Collision
        // clearance is applied only after it finishes, while Player/Bike
        // collisions are still ignored during the gesture.
        Vector3 exitPosition = entryStandingPoint != null
            ? entryStandingPoint.position
            : character.transform.position;
        Quaternion exitRotation = entryStandingPoint != null
            ? entryStandingPoint.rotation
            : character.transform.rotation;
        await PlayExitAnimationAnchored(
            character,
            exitAnimation,
            exitAnimationTransitionIn,
            exitAnimationTransitionOut,
            exitPosition,
            exitRotation,
            operationVersion
        );
        if (!IsOperationCurrent(operationVersion)) return;

        MoveCharacterToLeftExitPosition(character);
        RestoreCharacterPhysics(character);
        EndEntryCollisionIgnore();
        Physics.SyncTransforms();

        // Heavy Shooter weapons stay hidden through the complete exit gesture.
        // Restore the exact cached weapon only after the rider is back on foot.
        await FranklinShooterSystem.CompleteBikeDriverExit(character);
        if (!IsOperationCurrent(operationVersion)) return;

        if (character.Player != null)
        {
            character.Player.IsControllable = true;
        }

        if (bikeRigidbody != null)
        {
            if (!bikeRigidbody.isKinematic)
            {
                bikeRigidbody.linearVelocity = Vector3.zero;
                bikeRigidbody.angularVelocity = Vector3.zero;
            }
            bikeRigidbody.isKinematic = true;
        }
        if (_activeIkSetter != null)
        {
            _activeIkSetter.SetBeforeHandIK(null);
            _activeIkSetter.ConfigureManualSpineAdjustment(Vector3.zero, Vector3.zero);
            _activeIkSetter = null;
        }

        _ = this.onExit.Run(new Args(this.gameObject));

        ClearActiveEntrySide();
        _transitionCharacter = null;
        isExiting = false;
    }

    public Task<bool> MoveCharacterToEntryStandingPointAsync(Character character)
    {
        return MoveCharacterToEntryStandingPointAsync(character, _operationVersion);
    }

    private async Task<bool> MoveCharacterToEntryStandingPointAsync(
        Character character,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) ||
            character == null || character.Motion == null ||
            entryStandingPoint == null)
        {
            return false;
        }

        if (_activeEntryStandingPoint == null) SelectEntrySide(character);
        Transform standingPoint = GetActiveEntryStandingPoint();
        if (standingPoint == null) return false;

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
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion) &&
               Time.unscaledTime < deadline)
        {
            await Task.Yield();
        }

        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll) return false;
        Vector3 horizontalToStanding = Vector3.ProjectOnPlane(
            character.transform.position - standingPoint.position,
            Vector3.up
        );
        float allowedHorizontalError = Mathf.Max(0.15f, entryApproachStopDistance) + 0.05f;
        bool reachedStandingArea =
            horizontalToStanding.sqrMagnitude <= allowedHorizontalError * allowedHorizontalError;
        if ((!_approachFinished || !_approachSucceeded) && !reachedStandingArea)
        {
            character.Motion.MoveToDirection(
                Vector3.zero,
                Space.World,
                Mathf.Max(1, entryApproachMotionPriority)
            );
            character.Motion.StopToDirection(Mathf.Max(1, entryApproachMotionPriority));
            _approachCharacter = null;
            return false;
        }

        if (alignCharacterToStandingPoint)
            await SmoothAlignCharacterToStandingPoint(
                character,
                standingPoint,
                operationVersion
            );

        if (!IsOperationCurrent(operationVersion) ||
            character.Ragdoll.IsRagdoll) return false;
        _approachCharacter = null;
        return character != null && !character.Ragdoll.IsRagdoll;
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

    private async Task SmoothAlignCharacterToStandingPoint(
        Character character,
        Transform standingPoint,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) ||
            character == null || standingPoint == null)
        {
            return;
        }

        float duration = Mathf.Max(0f, entryApproachAlignmentDuration);
        Vector3 startPosition = character.transform.position;
        Quaternion startRotation = character.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration && character != null && standingPoint != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion))
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;
            progress = progress * progress * (3f - 2f * progress);
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, standingPoint.position, progress),
                Quaternion.Slerp(startRotation, standingPoint.rotation, progress)
            );
            await Task.Yield();
        }

        if (character != null && standingPoint != null &&
            !character.Ragdoll.IsRagdoll &&
            IsOperationCurrent(operationVersion))
        {
            character.transform.SetPositionAndRotation(
                standingPoint.position,
                standingPoint.rotation
            );
        }
    }

    private void AttachCharacterToSeat(Character character)
    {
        if (character == null || entryParent == null) return;

        ResetAirborneRiderLift();
        LockCharacterPhysics(character);
        character.transform.SetParent(entryParent);
        character.transform.localPosition = riderSeatOffset;
        character.transform.localRotation = Quaternion.identity;
        _mountedCharacter = character;
        _riderHelmetController =
            character.GetComponent<FranklinBikeHelmetController>() ??
            character.GetComponentInChildren<FranklinBikeHelmetController>(true);
    }

    private void LockCharacterPhysics(Character character)
    {
        if (character == null) return;
        if (_seatedPhysics != null) RestoreCharacterPhysics(_seatedPhysics.character);

        CharacterPhysicsSnapshot snapshot = _physicsSnapshotBuffer;
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

        _seatedPhysics = snapshot;
        Physics.SyncTransforms();
    }

    private void RestoreCharacterPhysics(Character character)
    {
        CharacterPhysicsSnapshot snapshot = _seatedPhysics;
        if (snapshot == null || snapshot.character != character) return;

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
            character.Driver.ForceGrounded(false);
        }

        if (character.Motion != null)
            character.Motion.MovementType = snapshot.movementType;

        snapshot.character = null;
        snapshot.colliders.Clear();
        snapshot.rigidbodies.Clear();
        _seatedPhysics = null;
        Physics.SyncTransforms();
    }

    private void MoveCharacterToLeftExitPosition(Character character)
    {
        if (!TryGetLeftExitPose(
            character,
            out Vector3 exitPosition,
            out Quaternion exitRotation
        )) return;

        character.transform.SetPositionAndRotation(exitPosition, exitRotation);
        character.Driver?.ResetVerticalVelocity();
    }

    private bool TryGetLeftExitPose(
        Character character,
        out Vector3 exitPosition,
        out Quaternion exitRotation)
    {
        Transform standingPoint = entryStandingPoint;
        exitPosition = character != null
            ? character.transform.position
            : transform.position;
        exitRotation = character != null
            ? character.transform.rotation
            : transform.rotation;
        if (character == null || standingPoint == null) return false;

        exitPosition = standingPoint.position;
        exitRotation = standingPoint.rotation;
        if (arcadeBikeRagdoll != null && character.Motion != null)
        {
            Vector3 leftDirection = mirroredEntryStandingPoint != null
                ? Vector3.ProjectOnPlane(
                    entryStandingPoint.position -
                    mirroredEntryStandingPoint.position,
                    Vector3.up
                )
                : Vector3.ProjectOnPlane(-transform.right, Vector3.up);
            if (leftDirection.sqrMagnitude < 0.0001f)
                leftDirection = -transform.right;

            exitPosition = arcadeBikeRagdoll.GetCollisionSafeExitPosition(
                exitPosition,
                leftDirection.normalized,
                character.Motion.Radius + 0.06f
            );
        }

        return true;
    }

    private async Task PlayExitAnimationAnchored(
        Character character,
        AnimationClip clip,
        float transitionIn,
        float transitionOut,
        Vector3 targetPosition,
        Quaternion targetRotation,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) || character == null) return;

        // Character_Exit_Bike is authored around the standing root, not the
        // seated Character transform. Its first Humanoid RootT is offset back
        // over the seat (x ~= 0.483 m) and the last RootT returns to zero.
        // Anchor the Character root at the left standing point before sampling
        // the clip; the skeleton then starts on the seat and exits left without
        // the 48 cm visual jump caused by anchoring the root on the seat.
        character.transform.SetPositionAndRotation(targetPosition, targetRotation);
        character.Driver?.ResetVerticalVelocity();

        float duration = 0f;

        if (clip != null)
        {
            const float speed = 1f;
            var gestureConfig = new ConfigGesture(
                0f,
                clip.length,
                speed,
                false,
                transitionIn,
                transitionOut
            );
            _ = character.Gestures.CrossFade(
                clip,
                animationMask,
                BlendMode.Blend,
                gestureConfig,
                true
            );
            duration = clip.length / speed;
        }

        float elapsed = 0f;
        while (elapsed < duration && character != null &&
               IsOperationCurrent(operationVersion))
        {
            character.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );
            character.Driver?.ResetVerticalVelocity();
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        if (character != null && IsOperationCurrent(operationVersion))
        {
            character.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );
            character.Driver?.ResetVerticalVelocity();
        }
    }

    private async Task PlayEntryAnimationAligned(
        Character character,
        AnimationClip clip,
        float transitionIn,
        float transitionOut,
        CharacterIKSetter ikSetter,
        int operationVersion)
    {
        if (!IsOperationCurrent(operationVersion) || character == null) return;

        float animationDuration = 0f;
        if (clip != null)
        {
            const float speed = 1f;
            var gestureConfig = new ConfigGesture(
                0f,
                clip.length,
                speed,
                useRootMotion,
                transitionIn,
                transitionOut
            );
            _ = character.Gestures.CrossFade(
                clip,
                animationMask,
                BlendMode.Blend,
                gestureConfig,
                true
            );
            animationDuration = clip.length / speed;
        }

        Quaternion startRotation = character.transform.rotation;
        Quaternion parallelRotation = GetBikeParallelRotation(startRotation);
        float blendDuration = Mathf.Max(0.05f, enterAlignmentDuration);
        float totalDuration = Mathf.Max(blendDuration, animationDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration && character != null &&
               !character.Ragdoll.IsRagdoll &&
               IsOperationCurrent(operationVersion))
        {
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));
            character.transform.rotation = Quaternion.Slerp(
                startRotation,
                parallelRotation,
                blend
            );

            if (entryParent != null)
            {
                float animationProgress = totalDuration > 0f
                    ? Mathf.Clamp01(elapsed / totalDuration)
                    : 1f;
                float seatBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                    enterSeatPositionBlendStart,
                    1f,
                    animationProgress
                ));
                character.transform.position = Vector3.Lerp(
                    character.transform.position,
                    entryParent.TransformPoint(riderSeatOffset),
                    seatBlend
                );
            }

            if (ikSetter != null) SetEntryHandIK(ikSetter, blend);

            elapsed += Time.deltaTime;
            await Task.Yield();
        }

        if (!IsOperationCurrent(operationVersion) || character == null ||
            character.Ragdoll.IsRagdoll) return;
        if (entryParent != null)
            character.transform.position = entryParent.TransformPoint(riderSeatOffset);
        character.transform.rotation = parallelRotation;
        if (ikSetter != null) SetEntryHandIK(ikSetter, 1f);
    }

    private void BeginEntryCollisionIgnore(Character character)
    {
        EndEntryCollisionIgnore();
        if (character == null) return;

        character.GetComponentsInChildren(true, _entryCharacterColliderBuffer);
        GetComponentsInChildren(true, _entryVehicleColliderBuffer);
        foreach (Collider characterCollider in _entryCharacterColliderBuffer)
        {
            if (characterCollider == null || !characterCollider.enabled ||
                characterCollider.isTrigger)
            {
                continue;
            }

            foreach (Collider vehicleCollider in _entryVehicleColliderBuffer)
            {
                if (vehicleCollider == null || !vehicleCollider.enabled ||
                    vehicleCollider.isTrigger || vehicleCollider == characterCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(characterCollider, vehicleCollider, true);
                _ignoredEntryCollisionPairs.Add(new IgnoredCollisionPair
                {
                    characterCollider = characterCollider,
                    vehicleCollider = vehicleCollider
                });
            }
        }
        _entryCharacterColliderBuffer.Clear();
        _entryVehicleColliderBuffer.Clear();
    }

    private void EndEntryCollisionIgnore()
    {
        foreach (IgnoredCollisionPair pair in _ignoredEntryCollisionPairs)
        {
            if (pair.characterCollider == null || pair.vehicleCollider == null)
                continue;
            Physics.IgnoreCollision(
                pair.characterCollider,
                pair.vehicleCollider,
                false
            );
        }
        _ignoredEntryCollisionPairs.Clear();
        _entryCharacterColliderBuffer.Clear();
        _entryVehicleColliderBuffer.Clear();
    }

    private void SelectEntrySide(Character character)
    {
        _activeEntryStandingPoint = entryStandingPoint;
        _activeEntryMirrored = false;

        bool mirroredAvailable = mirroredEntryStandingPoint != null &&
                                 mirroredEntryAnimation != null;
        if (!mirroredAvailable) return;

        bool useMirrored = entrySideMode == BikeEntrySideMode.Mirrored;
        if (entrySideMode == BikeEntrySideMode.Automatic && character != null)
        {
            Vector3 characterPosition = character.transform.position;
            Vector3 toOriginal = Vector3.ProjectOnPlane(
                characterPosition - entryStandingPoint.position,
                Vector3.up
            );
            Vector3 toMirrored = Vector3.ProjectOnPlane(
                characterPosition - mirroredEntryStandingPoint.position,
                Vector3.up
            );
            useMirrored = toMirrored.sqrMagnitude < toOriginal.sqrMagnitude;
        }

        if (!useMirrored) return;
        _activeEntryStandingPoint = mirroredEntryStandingPoint;
        _activeEntryMirrored = true;
    }

    private Transform GetActiveEntryStandingPoint()
    {
        return _activeEntryStandingPoint != null
            ? _activeEntryStandingPoint
            : entryStandingPoint;
    }

    private void ClearActiveEntrySide()
    {
        _activeEntryStandingPoint = null;
        _activeEntryMirrored = false;
    }

    private void SetEntryHandIK(CharacterIKSetter ikSetter, float blend)
    {
        if (ikSetter == null) return;
        blend = Mathf.Clamp01(blend);

        if (_activeEntryMirrored)
        {
            ikSetter.SetIKTargets(
                null,
                steeringWheelRightHandTarget,
                0f,
                rightHandIKWeight * blend,
                0f,
                handIKRotationWeight * blend
            );
            return;
        }

        ikSetter.SetIKTargets(
            steeringWheelLeftHandTarget,
            null,
            leftHandIKWeight * blend,
            0f,
            handIKRotationWeight * blend,
            0f
        );
    }

    private Quaternion GetBikeParallelRotation(Quaternion fallback)
    {
        Transform alignmentFrame = entryParent != null
            ? entryParent
            : bikeBody != null
                ? bikeBody
                : transform;
        Vector3 forward = Vector3.ProjectOnPlane(alignmentFrame.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : fallback;
    }

    private void ConfigureRiderIKSetter(CharacterIKSetter ikSetter)
    {
        if (ikSetter == null) return;
        _activeIkSetter = ikSetter;
        ikSetter.ConfigureManualSpineAdjustment(
            _shooterPoseActive ? Vector3.zero : riderSpinePositionOffset,
            _shooterPoseActive ? Vector3.zero : riderSpineRotationOffset
        );
        ikSetter.SetBeforeHandIK(
            _liveRiderPosePreview && !_shooterPoseActive
                ? ApplyLiveRiderPosePreview
                : null
        );
        ApplyReverseLookPoseToSetter(
            ikSetter,
            _shooterPoseActive ? 0f : _reverseLookWeight
        );
    }

    private void UpdateReverseLookPose(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = active ? reverseLookBlendIn : reverseLookBlendOut;
        _reverseLookWeight = Mathf.MoveTowards(
            _reverseLookWeight,
            target,
            Time.deltaTime / Mathf.Max(0.02f, duration)
        );
        ApplyReverseLookPoseToSetter(_activeIkSetter, _reverseLookWeight);
    }

    private void ResetReverseLookPose()
    {
        _reverseLookWeight = 0f;
        ApplyReverseLookPoseToSetter(_activeIkSetter, 0f);
    }

    private void ApplyReverseLookPoseToSetter(
        CharacterIKSetter ikSetter,
        float weight)
    {
        if (ikSetter == null) return;

        CharacterIKSetter.HandIKState handState = ikSetter.CaptureHandIKState();
        bool rightHandKeepsBike =
            handState.RightTarget == steeringWheelRightHandTarget &&
            handState.RightWeight > 0.001f &&
            _passengerSeat?.IsOccupied != true;
        float releaseWeight = reverseLookReleaseLeftHand && rightHandKeepsBike
            ? Mathf.Clamp01(weight) * reverseLookLeftHandReleaseWeight
            : 0f;
        ikSetter.SetReverseLeftHandRelease(
            releaseWeight,
            reverseLookLeftHandTarget,
            reverseLookLeftHandOffset
        );

        float side = reverseLookOverLeftShoulder ? -1f : 1f;
        float yaw = reverseLookYaw * side;
        float upperWeight = Mathf.Max(0f, reverseLookUpperChestWeight);
        float neckWeight = Mathf.Max(0f, reverseLookNeckWeight);
        float headWeight = Mathf.Max(0f, reverseLookHeadWeight);
        float weightSum = upperWeight + neckWeight + headWeight;
        if (weightSum <= 0.0001f)
        {
            ikSetter.SetReverseLookPose(
                0f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero
            );
            return;
        }

        float inverseWeightSum = 1f / weightSum;
        upperWeight *= inverseWeightSum;
        neckWeight *= inverseWeightSum;
        headWeight *= inverseWeightSum;
        ikSetter.SetReverseLookPose(
            weight,
            new Vector3(0f, yaw * upperWeight, 0f),
            new Vector3(0f, yaw * neckWeight, 0f),
            new Vector3(
                reverseLookHeadPitch,
                yaw * headWeight,
                0f
            )
        );
    }

    private Collider ResolvePhysicsCollider()
    {
        Collider rootCollider = GetComponent<Collider>();
        if (rootCollider != null && !rootCollider.isTrigger) return rootCollider;

        foreach (Collider candidate in GetComponentsInChildren<Collider>(true))
        {
            if (candidate != null && !candidate.isTrigger) return candidate;
        }

        return rootCollider;
    }

}
