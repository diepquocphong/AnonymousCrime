using ArcadeBP_Pro;
using FranklinGame.Animations;
using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Connects Arcade Bike Physics Pro to Franklin's vehicle entry flow and shared
    /// mobile controls. The Arcade controller remains the only bike physics engine.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArcadeBikeControllerPro), typeof(Rigidbody))]
    public sealed class FranklinArcadeBikeDriver : MonoBehaviour,
        IRvrVehicleInputController,
        IRvrVehicleAirborneState
    {
        private const string FIRST_PERSON_PREFERENCE_KEY =
            "Franklin.Vehicle.Bike.FirstPersonView";

        private static bool s_FirstPersonPreferenceLoaded;
        private static bool s_FirstPersonPreferred;

        [SerializeField] private ArcadeBikeControllerPro m_Controller;
        [SerializeField] private BikeEntry m_BikeEntry;
        [SerializeField] private VehicleLights m_VehicleLights;
        [SerializeField] private FranklinBikeFuel m_Fuel;

        [Header("Horn")]
        [SerializeField] private AudioClip m_HornClip;
        [SerializeField] private AudioSource m_HornSource;
        [SerializeField, Range(0f, 1f)] private float m_HornMaxVolume = 0.72f;
        [SerializeField, Range(0.5f, 2f)] private float m_HornPitch = 1.18f;
        [SerializeField, Min(0.01f)] private float m_HornFadeInSpeed = 18f;
        [SerializeField, Min(0.01f)] private float m_HornFadeOutSpeed = 14f;

        [Header("Slow Drive")]
        [SerializeField, Range(0.1f, 1f)] private float m_SlowThrottle = 0.35f;
        [SerializeField, Min(1f)]
        [Tooltip("Maximum speed while the Slow Drive mobile button is held, in km/h.")]
        private float m_SlowSpeedLimitKph = 50f;
        [SerializeField, Min(0.1f)]
        [Tooltip("Smooth deceleration in m/s² when Slow Drive is pressed above its speed limit.")]
        private float m_SlowSpeedDeceleration = 7.5f;

        [SerializeField] private bool m_ReadKeyboardInput = true;

        [Header("Mobile Thermal Budget")]
        [SerializeField, Range(30, 60)]
        [Tooltip("Only lowers an explicit mobile target above this value while riding. " +
                 "It never raises Unity's default or a lower project frame cap.")]
        private int m_MobileMaximumFrameRate = 60;

        [Header("Exit Stop")]
        [Tooltip("Planar deceleration applied while a moving bike is preparing to exit.")]
        [SerializeField, Min(0.1f)] private float m_ExitStopDeceleration = 7.5f;
        [Tooltip("Angular deceleration applied while preparing to exit so the bike settles before parking.")]
        [SerializeField, Min(0.1f)] private float m_ExitStopAngularDeceleration = 6f;

        [Header("Crash Engine Audio")]
        [SerializeField]
        [Tooltip("Keeps the engine idling while the crashed bike tumbles and waits to be raised instead of cutting the loop immediately.")]
        private bool m_KeepEngineRunningAfterCrash = true;
        [SerializeField, Range(0f, 1f)] private float m_CrashIdleEngineVolume = 0.5f;
        [SerializeField, Range(0.1f, 3f)] private float m_CrashIdleEnginePitch = 0.35f;
        [SerializeField, Range(2f, 30f)]
        [Tooltip("Maximum real-time duration for an unattended crashed Bike engine. " +
                 "A finite timeout avoids a permanent audio voice and fuel coroutine on mobile.")]
        private float m_CrashIdleEngineTimeout = 10f;

        private Rigidbody m_Rigidbody;
        private bool m_IsVehicleEnabled;
        private bool m_VirtualAccelerate;
        private bool m_VirtualSlowAccelerate;
        private bool m_VirtualBrakeReverse;
        private bool m_VirtualSteerLeft;
        private bool m_VirtualSteerRight;
        private bool m_VirtualHandbrake;
        private bool m_VirtualWheelie;
        private bool m_VirtualBurnout;
        private bool m_StuntWeaponSuppressionActive;
        private Character m_StuntWeaponRider;
        private FranklinBikeMainShotAim m_FirstPersonCameraManager;
        private bool m_ExternalHandbrake;
        private bool m_ShooterReloadSteeringLocked;
        private bool m_IsStoppingForExit;
        private bool m_KeepDynamicWhenDisabled;
        private RigidbodyConstraints m_DrivingConstraints;
        private bool m_DrivingUseGravity;
        private float m_DrivingLinearDamping;
        private float m_DrivingAngularDamping;
        private int m_DrivingSolverIterations;
        private int m_DrivingSolverVelocityIterations;
        private CollisionDetectionMode m_DrivingCollisionDetectionMode;
        private RigidbodyInterpolation m_DrivingInterpolation;
        private float m_DrivingMaxDepenetrationVelocity;
        private bool m_DrivingAutomaticCenterOfMass;
        private Vector3 m_DrivingCenterOfMass;
        private bool m_HasCapturedDrivingPhysics;
        private bool m_IsCrashEngineRunning;
        private bool m_IsDamageLocked;
        private bool m_HasFuel = true;
        private float m_CurrentThrottle;
        private bool m_HornPressed;
        private bool m_HornNeedsUpdate;
        private float m_CrashEngineStopAt;
        private FranklinBikeBrakeReverseFlare[] m_BrakeReverseFlares;
        private bool m_HasAppliedMobileFrameRateCap;
        private int m_PreviousMobileTargetFrameRate;

        public bool IsVehicleEnabled => this.m_IsVehicleEnabled;
        public bool IsDamageLocked => this.m_IsDamageLocked;
        public bool IsAirborne => this.m_IsVehicleEnabled &&
                                  this.m_Controller != null &&
                                  !this.m_Controller.frontWheelIsGrounded &&
                                  !this.m_Controller.rearWheelIsGrounded;
        public bool IsCrashCoasting => !this.m_IsVehicleEnabled &&
                                       this.m_KeepDynamicWhenDisabled;
        public bool IsFirstPersonViewActive =>
            this.m_FirstPersonCameraManager != null &&
            this.m_FirstPersonCameraManager.IsFirstPersonActive;
        public bool UseSeatEntryAlignment => false;

        public Transform VehicleBody
        {
            get
            {
                if (this.m_Controller == null) return this.transform;
                ArcadeBikeControllerPro.BikeReferences references =
                    this.m_Controller.bikeReferences;
                return references?.BikeModel != null
                    ? references.BikeModel
                    : references?.LeanTransform != null
                        ? references.LeanTransform
                        : this.transform;
            }
        }

        public float SpeedMetersPerSecond => this.m_Rigidbody != null
            ? Vector3.ProjectOnPlane(this.m_Rigidbody.linearVelocity, Vector3.up).magnitude
            : 0f;
        public float SpeedKph => this.SpeedMetersPerSecond * 3.6f;
        public float SignedForwardSpeedMetersPerSecond => this.m_Controller != null
            ? this.m_Controller.localBikeVelocity.z
            : 0f;
        public bool IsReverseInputActive => this.m_IsVehicleEnabled &&
                                            !this.m_VirtualBurnout &&
                                            this.m_Controller?.bikeInput != null &&
                                            this.m_Controller.bikeInput.Reverse > 0.01f &&
                                            this.m_Controller.bikeInput.Accelerate <= 0.01f;
        public float ThrottleMagnitude => this.m_CurrentThrottle;
        public float SlowSpeedLimitKph => this.m_SlowSpeedLimitKph;

        public void ToggleRiderHelmet()
        {
            Character rider = this.m_BikeEntry != null
                ? this.m_BikeEntry.SeatedCharacter
                : null;
            if (rider == null) return;

            FranklinBikeHelmetController helmet =
                rider.GetComponent<FranklinBikeHelmetController>();
            if (helmet == null)
                helmet = rider.GetComponentInChildren<FranklinBikeHelmetController>(true);
            helmet?.ToggleHelmet();
        }

        private void Awake()
        {
            this.ResolveReferences();
            this.CacheRuntimeEffects();
            if (this.m_Rigidbody != null)
            {
                this.CaptureDrivingPhysicsSettings();
                this.RestoreDrivingPhysicsSettings();
            }
            this.m_VehicleLights?.FrontLightsOff();

            // This driver executes before ArcadeBikeControllerPro. Parking the
            // controller here prevents every scene Bike from prewarming ABP smoke
            // and skidmark objects before a Player actually chooses that Bike.
            this.ApplyVehicleState();
        }

        private void Start()
        {
            this.ApplyVehicleState();
        }

        private void OnDisable()
        {
            this.m_ShooterReloadSteeringLocked = false;
            this.ResetVirtualInputs();
            this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            this.m_Fuel?.SetEngineActive(false);
            this.m_VehicleLights?.FrontLightsOff();
            this.SetBrakeReverseEffectsRuntimeActive(false);
            this.ApplyMobileFrameRateBudget(false);
            this.StopHornImmediately();
        }

        private void Update()
        {
            // Parked Bikes have no frame work. Public entry/damage methods remain
            // callable while this lightweight component waits for a state change.
            if (!this.m_IsVehicleEnabled && !this.m_IsCrashEngineRunning &&
                !this.m_HornNeedsUpdate)
            {
                if (this.m_StuntWeaponSuppressionActive)
                    this.SetStuntWeaponSuppression(false);
                return;
            }

            if (!this.ResolveReferences()) return;

            this.UpdateHorn();

            if (!this.m_IsVehicleEnabled)
            {
                if (this.m_StuntWeaponSuppressionActive)
                    this.SetStuntWeaponSuppression(false);
                if (this.m_IsCrashEngineRunning) this.UpdateCrashEngineAudio();
                return;
            }

            if (this.m_IsDamageLocked)
            {
                if (this.m_StuntWeaponSuppressionActive)
                    this.SetStuntWeaponSuppression(false);
                // SetDamageLocked already clears ABP input once. Avoid writing the
                // same six zero values every render frame while waiting to exit.
                return;
            }

            float accelerate = this.m_VirtualBurnout
                ? 1f
                : this.m_VirtualAccelerate
                    ? 1f
                    : this.m_VirtualSlowAccelerate ? this.m_SlowThrottle : 0f;
            bool slowDriveOnly = this.m_VirtualSlowAccelerate &&
                                 !this.m_VirtualAccelerate &&
                                 !this.m_VirtualBurnout;
            float reverse = this.m_VirtualBurnout || this.m_VirtualBrakeReverse
                ? 1f
                : 0f;
            float steerLeft = this.m_VirtualSteerLeft ? 1f : 0f;
            float steerRight = this.m_VirtualSteerRight ? 1f : 0f;
            float wheelie = this.m_VirtualWheelie ? 1f : 0f;
            float handbrake = this.m_VirtualHandbrake || this.m_ExternalHandbrake ||
                              this.m_IsStoppingForExit
                ? 1f
                : 0f;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = this.m_ReadKeyboardInput ? Keyboard.current : null;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) accelerate = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) reverse = 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerLeft = 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerRight = 1f;
                if (keyboard.spaceKey.isPressed) handbrake = 1f;
                if (keyboard.leftShiftKey.isPressed) wheelie = 1f;

                if (keyboard.eKey.wasPressedThisFrame)
                {
                    this.SetStuntWeaponSuppression(false);
                    this.RequestExit();
                    return;
                }
            }
