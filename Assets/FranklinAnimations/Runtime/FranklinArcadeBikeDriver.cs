using ArcadeBP_Pro;
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
        [SerializeField] private ArcadeBikeControllerPro m_Controller;
        [SerializeField] private BikeEntry m_BikeEntry;
        [SerializeField] private VehicleLights m_VehicleLights;
        [SerializeField, Range(0.1f, 1f)] private float m_SlowThrottle = 0.35f;
        [SerializeField] private bool m_ReadKeyboardInput = true;

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
        private bool m_ExternalHandbrake;
        private bool m_IsStoppingForExit;
        private bool m_KeepDynamicWhenDisabled;
        private RigidbodyConstraints m_DrivingConstraints;
        private bool m_DrivingUseGravity;
        private float m_DrivingLinearDamping;
        private float m_DrivingAngularDamping;
        private bool m_DrivingAutomaticCenterOfMass;
        private Vector3 m_DrivingCenterOfMass;
        private bool m_HasCapturedDrivingPhysics;
        private bool m_IsCrashEngineRunning;
        private bool m_IsDamageLocked;

        public bool IsVehicleEnabled => this.m_IsVehicleEnabled;
        public bool IsDamageLocked => this.m_IsDamageLocked;
        public bool IsAirborne => this.m_IsVehicleEnabled &&
                                  this.m_Controller != null &&
                                  !this.m_Controller.frontWheelIsGrounded &&
                                  !this.m_Controller.rearWheelIsGrounded;
        public bool IsCrashCoasting => !this.m_IsVehicleEnabled &&
                                       this.m_KeepDynamicWhenDisabled;
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

        private void Awake()
        {
            this.ResolveReferences();
            if (this.m_Rigidbody != null)
            {
                this.CaptureDrivingPhysicsSettings();
                this.RestoreDrivingPhysicsSettings();
            }
            this.m_VehicleLights?.FrontLightsOff();
        }

        private void Start()
        {
            this.ApplyVehicleState();
        }

        private void OnDisable()
        {
            this.ResetVirtualInputs();
            this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            this.m_VehicleLights?.FrontLightsOff();
        }

        private void Update()
        {
            if (!this.ResolveReferences()) return;

            if (!this.m_IsVehicleEnabled)
            {
                this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
                this.UpdateCrashEngineAudio();
                return;
            }

            if (this.m_IsDamageLocked)
            {
                // Keep suspension and the seated exit flow alive, but a bike at
                // zero GC2 health cannot receive any propulsion or steering.
                this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
                return;
            }

            float accelerate = this.m_VirtualBurnout
                ? 1f
                : this.m_VirtualAccelerate
                    ? 1f
                    : this.m_VirtualSlowAccelerate ? this.m_SlowThrottle : 0f;
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
                    this.RequestExit();
                    return;
                }
            }
#endif

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
            if (!this.m_IsStoppingForExit || !this.m_IsVehicleEnabled ||
                this.m_Rigidbody == null || this.m_Rigidbody.isKinematic)
            {
                return;
            }

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
                return;
            }

            FranklinArcadeBikeRagdoll bikeRagdoll =
                this.GetComponent<FranklinArcadeBikeRagdoll>();
            if (state)
            {
                this.m_IsCrashEngineRunning = false;
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
            this.ResetVirtualInputs();
            this.m_ExternalHandbrake = false;
            this.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            this.m_VehicleLights?.FrontLightsOff();
            this.ApplyVehicleState();

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
            this.m_VirtualSteerLeft = active;
        }

        public void SetVirtualSteerRightInput(bool active)
        {
            this.m_VirtualSteerRight = active;
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
            if (this.m_Rigidbody == null) this.m_Rigidbody = this.GetComponent<Rigidbody>();
            return this.m_Controller != null && this.m_Rigidbody != null;
        }

        private void CaptureDrivingPhysicsSettings()
        {
            if (this.m_HasCapturedDrivingPhysics || this.m_Rigidbody == null) return;

            this.m_DrivingConstraints = this.m_Rigidbody.constraints |
                                        RigidbodyConstraints.FreezeRotation;
            this.m_DrivingUseGravity = this.m_Rigidbody.useGravity;
            this.m_DrivingLinearDamping = this.m_Rigidbody.linearDamping;
            this.m_DrivingAngularDamping = this.m_Rigidbody.angularDamping;
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
            this.m_Controller.canAccelerate = this.m_IsVehicleEnabled;
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
            if (this.m_IsVehicleEnabled)
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
                if (this.m_IsCrashEngineRunning)
                {
                    this.EnsureCrashEngineAudioPlaying(engine);
                }
                else if (engine != null)
                {
                    engine.Stop();
                }
            }
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
            if (!this.m_IsCrashEngineRunning || this.m_Controller == null) return;

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
        }

        private void ProvideInput(
            float accelerate,
            float reverse,
            float handbrake,
            float steerLeft,
            float steerRight,
            float wheelie)
        {
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
            this.m_VirtualAccelerate = false;
            this.m_VirtualSlowAccelerate = false;
            this.m_VirtualBrakeReverse = false;
            this.m_VirtualSteerLeft = false;
            this.m_VirtualSteerRight = false;
            this.m_VirtualHandbrake = false;
            this.m_VirtualWheelie = false;
            this.m_VirtualBurnout = false;
        }
    }
}
