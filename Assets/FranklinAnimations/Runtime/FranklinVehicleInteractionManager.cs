using System.Reflection;
using FranklinGame.AirSystem;
using FranklinGame.Vehicles;
using FranklinGame.Rendering;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Bridges Player input to the selected vehicle. Cars own their complete
    /// enter/carjack/exit sequence through CarEntry; the Player only requests the
    /// action and supplies its GC2 Character reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinVehicleInteractionManager : MonoBehaviour
    {
        private const float ENTRY_BEGIN_TIMEOUT = 5f;
        private const int FALLEN_BIKE_HIT_CAPACITY = 128;
        private const float CANDIDATE_DISTANCE_EPSILON = 0.0001f;

        private static readonly FieldInfo THIRD_PERSON_SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Shoulder",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Lift",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Radius",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Pivot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_MaxYaw",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SmoothTime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        [SerializeField]
        [Tooltip("The owning GC2 Player Character. It is resolved from the parent when omitted.")]
        private Character m_Player;
        [SerializeField]
        [Tooltip("The Player locomotion bridge to suspend while a vehicle owns the body animation.")]
        private FranklinAnimationBridge m_MovementBridge;
        [SerializeField]
        [Tooltip("Player-only FBS suspended throughout vehicle entry, seating and exit.")]
        private FranklinBlobShadow m_PlayerBlobShadow;
        [SerializeField]
        [Tooltip("Applies bike-only TPS aim values to the current GC2 Main Camera Shot.")]
        private FranklinBikeMainShotAim m_BikeMainShotAim;
        [SerializeField]
        [Tooltip("Turns off the Player's temporary GC2 Object Direction mode before vehicle entry begins.")]
        private FranklinObjectDirectionToggle m_ObjectDirectionToggle;

        [Header("Fallen bike interaction")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Distance around the Player used to find a grounded fallen bike when its rotated GC2 Hotspot is no longer selected.")]
        private float m_FallenBikeInteractionRadius = 2.25f;

        [Header("Vehicle selection")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("A nearby vehicle must become this many metres closer before replacing the currently highlighted vehicle.")]
        private float m_VehicleSelectionHysteresis = 0.25f;

        [Header("Vehicle entry animation")]
        [SerializeField]
        [Tooltip("Prevents the vehicle's seated Driving State from appearing before its door/entry animation finishes.")]
        private bool m_DelayDrivingIdleUntilEntryFinishes = true;

        [Header("Vehicle camera")]
        [SerializeField]
        [Tooltip("Switch the Main Camera to the original RapidTemplate vehicle shot while driving.")]
        private bool m_UseVehicleCamera = true;
        [SerializeField]
        [Tooltip("The original Camera Shot Vehicle prefab. A temporary instance is created at runtime.")]
        private ShotCamera m_VehicleShotPrefab;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend from the player shot into the vehicle shot.")]
        private float m_VehicleCameraEnterBlend = 2.5f;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend back to the player shot after exiting the vehicle.")]
        private float m_VehicleCameraExitBlend = 2.5f;
        [SerializeField] private Easing.Type m_VehicleCameraEasing = Easing.Type.Linear;

        [Header("Vehicle camera - Pivot")]
        [SerializeField]
        [Tooltip("When enabled, the vehicle camera orbits around the CarEntry/BikeEntry object currently being driven.")]
        private bool m_PivotCameraToActiveVehicle = true;

        [Header("Vehicle camera - Third Person")]
        [SerializeField] private float m_VehicleShoulder = 0f;
        [SerializeField] private float m_VehicleLift = 1f;
        [SerializeField, Min(0.01f)] private float m_VehicleRadius = 5f;
        [SerializeField]
        [Tooltip("Keeps RapidTemplate's original Global Variable - Vehicles/Camera-Sensitivity value.")]
        private bool m_UseGlobalVehicleSensitivity = true;
        [SerializeField, Min(0f)] private float m_VehicleSensitivityX = 0.5f;
        [SerializeField, Min(0f)] private float m_VehicleSensitivityY = 0.5f;
        [SerializeField, Range(1f, 179f)] private float m_VehicleMaxPitch = 100f;
        [SerializeField] private bool m_EnableVehicleMaxYaw;
        [SerializeField, Range(0f, 179f)] private float m_VehicleMaxYaw = 100f;
        [SerializeField, Min(0f)] private float m_VehicleSmoothTime = 0.15f;
        [SerializeField] private bool m_VehicleAutoAlign = true;
        [SerializeField, Min(0f)] private float m_VehicleAlignDelay;
        [SerializeField, Min(0f)] private float m_VehicleAlignSmoothTime = 3f;

        private bool m_IsVehicleAnimationLocked;
        private bool m_HasEnteredVehicle;
        private float m_EntryRequestedAt;
        private bool m_IsDrivingStateSuppressed;
        private StateData m_DelayedDrivingState;
        private int m_DelayedDrivingStateLayer;
        private float m_DelayedDrivingStateTransitionIn;
        private float m_DelayedDrivingStateTransitionOut;
        private MainCamera m_MainCamera;
        private ShotCamera m_RuntimeVehicleShot;
        private ShotCamera m_ActiveVehicleShot;
        private ShotCamera m_PreVehicleShot;
        private PropertyGetGameObject m_DefaultVehiclePivot;
        private GameObject m_ActiveVehiclePivot;
        private CarEntry m_ActiveCarEntry;
        private BikeEntry m_ActiveBikeEntry;
        private SimcadeCarDriver m_ActiveSimcadeDriver;
        private FranklinArcadeBikeDriver m_ActiveBikeDriver;
        private HelicopterFlightController m_ActiveHelicopterDriver;
        private Component m_SelectedVehicleEntry;
        private bool m_IsVehicleCameraActive;
        private SimcadeCarjacking m_ActiveCarjacking;
        private readonly Collider[] m_FallenBikeHits =
            new Collider[FALLEN_BIKE_HIT_CAPACITY];

        private struct VehicleCandidate
        {
            public Component entry;
            public CarEntrySideMode carSide;
            public float distanceSqr;
            public int tieBreaker;

            public bool IsValid => this.entry != null;
        }

        private void Awake()
        {
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            if (this.m_ActiveHelicopterDriver != null &&
                this.m_ActiveCarEntry != null && this.m_Player != null)
            {
                // The manager owns the helicopter input/camera lease. If the
                // manager disappears independently, leave the Character safely
                // detached instead of preserving an occupied seat with no exit
                // route after this component is enabled again.
                this.m_ActiveCarEntry.ReleaseDriverForUnavailableVehicle(
                    this.m_Player
                );
            }

            if (this.m_IsVehicleAnimationLocked)
            {
                this.m_IsVehicleAnimationLocked = false;
                this.m_MovementBridge?.SetExternalAnimationLock(false);
            }

            this.SetPlayerBlobShadowSuspended(false);
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_HasEnteredVehicle = false;
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveBikeDriver = null;
            this.m_ActiveHelicopterDriver = null;
            this.m_ActiveCarjacking = null;
            this.ClearSelectedVehicleCandidate();
            this.m_BikeMainShotAim?.Deactivate();
        }

        private void OnDestroy()
        {
            if (this.m_RuntimeVehicleShot != null)
            {
                Destroy(this.m_RuntimeVehicleShot.gameObject);
            }
        }

        private void Update()
        {
            if (!this.ResolvePlayer()) return;
            this.UpdateVehicleAnimationLock();

            // Keep the physical desktop shortcut for Editor testing. The old on-screen "E"
            // prompt is disabled in the RVR car/bike prefabs and replaced by FranklinMobileHud.
            if (this.m_IsVehicleAnimationLocked) return;
            if (Keyboard.current?.eKey.wasPressedThisFrame != true) return;
            if (this.m_Player.Player?.IsControllable != true) return;

            this.RequestVehicleInteraction();
        }

        /// <summary>
        /// Driver selected by the Player's current vehicle interaction. The mobile
        /// HUD reads this cached reference instead of searching every vehicle in the
        /// scene while this manager is available.
        /// </summary>
        public IRvrVehicleInputController ActiveVehicleDriver =>
            this.m_ActiveSimcadeDriver != null
                ? this.m_ActiveSimcadeDriver
                : this.m_ActiveBikeDriver != null
                    ? this.m_ActiveBikeDriver
                    : this.m_ActiveHelicopterDriver;

        /// <summary>
        /// True only while RVR has selected the entry spot on an available car or bike.
        /// The mobile HUD uses this to hide its Enter button everywhere else.
        /// </summary>
        public bool CanRequestVehicleInteraction
        {
            get
            {
                if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                    this.m_Player.Player?.IsControllable != true ||
                    this.m_Player.Ragdoll.IsRagdoll)
                {
                    return false;
                }

                return this.TryUpdateSelectedVehicleCandidate(out _);
            }
        }

        /// <summary>
        /// Requests entry from the selected vehicle. CarEntry/BikeEntry remain
        /// authoritative for animation, alignment and vehicle ownership.
        /// </summary>
        public bool RequestVehicleInteraction()
        {
            if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                this.m_Player.Player?.IsControllable != true ||
                this.m_Player.Ragdoll.IsRagdoll)
            {
                return false;
            }

            VehicleCandidate candidate;
            if (this.m_SelectedVehicleEntry != null)
            {
                // The HUD displayed its button for this exact vehicle. Revalidate
                // it, but never jump to another overlapping vehicle on the tap.
                if (!this.TryGetPinnedVehicleCandidate(out candidate))
                {
                    this.ClearSelectedVehicleCandidate();
                    return false;
                }
            }
            else if (!this.TryUpdateSelectedVehicleCandidate(out candidate))
            {
                return false;
            }

            return this.TryStartVehicleInteraction(candidate);
        }

        /// <summary>
        /// Routes legacy GC2 vehicle instructions through the same Player lock as
        /// the mobile HUD. NPC instructions continue using their entry directly.
        /// </summary>
        public bool RequestSpecificVehicleInteraction(Component vehicleEntry)
        {
            if (!this.ResolvePlayer() || vehicleEntry == null ||
                this.m_IsVehicleAnimationLocked ||
                this.m_Player.Player?.IsControllable != true ||
                this.m_Player.Ragdoll.IsRagdoll ||
                !this.TryCreateCandidate(
                    vehicleEntry,
                    this.m_Player.transform.position,
                    out VehicleCandidate candidate
                ))
            {
                return false;
            }

            this.m_SelectedVehicleEntry = candidate.entry;
            return this.TryStartVehicleInteraction(candidate);
        }

        /// <summary>
        /// Releases a seated Bike rider without running the normal exit gesture.
        /// Death uses this before GC2 snapshots the skeleton for ragdoll.
        /// </summary>
        public bool ReleaseActiveBikeForDeath()
        {
            if (!this.ResolvePlayer() || this.m_ActiveBikeEntry == null ||
                this.m_ActiveBikeEntry.SeatedCharacter != this.m_Player)
            {
                return false;
            }

            if (!this.m_ActiveBikeEntry.ReleaseForCrash(this.m_Player)) return false;

            this.m_IsVehicleAnimationLocked = false;
            this.m_HasEnteredVehicle = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.SetPlayerBlobShadowSuspended(false);
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveBikeDriver = null;
            this.m_ActiveHelicopterDriver = null;
            this.m_ActiveCarjacking = null;
            return true;
        }

        /// <summary>
        /// Releases a seated helicopter pilot before GC2 captures the death
        /// ragdoll. CarEntry owns the Character physics and seated-state restore;
        /// the manager releases only its animation/camera leases afterwards.
        /// </summary>
        public bool ReleaseActiveHelicopterForDeath()
        {
            if (!this.ResolvePlayer() || this.m_ActiveHelicopterDriver == null ||
                this.m_ActiveCarEntry == null ||
                !this.m_ActiveCarEntry.ReleaseDriverForDeath(this.m_Player))
            {
                return false;
            }

            this.m_IsVehicleAnimationLocked = false;
            this.m_HasEnteredVehicle = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.SetPlayerBlobShadowSuspended(false);
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveBikeDriver = null;
            this.m_ActiveHelicopterDriver = null;
            this.m_ActiveCarjacking = null;
            return true;
        }

        private bool TryStartVehicleInteraction(VehicleCandidate candidate)
        {
            Component vehicleEntry = candidate.entry;
            if (vehicleEntry == null || !this.IsAvailableVehicleEntry(vehicleEntry))
                return false;

            // Object Direction continuously writes the Character root rotation from
            // the camera. Release it before GC2 starts walking toward either a Car
            // or Bike entry point so it cannot fight the authored approach/door pose.
            this.DisableObjectDirectionBeforeVehicleEntry();
            this.LockMovementAnimation();
            this.m_ActiveVehiclePivot = vehicleEntry.gameObject;
            this.m_ActiveCarEntry = vehicleEntry as CarEntry;
            this.m_ActiveBikeEntry = vehicleEntry as BikeEntry;
            this.m_ActiveSimcadeDriver = this.m_ActiveCarEntry != null
                ? this.m_ActiveCarEntry.GetComponent<SimcadeCarDriver>()
                : null;
            this.m_ActiveBikeDriver = this.m_ActiveBikeEntry != null
                ? this.m_ActiveBikeEntry.GetComponent<FranklinArcadeBikeDriver>()
                : null;
            this.m_ActiveHelicopterDriver = this.m_ActiveCarEntry != null
                ? this.m_ActiveCarEntry.GetComponent<HelicopterFlightController>()
                : null;
            this.ClearSelectedVehicleCandidate();
            this.DelayVehicleDrivingIdle(vehicleEntry);

            if (vehicleEntry is CarEntry carEntry)
            {
                // GC2 supplies the nearby car target; CarEntry remains
                // authoritative and compares every configured free door in
                // world space so overlapping Hotspots cannot choose the wrong
                // side of a four-door car.
                CarEntrySideMode requestedSide = candidate.carSide;
                if (requestedSide == CarEntrySideMode.Automatic)
                {
                    requestedSide = carEntry.ResolveEntrySide(
                        this.m_Player,
                        CarEntrySideMode.Automatic
                    );
                }
                bool startsCarjacking = carEntry.SeatedCharacter != null &&
                    (requestedSide == CarEntrySideMode.DriverDoor ||
                     requestedSide == CarEntrySideMode.PassengerDoor);
                if (!carEntry.RequestEnter(this.m_Player, requestedSide))
                {
                    this.CancelVehicleInteractionRequest();
                    return false;
                }

                this.m_ActiveCarjacking = startsCarjacking
                    ? carEntry.GetComponent<SimcadeCarjacking>()
                    : null;
                return true;
            }

            if (vehicleEntry is BikeEntry bikeEntry)
            {
                if (!bikeEntry.RequestEnter(this.m_Player))
                {
                    this.CancelVehicleInteractionRequest();
                    return false;
                }

                return true;
            }

            this.CancelVehicleInteractionRequest();
            return false;
        }

        private bool TryUpdateSelectedVehicleCandidate(
            out VehicleCandidate selected)
        {
            this.FindVehicleCandidates(
                out VehicleCandidate best,
                out VehicleCandidate pinned
            );

            if (pinned.IsValid)
            {
                if (!best.IsValid || best.entry == pinned.entry ||
                    Mathf.Sqrt(pinned.distanceSqr) <=
                    Mathf.Sqrt(best.distanceSqr) +
                    Mathf.Max(0f, this.m_VehicleSelectionHysteresis))
                {
                    selected = pinned;
                }
                else
                {
                    selected = best;
                }
            }
            else
            {
                selected = best;
            }

            if (!selected.IsValid)
            {
                this.ClearSelectedVehicleCandidate();
                return false;
            }

            this.m_SelectedVehicleEntry = selected.entry;
            return true;
        }

        private bool TryGetPinnedVehicleCandidate(out VehicleCandidate pinned)
        {
            this.FindVehicleCandidates(out _, out pinned);
            if (!pinned.IsValid) return false;
            return true;
        }

        private void FindVehicleCandidates(
            out VehicleCandidate best,
            out VehicleCandidate pinned)
        {
            best = default;
            pinned = default;
            if (this.m_Player == null) return;

            Vector3 playerPosition = this.m_Player.transform.position;
            System.Collections.Generic.List<ISpatialHash> interactions =
                this.m_Player.Interaction?.Interactions;
            if (interactions != null)
            {
                for (int index = 0; index < interactions.Count; ++index)
                {
                    if (interactions[index] is not IInteractive interactive ||
                        interactive.Instance == null)
                    {
                        continue;
                    }

                    GameObject instance = interactive.Instance;
                    Hotspot hotspot = instance.GetComponent<Hotspot>();
                    if (hotspot == null || !hotspot.isActiveAndEnabled ||
                        !hotspot.IsActive)
                    {
                        continue;
                    }

                    Component vehicleEntry = FindVehicleEntry(instance);
                    if (vehicleEntry == null ||
                        !IsDriverDoorTarget(instance, vehicleEntry.transform) ||
                        !this.TryCreateCandidate(
                            vehicleEntry,
                            interactive.Position,
                            out VehicleCandidate candidate
                        ))
                    {
                        continue;
                    }

                    ConsiderCandidate(ref best, candidate);
                    if (vehicleEntry == this.m_SelectedVehicleEntry)
                        ConsiderCandidate(ref pinned, candidate);
                }
            }

            // A fallen bike can rotate its Hotspot out of range. Score its actual
            // physical surface in the same candidate set instead of letting this
            // fallback silently override a nearby upright Bike or Car.
            this.CollectFallenBikeCandidates(
                playerPosition,
                ref best,
                ref pinned
            );
            this.CollectHelicopterCandidates(
                playerPosition,
                ref best,
                ref pinned
            );
        }

        private void CollectHelicopterCandidates(
            Vector3 playerPosition,
            ref VehicleCandidate best,
            ref VehicleCandidate pinned)
        {
            var helicopters = HelicopterFlightController.Instances;
            for (int index = 0; index < helicopters.Count; ++index)
            {
                HelicopterFlightController controller = helicopters[index];
                if (controller == null || controller.IsVehicleEnabled) continue;

                CarEntry entry = controller.Entry;
                if (entry == null || entry.IsTransitioning) continue;
                Vector3 position = entry.entryStandingPoint != null
                    ? entry.entryStandingPoint.position
                    : controller.transform.position;
                float maximumDistance = controller.InteractionDistance;
                if ((position - playerPosition).sqrMagnitude >
                    maximumDistance * maximumDistance)
                {
                    continue;
                }

                if (!this.TryCreateCandidate(
                        entry,
                        position,
                        out VehicleCandidate candidate
                    ))
                {
                    continue;
                }

                ConsiderCandidate(ref best, candidate);
                if (entry == this.m_SelectedVehicleEntry)
                    ConsiderCandidate(ref pinned, candidate);
            }
        }

        private bool TryCreateCandidate(
            Component vehicleEntry,
            Vector3 candidatePosition,
            out VehicleCandidate candidate)
        {
            candidate = default;
            if (!this.IsAvailableVehicleEntry(vehicleEntry)) return false;

            CarEntrySideMode carSide = CarEntrySideMode.Automatic;
            if (vehicleEntry is CarEntry carEntry)
            {
                // Pin only the Car, never a door. The nearest available seat is
                // resolved again at the exact tap position so approaching from
                // the rear cannot permanently lock a rear-seat door.
                if (!carEntry.CanRequestEnter(
                        this.m_Player,
                        CarEntrySideMode.Automatic
                    ))
                {
                    return false;
                }
            }
            else if (vehicleEntry is BikeEntry bikeEntry)
            {
                FranklinArcadeBikeRagdoll ragdoll =
                    bikeEntry.GetComponent<FranklinArcadeBikeRagdoll>();
                if (ragdoll != null && ragdoll.IsRagdoll &&
                    !ragdoll.CanInteractWhileFallen)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            candidate.entry = vehicleEntry;
            candidate.carSide = carSide;
            candidate.distanceSqr =
                (candidatePosition - this.m_Player.transform.position).sqrMagnitude;
            candidate.tieBreaker = vehicleEntry.GetInstanceID();
            return true;
        }

        private static void ConsiderCandidate(
            ref VehicleCandidate current,
            VehicleCandidate candidate)
        {
            if (!candidate.IsValid) return;
            if (!current.IsValid ||
                candidate.distanceSqr <
                current.distanceSqr - CANDIDATE_DISTANCE_EPSILON ||
                Mathf.Abs(candidate.distanceSqr - current.distanceSqr) <=
                CANDIDATE_DISTANCE_EPSILON &&
                candidate.tieBreaker < current.tieBreaker)
            {
                current = candidate;
            }
        }

        private bool IsAvailableVehicleEntry(Component vehicleEntry)
        {
            if (vehicleEntry is CarEntry carEntry)
            {
                if (carEntry.IsTransitioning) return false;
                SimcadeCarDriver driver = carEntry.GetComponent<SimcadeCarDriver>();
                if (driver != null && driver.IsVehicleEnabled) return false;
                HelicopterFlightController helicopter =
                    carEntry.GetComponent<HelicopterFlightController>();
                if (helicopter != null && helicopter.IsVehicleEnabled) return false;
            }
            else if (vehicleEntry is BikeEntry bikeEntry)
            {
                if (bikeEntry.IsTransitioning || bikeEntry.SeatedCharacter != null) return false;
                FranklinBikeHealth health =
                    bikeEntry.GetComponent<FranklinBikeHealth>();
                if (health != null && health.IsDestroyed) return false;
                FranklinArcadeBikeDriver driver =
                    bikeEntry.GetComponent<FranklinArcadeBikeDriver>();
                if (driver != null && driver.IsVehicleEnabled) return false;
            }
            else
            {
                return false;
            }

            return true;
        }

        private static bool IsDriverDoorTarget(
            GameObject targetInstance,
            Transform vehicleRoot)
        {
            for (Transform current = targetInstance != null
                     ? targetInstance.transform
                     : null;
                 current != null && current != vehicleRoot;
                 current = current.parent)
            {
                if (current.gameObject.name == "Triggers_Enter/Exit" ||
                    current.gameObject.name == "Triggers_Enter/Exit Passenger" ||
                    current.gameObject.name == "Triggers_Enter/Exit Rear Left" ||
                    current.gameObject.name == "Triggers_Enter/Exit Rear Right")
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectFallenBikeCandidates(
            Vector3 playerPosition,
            ref VehicleCandidate best,
            ref VehicleCandidate pinned)
        {
            if (this.m_Player == null) return;
            int hitCount = Physics.OverlapSphereNonAlloc(
                playerPosition,
                Mathf.Max(0.5f, this.m_FallenBikeInteractionRadius),
                this.m_FallenBikeHits,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = this.m_FallenBikeHits[index];
                if (hit == null) continue;

                BikeEntry bikeEntry = hit.GetComponentInParent<BikeEntry>();
                if (bikeEntry == null) continue;

                FranklinArcadeBikeRagdoll ragdoll =
                    bikeEntry.GetComponent<FranklinArcadeBikeRagdoll>();
                if (ragdoll == null || !ragdoll.CanInteractWhileFallen) continue;

                Vector3 closestPoint = hit.ClosestPoint(playerPosition);
                if (!this.TryCreateCandidate(
                        bikeEntry,
                        closestPoint,
                        out VehicleCandidate candidate
                    ))
                {
                    continue;
                }

                // Process every collider. This deliberately avoids the old
                // order-dependent bug where the first collider seen for a Bike
                // prevented a closer collider on that same Bike from scoring.
                ConsiderCandidate(ref best, candidate);
                if (bikeEntry == this.m_SelectedVehicleEntry)
                    ConsiderCandidate(ref pinned, candidate);
            }
        }

        private void ClearSelectedVehicleCandidate()
        {
            this.m_SelectedVehicleEntry = null;
        }

        private void CancelVehicleInteractionRequest()
        {
            this.m_IsVehicleAnimationLocked = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.SetPlayerBlobShadowSuspended(false);
            this.ClearDelayedDrivingState();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveBikeDriver = null;
            this.m_ActiveHelicopterDriver = null;
            this.m_ActiveCarjacking = null;
            this.ClearSelectedVehicleCandidate();
            this.m_BikeMainShotAim?.Deactivate();
        }

        private void LateUpdate()
        {
            if (!this.m_IsDrivingStateSuppressed || this.m_HasEnteredVehicle) return;
            // The carjacking sequence remains active while its door closes. Seat
            // ownership is the real handoff boundary: once Player owns the seat,
            // never remove the driving pose again.
            if (this.IsPlayerSeatedInActiveVehicle()) return;
            // Passenger-side carjacking has a short, real seated stage before
            // the driver seat changes ownership. Its Driving state is intentional
            // and remains paired with the passenger physics lock during the push.
            if (this.m_ActiveCarEntry?.IsPassengerCarjackingSeatOccupied == true)
                return;

            bool pairedCarjackingIsRunning = this.m_ActiveCarjacking != null &&
                this.m_ActiveCarjacking.IsTransitioning;
            bool carEntryIsTransitioning = this.m_ActiveCarEntry != null &&
                this.m_ActiveCarEntry.IsTransitioning;
            bool bikeEntryIsTransitioning = this.m_ActiveBikeEntry != null &&
                this.m_ActiveBikeEntry.IsTransitioning;
            if (this.m_Player?.Player?.IsControllable != true &&
                !pairedCarjackingIsRunning &&
                !carEntryIsTransitioning &&
                !bikeEntryIsTransitioning) return;

            // CarEntry/BikeEntry sets its driving state before playing the entry gesture.
            // LateUpdate removes that state before rendering, leaving the door/entry gesture in
            // charge until the character has actually taken the seat.
            this.m_Player.States?.Stop(this.m_DelayedDrivingStateLayer, 0f, 0f);
        }

        private bool IsPlayerSeatedInActiveVehicle()
        {
            return this.m_Player != null &&
                ((this.m_ActiveCarEntry != null &&
                  this.m_ActiveCarEntry.IsCharacterSeated(this.m_Player)) ||
                 (this.m_ActiveBikeEntry != null &&
                  this.m_ActiveBikeEntry.SeatedCharacter == this.m_Player));
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            if (this.m_MovementBridge == null && this.m_Player != null)
            {
                this.m_MovementBridge = this.m_Player.GetComponentInChildren<
                    FranklinAnimationBridge
                >(true);
            }
            if (this.m_PlayerBlobShadow == null && this.m_Player != null)
            {
                FranklinBlobShadow.TryGet(this.m_Player, out this.m_PlayerBlobShadow);
            }
            if (this.m_BikeMainShotAim == null)
            {
                this.m_BikeMainShotAim = this.GetComponent<FranklinBikeMainShotAim>();
            }
            if (this.m_ObjectDirectionToggle == null && this.m_Player != null)
            {
                this.m_ObjectDirectionToggle = this.m_Player.GetComponentInChildren<
                    FranklinObjectDirectionToggle
                >(true);
            }

            return this.m_Player != null;
        }

        private void DisableObjectDirectionBeforeVehicleEntry()
        {
            if (this.m_Player == null) return;

            if (this.m_ObjectDirectionToggle == null)
            {
                this.m_ObjectDirectionToggle = this.m_Player.GetComponentInChildren<
                    FranklinObjectDirectionToggle
                >(true);
            }

            // Normal mobile path: this also restores the exact facing mode that
            // was active before the Object Direction hold began (normally Pivot).
            this.m_ObjectDirectionToggle?.SetObjectDirectionEnabled(false);

            // Scene/prefab fallback: Object Direction may have been assigned
            // directly in GC2 without FranklinObjectDirectionToggle owning it.
            if (this.m_Player.Facing is UnitFacingObjectDirection)
            {
                this.m_Player.Kernel.ChangeFacing(
                    this.m_Player,
                    new UnitFacingPivot()
                );
            }
        }

        private void LockMovementAnimation()
        {
            this.m_IsVehicleAnimationLocked = true;
            this.m_HasEnteredVehicle = false;
            this.m_EntryRequestedAt = UnityEngine.Time.unscaledTime;
            this.m_MovementBridge?.SetExternalAnimationLock(true);
            this.SetPlayerBlobShadowSuspended(true);
        }

        private void UpdateVehicleAnimationLock()
        {
            if (!this.m_IsVehicleAnimationLocked) return;

            // A collision/explosion can ragdoll the Player while an entry request
            // is still walking or animating toward the seat. This is a cancelled
            // entry, never a successful uncontrollable-driver handoff. Release
            // the GC2 animation/shadow lock immediately instead of waiting for
            // the generic five-second request timeout or activating a Car camera.
            if (!this.m_HasEnteredVehicle &&
                this.m_Player?.Ragdoll.IsRagdoll == true)
            {
                this.CancelVehicleInteractionRequest();
                this.RestorePlayerCamera();
                return;
            }

            bool isControllable = this.m_Player.Player?.IsControllable == true;
            if (!this.m_HasEnteredVehicle)
            {
                bool playerIsSeatedInActiveVehicle = this.IsPlayerSeatedInActiveVehicle();
                // The paired Player/NPC gestures deliberately make the Player
                // uncontrollable before the driver seat changes ownership.
                if (!playerIsSeatedInActiveVehicle &&
                    ((this.m_ActiveCarEntry != null &&
                      this.m_ActiveCarEntry.IsTransitioning) ||
                     (this.m_ActiveBikeEntry != null &&
                      this.m_ActiveBikeEntry.IsTransitioning) ||
                     (this.m_ActiveCarjacking != null &&
                      this.m_ActiveCarjacking.IsTransitioning)))
                {
                    return;
                }

                if (!isControllable)
                {
                    this.m_HasEnteredVehicle = true;
                    this.m_ActiveCarjacking = null;
                    // CarEntry has already reasserted the final driving state
                    // before assigning SeatedCharacter. Replaying that same
                    // state here rebuilds the GC2 playable graph one frame before
                    // the Sim-Cade camera switch and creates a tiny vertical pop.
                    if (playerIsSeatedInActiveVehicle) this.ClearDelayedDrivingState();
                    else this.RestoreVehicleDrivingIdle();
                    this.ActivateVehicleCamera();
                    return;
                }

                // The nearby vehicle Trigger normally makes the Player uncontrollable once
                // its enter animation begins. Avoid leaving controls locked if its conditions
                // reject the interaction or another trigger consumes it.
                if (UnityEngine.Time.unscaledTime - this.m_EntryRequestedAt <
                    ENTRY_BEGIN_TIMEOUT)
                {
                    return;
                }
            }
            else if (!isControllable)
            {
                return;
            }

            this.m_IsVehicleAnimationLocked = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.SetPlayerBlobShadowSuspended(false);
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveBikeDriver = null;
            this.m_ActiveHelicopterDriver = null;
            this.m_ActiveCarjacking = null;
        }

        private void SetPlayerBlobShadowSuspended(bool suspended)
        {
            if (this.m_PlayerBlobShadow == null && this.m_Player != null)
            {
                FranklinBlobShadow.TryGet(this.m_Player, out this.m_PlayerBlobShadow);
            }

            this.m_PlayerBlobShadow?.SetSuspended(suspended);
        }

        private void DelayVehicleDrivingIdle(Component vehicleEntry)
        {
            this.ClearDelayedDrivingState();
            if (!this.m_DelayDrivingIdleUntilEntryFinishes) return;

            System.Type type = vehicleEntry.GetType();
            FieldInfo stateField = type.GetField("drivingState");
            if (stateField?.GetValue(vehicleEntry) is not StateData state) return;

            this.m_DelayedDrivingState = state;
            this.m_DelayedDrivingStateLayer = ReadIntField(
                type,
                vehicleEntry,
                "drivingStateLayer"
            );
            this.m_DelayedDrivingStateTransitionIn = ReadFloatField(
                type,
                vehicleEntry,
                "drivingStateTransitionIn"
            );
            this.m_DelayedDrivingStateTransitionOut = ReadFloatField(
                type,
                vehicleEntry,
                "drivingStateTransitionOut"
            );
            this.m_IsDrivingStateSuppressed = true;
        }

        private void RestoreVehicleDrivingIdle()
        {
            if (!this.m_IsDrivingStateSuppressed) return;

            ConfigState config = new ConfigState(
                0f,
                1f,
                1f,
                this.m_DelayedDrivingStateTransitionIn,
                this.m_DelayedDrivingStateTransitionOut
            );
            _ = this.m_Player.States?.SetState(
                this.m_DelayedDrivingState,
                this.m_DelayedDrivingStateLayer,
                BlendMode.Blend,
                config
            );
            this.ClearDelayedDrivingState();
        }

        private void ClearDelayedDrivingState()
        {
            this.m_IsDrivingStateSuppressed = false;
            this.m_DelayedDrivingState = default;
            this.m_DelayedDrivingStateLayer = 0;
            this.m_DelayedDrivingStateTransitionIn = 0f;
            this.m_DelayedDrivingStateTransitionOut = 0f;
        }

        private void ActivateVehicleCamera()
        {
            // Bikes always remain on the Player's current GC2 Main Camera Shot.
            // Only its Third Person aim values are temporarily overridden.
            if (this.m_ActiveBikeEntry != null)
            {
                if (this.m_BikeMainShotAim?.Activate(this.m_ActiveBikeEntry) == true)
                {
                    this.m_ActiveBikeEntry
                        .GetComponent<FranklinArcadeBikeDriver>()
                        ?.RestorePreferredFirstPersonView();
                }
                return;
            }
            if (!this.m_UseVehicleCamera || this.m_IsVehicleCameraActive) return;
            if (this.m_ActiveHelicopterDriver != null)
            {
                ShotCamera helicopterShot =
                    this.m_ActiveHelicopterDriver.CameraShot;
                if (helicopterShot == null || !this.ResolveMainCamera()) return;

                helicopterShot.enabled = true;
                this.m_PreVehicleShot =
                    this.m_MainCamera.Transition.CurrentShotCamera;
                this.m_ActiveVehicleShot = helicopterShot;
                this.m_MainCamera.Transition.ChangeToShot(
                    helicopterShot,
                    this.m_VehicleCameraEnterBlend,
                    this.m_VehicleCameraEasing
                );
                this.m_IsVehicleCameraActive = true;
                return;
            }
            if (this.m_ActiveSimcadeDriver != null)
            {
                return;
            }
            if (!this.EnsureVehicleShot() || !this.ResolveMainCamera()) return;

            this.ApplyVehicleCameraSettings();
            this.m_PreVehicleShot = this.m_MainCamera.Transition.CurrentShotCamera;
            this.m_ActiveVehicleShot = this.m_RuntimeVehicleShot;
            this.m_MainCamera.Transition.ChangeToShot(
                this.m_RuntimeVehicleShot,
                this.m_VehicleCameraEnterBlend,
                this.m_VehicleCameraEasing
            );
            this.m_IsVehicleCameraActive = true;
        }

        private void RestorePlayerCamera()
        {
            // No camera was changed for a bike, so its exit must not touch the
            // current Main Camera Shot. Restore only the TPS values captured on enter.
            if (this.m_ActiveBikeEntry != null)
            {
                this.m_BikeMainShotAim?.Deactivate();
                return;
            }

            if (!this.m_IsVehicleCameraActive) return;

            ShotCamera outgoingShot = this.m_ActiveVehicleShot;
            if (this.ResolveMainCamera())
            {
                ShotCamera currentShot =
                    this.m_MainCamera.Transition.CurrentShotCamera;
                bool stillOwnsShot = ReferenceEquals(currentShot, outgoingShot) ||
                    (currentShot != null && outgoingShot != null &&
                     currentShot == outgoingShot);
                if (stillOwnsShot)
                {
                    ShotCamera restoreShot = this.m_PreVehicleShot;
                    if (restoreShot == null)
                        restoreShot = ShortcutMainShot.Get<ShotCamera>();

                    bool canRestore = restoreShot != null &&
                        !ReferenceEquals(restoreShot, outgoingShot) &&
                        restoreShot != outgoingShot;
                    if (canRestore)
                    {
                        this.m_MainCamera.Transition.ChangeToShot(
                            restoreShot,
                            this.m_VehicleCameraExitBlend,
                            this.m_VehicleCameraEasing
                        );
                    }
                    else
                    {
                        // A streamed/destroyed aircraft can take its ShotCamera
                        // with it. Never leave GC2 pointing at that fake-null
                        // component when there is no valid main-shot fallback.
                        if (currentShot != null)
                            currentShot.OnDisableShot(this.m_MainCamera);
                        this.m_MainCamera.Transition.CurrentShotCamera = null;
                    }
                }
            }

            if (outgoingShot != null &&
                this.m_ActiveHelicopterDriver != null &&
                outgoingShot == this.m_ActiveHelicopterDriver.CameraShot)
            {
                outgoingShot.enabled = false;
            }

            this.m_PreVehicleShot = null;
            this.m_ActiveVehicleShot = null;
            this.m_IsVehicleCameraActive = false;
        }

        private bool EnsureVehicleShot()
        {
            if (this.m_RuntimeVehicleShot != null) return true;
            if (this.m_VehicleShotPrefab == null)
            {
                Debug.LogWarning(
                    "Vehicle camera is enabled but no Camera Shot Vehicle prefab is assigned.",
                    this
                );
                return false;
            }

            this.m_RuntimeVehicleShot = Instantiate(this.m_VehicleShotPrefab);
            this.m_RuntimeVehicleShot.name = "Runtime Vehicle Camera Shot";
            this.CacheDefaultVehiclePivot();
            return this.m_RuntimeVehicleShot != null;
        }

        private bool ResolveMainCamera()
        {
            if (this.m_MainCamera == null)
            {
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            }

            return this.m_MainCamera != null;
        }

        private void ApplyVehicleCameraSettings()
        {
            if (this.m_RuntimeVehicleShot?.ShotType is not ShotTypeThirdPerson shotType) return;

            ShotSystemThirdPerson thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            if (thirdPerson == null) return;

            SetDecimalField(thirdPerson, THIRD_PERSON_SHOULDER_FIELD, this.m_VehicleShoulder);
            SetDecimalField(thirdPerson, THIRD_PERSON_LIFT_FIELD, this.m_VehicleLift);
            SetDecimalField(thirdPerson, THIRD_PERSON_RADIUS_FIELD, this.m_VehicleRadius);
            this.ApplyVehicleCameraPivot(thirdPerson);

            SetDecimalField(
                thirdPerson,
                THIRD_PERSON_SMOOTH_TIME_FIELD,
                this.m_VehicleSmoothTime
            );

            if (!this.m_UseGlobalVehicleSensitivity)
            {
                thirdPerson.Sensitivity = new Vector2(
                    this.m_VehicleSensitivityX,
                    this.m_VehicleSensitivityY
                );
            }

            thirdPerson.MaxPitch = this.m_VehicleMaxPitch;
            thirdPerson.Alignment.AutoAlign = this.m_VehicleAutoAlign;
            thirdPerson.Alignment.Delay = this.m_VehicleAlignDelay;
            thirdPerson.Alignment.SmoothTime = this.m_VehicleAlignSmoothTime;

            if (THIRD_PERSON_MAX_YAW_FIELD?.GetValue(thirdPerson) is
                EnablerAngle180 maxYawSetting)
            {
                maxYawSetting.IsEnabled = this.m_EnableVehicleMaxYaw;
                maxYawSetting.Value = this.m_VehicleMaxYaw;
            }
        }

        private void CacheDefaultVehiclePivot()
        {
            if (this.m_RuntimeVehicleShot?.ShotType is not ShotTypeThirdPerson shotType) return;

            ShotSystemThirdPerson thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            if (thirdPerson == null) return;

            this.m_DefaultVehiclePivot = THIRD_PERSON_PIVOT_FIELD?.GetValue(
                thirdPerson
            ) as PropertyGetGameObject;
        }

        private void ApplyVehicleCameraPivot(ShotSystemThirdPerson thirdPerson)
        {
            if (THIRD_PERSON_PIVOT_FIELD == null) return;

            PropertyGetGameObject pivot = this.m_PivotCameraToActiveVehicle &&
                this.m_ActiveVehiclePivot != null
                ? GetGameObjectInstance.Create(this.m_ActiveVehiclePivot)
                : this.m_DefaultVehiclePivot;

            if (pivot != null)
            {
                THIRD_PERSON_PIVOT_FIELD.SetValue(thirdPerson, pivot);
            }
        }

        private static void SetDecimalField(
            ShotSystemThirdPerson thirdPerson,
            FieldInfo field,
            float value)
        {
            field?.SetValue(thirdPerson, new PropertyGetDecimal(value));
        }

        private static Component FindVehicleEntry(GameObject target)
        {
            for (Transform current = target.transform; current != null; current = current.parent)
            {
                Component entry = current.GetComponent("CarEntry") ??
                                  current.GetComponent("BikeEntry");
                if (entry != null) return entry;
            }

            return null;
        }

        private static int ReadIntField(System.Type type, object target, string fieldName)
        {
            return type.GetField(fieldName)?.GetValue(target) is int value ? value : 0;
        }

        private static float ReadFloatField(System.Type type, object target, string fieldName)
        {
            object value = type.GetField(fieldName)?.GetValue(target);
            return value is float result ? result : 0f;
        }

    }
}
