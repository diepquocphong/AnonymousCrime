using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

public interface ICarOccupiedEntryHandler
{
    bool TryEnterOccupiedCar(Character character);
}

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
    [Range(0.5f, 2f)] public float entryAnimationSpeed = 1.35f;
    [Range(0.5f, 2f)] public float exitAnimationSpeed = 1.35f;
    public bool useRootMotion = true;

    [Header("Speed Aware Exit")]
    public AnimationClip movingExitAnimation;
    public AnimationClip movingExitLandingAnimation;
    [Min(1f)] public float fastExitSpeedKph = 28f;
    [Min(0f)] public float stoppedExitSpeedKph = 0.8f;
    [Min(0.1f)] public float exitStopTimeout = 6f;
    [Range(0.5f, 2.5f)] public float movingExitAnimationSpeed = 1.45f;
    [Range(0.5f, 3f)] public float movingExitLandingSpeed = 1.6f;
    [Min(0f)] public float movingExitDoorLeadTime = 0.35f;
    [Min(0.1f)] public float movingExitLandingClipDuration = 2f;
    [Range(0f, 1f)] public float movingExitInheritedVelocity = 0.55f;
    [Min(0f)] public float movingExitLateralSpeed = 2.8f;
    [Min(0f)] public float movingExitUpwardSpeed = 1.2f;
    [Min(0f)] public float movingExitTransientDuration = 0.45f;
    [Min(0f)] public float movingExitTransientFade = 0.75f;
    [Min(0f)] public float movingExitClearance = 0.25f;

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
    private readonly CharacterPhysicsSnapshot _physicsSnapshotBuffer =
        new CharacterPhysicsSnapshot();
    private readonly List<Collider> _colliderBuffer = new List<Collider>(8);
    private readonly List<Rigidbody> _rigidbodyBuffer = new List<Rigidbody>(4);
    private readonly List<MonoBehaviour> _behaviourBuffer = new List<MonoBehaviour>(16);
    private Character _entryAlignCharacter;
    private float _entryAlignStartedAt;
    private float _entryAlignDuration;
    private Character _movingExitAlignCharacter;
    private float _movingExitAlignStartedAt;
    private float _movingExitAlignDuration;
    private CharacterIKSetter _doorHandleIKSetter;
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

    /// <summary>
    /// Single entry API owned by the car. The caller only supplies the GC2
    /// Character; this component chooses normal entry or occupied-car stealing.
    /// </summary>
    public bool RequestEnter(Character character)
    {
        if (character == null || IsTransitioning) return false;
        if (_seatedChar == null)
        {
            _ = EnterCarAsync(character, true);
            return true;
        }

        if (_seatedChar == character) return false;
        if (occupiedEntryHandler == null) CacheVehicleBehaviours();
        return occupiedEntryHandler != null &&
            occupiedEntryHandler.TryEnterOccupiedCar(character);
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
        return EnterCarAsync(character, false);
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
        UpdateMovingExitAlignment();
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

    private async Task<bool> EnterCarAsync(Character character, bool runDoorSequence)
    {
        if (isEntering || character == null ||
            (_seatedChar != null && _seatedChar != character)) return false;
        isEntering = true;

        if (externalDriveController != null) externalDriveController.SetVehicleEnabled(false);
        else if (hoverController != null) hoverController.isVehicleEnabled = false;
        else if (carController != null) carController.enabled = false;

        if (character.Player != null) character.Player.IsControllable = false;
        if (!await MoveCharacterToEntryStandingPointAsync(character))
        {
            if (character != null && character.Player != null)
                character.Player.IsControllable = true;
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

        if (runDoorSequence && doorTransform != null) _ = DoorRotationSequence();

        BeginDoorHandleIK(
            character,
            entryAnimation,
            entryDoorHandleIKCurve,
            true,
            entryAnimationSpeed
        );
        BeginEntrySeatAlignment(character, entryAnimation, entryAnimationSpeed);
        // Normal entry blends from locomotion. An already-open door means this
        // is the carjacking handoff: blending for 0.2s would keep the upright
        // kick-out gesture while the authored path is already moving into the
        // seat. Switch the full-body gesture immediately in that branch.
        float entryTransitionIn = runDoorSequence
            ? entryAnimationTransitionIn
            : 0f;
        var animTask = PlayAnimation(
            character, entryAnimation,
            entryTransitionIn,
            entryAnimationTransitionOut,
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

        _ = this.onEnter.Run(new Args(this.gameObject));
        isEntering = false;
        return true;
    }

    public void ExitCar(Character character)
    {
        if (isExiting || character == null || character != _seatedChar) return;
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
            bool useMovingExit = speedKph >= fastExitSpeedKph &&
                movingExitAnimation != null;

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

        _seatedChar = null;
        character.transform.SetParent(null, true);
        RestoreCharacterPhysics(character);
        ClearSteeringWheelIK(character);

        var carCollider = cachedCarCollider;
        var carRigidbody = cachedCarRigidbody;
        var playerCollider = character.GetComponent<Collider>();

        if (carRigidbody != null)
        {
            await Task.Yield();
            carRigidbody.isKinematic = true;
        }
        if (carCollider != null) carCollider.isTrigger = true;
        if (playerCollider != null) playerCollider.enabled = true;
        if (character.Driver != null) character.Driver.Collision = true;

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

        externalDriveController?.SetVehicleEnabled(false, true);
        ClearSteeringWheelIK(character);
        character.States.Stop(drivingStateLayer, 0f, 0f);

        if (doorTransform != null) _ = DoorRotationSequence();
        await WaitUnscaledSecondsAsync(movingExitDoorLeadTime);
        if (character == null) return;

        _seatedChar = null;
        BeginMovingExitAlignment(
            character,
            movingExitAnimation,
            movingExitAnimationSpeed
        );
        BeginDoorHandleIK(
            character,
            movingExitAnimation,
            exitDoorHandleIKCurve,
            false,
            movingExitAnimationSpeed
        );
        await PlayAnimation(
            character,
            movingExitAnimation,
            0.05f,
            0.08f,
            movingExitAnimationSpeed
        );
        EndDoorHandleIK();
        CompleteMovingExitAlignment(character);

        Vector3 sideDirection = GetDriverDoorSideDirection();
        Vector3 releasePosition = entryStandingPoint != null
            ? entryStandingPoint.position
            : character.transform.position;
        Quaternion releaseRotation = entryStandingPoint != null
            ? entryStandingPoint.rotation
            : character.transform.rotation;

        character.transform.SetParent(null, true);
        RestoreCharacterPhysics(character);
        character.transform.SetPositionAndRotation(
            releasePosition + sideDirection * movingExitClearance +
                Vector3.up * 0.05f,
            releaseRotation
        );
        if (character.Driver != null) character.Driver.Collision = true;
        Physics.SyncTransforms();

        Vector3 inheritedVelocity = Vector3.ProjectOnPlane(
            vehicleVelocity,
            Vector3.up
        ) * movingExitInheritedVelocity;
        Vector3 bailoutVelocity = inheritedVelocity +
            sideDirection * movingExitLateralSpeed +
            Vector3.up * movingExitUpwardSpeed;
        if (character.Motion != null && bailoutVelocity.sqrMagnitude > 0.001f)
        {
            character.Motion.SetMotionTransient(
                bailoutVelocity.normalized,
                bailoutVelocity.magnitude,
                movingExitTransientDuration,
                movingExitTransientFade
            );
        }

        if (movingExitLandingAnimation != null)
        {
            await PlayAnimation(
                character,
                movingExitLandingAnimation,
                0.04f,
                0.15f,
                movingExitLandingSpeed,
                false,
                movingExitLandingClipDuration
            );
        }

        if (character.Player != null)
            character.Player.IsControllable = true;

        _ = this.onExit.Run(new Args(this.gameObject));
    }

    private void AttachCharacterToSeat(Character character)
    {
        if (character == null || entryParent == null) return;

        LockCharacterPhysics(character);

        character.transform.SetParent(entryParent);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        _seatedChar = character;
        if (character.Player != null) character.Player.IsControllable = false;
        ConfigureSteeringWheelIK(character);
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

    private void RestoreCharacterPhysics(Character character, bool keepGrounded = false)
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
            if (!keepGrounded) character.Driver.ForceGrounded(false);
        }

        if (character.Motion != null)
            character.Motion.MovementType = snapshot.movementType;

        snapshot.character = null;
        snapshot.colliders.Clear();
        snapshot.rigidbodies.Clear();
        _seatedPhysics = null;
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
        if (ikSetter != null) ikSetter.SetIKTargets(null, null, 0f, 0f);
    }

    public async Task<bool> MoveCharacterToEntryStandingPointAsync(Character character)
    {
        if (character == null || character.Motion == null || entryStandingPoint == null)
            return false;

        _approachCharacter = character;
        _approachFinished = false;
        _approachSucceeded = false;

        Vector3 targetFeet = entryStandingPoint.position -
            Vector3.up * (character.Motion.Height * 0.5f);
        Location target = new Location(targetFeet, entryStandingPoint.rotation);
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
        if (character == null || entryStandingPoint == null) return;

        float duration = Mathf.Max(0f, entryApproachAlignmentDuration);
        if (duration <= 0f) return;

        Vector3 startPosition = character.transform.position;
        Quaternion startRotation = character.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration && character != null && entryStandingPoint != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);
            character.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, entryStandingPoint.position, progress),
                Quaternion.Slerp(startRotation, entryStandingPoint.rotation, progress)
            );
            await Task.Yield();
        }

        if (character != null && entryStandingPoint != null)
            character.transform.SetPositionAndRotation(
                entryStandingPoint.position,
                entryStandingPoint.rotation
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
        if (character == null || clip == null || doorHandleTarget == null) return;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;

        _doorHandleIKSetter = animator.GetComponent<CharacterIKSetter>();
        if (_doorHandleIKSetter == null)
            _doorHandleIKSetter = animator.gameObject.AddComponent<CharacterIKSetter>();

        _activeDoorHandleIKCurve = weightCurve;
        _doorHandleIKIsEntry = isEntry;
        _doorHandleIKStartedAt = Time.time;
        _doorHandleIKDuration = Mathf.Max(
            0.01f,
            clip.length / Mathf.Max(0.01f, speed)
        );
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

    private void BeginMovingExitAlignment(
        Character character,
        AnimationClip clip,
        float speed)
    {
        if (character == null || clip == null || entryParent == null ||
            entryStandingPoint == null)
        {
            _movingExitAlignCharacter = null;
            return;
        }

        _movingExitAlignCharacter = character;
        _movingExitAlignStartedAt = Time.time;
        _movingExitAlignDuration = Mathf.Max(
            0.01f,
            clip.length / Mathf.Max(0.01f, speed)
        );
    }

    private void UpdateMovingExitAlignment()
    {
        if (_movingExitAlignCharacter == null || entryParent == null ||
            entryStandingPoint == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(
            (Time.time - _movingExitAlignStartedAt) / _movingExitAlignDuration
        );
        float stepTime = Mathf.Clamp01(1f - entryStepNormalizedTime);
        Vector3 position;
        Quaternion rotation;

        if (entryStepPoint != null && normalized < stepTime)
        {
            float progress = Mathf.SmoothStep(
                0f,
                1f,
                normalized / Mathf.Max(0.01f, stepTime)
            );
            position = Vector3.Lerp(entryParent.position, entryStepPoint.position, progress);
            rotation = Quaternion.Slerp(
                entryParent.rotation,
                entryStepPoint.rotation,
                progress
            );
        }
        else
        {
            float progress = entryStepPoint != null
                ? Mathf.SmoothStep(
                    0f,
                    1f,
                    (normalized - stepTime) / Mathf.Max(0.01f, 1f - stepTime)
                )
                : Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 startPosition = entryStepPoint != null
                ? entryStepPoint.position
                : entryParent.position;
            Quaternion startRotation = entryStepPoint != null
                ? entryStepPoint.rotation
                : entryParent.rotation;
            position = Vector3.Lerp(startPosition, entryStandingPoint.position, progress);
            rotation = Quaternion.Slerp(
                startRotation,
                entryStandingPoint.rotation,
                progress
            );
        }

        _movingExitAlignCharacter.transform.SetPositionAndRotation(position, rotation);
    }

    private void CompleteMovingExitAlignment(Character character)
    {
        if (_movingExitAlignCharacter != character) return;
        if (character != null && entryStandingPoint != null)
        {
            character.transform.SetPositionAndRotation(
                entryStandingPoint.position,
                entryStandingPoint.rotation
            );
        }
        _movingExitAlignCharacter = null;
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
}
