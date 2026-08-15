using System.Collections;
using Ashsvp;
using FranklinGame.Shooter;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using FranklinGame.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Feeds desktop, gamepad and the original Sim-Cade mobile buttons into the
    /// real Sim-Cade controller. It also owns the package's chase camera while
    /// this car is occupied. No RVR driving physics runs through this component.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SimcadeVehicleController))]
    public sealed class SimcadeCarDriver : MonoBehaviour, IRvrVehicleInputController
    {
        private const string FIRST_PERSON_PREFERENCE_KEY =
            "Franklin.Vehicle.Car.FirstPersonView";
        private static bool s_FirstPersonPreferenceLoaded;
        private static bool s_FirstPersonPreferred;

        [Header("Sim-Cade")]
        [SerializeField] private SimcadeVehicleController m_Controller;
        [SerializeField] private GearSystem m_GearSystem;
        [SerializeField] private AudioSystem m_AudioSystem;

        [Header("Input")]
        [SerializeField, Min(0f)] private float m_AccelerationResponse = 15f;
        [SerializeField, Min(0f)] private float m_SteeringResponse = 15f;
        [SerializeField, Min(0f)] private float m_SteeringReturnResponse = 25f;

        [Header("Crash Steering Damage")]
        [SerializeField, Range(-0.25f, 0.25f)] private float m_DamageSteeringBias;
        [SerializeField, Range(0.05f, 0.25f)] private float m_MaxDamageSteeringBias = 0.16f;

        [Header("Slow Drive")]
        [SerializeField, Min(1f)] private float m_SlowModeMaxSpeedKph = 60f;
        [SerializeField, Range(0.05f, 1f)] private float m_SlowThrottle = 0.35f;
        [SerializeField, Range(0f, 0.95f)] private float m_SlowThrottleTaperStart = 0.65f;
        [SerializeField, Min(0f)] private float m_ExitStopDeceleration = 14f;
        [SerializeField, Min(0f)] private float m_CoastingParkingSpeedKph = 2f;

        [Header("Steering Wheel Visual")]
        [SerializeField] private Transform m_SteeringWheel;
        [SerializeField, Min(0f)] private float m_SteeringWheelMaxAngle = 360f;
        [SerializeField, Min(0f)] private float m_SteeringWheelResponse = 15f;
        [Tooltip("Maximum visual hand turn. Kept below one full wheel turn so " +
                 "the palms remain held left/right while steering is held.")]
        [SerializeField, Range(15f, 140f)]
        private float m_SteeringHandsMaxAngle = 85f;

        [Header("Sim-Cade Camera")]
        [SerializeField] private GameObject m_ChaseCameraPrefab;
        [SerializeField] private Transform m_CameraTarget;
        [SerializeField] private bool m_AutoCenterCameraTarget = true;
        [SerializeField] private Vector3 m_CameraTargetAdditionalOffset = Vector3.zero;
        [SerializeField] private int m_CameraPriority = 1000;

        [Header("Camera Orbit")]
        [SerializeField] private bool m_EnableCameraOrbit = true;
        [SerializeField, Min(0f)] private float m_MouseOrbitSensitivity = 0.15f;
        [SerializeField, Min(0f)] private float m_TouchOrbitSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float m_GamepadOrbitSpeed = 120f;
        [SerializeField, Range(0f, 0.95f)] private float m_GamepadOrbitDeadZone = 0.15f;
        [SerializeField, Min(0f)] private float m_OrbitRecenterDelay;
        [SerializeField, Min(0.01f)] private float m_OrbitRecenterTime = 0.45f;

        [Header("Hold Rear View")]
        [SerializeField] private bool m_EnableHoldRearView = true;

        [Header("GC2 FPS Pivot")]
        [Tooltip("Optional runtime pivot container. All FPS camera tuning comes " +
                 "from the Player child ManagerCameraFPS.")]
        [SerializeField] private Transform m_FirstPersonCameraAnchor;

        [Header("Sim-Cade Mobile UI")]
        [SerializeField] private GameObject m_MobileInputPrefab;
        [SerializeField] private bool m_UseMobileInput = true;
        [SerializeField] private bool m_ShowMobileInputInEditor;

        [Header("Car Dashboard")]
        [SerializeField] private SimcadeCarDashboard m_Dashboard;

        [Header("Fuel")]
        [SerializeField] private SimcadeCarFuel m_Fuel;

        [Header("Lights")]
        [SerializeField] private VehicleLights m_VehicleLights;

        [Header("Horn")]
        [SerializeField] private SimcadeCarHorn m_Horn;

        private Rigidbody m_Rigidbody;
        private CarEntry m_CarEntry;
        private bool m_IsVehicleEnabled;
        private bool m_ExternalHandbrake;
        private bool m_VirtualAccelerate;
        private bool m_VirtualBrakeReverse;
        private bool m_VirtualSteerLeft;
        private bool m_VirtualSteerRight;
        private bool m_VirtualHandbrake;
        private bool m_VirtualSlowAccelerate;
        private bool m_IsStoppingForExit;
        private bool m_IsCoastingAfterExit;
        private bool m_IsPassengerPresentation;
        private bool m_IsDestroyed;
        private bool m_HoldCameraDuringBailout;
        private bool m_HoldCameraDuringDestruction;
        private bool m_KeepEngineRunningAfterBailout;
        private bool m_HasFuel = true;
        private bool m_HandbrakeInput;
        private float m_AccelerationInput;
        private float m_SteeringInput;
        private float m_ExitInputAvailableAt;
        private Quaternion m_SteeringWheelInitialRotation;
        private Transform m_SteeringLeftHandTarget;
        private Transform m_SteeringRightHandTarget;
        private Vector3 m_SteeringLeftHandLocalPosition;
        private Vector3 m_SteeringRightHandLocalPosition;
        private Quaternion m_SteeringLeftHandLocalRotation;
        private Quaternion m_SteeringRightHandLocalRotation;
        private bool m_SteeringHandTargetsCached;

        private GameObject m_RuntimeCameraRig;
        private Transform m_RuntimeCameraTarget;
        private Transform m_RuntimeCameraOrbitTarget;
        private CinemachineCamera m_CinemachineCamera;
        private CinemachineBrain m_CinemachineBrain;
        private bool m_BrainWasEnabled;
        private bool m_CreatedBrain;
        private Behaviour m_GameCreatorCamera;
        private MainCamera m_Gc2MainCamera;
        private Camera m_UnityCamera;
        private bool m_GameCreatorCameraWasEnabled;
        private float m_CameraOrbitYaw;
        private float m_CameraOrbitVelocity;
        private float m_LastCameraOrbitInputTime;
        private bool m_RearViewPressed;
        private float m_RearViewReturnYaw;
        private bool m_FirstPersonViewActive;
        private bool m_RuntimeFirstPersonCameraAnchor;
        private Transform m_FirstPersonForwardMountParent;
        private Vector3 m_FirstPersonForwardMountLocalPosition;
        private Quaternion m_FirstPersonForwardMountLocalRotation;
        private Transform m_FirstPersonRearMountParent;
        private Vector3 m_FirstPersonRearMountLocalPosition;
        private Quaternion m_FirstPersonRearMountLocalRotation;
        private bool m_FirstPersonMountsPrepared;
        private float m_FirstPersonReturnYaw;
        private FirstPersonHeadOcclusion m_FirstPersonHeadOcclusion;
        private FranklinFirstPersonCameraManager m_FirstPersonCameraManager;
        private Vector2 m_Gc2RearViewReturnRotation;
        private bool m_Gc2FirstPersonActive;
        private Coroutine m_DestructionCameraRoutine;

        private GameObject m_MobileCanvas;
        private UiButton_SVP m_SteerLeft;
        private UiButton_SVP m_SteerRight;
        private UiButton_SVP m_Accelerate;
        private UiButton_SVP m_SlowAccelerate;
        private UiButton_SVP m_BrakeReverse;
        private UiButton_SVP m_Handbrake;

        public bool IsVehicleEnabled => this.m_IsVehicleEnabled;
        public bool IsPassengerPresentationActive => this.m_IsPassengerPresentation;
        public bool IsDestroyed => this.m_IsDestroyed;
        public bool HasFuel => this.m_HasFuel;
        public float ThrottleMagnitude => Mathf.Abs(this.m_AccelerationInput);
        public float SignedAccelerationInput => this.m_AccelerationInput;
        public bool IsHandbrakeRequested => this.m_HandbrakeInput ||
            this.m_ExternalHandbrake;
        public bool IsStoppingForExit => this.m_IsStoppingForExit;
        public bool HeadlightsEnabled => this.m_VehicleLights != null &&
            this.m_VehicleLights.AreLightsOn;
        public bool IsRearViewPressed => this.m_RearViewPressed;
        public bool IsFirstPersonViewActive => this.m_FirstPersonViewActive;
        public float DamageSteeringBias => this.m_DamageSteeringBias;
        public float MaximumDamageSteeringBias => this.m_MaxDamageSteeringBias;
        public float SlowModeMaxSpeedKph => this.m_SlowModeMaxSpeedKph;
        public bool UseSeatEntryAlignment => true;
        public float SpeedMetersPerSecond => this.m_Rigidbody != null
            ? Vector3.ProjectOnPlane(this.m_Rigidbody.linearVelocity, Vector3.up).magnitude
            : 0f;
        public float SpeedKph => this.SpeedMetersPerSecond * 3.6f;
        public Transform VehicleBody => this.m_Controller != null
            ? this.m_Controller.VehicleBody
            : this.transform;

        private void Awake()
        {
            this.m_Rigidbody = this.GetComponent<Rigidbody>();
            this.m_CarEntry = this.GetComponent<CarEntry>();
            if (this.m_Dashboard == null)
                this.m_Dashboard = this.GetComponent<SimcadeCarDashboard>();
            if (this.m_Fuel == null)
                this.m_Fuel = this.GetComponent<SimcadeCarFuel>();
            if (this.m_VehicleLights == null)
                this.m_VehicleLights = this.GetComponent<VehicleLights>();
            if (this.m_Horn == null)
                this.m_Horn = this.GetComponent<SimcadeCarHorn>();
            if (this.m_Controller == null)
            {
                this.m_Controller = this.GetComponent<SimcadeVehicleController>();
            }

            if (this.m_GearSystem == null) this.m_GearSystem = this.GetComponent<GearSystem>();
            if (this.m_AudioSystem == null) this.m_AudioSystem = this.GetComponent<AudioSystem>();
            if (this.m_CameraTarget == null) this.m_CameraTarget = this.transform;
            if (this.m_SteeringWheel == null)
            {
                this.m_SteeringWheel = this.FindChild("Steering");
            }

            if (this.m_SteeringWheel != null)
            {
                this.m_SteeringWheelInitialRotation = this.m_SteeringWheel.localRotation;
                this.CacheSteeringHandTargets();
            }
        }

        private void Start()
        {
            // Prewarm all runtime-only presentation objects at scene start so
            // the first mobile enter does not instantiate camera/UI objects in
            // the same frame as the animation handoff.
            this.EnsureCameraRig();
            this.EnsureRuntimeCameraTarget();
            this.ResolveUnityCamera();
            if (this.ShouldShowMobileControls()) this.EnsureMobileControls();
            this.SetVehicleEnabled(false);
        }

        private void OnDisable()
        {
            this.m_IsVehicleEnabled = false;
            this.m_IsPassengerPresentation = false;
            this.m_HoldCameraDuringBailout = false;
            this.m_HoldCameraDuringDestruction = false;
            this.m_KeepEngineRunningAfterBailout = false;
            if (this.m_DestructionCameraRoutine != null)
            {
                this.StopCoroutine(this.m_DestructionCameraRoutine);
                this.m_DestructionCameraRoutine = null;
            }
            this.ResetVirtualInputs();
            this.SendInputs(0f, 0f, true);
            if (this.m_Controller != null) this.m_Controller.enabled = false;
            this.SetCameraActive(false);
            this.SetMobileControlsActive(false);
            this.SetDashboardActive(false);
            this.SetAudioActive(false);
            this.m_Fuel?.SetEngineActive(false);
            this.ResetSteeringWheel();
        }

        private void OnDestroy()
        {
            this.DeactivateGc2FirstPersonCamera();
            this.RestoreFirstPersonPresentation();
            if (this.m_RuntimeFirstPersonCameraAnchor &&
                this.m_FirstPersonCameraAnchor != null)
            {
                Destroy(this.m_FirstPersonCameraAnchor.gameObject);
            }
            if (this.m_RuntimeCameraRig != null) Destroy(this.m_RuntimeCameraRig);
            if (this.m_MobileCanvas != null) Destroy(this.m_MobileCanvas);
        }

        private void Update()
        {
            this.UpdateControllerExecutionState();
            if (!this.m_IsVehicleEnabled && !this.m_IsPassengerPresentation) return;

            if (this.IsExitPressed())
            {
                this.RequestExit();
                return;
            }

            this.UpdateCameraOrbit();
            if (this.m_IsPassengerPresentation) return;

            if (this.m_IsStoppingForExit)
            {
                this.m_HandbrakeInput = true;
                this.m_AccelerationInput = 0f;
                this.m_SteeringInput = Mathf.MoveTowards(
                    this.m_SteeringInput,
                    0f,
                    this.m_SteeringReturnResponse * Time.deltaTime
                );
                this.SendInputs(0f, this.m_SteeringInput, true);
                return;
            }

            this.ReadInput(out float acceleration, out float steering, out bool handbrake);
            this.m_HandbrakeInput = handbrake;
            steering = Mathf.Clamp(
                steering + this.m_DamageSteeringBias,
                -1f,
                1f
            );

            float accelerationRate = this.m_AccelerationResponse * Time.deltaTime;
            this.m_AccelerationInput = Mathf.MoveTowards(
                this.m_AccelerationInput,
                acceleration,
                accelerationRate
            );

            float steeringRate = (Mathf.Abs(steering) > 0.001f
                ? this.m_SteeringResponse
                : this.m_SteeringReturnResponse) * Time.deltaTime;
            this.m_SteeringInput = Mathf.MoveTowards(
                this.m_SteeringInput,
                steering,
                steeringRate
            );

            this.SendInputs(
                this.m_AccelerationInput,
                this.m_SteeringInput,
                handbrake || this.m_ExternalHandbrake
            );
        }

        private void FixedUpdate()
        {
            if (this.m_Rigidbody == null || this.m_Rigidbody.isKinematic) return;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(
                this.m_Rigidbody.linearVelocity,
                Vector3.up
            );
            float planarSpeed = planarVelocity.magnitude;

            if (this.m_IsStoppingForExit)
            {
                this.ApplyPlanarDeceleration(
                    planarVelocity,
                    planarSpeed,
                    this.m_ExitStopDeceleration
                );
                return;
            }

            if (this.m_IsCoastingAfterExit &&
                planarSpeed <= this.m_CoastingParkingSpeedKph / 3.6f)
            {
                this.m_IsCoastingAfterExit = false;
                if (this.m_Controller != null)
                {
                    this.m_Controller.CanDrive = false;
                    this.m_Controller.CanAccelerate = false;
                    this.SendInputs(0f, 0f, true);
                }

                Vector3 verticalVelocity = Vector3.Project(
                    this.m_Rigidbody.linearVelocity,
                    Vector3.up
                );
                this.m_Rigidbody.linearVelocity = verticalVelocity;
                this.m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (!this.m_IsVehicleEnabled || this.m_SteeringWheel == null) return;

            float angle = -this.m_SteeringInput * this.m_SteeringWheelMaxAngle;
            Quaternion target = this.m_SteeringWheelInitialRotation *
                Quaternion.AngleAxis(angle, Vector3.forward);
            float blend = 1f - Mathf.Exp(-this.m_SteeringWheelResponse * Time.deltaTime);
            this.m_SteeringWheel.localRotation = Quaternion.Slerp(
                this.m_SteeringWheel.localRotation,
                target,
                blend
            );

            // The wheel mesh may spin a full revolution, but its IK targets must
            // not follow that entire circle. Otherwise a held ±1 input makes the
            // palms pass through the turn and return to their neutral pose at
            // 360 degrees. Keep the lightweight target pose at a natural capped
            // angle so both hands visibly remain left/right until input release.
            this.UpdateSteeringHandTargets(
                -this.m_SteeringInput * this.m_SteeringHandsMaxAngle
            );
        }

        public void SetVehicleEnabled(bool state)
        {
            this.SetVehicleEnabled(state, false);
        }

        public void SetVehicleEnabled(bool state, bool preserveMomentum)
        {
            if (state && this.m_IsDestroyed) return;
            if (state)
            {
                this.m_IsPassengerPresentation = false;
                this.m_HoldCameraDuringBailout = false;
                this.m_KeepEngineRunningAfterBailout = false;
            }
            this.m_IsVehicleEnabled = state;
            this.m_IsStoppingForExit = false;
            this.m_IsCoastingAfterExit = !state && preserveMomentum;
            this.ResetVirtualInputs();
            this.m_AccelerationInput = 0f;
            this.m_SteeringInput = 0f;
            this.m_ExternalHandbrake = false;
            this.m_HandbrakeInput = false;
            if (state)
            {
                this.m_ExitInputAvailableAt = Time.unscaledTime + 0.5f;
                this.ResetCameraOrbit();
            }
            else
            {
                this.ResetSteeringWheel();
            }

            if (this.m_Controller != null)
            {
                if (state) this.m_Controller.enabled = true;
                this.m_Controller.CanDrive = state || preserveMomentum;
                this.m_Controller.CanAccelerate = state && this.m_HasFuel;
            }

            this.SendInputs(0f, 0f, !state && !preserveMomentum);
            this.UpdateControllerExecutionState();
            bool engineRequested = state || this.m_KeepEngineRunningAfterBailout;
            this.m_Fuel?.SetEngineActive(engineRequested);
            this.SetAudioActive(
                engineRequested && this.m_HasFuel
            );
            this.SetMobileControlsActive(state && this.ShouldShowMobileControls());
            this.SetDashboardActive(state);
            if (state)
            {
                this.SetCameraActive(true);
                if (GetFirstPersonPreference())
                {
                    this.ApplyFirstPersonView(true, false);
                }
            }
            else if (!this.m_HoldCameraDuringBailout &&
                     !this.m_HoldCameraDuringDestruction &&
                     !this.m_IsPassengerPresentation)
            {
                this.SetCameraActive(false);
            }

            if (!state && !preserveMomentum && this.m_Rigidbody != null &&
                !this.m_Rigidbody.isKinematic)
            {
                this.m_Rigidbody.linearVelocity = Vector3.zero;
                this.m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Permanently disables driving/presentation while preserving the body's
        /// current momentum for the explosion impulse and detached wheels.
        /// </summary>
        public void SetDestroyed()
        {
            if (this.m_IsDestroyed) return;
            this.m_IsDestroyed = true;
            this.m_IsPassengerPresentation = false;
            this.m_HoldCameraDuringBailout = false;
            this.m_KeepEngineRunningAfterBailout = false;
            this.SetVehicleEnabled(false, true);
            this.m_IsCoastingAfterExit = false;

            if (this.m_Controller != null)
            {
                this.m_Controller.CanDrive = false;
                this.m_Controller.CanAccelerate = false;
                this.m_Controller.enabled = false;
            }
            this.SetAudioActive(false);
            this.SetHeadlightEnabled(false);
            this.SetMobileControlsActive(false);
            this.SetDashboardActive(false);
            if (!this.m_HoldCameraDuringDestruction)
                this.SetCameraActive(false);
        }

        /// <summary>
        /// Uses the Sim-Cade chase/orbit camera for a rear-seat Player without
        /// granting throttle, steering or mobile driving controls.
        /// </summary>
        public void SetPassengerPresentation(bool active)
        {
            if (active && this.m_IsDestroyed) return;
            if (active && this.m_IsVehicleEnabled) return;
            this.m_IsPassengerPresentation = active;
            if (active)
            {
                this.m_ExitInputAvailableAt = Time.unscaledTime + 0.5f;
                this.ResetCameraOrbit();
                this.SetCameraActive(true);
                this.SetDashboardActive(true);
            }
            else if (!this.m_IsVehicleEnabled &&
                     !this.m_HoldCameraDuringBailout &&
                     !this.m_HoldCameraDuringDestruction)
            {
                this.SetCameraActive(false);
                this.SetDashboardActive(false);
            }
        }

        public void BeginBailoutCameraHold()
        {
            if (!this.m_IsVehicleEnabled) return;
            this.m_HoldCameraDuringBailout = true;
        }

        public void KeepEngineRunningAfterBailout()
        {
            if (!this.m_IsVehicleEnabled) return;
            this.m_KeepEngineRunningAfterBailout = true;
        }

        public void EndBailoutCameraHold()
        {
            if (!this.m_HoldCameraDuringBailout) return;
            this.m_HoldCameraDuringBailout = false;
            if (!this.m_IsVehicleEnabled) this.SetCameraActive(false);
        }

        /// <summary>
        /// Retains the already-active Sim-Cade camera while terminal destruction
        /// ejects the Player. No camera is activated for an unoccupied/off-screen Car.
        /// Call before SetDestroyed().
        /// </summary>
        public bool BeginDestructionCameraHold()
        {
            if (this.m_Gc2FirstPersonActive)
            {
                // Destruction hold/lerp is authored on the Sim-Cade chase rig.
                // Restore that rig in the same frame before capturing ownership.
                this.ApplyFirstPersonView(false, false);
            }
            bool cameraIsActive = this.m_RuntimeCameraRig != null &&
                this.m_RuntimeCameraRig.activeSelf;
            this.m_HoldCameraDuringDestruction = cameraIsActive;
            return cameraIsActive;
        }

        /// <summary>
        /// Waits for the explosion burst, moves the chase target from the wreck
        /// to the physical Player, then restores the regular GC2 Player camera.
        /// </summary>
        public void ReturnDestructionCameraToPlayer(
            Character player,
            float holdDuration,
            float returnDuration)
        {
            if (!this.m_HoldCameraDuringDestruction) return;
            if (this.m_DestructionCameraRoutine != null)
                this.StopCoroutine(this.m_DestructionCameraRoutine);
            this.m_DestructionCameraRoutine = this.StartCoroutine(
                this.ReturnDestructionCameraRoutine(
                    player,
                    Mathf.Max(0f, holdDuration),
                    Mathf.Max(0f, returnDuration)
                )
            );
        }

        private IEnumerator ReturnDestructionCameraRoutine(
            Character player,
            float holdDuration,
            float returnDuration)
        {
            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            Transform playerTarget = this.ResolveDestructionCameraTarget(player);
            Transform cameraTarget = this.m_RuntimeCameraTarget;
            if (cameraTarget != null && playerTarget != null)
            {
                cameraTarget.SetParent(null, true);
                Vector3 startPosition = cameraTarget.position;
                Quaternion startRotation = cameraTarget.rotation;
                float elapsed = 0f;

                while (cameraTarget != null && playerTarget != null &&
                       elapsed < returnDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = returnDuration > 0f
                        ? Mathf.Clamp01(elapsed / returnDuration)
                        : 1f;
                    progress = progress * progress * (3f - 2f * progress);
                    cameraTarget.SetPositionAndRotation(
                        Vector3.Lerp(startPosition, playerTarget.position, progress),
                        Quaternion.Slerp(startRotation, playerTarget.rotation, progress)
                    );
                    yield return null;
                }

                if (cameraTarget != null && playerTarget != null)
                {
                    cameraTarget.SetPositionAndRotation(
                        playerTarget.position,
                        playerTarget.rotation
                    );
                }
                // Give Cinemachine one LateUpdate at the final Player target
                // before GC2 takes camera ownership back.
                yield return null;
            }

            this.m_HoldCameraDuringDestruction = false;
            this.m_DestructionCameraRoutine = null;
            this.SetCameraActive(false);
        }

        private Transform ResolveDestructionCameraTarget(Character player)
        {
            if (player == null) return null;
            Animator animator = player.Animim?.Animator;
            if (animator != null && animator.isHuman)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) return hips;
            }
            return animator != null ? animator.transform : player.transform;
        }

        public void BeginExitStop()
        {
            if (!this.m_IsVehicleEnabled) return;

            this.m_IsStoppingForExit = true;
            this.ResetVirtualInputs();
            this.m_AccelerationInput = 0f;
            this.m_SteeringInput = 0f;
            if (this.m_Controller != null)
            {
                this.m_Controller.CanDrive = true;
                this.m_Controller.CanAccelerate = false;
            }
            this.SendInputs(0f, 0f, true);
        }

        public void CancelExitStop()
        {
            if (!this.m_IsStoppingForExit) return;

            this.m_IsStoppingForExit = false;
            if (this.m_Controller != null && this.m_IsVehicleEnabled)
            {
                this.m_Controller.CanDrive = true;
                this.m_Controller.CanAccelerate = this.m_HasFuel;
            }
            this.SendInputs(0f, 0f, false);
        }

        public void SetHandbrakeInput(bool active)
        {
            this.m_ExternalHandbrake = active;
        }

        public void SetHeadlightEnabled(bool active)
        {
            if (this == null) return;
            if (this.m_VehicleLights == null)
                this.m_VehicleLights = this.GetComponent<VehicleLights>();
            if (this.m_VehicleLights == null) return;

            if (active) this.m_VehicleLights.LightsOn();
            else this.m_VehicleLights.LightsOff();
        }

        public void ToggleHeadlights()
        {
            this.SetHeadlightEnabled(!this.HeadlightsEnabled);
        }

        public void SetHornPressed(bool pressed)
        {
            if (this == null) return;
            if (this.m_Horn == null)
                this.m_Horn = this.GetComponent<SimcadeCarHorn>();
            this.m_Horn?.SetPressed(pressed);
        }

        /// <summary>
        /// Called by SimcadeCarFuel when the GC2 fuel Attribute crosses empty.
        /// Steering, braking and momentum remain available; engine and throttle stop.
        /// </summary>
        public void SetFuelAvailable(bool available)
        {
            this.m_HasFuel = available;
            if (!available) this.m_AccelerationInput = 0f;

            if (this.m_Controller != null && this.m_IsVehicleEnabled)
            {
                this.m_Controller.CanDrive = true;
                this.m_Controller.CanAccelerate = available &&
                    !this.m_IsStoppingForExit;
            }

            bool engineRequested = this.m_IsVehicleEnabled ||
                this.m_KeepEngineRunningAfterBailout;
            this.SetAudioActive(engineRequested && available && !this.m_IsDestroyed);
            if (!available) this.SendInputs(0f, this.m_SteeringInput, false);
        }

        /// <summary>
        /// Adds a small persistent wheel-alignment error after a body impact.
        /// The driver can counter-steer it and repair systems can reduce or reset it.
        /// </summary>
        public void AddDamageSteeringBias(float amount)
        {
            this.m_DamageSteeringBias = Mathf.Clamp(
                this.m_DamageSteeringBias + amount,
                -this.m_MaxDamageSteeringBias,
                this.m_MaxDamageSteeringBias
            );
        }

        public void RepairDamageSteering(float amount)
        {
            this.m_DamageSteeringBias = Mathf.MoveTowards(
                this.m_DamageSteeringBias,
                0f,
                Mathf.Max(0f, amount)
            );
        }

        public void ResetDamageSteering()
        {
            this.m_DamageSteeringBias = 0f;
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

        /// <summary>
        /// Hold to snap the existing Sim-Cade orbit camera 180 degrees. Releasing
        /// immediately restores the exact orbit angle that was active before the
        /// button press. No extra Camera or Cinemachine rig is created.
        /// </summary>
        public void SetRearViewPressed(bool active)
        {
            if (active)
            {
                if (!this.m_EnableHoldRearView || !this.m_IsVehicleEnabled ||
                    this.m_IsDestroyed || this.m_RearViewPressed)
                {
                    return;
                }

                this.m_RearViewReturnYaw = this.m_CameraOrbitYaw;
                this.m_RearViewPressed = true;
                this.m_CameraOrbitYaw = 180f;
                this.m_CameraOrbitVelocity = 0f;
                if (this.m_FirstPersonViewActive)
                {
                    this.ApplyFirstPersonAnchorMount(true);
                    this.ApplyGc2FirstPersonRearView(true);
                }
                this.ApplyCameraOrbit();
                if (this.m_CinemachineCamera != null)
                    this.m_CinemachineCamera.PreviousStateIsValid = false;
                return;
            }

            if (!this.m_RearViewPressed) return;

            this.m_RearViewPressed = false;
            this.m_CameraOrbitYaw = this.m_RearViewReturnYaw;
            this.m_CameraOrbitVelocity = 0f;
            this.m_LastCameraOrbitInputTime = Time.unscaledTime;
            if (this.m_FirstPersonViewActive)
            {
                this.ApplyFirstPersonAnchorMount(false);
                this.ApplyGc2FirstPersonRearView(false);
            }
            this.ApplyCameraOrbit();
            if (this.m_CinemachineCamera != null)
                this.m_CinemachineCamera.PreviousStateIsValid = false;
        }

        /// <summary>
        /// Uses the active GC2 Third Person Camera Shot as a head-mounted cockpit
        /// view. Passing false restores its complete snapshot and returns camera
        /// ownership to the existing Sim-Cade chase rig.
        /// </summary>
        public void SetFirstPersonView(bool active)
        {
            this.ApplyFirstPersonView(active, true);
        }

        /// <summary>
        /// Lets the shared mobile HUD temporarily reserve a pointer without
        /// introducing a second Car-specific orbit implementation.
        /// </summary>
        public void SetFirstPersonOrbitSuppressed(bool suppressed)
        {
            this.m_FirstPersonCameraManager?.SetOrbitSuppressed(
                this,
                suppressed
            );
        }

        /// <summary>
        /// Restores TPS for an exit, modal UI or destruction handoff without
        /// overwriting the camera mode selected by the user.
        /// </summary>
        public void RestoreThirdPersonViewPreservingPreference()
        {
            this.ApplyFirstPersonView(false, false);
        }

        private void ApplyFirstPersonView(bool active, bool persistPreference)
        {
            if (active && (!this.m_IsVehicleEnabled || this.m_IsDestroyed ||
                           this.m_IsPassengerPresentation))
            {
                return;
            }

            if (this.m_FirstPersonViewActive == active)
            {
                if (!active)
                {
                    this.DeactivateGc2FirstPersonCamera();
                    this.RestoreFirstPersonPresentation();
                }
                return;
            }

            this.EnsureCameraRig();
            this.EnsureRuntimeCameraTarget();
            if (active && this.EnsureFirstPersonCameraAnchor() == null) return;

            if (this.m_RearViewPressed)
            {
                this.m_CameraOrbitYaw = this.m_RearViewReturnYaw;
                this.m_RearViewPressed = false;
            }

            if (active)
            {
                this.m_FirstPersonReturnYaw = this.m_CameraOrbitYaw;
                this.m_CameraOrbitYaw = 0f;
                this.m_FirstPersonViewActive = true;
                this.PrepareFirstPersonPresentation();
                if (!this.ActivateGc2FirstPersonCamera())
                {
                    this.m_FirstPersonViewActive = false;
                    this.RestoreFirstPersonPresentation();
                    this.m_CameraOrbitYaw = this.m_FirstPersonReturnYaw;
                    this.m_FirstPersonReturnYaw = 0f;
                    return;
                }
            }
            else
            {
                this.DeactivateGc2FirstPersonCamera();
                this.m_FirstPersonViewActive = false;
                this.RestoreFirstPersonPresentation();
                this.m_CameraOrbitYaw = this.m_FirstPersonReturnYaw;
                this.m_FirstPersonReturnYaw = 0f;
            }

            this.m_CameraOrbitVelocity = 0f;
            this.m_LastCameraOrbitInputTime = Time.unscaledTime;
            this.ApplyCameraOrbit();
            if (!this.m_Gc2FirstPersonActive) this.ApplyCameraViewMode();
            this.m_Dashboard?.SetDrivingTelemetryVisible(!active);
            if (persistPreference && this.m_IsVehicleEnabled &&
                !this.m_IsDestroyed && !this.m_IsPassengerPresentation)
            {
                SetFirstPersonPreference(active);
            }
        }

        private void ResetVirtualInputs()
        {
            if (this.m_FirstPersonViewActive)
            {
                // Exit/disable restores the Player camera without changing the
                // saved TPS/FPS choice used by the next Car.
                this.ApplyFirstPersonView(false, false);
            }

            if (this.m_RearViewPressed)
            {
                this.m_CameraOrbitYaw = this.m_RearViewReturnYaw;
                this.m_CameraOrbitVelocity = 0f;
                this.ApplyCameraOrbit();
            }

            this.m_VirtualAccelerate = false;
            this.m_VirtualBrakeReverse = false;
            this.m_VirtualSteerLeft = false;
            this.m_VirtualSteerRight = false;
            this.m_VirtualHandbrake = false;
            this.m_VirtualSlowAccelerate = false;
            this.m_HandbrakeInput = false;
            this.m_RearViewPressed = false;
            this.m_RearViewReturnYaw = 0f;
            this.m_Horn?.SetPressed(false);
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

        public void ResetVehicle()
        {
            if (this.m_Rigidbody == null) return;

            Vector3 position = this.transform.position + Vector3.up;
            float yaw = this.transform.eulerAngles.y;
            this.m_Rigidbody.position = position;
            this.m_Rigidbody.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!this.m_Rigidbody.isKinematic)
            {
                this.m_Rigidbody.linearVelocity = Vector3.zero;
                this.m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Gives this Sim-Cade car a deterministic exit path. The original RVR
        /// Switch/Tab trigger remains available, while E and the mobile button
        /// use this method directly.
        /// </summary>
        public void RequestExit()
        {
            if ((!this.m_IsVehicleEnabled && !this.m_IsPassengerPresentation) ||
                this.m_CarEntry == null ||
                this.m_CarEntry.IsTransitioning)
            {
                return;
            }

            Character character = this.m_IsPassengerPresentation
                ? this.m_CarEntry.RearPassengerCharacter
                : this.m_CarEntry.SeatedCharacter;
            if (character == null) return;

            this.m_CarEntry.RequestExit(character);
        }

        private void ReadInput(out float acceleration, out float steering, out bool handbrake)
        {
            acceleration = 0f;
            steering = 0f;
            handbrake = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) acceleration += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) acceleration -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steering -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steering += 1f;
                handbrake |= keyboard.spaceKey.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float gamepadAcceleration = gamepad.rightTrigger.ReadValue() -
                    gamepad.leftTrigger.ReadValue();
                if (Mathf.Abs(gamepadAcceleration) > Mathf.Abs(acceleration))
                {
                    acceleration = gamepadAcceleration;
                }

                float gamepadSteering = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(gamepadSteering) > Mathf.Abs(steering))
                {
                    steering = gamepadSteering;
                }

                handbrake |= gamepad.buttonSouth.isPressed;
            }

            if (this.m_MobileCanvas != null && this.m_MobileCanvas.activeSelf)
            {
                if (this.IsPressed(this.m_Accelerate)) acceleration += 1f;
                if (this.IsPressed(this.m_BrakeReverse)) acceleration -= 1f;
                if (this.IsPressed(this.m_SteerLeft)) steering -= 1f;
                if (this.IsPressed(this.m_SteerRight)) steering += 1f;
                handbrake |= this.IsPressed(this.m_Handbrake);
            }

            if (this.m_VirtualAccelerate) acceleration += 1f;
            if (this.m_VirtualBrakeReverse) acceleration -= 1f;
            if (this.m_VirtualSteerLeft) steering -= 1f;
            if (this.m_VirtualSteerRight) steering += 1f;
            handbrake |= this.m_VirtualHandbrake;

            bool slowAccelerate = this.m_VirtualSlowAccelerate ||
                                  (keyboard != null && keyboard.lKey.isPressed) ||
                                  (this.m_MobileCanvas != null &&
                                   this.m_MobileCanvas.activeSelf &&
                                   this.IsPressed(this.m_SlowAccelerate));
            if (slowAccelerate && Mathf.Abs(acceleration) < 0.001f)
            {
                acceleration = this.CalculateSlowAccelerationInput();
            }

            acceleration = Mathf.Clamp(acceleration, -1f, 1f);
            steering = Mathf.Clamp(steering, -1f, 1f);
        }

        private bool IsExitPressed()
        {
            if (Time.unscaledTime < this.m_ExitInputAvailableAt) return false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
        }

        private void SendInputs(float acceleration, float steering, bool handbrake)
        {
            if (this.m_Controller == null) return;
            if (!this.m_HasFuel) acceleration = 0f;
            this.m_Controller.ProvideInputs(acceleration, steering, handbrake ? 1f : 0f);
        }

        private void UpdateControllerExecutionState()
        {
            if (this.m_Controller == null) return;

            // Keep the component alive while the parked car has a dynamic body.
            // Some of the original interaction setup is initialized while all
            // vehicle behaviours are active. Suspend only during the temporary
            // kinematic window used by the entry/exit animations.
            bool shouldRun = !this.m_IsDestroyed && (this.m_IsVehicleEnabled ||
                (this.m_Rigidbody != null && !this.m_Rigidbody.isKinematic));
            if (this.m_Controller.enabled != shouldRun)
            {
                this.m_Controller.enabled = shouldRun;
            }
        }

        private void SetAudioActive(bool active)
        {
            if (this.m_GearSystem != null) this.m_GearSystem.enabled = active;
            if (this.m_AudioSystem == null) return;

            this.m_AudioSystem.enabled = active;
            this.SetAudioSourceActive(this.m_AudioSystem.engineSound, active, true);
            this.SetAudioSourceActive(this.m_AudioSystem.GearSound, false, false);
        }

        private void SetAudioSourceActive(AudioSource source, bool active, bool loop)
        {
            if (source == null) return;
            source.loop = loop;

            if (active)
            {
                if (!source.isPlaying && source.clip != null) source.Play();
            }
            else
            {
                source.Stop();
            }
        }

        private bool ShouldShowMobileControls()
        {
            if (!this.m_UseMobileInput) return false;
            if (FranklinMobileHud.IsActive) return false;
            return Application.isMobilePlatform ||
                (Application.isEditor && this.m_ShowMobileInputInEditor);
        }

        private void SetMobileControlsActive(bool active)
        {
            if (active) this.EnsureMobileControls();
            if (this.m_MobileCanvas != null) this.m_MobileCanvas.SetActive(active);
        }

        private void SetDashboardActive(bool active)
        {
            if (this.m_Dashboard == null)
                this.m_Dashboard = this.GetComponent<SimcadeCarDashboard>();
            this.m_Dashboard?.SetPresentationActive(active);
        }

        private void EnsureMobileControls()
        {
            if (this.m_MobileCanvas != null || this.m_MobileInputPrefab == null) return;

            this.m_MobileCanvas = new GameObject(
                "Sim-Cade Mobile Controls",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            Canvas canvas = this.m_MobileCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = this.m_MobileCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject controls = Instantiate(this.m_MobileInputPrefab, this.m_MobileCanvas.transform);
            controls.name = this.m_MobileInputPrefab.name;
            this.ResolveMobileButtons(controls);
            this.CreateMobileExitButton();
            this.CreateMobileSlowAccelerateButton();
            this.EnsureEventSystem();
        }

        private void CreateMobileExitButton()
        {
            GameObject buttonObject = new GameObject(
                "Exit Car",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(this.m_MobileCanvas.transform, false);
            buttonRect.anchorMin = Vector2.one;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.pivot = Vector2.one;
            buttonRect.anchoredPosition = new Vector2(-40f, -40f);
            buttonRect.sizeDelta = new Vector2(180f, 72f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.55f, 0.08f, 0.08f, 0.9f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(this.RequestExit);

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            label.text = "EXIT";
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private void CreateMobileSlowAccelerateButton()
        {
            GameObject buttonObject = new GameObject(
                "Slow Drive",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(UiButton_SVP)
            );
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(this.m_MobileCanvas.transform, false);
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-130f, 65f);
            buttonRect.sizeDelta = new Vector2(130f, 130f);

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(
                "FranklinMobileUI/vehicle-control-slow"
            );
            image.preserveAspect = true;
            this.m_SlowAccelerate = buttonObject.GetComponent<UiButton_SVP>();
        }

        private float CalculateSlowAccelerationInput()
        {
            if (this.m_Rigidbody == null) return this.m_SlowThrottle;

            float maximumSpeed = this.m_SlowModeMaxSpeedKph / 3.6f;
            float forwardSpeed = Vector3.Dot(
                this.m_Rigidbody.linearVelocity,
                this.transform.forward
            );
            if (forwardSpeed <= 0f) return this.m_SlowThrottle;
            if (forwardSpeed >= maximumSpeed) return 0f;

            float taperStartSpeed = maximumSpeed * this.m_SlowThrottleTaperStart;
            float taper = Mathf.InverseLerp(maximumSpeed, taperStartSpeed, forwardSpeed);
            taper = Mathf.SmoothStep(0f, 1f, taper);
            return this.m_SlowThrottle * taper;
        }

        private void ApplyPlanarDeceleration(
            Vector3 planarVelocity,
            float planarSpeed,
            float deceleration)
        {
            float maximumSpeedChange = deceleration * Time.fixedDeltaTime;
            if (planarSpeed <= Mathf.Max(0.05f, maximumSpeedChange))
            {
                Vector3 verticalVelocity = Vector3.Project(
                    this.m_Rigidbody.linearVelocity,
                    Vector3.up
                );
                this.m_Rigidbody.linearVelocity = verticalVelocity;
                return;
            }

            if (deceleration > 0f)
            {
                this.m_Rigidbody.AddForce(
                    -planarVelocity.normalized * deceleration,
                    ForceMode.Acceleration
                );
            }
        }

        private void ResolveMobileButtons(GameObject controls)
        {
            UiButton_SVP[] buttons = controls.GetComponentsInChildren<UiButton_SVP>(true);
            foreach (UiButton_SVP button in buttons)
            {
                switch (button.gameObject.name)
                {
                    case "Steer Left": this.m_SteerLeft = button; break;
                    case "Steer Right": this.m_SteerRight = button; break;
                    case "Accelerate": this.m_Accelerate = button; break;
                    case "Brake/Reverse": this.m_BrakeReverse = button; break;
                    case "Handbrake": this.m_Handbrake = button; break;
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject eventSystemObject = new GameObject(
                "EventSystem (Sim-Cade)",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            DontDestroyOnLoad(eventSystemObject);
        }

        private void SetCameraActive(bool active)
        {
            if (active)
            {
                this.EnsureCameraRig();
                if (this.m_RuntimeCameraRig == null || this.m_CinemachineCamera == null) return;

                this.ResolveUnityCamera();
                if (this.m_CinemachineBrain == null) return;

                if (this.m_GameCreatorCamera != null)
                {
                    this.m_GameCreatorCameraWasEnabled = this.m_GameCreatorCamera.enabled;
                    this.m_GameCreatorCamera.enabled = false;
                }

                this.m_CinemachineBrain.enabled = true;
                this.EnsureRuntimeCameraTarget();
                this.ApplyCameraViewMode();
                this.m_CinemachineCamera.Priority = this.m_CameraPriority;
                this.m_CinemachineCamera.PreviousStateIsValid = false;
                this.m_RuntimeCameraRig.SetActive(true);
                return;
            }

            if (this.m_RuntimeCameraRig != null) this.m_RuntimeCameraRig.SetActive(false);
            if (this.m_CinemachineBrain != null)
            {
                this.m_CinemachineBrain.enabled = this.m_CreatedBrain
                    ? false
                    : this.m_BrainWasEnabled;
            }

            if (this.m_GameCreatorCamera != null)
            {
                this.m_GameCreatorCamera.enabled = this.m_GameCreatorCameraWasEnabled;
            }
        }

        private void EnsureCameraRig()
        {
            if (this.m_RuntimeCameraRig != null || this.m_ChaseCameraPrefab == null) return;

            this.m_RuntimeCameraRig = Instantiate(this.m_ChaseCameraPrefab);
            this.m_RuntimeCameraRig.name = "Sim-Cade Chase Camera (Runtime)";
            this.m_CinemachineCamera = this.m_RuntimeCameraRig.GetComponent<CinemachineCamera>();
            this.m_RuntimeCameraRig.SetActive(false);
        }

        private void ApplyCameraViewMode()
        {
            if (this.m_CinemachineCamera == null) return;

            Transform thirdPersonTarget = this.EnsureRuntimeCameraTarget();
            this.m_CinemachineCamera.Follow = this.m_RuntimeCameraOrbitTarget != null
                ? this.m_RuntimeCameraOrbitTarget
                : thirdPersonTarget;
            this.m_CinemachineCamera.LookAt = thirdPersonTarget;

            this.m_CinemachineCamera.PreviousStateIsValid = false;
        }

        private Transform EnsureFirstPersonCameraAnchor()
        {
            if (this.m_FirstPersonCameraAnchor != null)
                return this.m_FirstPersonCameraAnchor;

            Character driver = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            if (!this.ResolveFirstPersonCameraManager(driver)) return null;

            Transform parent = this.m_CarEntry != null &&
                               this.m_CarEntry.entryParent != null
                ? this.m_CarEntry.entryParent
                : this.m_CameraTarget != null
                    ? this.m_CameraTarget
                    : this.transform;
            GameObject anchorObject = new GameObject(
                "Sim-Cade First Person Camera Anchor (Runtime)"
            );
            this.m_FirstPersonCameraAnchor = anchorObject.transform;
            Animator animator = driver != null ? driver.Animim.Animator : null;
            Vector3 worldPosition =
                this.m_FirstPersonCameraManager.GetEyeWorldPosition(
                    FranklinFirstPersonCameraManager.Context.Car,
                    animator,
                    parent,
                    driver != null ? driver.transform : parent
                );
            this.m_FirstPersonCameraAnchor.SetPositionAndRotation(
                worldPosition,
                parent.rotation
            );
            this.m_FirstPersonCameraAnchor.SetParent(parent, true);
            this.m_RuntimeFirstPersonCameraAnchor = true;
            return this.m_FirstPersonCameraAnchor;
        }

        private void PrepareFirstPersonPresentation()
        {
            if (this.m_FirstPersonCameraAnchor == null)
            {
                return;
            }

            Character driver = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            if (!this.ResolveFirstPersonCameraManager(driver)) return;

            this.RefreshFirstPersonPreviewPose();

            Animator animator = driver != null
                ? driver.Animim.Animator
                : null;
            if (animator == null) return;

            this.m_FirstPersonHeadOcclusion =
                animator.GetComponent<FirstPersonHeadOcclusion>();
            bool requiresHeadOcclusionBegin =
                this.m_FirstPersonHeadOcclusion == null;
            if (this.m_FirstPersonHeadOcclusion == null)
            {
                this.m_FirstPersonHeadOcclusion =
                    animator.gameObject.AddComponent<FirstPersonHeadOcclusion>();
            }
            if (requiresHeadOcclusionBegin ||
                !this.m_FirstPersonHeadOcclusion.enabled)
            {
                this.m_FirstPersonHeadOcclusion.Begin(animator);
            }
        }

        /// <summary>
        /// Rebuilds both Car FPS mounts after the Player manager's Position or
        /// Fallback Position changes in Play Mode. This only updates presentation
        /// transforms; head occlusion remains owned by the activation lifecycle.
        /// </summary>
        private void RefreshFirstPersonPreviewPose()
        {
            if (this.m_FirstPersonCameraAnchor == null) return;

            Character driver = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            if (!this.ResolveFirstPersonCameraManager(driver)) return;

            Transform heading = this.m_CarEntry != null &&
                                this.m_CarEntry.entryParent != null
                ? this.m_CarEntry.entryParent
                : this.m_CameraTarget != null
                    ? this.m_CameraTarget
                    : this.transform;
            Animator animator = driver != null
                ? driver.Animim.Animator
                : null;
            Transform head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            Transform neck = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Neck)
                : null;
            Transform forwardMount = neck != null
                ? neck
                : head != null && head.parent != null
                    ? head.parent
                    : animator != null
                        ? animator.transform
                        : heading;

            Vector3 eyeWorldPosition =
                this.m_FirstPersonCameraManager.GetEyeWorldPosition(
                    FranklinFirstPersonCameraManager.Context.Car,
                    animator,
                    heading,
                    driver != null ? driver.transform : heading
                );
            Vector3 rearWorldPosition =
                this.m_FirstPersonCameraManager.GetCarRearWorldPosition(
                    animator,
                    heading,
                    driver != null ? driver.transform : heading
                );

            // Forward view follows Neck (or the safest available Animator/seat
            // transform), while rear view remains seat-owned so the torso cannot
            // move over the camera. Rebuilding both caches prevents a rear-view
            // toggle from restoring the pre-preview Position.
            CacheFirstPersonMount(
                forwardMount,
                eyeWorldPosition,
                heading.rotation,
                out this.m_FirstPersonForwardMountLocalPosition,
                out this.m_FirstPersonForwardMountLocalRotation
            );
            this.m_FirstPersonForwardMountParent = forwardMount;
            CacheFirstPersonMount(
                heading,
                rearWorldPosition,
                heading.rotation,
                out this.m_FirstPersonRearMountLocalPosition,
                out this.m_FirstPersonRearMountLocalRotation
            );
            this.m_FirstPersonRearMountParent = heading;
            this.m_FirstPersonMountsPrepared = true;
            this.ApplyFirstPersonAnchorMount(this.m_RearViewPressed);
        }

        private void RestoreFirstPersonPresentation()
        {
            if (this.m_FirstPersonHeadOcclusion != null)
            {
                this.m_FirstPersonHeadOcclusion.End();
                this.m_FirstPersonHeadOcclusion = null;
            }

            this.m_FirstPersonMountsPrepared = false;
            this.m_FirstPersonForwardMountParent = null;
            this.m_FirstPersonRearMountParent = null;

            if (!this.m_RuntimeFirstPersonCameraAnchor ||
                this.m_FirstPersonCameraAnchor == null)
            {
                return;
            }

            Transform parent = this.m_CarEntry != null &&
                               this.m_CarEntry.entryParent != null
                ? this.m_CarEntry.entryParent
                : this.m_CameraTarget != null
                    ? this.m_CameraTarget
                    : this.transform;
            this.m_FirstPersonCameraAnchor.SetParent(parent, false);
            this.m_FirstPersonCameraAnchor.localPosition = Vector3.zero;
            this.m_FirstPersonCameraAnchor.localRotation = Quaternion.identity;
        }

        private static void CacheFirstPersonMount(
            Transform parent,
            Vector3 worldPosition,
            Quaternion worldRotation,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            localPosition = parent.InverseTransformPoint(worldPosition);
            localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
        }

        private void ApplyFirstPersonAnchorMount(bool rearView)
        {
            if (!this.m_FirstPersonMountsPrepared ||
                this.m_FirstPersonCameraAnchor == null)
            {
                return;
            }

            Transform mountParent = rearView
                ? this.m_FirstPersonRearMountParent
                : this.m_FirstPersonForwardMountParent;
            if (mountParent == null) return;

            this.m_FirstPersonCameraAnchor.SetParent(mountParent, false);
            this.m_FirstPersonCameraAnchor.localPosition = rearView
                ? this.m_FirstPersonRearMountLocalPosition
                : this.m_FirstPersonForwardMountLocalPosition;
            this.m_FirstPersonCameraAnchor.localRotation = rearView
                ? this.m_FirstPersonRearMountLocalRotation
                : this.m_FirstPersonForwardMountLocalRotation;
        }

        private bool ResolveFirstPersonCameraManager(Character driver = null)
        {
            driver ??= this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            if (driver == null) return false;

            this.m_FirstPersonCameraManager =
                FranklinFirstPersonCameraManager.Resolve(driver);
            return this.m_FirstPersonCameraManager != null;
        }

        private bool ActivateGc2FirstPersonCamera()
        {
            if (this.m_Gc2FirstPersonActive &&
                this.m_FirstPersonCameraManager != null &&
                this.m_FirstPersonCameraManager.IsOwnedBy(this))
            {
                return true;
            }
            if (this.m_FirstPersonCameraAnchor == null) return false;

            this.ResolveUnityCamera();
            if (this.m_Gc2MainCamera == null)
            {
                this.m_Gc2MainCamera = ShortcutMainCamera.Get<MainCamera>();
            }
            if (this.m_Gc2MainCamera == null) return false;

            ShotCamera shot = this.m_Gc2MainCamera.Transition.CurrentShotCamera;
            Character driver = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            if (!this.ResolveFirstPersonCameraManager(driver)) return false;

            if (this.m_RuntimeCameraRig != null)
                this.m_RuntimeCameraRig.SetActive(false);
            if (this.m_CinemachineBrain != null)
                this.m_CinemachineBrain.enabled = false;
            this.m_Gc2MainCamera.enabled = true;

            Transform heading = this.m_CarEntry != null &&
                                this.m_CarEntry.entryParent != null
                ? this.m_CarEntry.entryParent
                : this.transform;
            if (!this.m_FirstPersonCameraManager.Activate(
                    this,
                    FranklinFirstPersonCameraManager.Context.Car,
                    shot,
                    this.m_FirstPersonCameraAnchor,
                    heading,
                    this.RefreshFirstPersonPreviewPose
                ))
            {
                this.m_Gc2MainCamera.enabled = false;
                if (this.m_CinemachineBrain != null)
                    this.m_CinemachineBrain.enabled = true;
                if (this.m_RuntimeCameraRig != null)
                    this.m_RuntimeCameraRig.SetActive(true);
                return false;
            }

            this.m_Gc2FirstPersonActive = true;
            return true;
        }

        private void DeactivateGc2FirstPersonCamera()
        {
            if (!this.m_Gc2FirstPersonActive) return;

            this.m_FirstPersonCameraManager?.Deactivate(this, true, 0f);

            if (this.m_Gc2MainCamera != null)
            {
                this.m_Gc2MainCamera.enabled = false;
            }

            if (this.m_CinemachineBrain != null)
                this.m_CinemachineBrain.enabled = true;
            if (this.m_RuntimeCameraRig != null)
            {
                this.m_RuntimeCameraRig.SetActive(true);
                if (this.m_CinemachineCamera != null)
                    this.m_CinemachineCamera.PreviousStateIsValid = false;
            }

            this.m_Gc2FirstPersonActive = false;
            this.m_Gc2RearViewReturnRotation = Vector2.zero;
        }

        private void ApplyGc2FirstPersonRearView(bool rearView)
        {
            if (!this.m_Gc2FirstPersonActive ||
                this.m_FirstPersonCameraManager == null ||
                !this.m_FirstPersonCameraManager.IsOwnedBy(this))
            {
                return;
            }

            if (rearView)
            {
                this.m_Gc2RearViewReturnRotation =
                    this.m_FirstPersonCameraManager.GetRotation(this);
                this.m_FirstPersonCameraManager.SetAlignmentSuspended(this, true);
                Vector3 forward = this.m_CarEntry != null &&
                                  this.m_CarEntry.entryParent != null
                    ? this.m_CarEntry.entryParent.forward
                    : this.transform.forward;
                this.m_FirstPersonCameraManager.SetDirection(
                    this,
                    -forward,
                    false
                );
            }
            else
            {
                this.m_FirstPersonCameraManager.SetRotation(
                    this,
                    this.m_Gc2RearViewReturnRotation,
                    false
                );
                this.m_FirstPersonCameraManager.SetAlignmentSuspended(this, false);
            }

            this.m_Gc2MainCamera?.Sync();
        }

        private Transform EnsureRuntimeCameraTarget()
        {
            if (this.m_RuntimeCameraTarget != null) return this.m_RuntimeCameraTarget;

            Transform targetParent = this.m_CameraTarget != null
                ? this.m_CameraTarget
                : this.transform;
            GameObject targetObject = new GameObject("Sim-Cade Camera Target (Runtime)");
            this.m_RuntimeCameraTarget = targetObject.transform;
            this.m_RuntimeCameraTarget.SetParent(targetParent, false);

            Vector3 localCenter = Vector3.zero;
            Quaternion localRotation = Quaternion.identity;
            if (this.m_AutoCenterCameraTarget && this.m_Controller != null &&
                this.m_Controller.Wheels != null && this.m_Controller.Wheels.Length >= 4)
            {
                Transform frontLeft = this.m_Controller.Wheels[0];
                Transform frontRight = this.m_Controller.Wheels[1];
                Transform rearLeft = this.m_Controller.Wheels[2];
                Transform rearRight = this.m_Controller.Wheels[3];
                if (frontLeft != null && frontRight != null &&
                    rearLeft != null && rearRight != null)
                {
                    Vector3 frontCenter = (frontLeft.position + frontRight.position) * 0.5f;
                    Vector3 rearCenter = (rearLeft.position + rearRight.position) * 0.5f;
                    Vector3 wheelCenter = (frontCenter + rearCenter) * 0.5f;
                    localCenter = targetParent.InverseTransformPoint(wheelCenter);

                    Vector3 localForward = targetParent.InverseTransformDirection(
                        frontCenter - rearCenter
                    );
                    localForward.y = 0f;
                    if (localForward.sqrMagnitude > 0.001f)
                    {
                        float yaw = Mathf.Atan2(localForward.x, localForward.z) *
                            Mathf.Rad2Deg;
                        localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
            }

            BoxCollider bodyCollider = this.GetComponent<BoxCollider>();
            if (bodyCollider != null)
            {
                Vector3 colliderCenter = targetParent.InverseTransformPoint(
                    this.transform.TransformPoint(bodyCollider.center)
                );
                localCenter.y = colliderCenter.y;
            }

            this.m_RuntimeCameraTarget.localPosition = localCenter +
                this.m_CameraTargetAdditionalOffset;
            this.m_RuntimeCameraTarget.localRotation = localRotation;

            GameObject orbitObject = new GameObject("Sim-Cade Camera Orbit (Runtime)");
            this.m_RuntimeCameraOrbitTarget = orbitObject.transform;
            this.m_RuntimeCameraOrbitTarget.SetParent(this.m_RuntimeCameraTarget, false);
            this.ApplyCameraOrbit();
            return this.m_RuntimeCameraTarget;
        }

        private void UpdateCameraOrbit()
        {
            // The active GC2 TPS Shot owns mobile/mouse/gamepad pitch and yaw in
            // FPS. Reading the same input here would apply the delta twice.
            if (this.m_Gc2FirstPersonActive) return;
            if (this.m_RuntimeCameraOrbitTarget == null) return;

            if (this.m_RearViewPressed)
            {
                this.m_CameraOrbitYaw = 180f;
                this.m_CameraOrbitVelocity = 0f;
                this.ApplyCameraOrbit();
                return;
            }

            if (!this.m_EnableCameraOrbit) return;

            float yawDelta = 0f;
            bool isInteracting = false;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                isInteracting = true;
                yawDelta += mouse.delta.ReadValue().x * this.m_MouseOrbitSensitivity;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float gamepadYaw = gamepad.rightStick.x.ReadValue();
                if (Mathf.Abs(gamepadYaw) >= this.m_GamepadOrbitDeadZone)
                {
                    isInteracting = true;
                    yawDelta += gamepadYaw * this.m_GamepadOrbitSpeed *
                        Time.unscaledDeltaTime;
                }
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                for (int index = 0; index < touchscreen.touches.Count; index++)
                {
                    var touch = touchscreen.touches[index];
                    if (!touch.press.isPressed) continue;

                    Vector2 position = touch.position.ReadValue();
                    if (position.x < Screen.width * 0.5f) continue;

                    int touchId = touch.touchId.ReadValue();
                    if (EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touchId))
                    {
                        continue;
                    }

                    isInteracting = true;
                    yawDelta += touch.delta.ReadValue().x * this.m_TouchOrbitSensitivity;
                    break;
                }
            }

            if (isInteracting)
            {
                this.m_LastCameraOrbitInputTime = Time.unscaledTime;
                this.m_CameraOrbitVelocity = 0f;
                this.m_CameraOrbitYaw = Mathf.DeltaAngle(
                    0f,
                    this.m_CameraOrbitYaw + yawDelta
                );
            }
            else if (Time.unscaledTime >=
                this.m_LastCameraOrbitInputTime + this.m_OrbitRecenterDelay)
            {
                this.m_CameraOrbitYaw = Mathf.SmoothDampAngle(
                    this.m_CameraOrbitYaw,
                    0f,
                    ref this.m_CameraOrbitVelocity,
                    this.m_OrbitRecenterTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );

                if (Mathf.Abs(this.m_CameraOrbitYaw) < 0.05f)
                {
                    this.m_CameraOrbitYaw = 0f;
                    this.m_CameraOrbitVelocity = 0f;
                }
            }

            this.ApplyCameraOrbit();
        }

        private void ResetCameraOrbit()
        {
            this.m_RearViewPressed = false;
            this.m_RearViewReturnYaw = 0f;
            this.m_CameraOrbitYaw = 0f;
            this.m_CameraOrbitVelocity = 0f;
            this.m_LastCameraOrbitInputTime = Time.unscaledTime;
            this.ApplyCameraOrbit();
        }

        private void ApplyCameraOrbit()
        {
            if (this.m_RuntimeCameraOrbitTarget == null) return;
            this.m_RuntimeCameraOrbitTarget.localRotation = Quaternion.Euler(
                0f,
                this.m_CameraOrbitYaw,
                0f
            );
        }

        private void ResolveUnityCamera()
        {
            if (this.m_CinemachineBrain != null &&
                this.m_UnityCamera != null &&
                this.m_Gc2MainCamera != null)
            {
                return;
            }

            Camera unityCamera = Camera.main;
            if (unityCamera == null) unityCamera = FindFirstObjectByType<Camera>();
            if (unityCamera == null) return;
            this.m_UnityCamera = unityCamera;

            if (this.m_CinemachineBrain == null)
            {
                this.m_CinemachineBrain =
                    unityCamera.GetComponent<CinemachineBrain>();
                this.m_CreatedBrain = this.m_CinemachineBrain == null;
                if (this.m_CreatedBrain)
                {
                    this.m_CinemachineBrain =
                        unityCamera.gameObject.AddComponent<CinemachineBrain>();
                }

                this.m_BrainWasEnabled = this.m_CinemachineBrain.enabled;
            }

            foreach (Behaviour behaviour in unityCamera.GetComponents<Behaviour>())
            {
                if (behaviour != null &&
                    behaviour.GetType().FullName == "GameCreator.Runtime.Cameras.MainCamera")
                {
                    this.m_GameCreatorCamera = behaviour;
                    this.m_Gc2MainCamera = behaviour as MainCamera;
                    // ResolveUnityCamera can run during the mobile prewarm while
                    // Sim-Cade is inactive. Capture GC2's real initial state so
                    // SetCameraActive(false) restores it instead of applying the
                    // default false field value and freezing every Camera Shot.
                    this.m_GameCreatorCameraWasEnabled = behaviour.enabled;
                    break;
                }
            }
        }

        private bool IsPressed(UiButton_SVP button)
        {
            return button != null && button.isPressed;
        }

        private Transform FindChild(string childName)
        {
            foreach (Transform child in this.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child;
            }

            return null;
        }

        private void CacheSteeringHandTargets()
        {
            if (this.m_SteeringWheel == null || this.m_CarEntry == null) return;

            this.m_SteeringLeftHandTarget =
                this.m_CarEntry.steeringWheelLeftHandTarget;
            this.m_SteeringRightHandTarget =
                this.m_CarEntry.steeringWheelRightHandTarget;
            if (this.m_SteeringLeftHandTarget == null ||
                this.m_SteeringRightHandTarget == null)
            {
                return;
            }

            this.m_SteeringLeftHandLocalPosition =
                this.m_SteeringWheel.InverseTransformPoint(
                    this.m_SteeringLeftHandTarget.position
                );
            this.m_SteeringRightHandLocalPosition =
                this.m_SteeringWheel.InverseTransformPoint(
                    this.m_SteeringRightHandTarget.position
                );
            Quaternion inverseWheelRotation =
                Quaternion.Inverse(this.m_SteeringWheel.rotation);
            this.m_SteeringLeftHandLocalRotation = inverseWheelRotation *
                this.m_SteeringLeftHandTarget.rotation;
            this.m_SteeringRightHandLocalRotation = inverseWheelRotation *
                this.m_SteeringRightHandTarget.rotation;
            this.m_SteeringHandTargetsCached = true;
        }

        private void UpdateSteeringHandTargets(float angle)
        {
            if (!this.m_SteeringHandTargetsCached)
            {
                this.CacheSteeringHandTargets();
            }
            if (!this.m_SteeringHandTargetsCached ||
                this.m_SteeringWheel == null)
            {
                return;
            }

            Quaternion desiredLocalRotation = this.m_SteeringWheelInitialRotation *
                Quaternion.AngleAxis(angle, Vector3.forward);
            Quaternion parentRotation = this.m_SteeringWheel.parent != null
                ? this.m_SteeringWheel.parent.rotation
                : Quaternion.identity;
            Quaternion desiredWorldRotation = parentRotation * desiredLocalRotation;
            Vector3 wheelScale = this.m_SteeringWheel.lossyScale;

            this.ApplySteeringHandTargetPose(
                this.m_SteeringLeftHandTarget,
                this.m_SteeringLeftHandLocalPosition,
                this.m_SteeringLeftHandLocalRotation,
                desiredWorldRotation,
                wheelScale
            );
            this.ApplySteeringHandTargetPose(
                this.m_SteeringRightHandTarget,
                this.m_SteeringRightHandLocalPosition,
                this.m_SteeringRightHandLocalRotation,
                desiredWorldRotation,
                wheelScale
            );
        }

        private void ApplySteeringHandTargetPose(
            Transform target,
            Vector3 wheelLocalPosition,
            Quaternion wheelLocalRotation,
            Quaternion desiredWheelWorldRotation,
            Vector3 wheelWorldScale)
        {
            if (target == null) return;

            Vector3 scaledPosition = Vector3.Scale(
                wheelLocalPosition,
                wheelWorldScale
            );
            target.SetPositionAndRotation(
                this.m_SteeringWheel.position +
                    desiredWheelWorldRotation * scaledPosition,
                desiredWheelWorldRotation * wheelLocalRotation
            );
        }

        private void ResetSteeringWheel()
        {
            if (this.m_SteeringWheel != null)
            {
                this.m_SteeringWheel.localRotation = this.m_SteeringWheelInitialRotation;
                this.UpdateSteeringHandTargets(0f);
            }
        }

        private void OnValidate()
        {
            this.m_MaxDamageSteeringBias = Mathf.Clamp(
                this.m_MaxDamageSteeringBias,
                0.05f,
                0.25f
            );
            this.m_DamageSteeringBias = Mathf.Clamp(
                this.m_DamageSteeringBias,
                -this.m_MaxDamageSteeringBias,
                this.m_MaxDamageSteeringBias
            );
            this.m_SteeringHandsMaxAngle = Mathf.Clamp(
                this.m_SteeringHandsMaxAngle,
                15f,
                140f
            );
        }

#if UNITY_EDITOR
        public void Configure(
            SimcadeVehicleController controller,
            GearSystem gearSystem,
            AudioSystem audioSystem,
            GameObject chaseCameraPrefab,
            GameObject mobileInputPrefab,
            Transform steeringWheel)
        {
            this.m_Controller = controller;
            this.m_GearSystem = gearSystem;
            this.m_AudioSystem = audioSystem;
            this.m_ChaseCameraPrefab = chaseCameraPrefab;
            this.m_MobileInputPrefab = mobileInputPrefab;
            this.m_CameraTarget = this.transform;
            this.m_AutoCenterCameraTarget = true;
            this.m_CameraTargetAdditionalOffset = Vector3.zero;
            this.m_EnableCameraOrbit = true;
            this.m_SteeringWheel = steeringWheel;
            this.m_VehicleLights = this.GetComponent<VehicleLights>();
            this.m_Horn = this.GetComponent<SimcadeCarHorn>();
            this.m_DamageSteeringBias = 0f;
            this.m_MaxDamageSteeringBias = 0.16f;
            this.m_SlowModeMaxSpeedKph = 60f;
            this.m_SteeringHandsMaxAngle = 85f;
            this.m_SteeringHandTargetsCached = false;
        }
#endif
    }
}