#endif

            if (this.m_ShooterReloadSteeringLocked)
            {
                steerLeft = 0f;
                steerRight = 0f;
            }

            if (slowDriveOnly && this.SpeedKph >= this.m_SlowSpeedLimitKph)
            {
                accelerate = 0f;
            }

            if (!this.m_HasFuel)
            {
                accelerate = 0f;
                wheelie = 0f;
            }

            bool stuntInputActive = wheelie > 0.01f ||
                                    (accelerate > 0.01f && reverse > 0.01f);
            this.SetStuntWeaponSuppression(
                stuntInputActive && !this.m_IsStoppingForExit
            );

            if (this.m_IsStoppingForExit)
            {
                // Mobile/keyboard input is ignored once exit braking begins.
                // Keep ABP active so suspension and balance still settle while
                // braking, but never allow throttle, reverse or steering input.
                this.ProvideInput(0f, 0f, 1f, 0f, 0f, 0f);
                return;
            }

            this.ProvideInput(
                accelerate,
                reverse,
                handbrake,
                steerLeft,
                steerRight,
                wheelie
            );
        }

        private void FixedUpdate()
        {
            if (!this.m_IsVehicleEnabled || this.m_Rigidbody == null ||
                this.m_Rigidbody.isKinematic)
            {
                return;
            }

            bool slowDriveOnly = this.m_VirtualSlowAccelerate &&
                                 !this.m_VirtualAccelerate &&
                                 !this.m_VirtualBurnout;
            if (slowDriveOnly) this.ApplySlowDriveSpeedLimit();
            if (!this.m_IsStoppingForExit) return;

            Vector3 velocity = this.m_Rigidbody.linearVelocity;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 verticalVelocity = velocity - planarVelocity;
            planarVelocity = Vector3.MoveTowards(
                planarVelocity,
                Vector3.zero,
                this.m_ExitStopDeceleration * Time.fixedDeltaTime
            );
            this.m_Rigidbody.linearVelocity = verticalVelocity + planarVelocity;
            this.m_Rigidbody.angularVelocity = Vector3.MoveTowards(
                this.m_Rigidbody.angularVelocity,
                Vector3.zero,
                this.m_ExitStopAngularDeceleration * Time.fixedDeltaTime
            );
        }

        private void ApplySlowDriveSpeedLimit()
        {
            float limitMetersPerSecond =
                Mathf.Max(1f, this.m_SlowSpeedLimitKph) / 3.6f;
            Vector3 velocity = this.m_Rigidbody.linearVelocity;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float planarSpeed = planarVelocity.magnitude;
            if (planarSpeed <= limitMetersPerSecond || planarSpeed <= 0.0001f) return;

            float limitedSpeed = Mathf.MoveTowards(
                planarSpeed,
                limitMetersPerSecond,
                this.m_SlowSpeedDeceleration * Time.fixedDeltaTime
            );
            Vector3 verticalVelocity = velocity - planarVelocity;
            this.m_Rigidbody.linearVelocity = verticalVelocity +
                                              planarVelocity * (limitedSpeed / planarSpeed);
        }

        public void SetVehicleEnabled(bool state)
        {
            this.SetVehicleEnabled(state, false);
        }

        public void SetVehicleEnabled(bool state, bool preserveMomentum)
        {
            this.ResolveReferences();
            if (state && this.m_IsDamageLocked)
            {
                this.m_IsVehicleEnabled = false;
                this.m_IsStoppingForExit = false;
                this.ResetVirtualInputs();
                this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
                this.ApplyVehicleState();
                this.m_Fuel?.SetEngineActive(false);
                return;
            }

            FranklinArcadeBikeRagdoll bikeRagdoll =
                this.GetComponent<FranklinArcadeBikeRagdoll>();
            if (state)
            {
                this.m_IsCrashEngineRunning = false;
                this.m_CrashEngineStopAt = 0f;
                bikeRagdoll?.DeactivateRagdollForDriving();
            }
            bool preserveDynamicRagdoll = !state && bikeRagdoll != null &&
                                          bikeRagdoll.IsRagdoll;
            this.m_IsVehicleEnabled = state;
            this.m_IsStoppingForExit = false;
            this.m_KeepDynamicWhenDisabled = preserveDynamicRagdoll;

            if (!state)
            {
                this.ResetVirtualInputs();
                this.m_ExternalHandbrake = false;
                this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
                this.m_VehicleLights?.FrontLightsOff();
            }

            if (!preserveMomentum && this.m_Rigidbody != null &&
                !this.m_Rigidbody.isKinematic)
            {
                this.m_Rigidbody.linearVelocity = Vector3.zero;
                this.m_Rigidbody.angularVelocity = Vector3.zero;
            }

            this.ApplyVehicleState();
            this.m_Fuel?.SetEngineActive(state);
        }

        /// <summary>
        /// Releases all rider input after a crash but lets the unoccupied bike keep
        /// its current Rigidbody momentum. This is deliberately separate from the
        /// normal exit path, which parks the bike immediately.
        /// </summary>
        public void CrashDismount(float fallSign, float toppleAngularVelocity)
        {
            this.ResolveReferences();
            this.CaptureDrivingPhysicsSettings();
            this.m_IsVehicleEnabled = false;
            this.m_IsStoppingForExit = false;
            this.m_KeepDynamicWhenDisabled = true;
            this.m_IsCrashEngineRunning = this.m_KeepEngineRunningAfterCrash;
            this.m_CrashEngineStopAt = this.m_IsCrashEngineRunning
                ? Time.unscaledTime + Mathf.Max(2f, this.m_CrashIdleEngineTimeout)
                : 0f;
            this.ResetVirtualInputs();
            this.m_ExternalHandbrake = false;
            this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            this.m_VehicleLights?.FrontLightsOff();
            this.ApplyVehicleState();
            this.m_Fuel?.SetEngineActive(this.m_IsCrashEngineRunning);

            if (this.m_Rigidbody != null)
            {
                Vector3 fallAxis = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fallAxis.sqrMagnitude < 0.0001f) fallAxis = transform.forward;
                fallAxis.Normalize();
                this.m_Rigidbody.maxAngularVelocity = Mathf.Max(
                    this.m_Rigidbody.maxAngularVelocity,
                    Mathf.Max(3f, toppleAngularVelocity * 1.5f)
                );
                this.m_Rigidbody.angularVelocity =
                    fallAxis * (fallSign < 0f ? -1f : 1f) *
                    Mathf.Max(0f, toppleAngularVelocity);
            }
        }

        /// <summary>
        /// Keeps an ABP bike ragdoll fully physical even after it has come to rest.
        /// Sleeping is allowed, but kinematic and frozen rotation are never used.
        /// </summary>
        public void KeepCrashRagdollDynamic()
        {
            if (this.m_IsVehicleEnabled || this.m_Rigidbody == null) return;
            this.m_KeepDynamicWhenDisabled = true;
            this.m_Rigidbody.isKinematic = false;
            this.m_Rigidbody.constraints = this.m_DrivingConstraints &
                                           ~RigidbodyConstraints.FreezeRotation;
            this.m_Rigidbody.useGravity = true;
            this.m_Rigidbody.ResetCenterOfMass();
        }

        /// <summary>
        /// Parks a bike only after FranklinArcadeBikeRagdoll has confirmed that a
        /// left/right model surface is stably resting on static ground.
        /// </summary>
        public void ParkGroundedRagdoll()
        {
            if (this.m_IsVehicleEnabled || this.m_Rigidbody == null) return;
            this.m_KeepDynamicWhenDisabled = false;
            this.m_Rigidbody.linearVelocity = Vector3.zero;
            this.m_Rigidbody.angularVelocity = Vector3.zero;
            this.ApplyVehicleState();
        }

        /// <summary>
        /// Releases the crash-only audio voice and fuel tick immediately. Terminal
        /// destruction calls this explicitly; ordinary crashes also use the finite
        /// timeout configured above.
        /// </summary>
        public void StopCrashEngineImmediately()
        {
            this.m_IsCrashEngineRunning = false;
            this.m_CrashEngineStopAt = 0f;
            this.m_Fuel?.SetEngineActive(false);

            AudioSource engine = this.m_Controller?.bikeAudio?.engineSound;
            if (engine != null) engine.Stop();
        }

        public void BeginExitStop()
        {
            if (!this.m_IsVehicleEnabled) return;
            this.m_IsStoppingForExit = true;
            this.ResetVirtualInputs();
            this.m_ExternalHandbrake = false;
            this.ProvideInput(0f, 0f, 1f, 0f, 0f, 0f);
        }

        public void CancelExitStop()
        {
            this.m_IsStoppingForExit = false;
        }

        public void SetHandbrakeInput(bool active)
        {
            this.m_ExternalHandbrake = active;
        }

        public void SetVirtualAccelerateInput(bool active)
        {
            this.m_VirtualAccelerate = active;
        }

        public void SetVirtualSlowAccelerateInput(bool active)
        {
            this.m_VirtualSlowAccelerate = active;
        }

        public void SetVirtualBrakeReverseInput(bool active)
        {
            this.m_VirtualBrakeReverse = active;
        }

        public void SetVirtualSteerLeftInput(bool active)
        {
            this.m_VirtualSteerLeft = active &&
                                      !this.m_ShooterReloadSteeringLocked;
        }

        public void SetVirtualSteerRightInput(bool active)
        {
            this.m_VirtualSteerRight = active &&
                                       !this.m_ShooterReloadSteeringLocked;
        }

        public void SetShooterReloadSteeringLocked(bool active)
        {
            if (this.m_ShooterReloadSteeringLocked == active) return;
            this.m_ShooterReloadSteeringLocked = active;
            if (!active) return;

            this.m_VirtualSteerLeft = false;
            this.m_VirtualSteerRight = false;
            if (this.m_Controller?.bikeInput == null) return;

            this.m_Controller.bikeInput.SteeringLeft = 0f;
            this.m_Controller.bikeInput.SteeringRight = 0f;
        }

        public void SetVirtualHandbrakeInput(bool active)
        {
            this.m_VirtualHandbrake = active;
        }

        public void SetVirtualWheelieInput(bool active)
        {
            this.m_VirtualWheelie = active;
        }

        public void SetVirtualBurnoutInput(bool active)
        {
            this.m_VirtualBurnout = active;
        }

        /// <summary>
        /// Locks propulsion at zero Bike health without breaking the normal exit
        /// request. Repair clears the lock but never auto-mounts or auto-enables.
        /// </summary>
        public void SetDamageLocked(bool locked)
        {
            if (this.m_IsDamageLocked == locked) return;
            this.m_IsDamageLocked = locked;
            if (!locked) return;

            this.m_IsStoppingForExit = false;
            this.ResetVirtualInputs();
            this.m_ExternalHandbrake = false;
            this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            this.m_VehicleLights?.FrontLightsOff();
            this.SetBrakeReverseEffectsRuntimeActive(false);
        }

        public void ConfigureFuel(FranklinBikeFuel fuel)
        {
            this.m_Fuel = fuel;
        }

        /// <summary>
        /// Fuel only disables propulsion. Steering, braking, suspension and the
        /// normal exit flow remain available when the tank reaches empty.
        /// </summary>
        public void SetFuelAvailable(bool available)
        {
            this.m_HasFuel = available;
            if (!available)
            {
                this.m_VirtualAccelerate = false;
                this.m_VirtualSlowAccelerate = false;
                this.m_VirtualWheelie = false;
                this.m_VirtualBurnout = false;
                this.SetStuntWeaponSuppression(false);
                this.m_CurrentThrottle = 0f;
            }

            if (this.m_Controller != null)
                this.m_Controller.canAccelerate = this.m_IsVehicleEnabled &&
                                                  !this.m_IsDamageLocked && available;

            AudioSource engine = this.m_Controller?.bikeAudio?.engineSound;
            if (!available)
            {
                engine?.Stop();
            }
            else if (this.m_IsVehicleEnabled && engine != null &&
                     !engine.isPlaying && engine.clip != null)
            {
                engine.mute = false;
                engine.Play();
            }
        }

        public void SetHeadlightEnabled(bool active)
        {
            if (this.m_VehicleLights == null)
            {
                this.m_VehicleLights = this.GetComponent<VehicleLights>();
            }
            if (this.m_VehicleLights == null) return;

            if (active && this.m_IsVehicleEnabled)
            {
                this.m_VehicleLights.FrontLightsOn();
            }
            else
            {
                this.m_VehicleLights.FrontLightsOff();
            }
        }

        /// <summary>
        /// Starts or releases the Bike's 3D hold horn. Its AudioSource is created
        /// once on first use and reused for the lifetime of the Bike.
        /// </summary>
        public void SetHornPressed(bool pressed)
        {
            if (pressed && (!this.m_IsVehicleEnabled || this.m_IsDamageLocked))
                return;

            this.m_HornPressed = pressed;
            if (!pressed)
            {
                this.m_HornNeedsUpdate = this.m_HornSource != null &&
                                         this.m_HornSource.isPlaying;
                return;
            }

            if (!this.EnsureHornSource())
            {
                this.m_HornPressed = false;
                this.m_HornNeedsUpdate = false;
                return;
            }

            this.m_HornNeedsUpdate = true;
            if (this.m_HornSource.isPlaying) return;
            this.m_HornSource.volume = 0f;
            this.m_HornSource.Play();
        }

        /// <summary>
        /// Delegates Bike FPS to the shared ManagerVehicle camera component under
        /// Player. Every bike therefore uses the same GC2 Main Camera Shot.
        /// </summary>
        public void SetFirstPersonView(bool active)
        {
            this.ApplyFirstPersonView(active, true);
        }

        /// <summary>
        /// Temporarily restores TPS for exit, crash or modal UI without changing
        /// the FPS/TPS choice saved by the Player.
        /// </summary>
        public void RestoreThirdPersonViewPreservingPreference()
        {
            this.ApplyFirstPersonView(false, false);
        }

        /// <summary>
        /// Called after ManagerVehicle has activated the Bike Main Shot. This
        /// second handoff is required because BikeEntry enables physics before
        /// the shared GC2 camera profile becomes active.
        /// </summary>
        public void RestorePreferredFirstPersonView()
        {
            if (this.m_IsVehicleEnabled && GetFirstPersonPreference())
                this.ApplyFirstPersonView(true, false);
        }

        private void ApplyFirstPersonView(bool active, bool persistPreference)
        {
            if (active && (!this.m_IsVehicleEnabled || this.m_IsDamageLocked)) return;

            Character rider = active
                ? this.m_BikeEntry?.SeatedCharacter
                : this.m_StuntWeaponRider ?? this.m_BikeEntry?.SeatedCharacter;
            if (active)
            {
                if (rider == null) return;
                this.m_FirstPersonCameraManager = rider.GetComponentInChildren<
                    FranklinBikeMainShotAim
                >(true);
            }

            bool applied = active
                ? this.m_FirstPersonCameraManager != null &&
                  this.m_FirstPersonCameraManager.SetFirstPersonActive(true)
                : true;
            if (!applied) return;

            if (!active)
            {
                this.m_FirstPersonCameraManager?.SetFirstPersonActive(false);
                this.m_FirstPersonCameraManager = null;
            }

            if (persistPreference && this.m_IsVehicleEnabled &&
                !this.m_IsDamageLocked)
            {
                SetFirstPersonPreference(active);
            }
        }

        public void SetFirstPersonOrbitSuppressed(bool suppressed)
        {
            this.m_FirstPersonCameraManager?.SetFirstPersonOrbitSuppressed(suppressed);
        }

        public void RequestExit()
        {
            if (!this.m_IsVehicleEnabled || this.m_BikeEntry == null ||
                this.m_BikeEntry.IsTransitioning)
            {
                return;
            }

            Character character = this.m_BikeEntry.SeatedCharacter;
            if (character != null) this.m_BikeEntry.RequestExit(character);
        }

        public void ResetVehicle()
        {
            if (!this.ResolveReferences()) return;

            this.m_Rigidbody.linearVelocity = Vector3.zero;
            this.m_Rigidbody.angularVelocity = Vector3.zero;
            this.m_Rigidbody.position += Vector3.up * 0.5f;

            Transform rotator = this.m_Controller.bikeReferences.Rotator;
            if (rotator != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(rotator.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                rotator.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            if (this.m_Controller.bikeReferences.WheelieTransform != null)
                this.m_Controller.bikeReferences.WheelieTransform.localRotation = Quaternion.identity;
            if (this.m_Controller.bikeReferences.LeanTransform != null)
                this.m_Controller.bikeReferences.LeanTransform.localRotation = Quaternion.identity;
        }

        private bool ResolveReferences()
        {
            if (this.m_Controller == null) this.m_Controller = this.GetComponent<ArcadeBikeControllerPro>();
            if (this.m_BikeEntry == null) this.m_BikeEntry = this.GetComponent<BikeEntry>();
            if (this.m_VehicleLights == null)
                this.m_VehicleLights = this.GetComponent<VehicleLights>();
            if (this.m_Fuel == null) this.m_Fuel = this.GetComponent<FranklinBikeFuel>();
            if (this.m_Rigidbody == null) this.m_Rigidbody = this.GetComponent<Rigidbody>();
            return this.m_Controller != null && this.m_Rigidbody != null;
        }

        private bool EnsureHornSource()
        {
            if (this.m_HornClip == null) return false;
            if (this.m_HornSource == null)
                this.m_HornSource = this.gameObject.AddComponent<AudioSource>();

            this.m_HornSource.clip = this.m_HornClip;
            this.m_HornSource.playOnAwake = false;
            this.m_HornSource.loop = true;
            this.m_HornSource.spatialBlend = 1f;
            this.m_HornSource.dopplerLevel = 0.2f;
            this.m_HornSource.minDistance = 3f;
            this.m_HornSource.maxDistance = 45f;
            this.m_HornSource.rolloffMode = AudioRolloffMode.Logarithmic;
            this.m_HornSource.priority = 96;
            this.m_HornSource.pitch = this.m_HornPitch;
            return true;
        }

        private void UpdateHorn()
        {
            if (!this.m_HornNeedsUpdate || this.m_HornSource == null) return;
            if (!this.m_IsVehicleEnabled || this.m_IsDamageLocked)
                this.m_HornPressed = false;

            if (!this.m_HornSource.isPlaying)
            {
                if (!this.m_HornPressed || !this.EnsureHornSource()) return;
                this.m_HornSource.volume = 0f;
                this.m_HornSource.Play();
            }

            float targetVolume = this.m_HornPressed ? this.m_HornMaxVolume : 0f;
            float fadeSpeed = this.m_HornPressed
                ? this.m_HornFadeInSpeed
                : this.m_HornFadeOutSpeed;
            this.m_HornSource.volume = Mathf.MoveTowards(
                this.m_HornSource.volume,
                targetVolume,
                fadeSpeed * Time.unscaledDeltaTime
            );

            if (!this.m_HornPressed && this.m_HornSource.volume <= 0.001f)
            {
                this.StopHornImmediately();
                return;
            }

            if (this.m_HornPressed &&
                Mathf.Abs(this.m_HornSource.volume - this.m_HornMaxVolume) <= 0.001f)
            {
                // The AudioSource loops natively; no C# polling is required while
                // the held horn is already at its target volume.
                this.m_HornNeedsUpdate = false;
            }
        }

        private void StopHornImmediately()
        {
            this.m_HornPressed = false;
            this.m_HornNeedsUpdate = false;
            if (this.m_HornSource == null) return;
            this.m_HornSource.Stop();
            this.m_HornSource.volume = 0f;
        }

        private void CaptureDrivingPhysicsSettings()
        {
            if (this.m_HasCapturedDrivingPhysics || this.m_Rigidbody == null) return;

            this.m_DrivingConstraints = this.m_Rigidbody.constraints |
                                        RigidbodyConstraints.FreezeRotation;
            this.m_DrivingUseGravity = this.m_Rigidbody.useGravity;
            this.m_DrivingLinearDamping = this.m_Rigidbody.linearDamping;
            this.m_DrivingAngularDamping = this.m_Rigidbody.angularDamping;
            this.m_DrivingSolverIterations = this.m_Rigidbody.solverIterations;
            this.m_DrivingSolverVelocityIterations =
                this.m_Rigidbody.solverVelocityIterations;
            this.m_DrivingCollisionDetectionMode =
                this.m_Rigidbody.collisionDetectionMode;
            this.m_DrivingInterpolation = this.m_Rigidbody.interpolation;
            this.m_DrivingMaxDepenetrationVelocity =
                this.m_Rigidbody.maxDepenetrationVelocity;
            this.m_DrivingAutomaticCenterOfMass =
                this.m_Rigidbody.automaticCenterOfMass;
            this.m_DrivingCenterOfMass = this.m_Rigidbody.centerOfMass;
            this.m_HasCapturedDrivingPhysics = true;
        }

        private void RestoreDrivingPhysicsSettings()
        {
            if (this.m_Rigidbody == null) return;
            this.CaptureDrivingPhysicsSettings();
            this.m_Rigidbody.constraints = this.m_DrivingConstraints;
            this.m_Rigidbody.useGravity = this.m_DrivingUseGravity;
            this.m_Rigidbody.linearDamping = this.m_DrivingLinearDamping;
            this.m_Rigidbody.angularDamping = this.m_DrivingAngularDamping;
            this.m_Rigidbody.solverIterations = this.m_DrivingSolverIterations;
            this.m_Rigidbody.solverVelocityIterations =
                this.m_DrivingSolverVelocityIterations;
            this.m_Rigidbody.collisionDetectionMode =
                this.m_DrivingCollisionDetectionMode;
            this.m_Rigidbody.interpolation = this.m_DrivingInterpolation;
            this.m_Rigidbody.maxDepenetrationVelocity =
                this.m_DrivingMaxDepenetrationVelocity;
            if (this.m_DrivingAutomaticCenterOfMass)
            {
                this.m_Rigidbody.ResetCenterOfMass();
            }
            else
            {
                this.m_Rigidbody.centerOfMass = this.m_DrivingCenterOfMass;
            }
        }

        private void ApplyVehicleState()
        {
            if (this.m_Controller == null) return;

            this.m_Controller.enabled = this.m_IsVehicleEnabled;
            this.m_Controller.canAccelerate = this.m_IsVehicleEnabled &&
                                              !this.m_IsDamageLocked &&
                                              this.m_HasFuel;
            this.m_Controller.canTurn = this.m_IsVehicleEnabled;

            // A normal empty bike is parked kinematically so GC2 character pushes
            // cannot cause drift. An ABP ragdoll remains dynamic with gravity and
            // free rotation for its entire lifetime, including while asleep.
            if (this.m_Rigidbody != null)
            {
                if (!this.m_IsVehicleEnabled)
                {
                    if (this.m_KeepDynamicWhenDisabled)
                    {
                        this.m_Rigidbody.isKinematic = false;
                        this.m_Rigidbody.constraints = this.m_DrivingConstraints &
                                                      ~RigidbodyConstraints.FreezeRotation;
                        this.m_Rigidbody.useGravity = true;
                        this.m_Rigidbody.ResetCenterOfMass();
                        this.m_Rigidbody.linearDamping = Mathf.Max(
                            this.m_DrivingLinearDamping,
                            2.25f
                        );
                        this.m_Rigidbody.angularDamping = Mathf.Max(
                            this.m_DrivingAngularDamping,
                            1.1f
                        );
                    }
                    else
                    {
                        if (!this.m_Rigidbody.isKinematic)
                        {
                            this.m_Rigidbody.linearVelocity = Vector3.zero;
                            this.m_Rigidbody.angularVelocity = Vector3.zero;
                        }
                        this.RestoreDrivingPhysicsSettings();
                        this.m_Rigidbody.isKinematic = true;
                    }
                }
                else
                {
                    this.RestoreDrivingPhysicsSettings();
                    this.m_Rigidbody.isKinematic = false;
                }
            }

            AudioSource engine = this.m_Controller.bikeAudio?.engineSound;
            AudioSource skid = this.m_Controller.bikeAudio?.SkidSound;
            if (this.m_IsVehicleEnabled && this.m_HasFuel)
            {
                if (engine != null)
                {
                    engine.mute = false;
                    if (!engine.isPlaying && engine.clip != null) engine.Play();
                }
                if (skid != null && !skid.isPlaying && skid.clip != null) skid.Play();
            }
            else
            {
                if (skid != null) skid.Stop();
                if (this.m_IsCrashEngineRunning && this.m_HasFuel)
                {
                    this.EnsureCrashEngineAudioPlaying(engine);
                }
                else if (engine != null)
                {
                    engine.Stop();
                }
            }

            this.SetBrakeReverseEffectsRuntimeActive(
                this.m_IsVehicleEnabled && !this.m_IsDamageLocked
            );
            this.ApplyMobileFrameRateBudget(this.m_IsVehicleEnabled);
        }

        private void EnsureCrashEngineAudioPlaying(AudioSource engine)
        {
            if (engine == null || engine.clip == null) return;
            engine.mute = false;
            engine.loop = true;
            if (!engine.isPlaying) engine.Play();
        }

        private void UpdateCrashEngineAudio()
        {
            if (!this.m_IsCrashEngineRunning || !this.m_HasFuel ||
                this.m_Controller == null)
            {
                return;
            }

            if (this.m_CrashEngineStopAt > 0f &&
                Time.unscaledTime >= this.m_CrashEngineStopAt)
            {
                this.StopCrashEngineImmediately();
                return;
            }

            AudioSource engine = this.m_Controller.bikeAudio?.engineSound;
            if (engine == null) return;
            this.EnsureCrashEngineAudioPlaying(engine);

            float deltaTime = Time.unscaledDeltaTime;
            engine.volume = Mathf.MoveTowards(
                engine.volume,
                this.m_CrashIdleEngineVolume,
                deltaTime * 0.8f
            );
            engine.pitch = Mathf.MoveTowards(
                engine.pitch,
                this.m_CrashIdleEnginePitch,
                deltaTime * 1.2f
            );
        }

        private void OnValidate()
        {
            this.m_SlowThrottle = Mathf.Clamp(this.m_SlowThrottle, 0.1f, 1f);
            this.m_SlowSpeedLimitKph = Mathf.Max(1f, this.m_SlowSpeedLimitKph);
            this.m_SlowSpeedDeceleration = Mathf.Max(
                0.1f,
                this.m_SlowSpeedDeceleration
            );
            this.m_ExitStopDeceleration = Mathf.Max(0.1f, this.m_ExitStopDeceleration);
            this.m_ExitStopAngularDeceleration = Mathf.Max(
                0.1f,
                this.m_ExitStopAngularDeceleration
            );
            this.m_CrashIdleEngineVolume = Mathf.Clamp01(
                this.m_CrashIdleEngineVolume
            );
            this.m_CrashIdleEnginePitch = Mathf.Clamp(
                this.m_CrashIdleEnginePitch,
                0.1f,
                3f
            );
            this.m_CrashIdleEngineTimeout = Mathf.Clamp(
                this.m_CrashIdleEngineTimeout,
                2f,
                30f
            );
            this.m_HornMaxVolume = Mathf.Clamp01(this.m_HornMaxVolume);
            this.m_HornPitch = Mathf.Clamp(this.m_HornPitch, 0.5f, 2f);
            this.m_HornFadeInSpeed = Mathf.Max(0.01f, this.m_HornFadeInSpeed);
            this.m_HornFadeOutSpeed = Mathf.Max(0.01f, this.m_HornFadeOutSpeed);
            this.m_MobileMaximumFrameRate = Mathf.Clamp(
                this.m_MobileMaximumFrameRate,
                30,
                60
            );
            if (this.m_HornSource != null && this.m_HornClip != null)
                this.EnsureHornSource();
        }

        private void ProvideInput(
            float accelerate,
            float reverse,
            float handbrake,
            float steerLeft,
            float steerRight,
            float wheelie)
        {
            this.m_CurrentThrottle = Mathf.Clamp01(accelerate);
            this.m_Controller?.provideInput(
                accelerate,
                reverse,
                handbrake,
                steerLeft,
                steerRight,
                wheelie
            );
        }

        private void ResetVirtualInputs()
        {
            this.RestoreThirdPersonViewPreservingPreference();
            this.SetStuntWeaponSuppression(false);
            this.SetHornPressed(false);
            this.m_VirtualAccelerate = false;
            this.m_VirtualSlowAccelerate = false;
            this.m_VirtualBrakeReverse = false;
            this.m_VirtualSteerLeft = false;
            this.m_VirtualSteerRight = false;
            this.m_VirtualHandbrake = false;
            this.m_VirtualWheelie = false;
            this.m_VirtualBurnout = false;
        }

        private void CacheRuntimeEffects()
        {
            if (this.m_BrakeReverseFlares != null) return;
            this.m_BrakeReverseFlares = this.GetComponentsInChildren<
                FranklinBikeBrakeReverseFlare
            >(true);
        }

        private void SetBrakeReverseEffectsRuntimeActive(bool active)
        {
            this.CacheRuntimeEffects();
            if (this.m_BrakeReverseFlares == null) return;

            for (int i = 0; i < this.m_BrakeReverseFlares.Length; ++i)
            {
                FranklinBikeBrakeReverseFlare flare = this.m_BrakeReverseFlares[i];
                if (flare != null) flare.SetRuntimeActive(active);
            }
        }

        private void ApplyMobileFrameRateBudget(bool active)
        {
            if (!Application.isMobilePlatform) return;

            int cap = Mathf.Clamp(this.m_MobileMaximumFrameRate, 30, 60);
            if (active)
            {
                if (this.m_HasAppliedMobileFrameRateCap ||
                    Application.targetFrameRate <= cap)
                {
                    return;
                }

                this.m_PreviousMobileTargetFrameRate = Application.targetFrameRate;
                Application.targetFrameRate = cap;
                this.m_HasAppliedMobileFrameRateCap = true;
                return;
            }

            if (!this.m_HasAppliedMobileFrameRateCap) return;
            if (Application.targetFrameRate == cap)
                Application.targetFrameRate = this.m_PreviousMobileTargetFrameRate;
            this.m_HasAppliedMobileFrameRateCap = false;
        }

        private static bool GetFirstPersonPreference()
        {
            if (s_FirstPersonPreferenceLoaded) return s_FirstPersonPreferred;

            s_FirstPersonPreferred = PlayerPrefs.GetInt(
                FIRST_PERSON_PREFERENCE_KEY,
                0
            ) != 0;
            s_FirstPersonPreferenceLoaded = true;
            return s_FirstPersonPreferred;
        }

        private static void SetFirstPersonPreference(bool active)
        {
            s_FirstPersonPreferred = active;
            s_FirstPersonPreferenceLoaded = true;
            PlayerPrefs.SetInt(FIRST_PERSON_PREFERENCE_KEY, active ? 1 : 0);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFirstPersonPreferenceCache()
        {
            s_FirstPersonPreferenceLoaded = false;
            s_FirstPersonPreferred = false;
        }

        private void SetStuntWeaponSuppression(bool active)
        {
            if (this.m_StuntWeaponSuppressionActive == active) return;

            Character rider = active
                ? this.m_BikeEntry?.SeatedCharacter
                : this.m_StuntWeaponRider ?? this.m_BikeEntry?.SeatedCharacter;
            if (active && rider == null) return;

            this.m_StuntWeaponSuppressionActive = active;
            if (active) this.m_StuntWeaponRider = rider;
            FranklinShooterSystem.SetBikeStuntWeaponSuppressed(rider, active);
            if (!active) this.m_StuntWeaponRider = null;
        }
    }
}
