using System.Collections.Generic;
using FranklinGame.Animations;
using FranklinGame.UI;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Semi-physical rotorcraft motor for the AllStar Gyrocopter visual. Gravity,
    /// lift, aerodynamic drag and attitude are all solved in FixedUpdate through
    /// forces/torques. The component also implements Franklin's vehicle input
    /// contract so the existing GC2 enter/exit flow remains authoritative.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(CarEntry))]
    public sealed class HelicopterFlightController : MonoBehaviour,
        IRvrVehicleInputController,
        IRvrVehicleOccupantCollisionPolicy,
        IAirHudActionHandler
    {
        private static readonly List<HelicopterFlightController> INSTANCES = new(2);

        [Header("References")]
        [SerializeField] private Rigidbody m_Body;
        [SerializeField] private CarEntry m_Entry;
        [SerializeField] private ShotCamera m_CameraShot;
        [SerializeField] private Transform m_LiftPoint;
        [SerializeField] private Transform m_MainRotor;
        [SerializeField] private Transform m_TailRotor;
        [SerializeField] private AudioSource m_RotorAudio;
        [SerializeField] private DroneMobileHud m_HudPrefab;
        [SerializeField] private bool m_ShowTouchHudInEditor = true;

        [Header("Rotor and Lift")]
        [SerializeField, Min(100f)] private float m_MainRotorRpm = 360f;
        [Tooltip("Visual RPM kept away from common 30/60 FPS harmonics to avoid tail-rotor strobing on mobile.")]
        [SerializeField, Min(500f)] private float m_TailRotorRpm = 850f;
        [SerializeField, Min(0.1f)] private float m_SpoolUpSeconds = 5.5f;
        [SerializeField, Min(0.1f)] private float m_SpoolDownSeconds = 4f;
        [SerializeField, Range(0.2f, 0.9f)] private float m_HoverCollective = 0.55f;
        [SerializeField, Min(1f)] private float m_MaxLiftMultiple = 1.85f;
        [SerializeField, Range(0f, 0.4f)] private float m_GroundEffectStrength = 0.18f;
        [SerializeField, Min(0.5f)] private float m_GroundEffectHeight = 4.5f;

        [Header("Flight Envelope")]
        [SerializeField, Min(5f)] private float m_MaxHorizontalSpeed = 28f;
        [SerializeField, Min(1f)] private float m_MaxClimbSpeed = 6f;
        [SerializeField, Min(1f)] private float m_MaxDescentSpeed = 5f;
        [SerializeField, Range(5f, 40f)] private float m_MaxPitch = 21f;
        [SerializeField, Range(5f, 40f)] private float m_MaxRoll = 23f;
        [SerializeField, Min(10f)] private float m_YawRate = 58f;
        [SerializeField, Min(0.1f)] private float m_AttitudeResponse = 5.2f;
        [SerializeField, Min(0.1f)] private float m_AttitudeDamping = 7.5f;
        [SerializeField, Min(0.1f)] private float m_MaxAngularAcceleration = 28f;
        [SerializeField, Min(0f)] private float m_PlanarDrag = 0.18f;
        [SerializeField, Min(0f)] private float m_VerticalDrag = 0.1f;

        [Header("Landing")]
        [SerializeField, Min(0.1f)] private float m_LandingClearance = 0.8f;
        [SerializeField, Min(0.5f)] private float m_AutoLandSpeed = 4f;
        [Tooltip("Continuous grounded time while the Air HUD Down button is held before requesting the normal GC2 exit flow.")]
        [SerializeField, Min(0.5f)] private float m_DescendGroundExitSeconds = 3f;
        [SerializeField] private LayerMask m_GroundMask =
            ~((1 << 2) | (1 << 3) | (1 << 11) | (1 << 14));

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float m_IdleVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float m_FlightVolume = 0.72f;
        [SerializeField, Range(0.1f, 3f)] private float m_IdlePitch = 0.72f;
        [SerializeField, Range(0.1f, 3f)] private float m_FlightPitch = 1.18f;

        [Header("Interaction")]
        [SerializeField, Min(1f)] private float m_InteractionDistance = 3.75f;

        private DroneMobileHud m_Hud;
        private Vector2 m_CyclicInput;
        private float m_CollectiveInput;
        private float m_YawInput;
        private float m_RotorSpeed01;
        private float m_TargetYaw;
        private bool m_IsVehicleEnabled;
        private bool m_EngineRunning;
        private bool m_IsAutoLanding;
        private bool m_ExitRequested;
        private bool m_PreserveMomentumWhenDisabled;
        private bool m_HudSuppressed;
        private bool m_ShowTouchControls;
        private bool m_VirtualAscend;
        private bool m_VirtualForward;
        private bool m_VirtualDescend;
        private bool m_VirtualYawLeft;
        private bool m_VirtualYawRight;
        private bool m_VirtualBrake;
        private bool m_HudDescendHeld;
        private float m_DescendGroundedSeconds;
        private Vector3 m_CameraForward;
        private WheelCollider[] m_LandingWheels;
        private readonly List<Collider> m_VehicleColliders = new(8);
        private readonly List<Collider> m_OccupantColliders = new(16);
        private Character m_CollisionIgnoredOccupant;

        public static IReadOnlyList<HelicopterFlightController> Instances => INSTANCES;

        public bool IsVehicleEnabled => this.m_IsVehicleEnabled;
        public bool UseSeatEntryAlignment => true;
        public Transform VehicleBody => this.transform;
        public float SpeedMetersPerSecond => this.m_Body != null
            ? this.m_Body.linearVelocity.magnitude
            : 0f;
        public float InteractionDistance => this.m_InteractionDistance;
        public CarEntry Entry => this.m_Entry;
        public ShotCamera CameraShot => this.m_CameraShot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            INSTANCES.Clear();
        }

        private void Awake()
        {
            this.ResolveReferences();
            this.CacheVehicleColliders();
            this.m_LandingWheels = this.GetComponentsInChildren<WheelCollider>(true);
            if (this.m_Body != null)
            {
                this.m_Body.centerOfMass = new Vector3(0f, 0.72f, -0.42f);
                this.m_Body.maxAngularVelocity = 6f;
            }

            if (this.m_CameraShot != null) this.m_CameraShot.enabled = false;
            this.ConfigureParkedState(true);

            this.m_ShowTouchControls = Application.isMobilePlatform;
#if UNITY_EDITOR
            this.m_ShowTouchControls |= this.m_ShowTouchHudInEditor;
#endif
        }

        private void OnEnable()
        {
            if (!INSTANCES.Contains(this)) INSTANCES.Add(this);
        }

        private void OnDisable()
        {
            INSTANCES.Remove(this);
            this.ResolveReferences();
            bool preserveMomentum = this.m_PreserveMomentumWhenDisabled;
            if (this.m_Entry?.ReleaseDriverForUnavailableVehicle() == true)
                preserveMomentum = false;
            this.ReleasePresentation();
            this.ClearInput();
            this.m_IsVehicleEnabled = false;
            this.m_EngineRunning = false;
            this.m_IsAutoLanding = false;
            this.m_ExitRequested = false;
            this.m_PreserveMomentumWhenDisabled = preserveMomentum;
            this.m_RotorSpeed01 = 0f;
            if (this.m_RotorAudio != null)
            {
                this.m_RotorAudio.Stop();
                this.m_RotorAudio.volume = 0f;
            }
            if (preserveMomentum) this.ConfigureUncontrolledState();
            else this.ConfigureParkedState(true);
            this.EndOccupantCollisionIgnore(null);
        }

        public void BeginOccupantCollisionIgnore(Character character)
        {
            if (character == null || character == this.m_CollisionIgnoredOccupant)
                return;

            this.EndOccupantCollisionIgnore(null);
            this.CacheVehicleColliders();
            this.m_OccupantColliders.Clear();
            character.GetComponentsInChildren(true, this.m_OccupantColliders);
            this.m_CollisionIgnoredOccupant = character;

            for (int occupantIndex = 0;
                 occupantIndex < this.m_OccupantColliders.Count;
                 ++occupantIndex)
            {
                Collider occupantCollider = this.m_OccupantColliders[occupantIndex];
                if (occupantCollider == null) continue;
                for (int vehicleIndex = 0;
                     vehicleIndex < this.m_VehicleColliders.Count;
                     ++vehicleIndex)
                {
                    Collider vehicleCollider = this.m_VehicleColliders[vehicleIndex];
                    if (vehicleCollider == null || vehicleCollider == occupantCollider)
                        continue;
                    Physics.IgnoreCollision(occupantCollider, vehicleCollider, true);
                }
            }
        }

        public void EndOccupantCollisionIgnore(Character character)
        {
            if (this.m_CollisionIgnoredOccupant == null)
            {
                this.m_OccupantColliders.Clear();
                return;
            }
            if (character != null && character != this.m_CollisionIgnoredOccupant)
                return;

            for (int occupantIndex = 0;
                 occupantIndex < this.m_OccupantColliders.Count;
                 ++occupantIndex)
            {
                Collider occupantCollider = this.m_OccupantColliders[occupantIndex];
                if (occupantCollider == null) continue;
                for (int vehicleIndex = 0;
                     vehicleIndex < this.m_VehicleColliders.Count;
                     ++vehicleIndex)
                {
                    Collider vehicleCollider = this.m_VehicleColliders[vehicleIndex];
                    if (vehicleCollider == null || vehicleCollider == occupantCollider)
                        continue;
                    Physics.IgnoreCollision(occupantCollider, vehicleCollider, false);
                }
            }

            this.m_CollisionIgnoredOccupant = null;
            this.m_OccupantColliders.Clear();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) this.ClearTransientInput();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) this.ClearTransientInput();
        }

        private void OnValidate()
        {
            this.ResolveReferences();
            this.m_MainRotorRpm = Mathf.Max(100f, this.m_MainRotorRpm);
            this.m_TailRotorRpm = Mathf.Max(500f, this.m_TailRotorRpm);
            this.m_SpoolUpSeconds = Mathf.Max(0.1f, this.m_SpoolUpSeconds);
            this.m_SpoolDownSeconds = Mathf.Max(0.1f, this.m_SpoolDownSeconds);
            this.m_MaxHorizontalSpeed = Mathf.Max(5f, this.m_MaxHorizontalSpeed);
            this.m_MaxClimbSpeed = Mathf.Max(1f, this.m_MaxClimbSpeed);
            this.m_MaxDescentSpeed = Mathf.Max(1f, this.m_MaxDescentSpeed);
            this.m_DescendGroundExitSeconds = Mathf.Max(
                0.5f,
                this.m_DescendGroundExitSeconds
            );
            this.m_InteractionDistance = Mathf.Max(1f, this.m_InteractionDistance);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            this.UpdateRotorAndAudio(deltaTime);
            if (!this.m_IsVehicleEnabled)
            {
                // CarEntry briefly restores its generic car Rigidbody state at
                // the end of the exit gesture. Reassert the rotorcraft's parked
                // contract locally without changing shared vehicle code.
                if (!this.m_PreserveMomentumWhenDisabled &&
                    this.m_Body != null && !this.m_Body.isKinematic)
                    this.ConfigureParkedState(true);
                return;
            }

            if (this.m_ExitRequested)
            {
                if (WasExitPressed())
                {
                    this.RequestExit();
                    return;
                }
                this.TryBeginPendingExit();
                return;
            }
            if (this.m_Entry != null && this.m_Entry.IsTransitioning) return;

            if (WasExitPressed())
            {
                this.RequestExit();
                return;
            }

            this.ReadFlightInput();
        }

        private void FixedUpdate()
        {
            if (!this.m_IsVehicleEnabled || this.m_Body == null ||
                this.m_Body.isKinematic)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            float altitude = this.ReadGroundDistance();
            Vector2 cyclic = this.m_IsAutoLanding ? Vector2.zero : this.m_CyclicInput;
            float yaw = this.m_IsAutoLanding ? 0f : this.m_YawInput;
            float collective = this.ResolveCollective(altitude);

            this.ApplyLift(collective, altitude);
            this.ApplyAttitude(cyclic, yaw, deltaTime);
            this.ApplyAerodynamicDrag();
            this.ApplyFlightEnvelope();
            if (this.m_IsAutoLanding) this.TryCompleteAutoLanding(altitude);
            this.UpdateDescendGroundExit(deltaTime, altitude);
        }

        private void LateUpdate()
        {
            if (this.m_RotorSpeed01 <= 0.001f) return;

            float mainDegrees = this.m_MainRotorRpm * 6f *
                                this.m_RotorSpeed01 * Time.deltaTime;
            float tailDegrees = this.m_TailRotorRpm * 6f *
                                this.m_RotorSpeed01 * Time.deltaTime;
            if (this.m_MainRotor != null)
                this.m_MainRotor.Rotate(0f, 0f, -mainDegrees, Space.Self);
            if (this.m_TailRotor != null)
                this.m_TailRotor.Rotate(0f, 0f, -tailDegrees, Space.Self);
        }

        public void SetVehicleEnabled(bool state)
        {
            this.SetVehicleEnabled(state, false);
        }

        public void SetVehicleEnabled(bool state, bool preserveMomentum)
        {
            this.ResolveReferences();
            if (state)
            {
                this.m_IsVehicleEnabled = true;
                this.m_EngineRunning = true;
                this.m_IsAutoLanding = false;
                this.m_ExitRequested = false;
                this.m_PreserveMomentumWhenDisabled = false;
                this.m_TargetYaw = this.transform.eulerAngles.y;
                this.ConfigureActiveState();
                this.AcquirePresentation();
                return;
            }

            this.m_IsVehicleEnabled = false;
            this.m_EngineRunning = false;
            this.m_IsAutoLanding = false;
            this.m_ExitRequested = false;
            this.m_PreserveMomentumWhenDisabled = preserveMomentum;
            this.ClearInput();
            this.ReleasePresentation();

            if (preserveMomentum) this.ConfigureUncontrolledState();
            else this.ConfigureParkedState(true);
        }

        public void BeginExitStop()
        {
            if (!this.m_IsVehicleEnabled) return;
            this.m_IsAutoLanding = true;
            this.ClearInput();
        }

        public void CancelExitStop()
        {
            this.m_IsAutoLanding = false;
            this.m_ExitRequested = false;
            if (this.m_IsVehicleEnabled && this.m_Body != null &&
                this.m_Body.isKinematic)
            {
                this.ConfigureActiveState();
            }
        }

        public void SetHandbrakeInput(bool active)
        {
            this.m_VirtualBrake = active;
            if (active)
            {
                this.m_IsAutoLanding = true;
            }
            else if (!this.m_ExitRequested)
            {
                this.m_IsAutoLanding = false;
                if (this.m_IsVehicleEnabled && this.m_Body != null &&
                    this.m_Body.isKinematic)
                {
                    this.ConfigureActiveState();
                }
            }
        }

        public void ResetVehicle()
        {
            if (this.m_Body == null) return;
            float yaw = this.m_Body.rotation.eulerAngles.y;
            this.m_Body.position += Vector3.up * 0.35f;
            this.m_Body.rotation = Quaternion.Euler(0f, yaw, 0f);
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            this.m_TargetYaw = yaw;
        }

        public void SetVirtualAccelerateInput(bool active)
        {
            this.m_VirtualAscend = active;
        }

        public void SetVirtualSlowAccelerateInput(bool active)
        {
            this.m_VirtualForward = active;
        }

        public void SetVirtualBrakeReverseInput(bool active)
        {
            this.m_VirtualDescend = active;
        }

        public void SetVirtualSteerLeftInput(bool active)
        {
            this.m_VirtualYawLeft = active;
        }

        public void SetVirtualSteerRightInput(bool active)
        {
            this.m_VirtualYawRight = active;
        }

        public void SetVirtualHandbrakeInput(bool active)
        {
            this.SetHandbrakeInput(active);
        }

        public void RequestExit()
        {
            if (this.m_ExitRequested)
            {
                this.m_ExitRequested = false;
                this.m_IsAutoLanding = false;
                if (this.m_IsVehicleEnabled && this.m_Body != null &&
                    this.m_Body.isKinematic)
                {
                    this.ConfigureActiveState();
                }
                return;
            }

            Character character = this.m_Entry != null
                ? this.m_Entry.SeatedCharacter
                : null;
            if (character == null || this.m_Entry.IsTransitioning ||
                !this.m_IsVehicleEnabled)
            {
                return;
            }

            // A hovering craft can have almost zero velocity while still being
            // metres above the ground. Do not let the generic CarEntry speed
            // check interpret that as a safe stopped exit: land first, then
            // hand control back to its GC2 animation sequence.
            this.m_ExitRequested = true;
            this.m_IsAutoLanding = true;
            this.ClearInput();
        }

        public void HandleHudAction(DroneHudAction action)
        {
            if (action == DroneHudAction.Exit) this.RequestExit();
        }

        private void ResolveReferences()
        {
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
            if (this.m_Entry == null) this.m_Entry = this.GetComponent<CarEntry>();
        }

        private void CacheVehicleColliders()
        {
            if (this.m_VehicleColliders.Count > 0) return;
            this.GetComponentsInChildren(true, this.m_VehicleColliders);
        }

        private void ConfigureActiveState()
        {
            if (this.m_Body == null) return;
            this.m_Body.isKinematic = false;
            this.m_Body.useGravity = true;
            this.m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            this.m_Body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            this.m_Body.constraints = RigidbodyConstraints.None;
            this.m_Body.WakeUp();
        }

        private void ConfigureUncontrolledState()
        {
            if (this.m_Body == null) return;
            this.m_Body.useGravity = true;
            this.m_Body.isKinematic = false;
            this.m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            this.m_Body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            this.m_Body.constraints = RigidbodyConstraints.None;
            this.m_Body.WakeUp();
        }

        private void ConfigureParkedState(bool clearMomentum)
        {
            if (this.m_Body == null) return;
            if (clearMomentum)
            {
                // Unity 6 rejects velocity writes while a Rigidbody is already
                // kinematic. CarEntry can set that flag before handing the
                // parked state back to this controller, so clear momentum in a
                // same-frame dynamic window and immediately restore kinematic.
                if (this.m_Body.isKinematic)
                    this.m_Body.isKinematic = false;
                this.m_Body.linearVelocity = Vector3.zero;
                this.m_Body.angularVelocity = Vector3.zero;
            }
            this.m_Body.useGravity = false;
            this.m_Body.isKinematic = true;
            this.m_Body.interpolation = RigidbodyInterpolation.None;
            this.m_Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            this.m_Body.Sleep();
        }

        private void AcquirePresentation()
        {
            if (!this.m_HudSuppressed)
            {
                FranklinMobileHud.AcquireControlsSuppression(this);
                this.m_HudSuppressed = true;
            }
            this.EnsureHud();
            if (this.m_Hud != null) this.m_Hud.SetState(false, true);
        }

        private void EnsureHud()
        {
            if (this.m_Hud != null || !this.m_ShowTouchControls ||
                this.m_HudPrefab == null)
            {
                return;
            }

            // A parked scenery aircraft allocates no Canvas hierarchy. The HUD
            // is created once on first use and then only enabled/disabled.
            this.m_Hud = DroneMobileHud.Create(
                this.m_HudPrefab,
                this,
                this.transform,
                true
            );
            if (this.m_Hud != null) this.m_Hud.SetState(false, false);
        }

        private void ReleasePresentation()
        {
            if (this.m_Hud != null) this.m_Hud.SetState(false, false);
            if (this.m_HudSuppressed)
            {
                FranklinMobileHud.ReleaseControlsSuppression(this);
                this.m_HudSuppressed = false;
            }
        }

        private void ReadFlightInput()
        {
            Vector2 cyclic = this.m_VirtualForward ? Vector2.up : Vector2.zero;
            float collective = (this.m_VirtualAscend ? 1f : 0f) -
                               (this.m_VirtualDescend ? 1f : 0f);
            float yaw = (this.m_VirtualYawRight ? 1f : 0f) -
                        (this.m_VirtualYawLeft ? 1f : 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                cyclic.x += ReadAxis(
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed
                );
                cyclic.y += ReadAxis(
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed
                );
                yaw += ReadAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
                collective += ReadAxis(
                    keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed,
                    keyboard.spaceKey.isPressed
                );
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                cyclic += gamepad.leftStick.ReadValue();
                yaw += ReadAxis(
                    gamepad.leftShoulder.isPressed,
                    gamepad.rightShoulder.isPressed
                );
                collective += gamepad.rightTrigger.ReadValue() -
                              gamepad.leftTrigger.ReadValue();
            }

            if (this.m_Hud != null)
            {
                cyclic += this.m_Hud.MoveInput;
                float hudLift = this.m_Hud.LiftInput;
                collective += hudLift;
                this.m_HudDescendHeld = hudLift < -0.1f;
            }
            else this.m_HudDescendHeld = false;

            this.m_CyclicInput = Vector2.ClampMagnitude(cyclic, 1f);
            this.m_CollectiveInput = Mathf.Clamp(collective, -1f, 1f);
            this.m_YawInput = Mathf.Clamp(yaw, -1f, 1f);
            Transform cameraTransform = ShortcutMainCamera.Transform;
            this.m_CameraForward = cameraTransform != null
                ? cameraTransform.forward
                : this.transform.forward;
        }

        private float ResolveCollective(float altitude)
        {
            if (this.m_IsAutoLanding)
            {
                float targetDescent = altitude < 0f
                    ? -this.m_AutoLandSpeed
                    : altitude > this.m_LandingClearance
                        ? -Mathf.Min(
                            this.m_AutoLandSpeed,
                            0.55f + altitude * 0.22f
                        )
                        : -0.25f;
                float verticalError = targetDescent - this.m_Body.linearVelocity.y;
                return Mathf.Clamp01(
                    this.m_HoverCollective + verticalError * 0.075f
                );
            }

            float range = this.m_CollectiveInput >= 0f
                ? 1f - this.m_HoverCollective
                : this.m_HoverCollective - 0.12f;
            return Mathf.Clamp01(
                this.m_HoverCollective + this.m_CollectiveInput * range
            );
        }

        private void ApplyLift(float collective, float altitude)
        {
            float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
            float rotorLift = this.m_RotorSpeed01 * this.m_RotorSpeed01;
            float lift = this.m_Body.mass * gravity *
                         (collective / Mathf.Max(0.01f, this.m_HoverCollective)) *
                         rotorLift;
            lift = Mathf.Min(lift, this.m_Body.mass * gravity * this.m_MaxLiftMultiple);

            if (altitude >= 0f && altitude < this.m_GroundEffectHeight)
            {
                float groundEffect = 1f - altitude / this.m_GroundEffectHeight;
                lift *= 1f + groundEffect * groundEffect *
                        this.m_GroundEffectStrength;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                this.m_Body.linearVelocity,
                Vector3.up
            );
            float translationalLift = Mathf.InverseLerp(
                3f,
                18f,
                horizontalVelocity.magnitude
            );
            lift *= 1f + translationalLift * 0.1f;

            Vector3 point = this.m_LiftPoint != null
                ? this.m_LiftPoint.position
                : this.m_Body.worldCenterOfMass + this.transform.up * 0.8f;
            this.m_Body.AddForceAtPosition(this.transform.up * lift, point);
        }

        private void ApplyAttitude(Vector2 cyclic, float yaw, float deltaTime)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(
                this.m_CameraForward,
                Vector3.up
            );
            if (cameraForward.sqrMagnitude < 0.0001f)
                cameraForward = Vector3.ProjectOnPlane(this.transform.forward, Vector3.up);
            if (cameraForward.sqrMagnitude < 0.0001f) cameraForward = Vector3.forward;
            else cameraForward.Normalize();
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;

            if (Mathf.Abs(yaw) > 0.01f)
            {
                this.m_TargetYaw = Mathf.Repeat(
                    this.m_TargetYaw + yaw * this.m_YawRate * deltaTime,
                    360f
                );
            }
            else if (cyclic.y > 0.1f)
            {
                float cameraYaw = Mathf.Atan2(cameraForward.x, cameraForward.z) *
                                  Mathf.Rad2Deg;
                this.m_TargetYaw = Mathf.MoveTowardsAngle(
                    this.m_TargetYaw,
                    cameraYaw,
                    this.m_YawRate * 0.75f * deltaTime
                );
            }

            Vector3 desiredWorld = cameraForward * cyclic.y + cameraRight * cyclic.x;
            Quaternion yawFrame = Quaternion.Euler(0f, this.m_TargetYaw, 0f);
            Vector3 localCommand = Quaternion.Inverse(yawFrame) * desiredWorld;
            Quaternion targetRotation = Quaternion.Euler(
                localCommand.z * this.m_MaxPitch,
                this.m_TargetYaw,
                -localCommand.x * this.m_MaxRoll
            );

            Quaternion error = targetRotation * Quaternion.Inverse(this.m_Body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude < 0.0001f) return;

            Vector3 desiredAngularVelocity = axis.normalized *
                                             (angle * Mathf.Deg2Rad *
                                              this.m_AttitudeResponse);
            Vector3 angularAcceleration =
                (desiredAngularVelocity - this.m_Body.angularVelocity) *
                this.m_AttitudeDamping;
            angularAcceleration = Vector3.ClampMagnitude(
                angularAcceleration,
                this.m_MaxAngularAcceleration
            );
            this.m_Body.AddTorque(angularAcceleration, ForceMode.Acceleration);
        }

        private void ApplyAerodynamicDrag()
        {
            Vector3 velocity = this.m_Body.linearVelocity;
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 vertical = Vector3.up * Vector3.Dot(velocity, Vector3.up);
            this.m_Body.AddForce(
                -planar * this.m_PlanarDrag - vertical * this.m_VerticalDrag,
                ForceMode.Acceleration
            );
        }

        private void ApplyFlightEnvelope()
        {
            Vector3 velocity = this.m_Body.linearVelocity;
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (planar.magnitude > this.m_MaxHorizontalSpeed)
            {
                Vector3 excess = planar - planar.normalized * this.m_MaxHorizontalSpeed;
                this.m_Body.AddForce(-excess * 2.5f, ForceMode.Acceleration);
            }

            if (velocity.y > this.m_MaxClimbSpeed)
            {
                this.m_Body.AddForce(
                    Vector3.down * (velocity.y - this.m_MaxClimbSpeed) * 3f,
                    ForceMode.Acceleration
                );
            }
            else if (velocity.y < -this.m_MaxDescentSpeed)
            {
                this.m_Body.AddForce(
                    Vector3.up * (-this.m_MaxDescentSpeed - velocity.y) * 3f,
                    ForceMode.Acceleration
                );
            }
        }

        private void TryCompleteAutoLanding(float altitude)
        {
            if (altitude < 0f ||
                altitude > this.m_LandingClearance + 0.25f)
            {
                return;
            }

            Vector3 velocity = this.m_Body.linearVelocity;
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float uprightAngle = Vector3.Angle(this.transform.up, Vector3.up);
            if (planar.sqrMagnitude > 0.36f || Mathf.Abs(velocity.y) > 1f ||
                uprightAngle > 14f)
            {
                return;
            }

            // Hand CarEntry a deterministic, fully stopped vehicle. It observes
            // zero speed on the next frame, performs the GC2 exit gesture and
            // then calls SetVehicleEnabled(false) through the shared contract.
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            this.m_Body.useGravity = false;
            this.m_Body.isKinematic = true;
        }

        private void UpdateDescendGroundExit(float deltaTime, float altitude)
        {
            if (!this.m_HudDescendHeld || this.m_IsAutoLanding ||
                this.m_ExitRequested || this.m_Entry == null ||
                this.m_Entry.IsTransitioning ||
                this.m_Entry.SeatedCharacter == null ||
                !this.IsLandingGearGrounded(altitude))
            {
                this.m_DescendGroundedSeconds = 0f;
                return;
            }

            this.m_DescendGroundedSeconds += deltaTime;
            if (this.m_DescendGroundedSeconds < this.m_DescendGroundExitSeconds)
                return;

            this.m_DescendGroundedSeconds = 0f;
            this.RequestExit();
        }

        private bool IsLandingGearGrounded(float altitude)
        {
            bool hasActiveWheel = false;
            if (this.m_LandingWheels != null)
            {
                for (int i = 0; i < this.m_LandingWheels.Length; ++i)
                {
                    WheelCollider wheel = this.m_LandingWheels[i];
                    if (wheel == null || !wheel.enabled ||
                        !wheel.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    hasActiveWheel = true;
                    if (wheel.isGrounded) return true;
                }
            }

            // Prefabs without active WheelColliders keep the feature through the
            // existing non-allocating landing ray instead of doing another cast.
            return !hasActiveWheel && altitude >= 0f &&
                   altitude <= this.m_LandingClearance + 0.25f;
        }

        private void TryBeginPendingExit()
        {
            if (!this.m_ExitRequested || this.m_Entry == null) return;
            Character character = this.m_Entry.SeatedCharacter;
            if (character == null)
            {
                this.m_ExitRequested = false;
                this.m_IsAutoLanding = false;
                return;
            }
            if (this.m_Entry.IsTransitioning || this.m_Body == null ||
                !this.m_Body.isKinematic)
            {
                return;
            }

            if (this.m_Entry.RequestExit(character))
                this.m_ExitRequested = false;
        }

        private float ReadGroundDistance()
        {
            Vector3 origin = this.m_Body.worldCenterOfMass + Vector3.up * 0.2f;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    1000f,
                    this.m_GroundMask,
                    QueryTriggerInteraction.Ignore
                ))
            {
                return Mathf.Max(0f, hit.distance - 0.2f);
            }
            return -1f;
        }

        private void UpdateRotorAndAudio(float deltaTime)
        {
            float target = this.m_EngineRunning ? 1f : 0f;
            float duration = this.m_EngineRunning
                ? this.m_SpoolUpSeconds
                : this.m_SpoolDownSeconds;
            this.m_RotorSpeed01 = Mathf.MoveTowards(
                this.m_RotorSpeed01,
                target,
                deltaTime / Mathf.Max(0.1f, duration)
            );

            if (this.m_RotorAudio == null) return;
            if (this.m_RotorSpeed01 > 0.001f)
            {
                if (!this.m_RotorAudio.isPlaying) this.m_RotorAudio.Play();
                float load = Mathf.Clamp01(
                    Mathf.Abs(this.m_CollectiveInput) * 0.45f +
                    this.m_CyclicInput.magnitude * 0.25f
                );
                this.m_RotorAudio.volume = Mathf.Lerp(
                    0f,
                    Mathf.Lerp(this.m_IdleVolume, this.m_FlightVolume, load),
                    this.m_RotorSpeed01
                );
                this.m_RotorAudio.pitch = Mathf.Lerp(
                    this.m_IdlePitch,
                    this.m_FlightPitch,
                    Mathf.Clamp01(this.m_RotorSpeed01 * 0.8f + load * 0.2f)
                );
            }
            else if (this.m_RotorAudio.isPlaying)
            {
                this.m_RotorAudio.Stop();
            }
        }

        private void ClearTransientInput()
        {
            if (this.m_Hud != null) this.m_Hud.ResetInput();
            this.ClearInput();
        }

        private void ClearInput()
        {
            this.m_CyclicInput = Vector2.zero;
            this.m_CollectiveInput = 0f;
            this.m_YawInput = 0f;
            this.m_VirtualAscend = false;
            this.m_VirtualForward = false;
            this.m_VirtualDescend = false;
            this.m_VirtualYawLeft = false;
            this.m_VirtualYawRight = false;
            this.m_VirtualBrake = false;
            this.m_HudDescendHeld = false;
            this.m_DescendGroundedSeconds = 0f;
        }

        private static bool WasExitPressed()
        {
            return Keyboard.current?.fKey.wasPressedThisFrame == true ||
                   Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                   Gamepad.current?.buttonEast.wasPressedThisFrame == true;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
