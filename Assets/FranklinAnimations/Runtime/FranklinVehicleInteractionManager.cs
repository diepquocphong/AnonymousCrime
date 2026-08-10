using System.Reflection;
using FranklinGame.Vehicles;
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
        private const int FALLEN_BIKE_HIT_CAPACITY = 32;

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
        [Tooltip("Applies bike-only TPS aim values to the current GC2 Main Camera Shot.")]
        private FranklinBikeMainShotAim m_BikeMainShotAim;

        [Header("Fallen bike interaction")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Distance around the Player used to find a grounded fallen bike when its rotated GC2 Hotspot is no longer selected.")]
        private float m_FallenBikeInteractionRadius = 2.25f;

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
        [SerializeField] private bool m_VehicleAutoAlign;
        [SerializeField, Min(0f)] private float m_VehicleAlignDelay = 3f;
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
        private ShotCamera m_PreVehicleShot;
        private PropertyGetGameObject m_DefaultVehiclePivot;
        private GameObject m_ActiveVehiclePivot;
        private CarEntry m_ActiveCarEntry;
        private BikeEntry m_ActiveBikeEntry;
        private SimcadeCarDriver m_ActiveSimcadeDriver;
        private bool m_IsVehicleCameraActive;
        private SimcadeCarjacking m_ActiveCarjacking;
        private readonly Collider[] m_FallenBikeHits =
            new Collider[FALLEN_BIKE_HIT_CAPACITY];

        private void Awake()
        {
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            if (this.m_IsVehicleAnimationLocked)
            {
                this.m_IsVehicleAnimationLocked = false;
                this.m_MovementBridge?.SetExternalAnimationLock(false);
            }

            this.RestorePlayerCamera();
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
        /// True only while RVR has selected the entry spot on an available car or bike.
        /// The mobile HUD uses this to hide its Enter button everywhere else.
        /// </summary>
        public bool CanRequestVehicleInteraction
        {
            get
            {
                if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                    this.m_Player.Player?.IsControllable != true)
                {
                    return false;
                }

                return this.TryGetSelectedDriverDoor(out _, out _);
            }
        }

        /// <summary>
        /// Requests entry from the selected vehicle. CarEntry/BikeEntry remain
        /// authoritative for animation, alignment and vehicle ownership.
        /// </summary>
        public bool RequestVehicleInteraction()
        {
            if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                this.m_Player.Player?.IsControllable != true)
            {
                return false;
            }

            return this.TryStartVehicleInteraction();
        }

        private bool TryStartVehicleInteraction()
        {
            if (!this.TryGetSelectedDriverDoor(
                    out IInteractive target,
                    out Component vehicleEntry))
            {
                return false;
            }

            this.LockMovementAnimation();
            this.m_ActiveVehiclePivot = vehicleEntry.gameObject;
            this.m_ActiveCarEntry = vehicleEntry as CarEntry;
            this.m_ActiveBikeEntry = vehicleEntry as BikeEntry;
            this.m_ActiveSimcadeDriver = this.m_ActiveCarEntry != null
                ? this.m_ActiveCarEntry.GetComponent<SimcadeCarDriver>()
                : null;
            this.DelayVehicleDrivingIdle(vehicleEntry);

            if (vehicleEntry is CarEntry carEntry)
            {
                bool wasOccupied = carEntry.SeatedCharacter != null;
                CarEntrySideMode requestedSide = IsPassengerDoorTarget(
                    target?.Instance,
                    vehicleEntry.transform
                )
                    ? CarEntrySideMode.PassengerDoor
                    : CarEntrySideMode.DriverDoor;
                if (!carEntry.RequestEnter(this.m_Player, requestedSide))
                {
                    this.CancelVehicleInteractionRequest();
                    return false;
                }

                this.m_ActiveCarjacking = wasOccupied
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

        private bool TryGetSelectedDriverDoor(
            out IInteractive target,
            out Component vehicleEntry)
        {
            target = this.m_Player.Interaction.Target;
            vehicleEntry = target?.Instance != null
                ? FindVehicleEntry(target.Instance)
                : null;
            Hotspot hotspot = target?.Instance != null
                ? target.Instance.GetComponent<Hotspot>()
                : null;
            if (hotspot != null && hotspot.IsActive && vehicleEntry != null &&
                this.IsAvailableVehicleEntry(vehicleEntry) &&
                IsDriverDoorTarget(target.Instance, vehicleEntry.transform))
            {
                return true;
            }

            // A fallen bike rotates its authored Hotspot with the Rigidbody. GC2
            // may therefore select no target (or a different nearby target) even
            // while the Player is touching the bike. Use the physical rendered
            // body as a fallback, but only after the ragdoll is grounded and slow
            // enough to begin the manual lift sequence.
            target = null;
            if (this.TryGetNearbyFallenBike(out BikeEntry fallenBike))
            {
                vehicleEntry = fallenBike;
                return true;
            }

            vehicleEntry = null;
            return false;
        }

        private bool IsAvailableVehicleEntry(Component vehicleEntry)
        {
            if (vehicleEntry is CarEntry carEntry)
            {
                if (carEntry.IsTransitioning) return false;
                if (carEntry.SeatedCharacter != null)
                {
                    SimcadeCarjacking carjacking = carEntry.GetComponent<SimcadeCarjacking>();
                    if (carjacking == null || !carjacking.CanCarjack(this.m_Player)) return false;
                }

                SimcadeCarDriver driver = carEntry.GetComponent<SimcadeCarDriver>();
                if (driver != null && driver.IsVehicleEnabled) return false;
            }
            else if (vehicleEntry is BikeEntry bikeEntry)
            {
                if (bikeEntry.IsTransitioning || bikeEntry.SeatedCharacter != null) return false;
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
                    current.gameObject.name == "Triggers_Enter/Exit Passenger")
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetNearbyFallenBike(out BikeEntry closestBike)
        {
            closestBike = null;
            if (this.m_Player == null) return false;

            Vector3 playerPosition = this.m_Player.transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                playerPosition,
                Mathf.Max(0.5f, this.m_FallenBikeInteractionRadius),
                this.m_FallenBikeHits,
                ~0,
                QueryTriggerInteraction.Collide
            );
            float closestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = this.m_FallenBikeHits[index];
                if (hit == null) continue;

                BikeEntry bikeEntry = hit.GetComponentInParent<BikeEntry>();
                if (bikeEntry == null || bikeEntry == closestBike ||
                    !this.IsAvailableVehicleEntry(bikeEntry))
                {
                    continue;
                }

                FranklinArcadeBikeRagdoll ragdoll =
                    bikeEntry.GetComponent<FranklinArcadeBikeRagdoll>();
                if (ragdoll == null || !ragdoll.CanInteractWhileFallen) continue;

                Vector3 closestPoint = hit.ClosestPoint(playerPosition);
                float distance = (closestPoint - playerPosition).sqrMagnitude;
                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closestBike = bikeEntry;
            }

            return closestBike != null;
        }

        private static bool IsPassengerDoorTarget(
            GameObject targetInstance,
            Transform vehicleRoot)
        {
            for (Transform current = targetInstance != null
                     ? targetInstance.transform
                     : null;
                 current != null && current != vehicleRoot;
                 current = current.parent)
            {
                if (current.gameObject.name == "Triggers_Enter/Exit Passenger")
                    return true;
            }

            return false;
        }

        private void CancelVehicleInteractionRequest()
        {
            this.m_IsVehicleAnimationLocked = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.ClearDelayedDrivingState();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveCarjacking = null;
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
                  this.m_ActiveCarEntry.SeatedCharacter == this.m_Player) ||
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
            if (this.m_BikeMainShotAim == null)
            {
                this.m_BikeMainShotAim = this.GetComponent<FranklinBikeMainShotAim>();
            }

            return this.m_Player != null;
        }

        private void LockMovementAnimation()
        {
            this.m_IsVehicleAnimationLocked = true;
            this.m_HasEnteredVehicle = false;
            this.m_EntryRequestedAt = UnityEngine.Time.unscaledTime;
            this.m_MovementBridge?.SetExternalAnimationLock(true);
        }

        private void UpdateVehicleAnimationLock()
        {
            if (!this.m_IsVehicleAnimationLocked) return;

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
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_ActiveVehiclePivot = null;
            this.m_ActiveCarEntry = null;
            this.m_ActiveBikeEntry = null;
            this.m_ActiveSimcadeDriver = null;
            this.m_ActiveCarjacking = null;
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
                this.m_BikeMainShotAim?.Activate(this.m_ActiveBikeEntry);
                return;
            }
            if (!this.m_UseVehicleCamera || this.m_IsVehicleCameraActive) return;
            if (this.m_ActiveSimcadeDriver != null)
            {
                return;
            }
            if (!this.EnsureVehicleShot() || !this.ResolveMainCamera()) return;

            this.ApplyVehicleCameraSettings();
            this.m_PreVehicleShot = this.m_MainCamera.Transition.CurrentShotCamera;
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

            if (this.ResolveMainCamera() && this.m_PreVehicleShot != null &&
                this.m_MainCamera.Transition.CurrentShotCamera == this.m_RuntimeVehicleShot)
            {
                this.m_MainCamera.Transition.ChangeToShot(
                    this.m_PreVehicleShot,
                    this.m_VehicleCameraExitBlend,
                    this.m_VehicleCameraEasing
                );
            }

            this.m_PreVehicleShot = null;
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
