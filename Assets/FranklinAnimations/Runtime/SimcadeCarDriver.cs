using Ashsvp;
using GameCreator.Runtime.Characters;
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
        [Header("Sim-Cade")]
        [SerializeField] private SimcadeVehicleController m_Controller;
        [SerializeField] private GearSystem m_GearSystem;
        [SerializeField] private AudioSystem m_AudioSystem;

        [Header("Input")]
        [SerializeField, Min(0f)] private float m_AccelerationResponse = 15f;
        [SerializeField, Min(0f)] private float m_SteeringResponse = 15f;
        [SerializeField, Min(0f)] private float m_SteeringReturnResponse = 25f;

        [Header("Slow Drive")]
        [SerializeField, Min(1f)] private float m_SlowModeMaxSpeedKph = 18f;
        [SerializeField, Range(0.05f, 1f)] private float m_SlowThrottle = 0.35f;
        [SerializeField, Range(0f, 0.95f)] private float m_SlowThrottleTaperStart = 0.65f;
        [SerializeField, Min(0f)] private float m_ExitStopDeceleration = 14f;
        [SerializeField, Min(0f)] private float m_CoastingParkingSpeedKph = 2f;

        [Header("Steering Wheel Visual")]
        [SerializeField] private Transform m_SteeringWheel;
        [SerializeField, Min(0f)] private float m_SteeringWheelMaxAngle = 360f;
        [SerializeField, Min(0f)] private float m_SteeringWheelResponse = 15f;

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
        [SerializeField, Min(0f)] private float m_OrbitRecenterDelay = 0.65f;
        [SerializeField, Min(0.01f)] private float m_OrbitRecenterTime = 0.45f;

        [Header("Sim-Cade Mobile UI")]
        [SerializeField] private GameObject m_MobileInputPrefab;
        [SerializeField] private bool m_UseMobileInput = true;
        [SerializeField] private bool m_ShowMobileInputInEditor;

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
        private bool m_HoldCameraDuringBailout;
        private bool m_KeepEngineRunningAfterBailout;
        private float m_AccelerationInput;
        private float m_SteeringInput;
        private float m_ExitInputAvailableAt;
        private Quaternion m_SteeringWheelInitialRotation;

        private GameObject m_RuntimeCameraRig;
        private Transform m_RuntimeCameraTarget;
        private Transform m_RuntimeCameraOrbitTarget;
        private CinemachineCamera m_CinemachineCamera;
        private CinemachineBrain m_CinemachineBrain;
        private bool m_BrainWasEnabled;
        private bool m_CreatedBrain;
        private Behaviour m_GameCreatorCamera;
        private bool m_GameCreatorCameraWasEnabled;
        private float m_CameraOrbitYaw;
        private float m_CameraOrbitVelocity;
        private float m_LastCameraOrbitInputTime;

        private GameObject m_MobileCanvas;
        private UiButton_SVP m_SteerLeft;
        private UiButton_SVP m_SteerRight;
        private UiButton_SVP m_Accelerate;
        private UiButton_SVP m_SlowAccelerate;
        private UiButton_SVP m_BrakeReverse;
        private UiButton_SVP m_Handbrake;

        public bool IsVehicleEnabled => this.m_IsVehicleEnabled;
        public bool IsPassengerPresentationActive => this.m_IsPassengerPresentation;
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
            this.m_KeepEngineRunningAfterBailout = false;
            this.ResetVirtualInputs();
            this.SendInputs(0f, 0f, true);
            if (this.m_Controller != null) this.m_Controller.enabled = false;
            this.SetCameraActive(false);
            this.SetMobileControlsActive(false);
            this.SetAudioActive(false);
            this.ResetSteeringWheel();
        }

        private void OnDestroy()
        {
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
        }

        public void SetVehicleEnabled(bool state)
        {
            this.SetVehicleEnabled(state, false);
        }

        public void SetVehicleEnabled(bool state, bool preserveMomentum)
        {
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
                this.m_Controller.CanAccelerate = state;
            }

            this.SendInputs(0f, 0f, !state && !preserveMomentum);
            this.UpdateControllerExecutionState();
            this.SetAudioActive(
                state || this.m_KeepEngineRunningAfterBailout
            );
            this.SetMobileControlsActive(state && this.ShouldShowMobileControls());
            if (state)
            {
                this.SetCameraActive(true);
            }
            else if (!this.m_HoldCameraDuringBailout &&
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
        /// Uses the Sim-Cade chase/orbit camera for a rear-seat Player without
        /// granting throttle, steering or mobile driving controls.
        /// </summary>
        public void SetPassengerPresentation(bool active)
        {
            if (active && this.m_IsVehicleEnabled) return;
            this.m_IsPassengerPresentation = active;
            if (active)
            {
                this.m_ExitInputAvailableAt = Time.unscaledTime + 0.5f;
                this.ResetCameraOrbit();
                this.SetCameraActive(true);
            }
            else if (!this.m_IsVehicleEnabled && !this.m_HoldCameraDuringBailout)
            {
                this.SetCameraActive(false);
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
                this.m_Controller.CanAccelerate = true;
            }
            this.SendInputs(0f, 0f, false);
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

        private void ResetVirtualInputs()
        {
            this.m_VirtualAccelerate = false;
            this.m_VirtualBrakeReverse = false;
            this.m_VirtualSteerLeft = false;
            this.m_VirtualSteerRight = false;
            this.m_VirtualHandbrake = false;
            this.m_VirtualSlowAccelerate = false;
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
            this.m_Controller.ProvideInputs(acceleration, steering, handbrake ? 1f : 0f);
        }

        private void UpdateControllerExecutionState()
        {
            if (this.m_Controller == null) return;

            // Keep the component alive while the parked car has a dynamic body.
            // Some of the original interaction setup is initialized while all
            // vehicle behaviours are active. Suspend only during the temporary
            // kinematic window used by the entry/exit animations.
            bool shouldRun = this.m_IsVehicleEnabled ||
                (this.m_Rigidbody != null && !this.m_Rigidbody.isKinematic);
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
                Transform cameraTarget = this.EnsureRuntimeCameraTarget();
                this.m_CinemachineCamera.Follow = this.m_RuntimeCameraOrbitTarget != null
                    ? this.m_RuntimeCameraOrbitTarget
                    : cameraTarget;
                this.m_CinemachineCamera.LookAt = cameraTarget;
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
            if (!this.m_EnableCameraOrbit || this.m_RuntimeCameraOrbitTarget == null) return;

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
            if (this.m_CinemachineBrain != null) return;

            Camera unityCamera = Camera.main;
            if (unityCamera == null) unityCamera = FindFirstObjectByType<Camera>();
            if (unityCamera == null) return;

            this.m_CinemachineBrain = unityCamera.GetComponent<CinemachineBrain>();
            this.m_CreatedBrain = this.m_CinemachineBrain == null;
            if (this.m_CreatedBrain)
            {
                this.m_CinemachineBrain = unityCamera.gameObject.AddComponent<CinemachineBrain>();
            }

            this.m_BrainWasEnabled = this.m_CinemachineBrain.enabled;
            foreach (Behaviour behaviour in unityCamera.GetComponents<Behaviour>())
            {
                if (behaviour != null &&
                    behaviour.GetType().FullName == "GameCreator.Runtime.Cameras.MainCamera")
                {
                    this.m_GameCreatorCamera = behaviour;
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

        private void ResetSteeringWheel()
        {
            if (this.m_SteeringWheel != null)
            {
                this.m_SteeringWheel.localRotation = this.m_SteeringWheelInitialRotation;
            }
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
        }
#endif
    }
}
