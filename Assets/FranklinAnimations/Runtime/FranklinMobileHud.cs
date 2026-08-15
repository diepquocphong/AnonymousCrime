using System;
using System.Collections.Generic;
using FranklinGame.Animations;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FranklinGame.UI
{
    /// <summary>
    /// Creates the Franklin touch HUD from the ImageGen sprites in Resources. It reuses the
    /// existing Tactile movement stick while on foot and swaps to the shared car controls
    /// while driving either a Sim-Cade car or an RVR bike.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class FranklinMobileHud : MonoBehaviour
    {
        private const float REFERENCE_REFRESH_SECONDS = 0.5f;
        private const string RESOURCE_ROOT = "FranklinMobileUI/";
        private const float BIKE_SPEED_UPDATE_SECONDS = 0.1f;
        private const float BIKE_FUEL_GAUGE_WIDTH = 74f;
        private const float BIKE_FUEL_GAUGE_HEIGHT = 152f;
        private const float BIKE_HEALTH_LAYOUT_WIDTH = 104f;
        private const float BIKE_HEALTH_LAYOUT_HEIGHT = 230f;
        private const float BIKE_HEALTH_GAUGE_WIDTH = 40f;
        private const float BIKE_HEALTH_GAUGE_HEIGHT = 184f;
        private const float BIKE_HEALTH_BAR_OFFSET_X = 18f;
        private const float BIKE_HEALTH_BAR_OFFSET_Y = -23f;

        private static readonly Color VEHICLE_HEALTH_SKY_BLUE =
            new Color(0.16f, 0.72f, 1f, 1f);
        private static readonly Color SPEED_BACKGROUND_COLOR =
            new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color SPEED_BACKGROUND_BORDER_COLOR =
            new Color(1f, 1f, 1f, 0.12f);
        private static readonly Vector2 SPEED_BACKGROUND_MAX_SIZE =
            new Vector2(176f, 48f);
        private static readonly Vector2 SPEED_BACKGROUND_OFFSET =
            new Vector2(0f, 3f);

        private static readonly Dictionary<string, Sprite> SPRITES = new();
        private static readonly HashSet<object> CONTROL_SUPPRESSION_OWNERS = new();
        private static readonly HashSet<object> FAST_MOVEMENT_SUPPRESSION_OWNERS =
            new();
        private static readonly HashSet<object> FAST_MOVEMENT_BYPASS_OWNERS =
            new();
        private static FranklinMobileHud s_Instance;
        private static bool s_ControlsSuppressed;

        [Header("Bike Curved Health + Fuel UI")]
        [SerializeField] private Sprite m_BikeGaugeSprite;
        [SerializeField] private Material m_BikeGaugeAlphaTintMaterial;
        [SerializeField] private Vector2 m_BikeFuelGaugeOffset = new Vector2(-45f, -112f);
        [Tooltip("Khoảng cách world-space sang phải thân Bike của thanh máu.")]
        [SerializeField, Min(0.5f)] private float m_BikeHealthWorldRightOffset = 2.35f;
        [SerializeField] private float m_BikeHealthWorldHeight = 0.82f;
        [Tooltip("Bù pixel của thanh máu sau khi mirror vị trí thanh xăng.")]
        [SerializeField] private Vector2 m_BikeHealthScreenOffset = Vector2.zero;

        [Header("Bike Speed UI - Font (Editable)")]
        [SerializeField] private Font m_BikeSpeedFont;
        [SerializeField, Range(16, 96)] private int m_BikeSpeedValueFontSize = 56;
        [SerializeField, Range(10, 64)] private int m_BikeSpeedUnitFontSize = 29;
        [SerializeField] private FontStyle m_BikeSpeedFontStyle = FontStyle.Normal;
        [SerializeField] private Color m_BikeSpeedTextColor = Color.white;
        [SerializeField] private Color m_BikeSpeedShadowColor =
            new Color(0f, 0f, 0f, 0.34f);
        [SerializeField] private Vector2 m_BikeSpeedShadowOffset = new Vector2(1f, -1f);

        [Header("Bike Speed UI - Position (Editable)")]
        [Tooltip("Bật: số km/h bám theo vị trí world-space bên trái Bike như Car. Tắt: dùng Fixed Screen Position.")]
        [SerializeField] private bool m_BikeSpeedFollowBike = true;
        [Tooltip("Vị trí UI tính từ tâm màn hình khi Follow Bike bị tắt.")]
        [SerializeField] private Vector2 m_BikeSpeedFixedScreenPosition = Vector2.zero;
        [SerializeField] private Vector2 m_BikeSpeedRectSize = new Vector2(286f, 78f);
        [Tooltip("Khoảng cách world-space sang trái thân Bike.")]
        [SerializeField, Min(0.5f)] private float m_BikeSpeedWorldLeftOffset = 2.35f;
        [Tooltip("Độ cao world-space của target tốc độ so với gốc Bike.")]
        [SerializeField] private float m_BikeSpeedWorldHeight = 0.82f;
        [Tooltip("Tinh chỉnh cuối bằng pixel sau khi vị trí Bike được project lên Canvas.")]
        [SerializeField] private Vector2 m_BikeSpeedScreenOffset = new Vector2(0f, -130f);
        [SerializeField, Range(0.03f, 0.3f)] private float m_BikeSpeedFollowSmooth = 0.11f;

        private RectTransform m_OnFootGroup;
        private RectTransform m_VehicleGroup;
        private FranklinHudButton m_JogButton;
        private FranklinHudButton m_SprintButton;
        private FranklinHudButton m_PointDirectionButton;
        private FranklinHudButton m_ObjectDirectionButton;
        private GameObject m_MeleeButton;
        private RectTransform m_BikeHealthRoot;
        private Image m_BikeHealthFillImage;
        private RectTransform m_BikeFuelRoot;
        private Image m_BikeFuelFillImage;
        private RectTransform m_BikeSpeedBackground;
        private RectTransform m_BikeSpeedRoot;
        private Text m_BikeSpeedText;
        private GameObject m_EnterVehicleButton;
        private FranklinHudButton m_SlowDriveButton;
        private FranklinHudButton m_BikeHeadlightButton;
        private FranklinHudButton m_CarHornButton;
        private FranklinHudButton m_CarRearViewButton;
        private FranklinHudButton m_CarCameraModeButton;
        private FranklinHudButton m_BikeWheelieButton;
        private FranklinHudButton m_BikeBurnoutButton;
        private FranklinHudButton m_BikeHelmetButton;
        private FranklinHudButton m_OnFootHelmetButton;
        private FranklinBikeHelmetController m_PlayerHelmetController;
        private FranklinAnimationBridge m_MovementBridge;
        private FranklinObjectDirectionToggle m_ObjectDirectionToggle;
        private FranklinCameraPointing m_CameraPointing;
        private FranklinVehicleInteractionManager m_VehicleInteraction;
        private IRvrVehicleInputController m_ActiveDriver;
        private FranklinArcadeBikeDriver m_BikeHealthDriver;
        private FranklinBikeHealth m_ActiveBikeHealth;
        private FranklinArcadeBikeDriver m_BikeFuelDriver;
        private FranklinBikeFuel m_ActiveBikeFuel;
        private FranklinArcadeBikeDriver m_BikeSpeedDriver;
        private Camera m_BikeSpeedCamera;
        private GameObject m_TactileCanvas;
        private GameObject m_TactileMoveStick;
        private float m_NextReferenceRefresh;
        private float m_NextBikeSpeedUpdate;
        private int m_LastDisplayedBikeSpeed = int.MinValue;
        private Vector2 m_BikeSpeedPosition;
        private Vector2 m_BikeSpeedVelocity;
        private Vector2 m_BikeHealthPosition;
        private Vector2 m_BikeHealthVelocity;
        private float m_BikeFuelTarget;
        private float m_BikeFuelVelocity;
        private bool m_HasBikeFuelTarget;
        private bool m_BikeTelemetrySuppressed;
        private bool m_WasDriving;
        private bool m_WasPassengerMode;
        private bool m_HasAppliedMode;
        private bool m_UsesCanvasPlayerControl;
        private bool m_HasAppliedSuppression;
        private bool m_HasAppliedFastMovementSuppression;
        private bool m_WasFastMovementBypassed;
        private bool m_HasMeleeVisibilitySnapshot;
        private bool m_MeleeButtonWasActive;
        private bool m_HasBikeSpeedPosition;
        private bool m_HasBikeHealthPosition;

        public static bool IsActive => s_Instance != null &&
                                       s_Instance.isActiveAndEnabled;
        public static bool ControlsSuppressed => s_ControlsSuppressed ||
                                                 CONTROL_SUPPRESSION_OWNERS.Count > 0;
        public static bool FastMovementSuppressed =>
            FAST_MOVEMENT_SUPPRESSION_OWNERS.Count > 0;
        private static bool FastMovementBypassed =>
            FastMovementSuppressed &&
            FAST_MOVEMENT_SUPPRESSION_OWNERS.IsSubsetOf(
                FAST_MOVEMENT_BYPASS_OWNERS
            );

        /// <summary>Raised when the HUD phone button is pressed.</summary>
        public event Action EventPhoneRequested;
        /// <summary>Raised when the HUD home button is pressed.</summary>
        public event Action EventHomeRequested;
        /// <summary>Raised when the HUD settings button is pressed.</summary>
        public event Action EventSettingsRequested;

        public static void SetControlsSuppressed(bool suppressed)
        {
            s_ControlsSuppressed = suppressed;
            ApplyCurrentControlSuppression();
        }

        /// <summary>
        /// Suppresses mobile controls for one UI/system without overriding other
        /// active suppression owners such as death or vehicle destruction.
        /// </summary>
        public static void AcquireControlsSuppression(object owner)
        {
            if (owner == null) return;
            CONTROL_SUPPRESSION_OWNERS.Add(owner);
            ApplyCurrentControlSuppression();
        }

        /// <summary>Releases only the suppression previously acquired by owner.</summary>
        public static void ReleaseControlsSuppression(object owner)
        {
            if (owner == null) return;
            CONTROL_SUPPRESSION_OWNERS.Remove(owner);
            ApplyCurrentControlSuppression();
        }

        /// <summary>
        /// Hides Jog/Sprint and clears their held/auto-run state while keeping
        /// ordinary walk input available for presentation systems such as Phone.
        /// </summary>
        public static void AcquireFastMovementSuppression(object owner)
        {
            if (owner == null) return;
            FAST_MOVEMENT_SUPPRESSION_OWNERS.Add(owner);
            ApplyCurrentControlSuppression();
        }

        public static void ReleaseFastMovementSuppression(object owner)
        {
            if (owner == null) return;
            FAST_MOVEMENT_SUPPRESSION_OWNERS.Remove(owner);
            FAST_MOVEMENT_BYPASS_OWNERS.Remove(owner);
            ApplyCurrentControlSuppression();
        }

        /// <summary>
        /// Keeps non-movement phone controls suppressed but temporarily exposes
        /// Jog/Sprint for a suppression owner, such as live BACK camera mode.
        /// A second suppression owner without a bypass still takes priority.
        /// </summary>
        public static void SetFastMovementSuppressionBypassed(
            object owner,
            bool bypassed)
        {
            if (owner == null) return;
            if (bypassed) FAST_MOVEMENT_BYPASS_OWNERS.Add(owner);
            else FAST_MOVEMENT_BYPASS_OWNERS.Remove(owner);
            ApplyCurrentControlSuppression();
        }

        private static void ApplyCurrentControlSuppression()
        {
            if (s_Instance == null) return;
            if (ControlsSuppressed) s_Instance.ApplyControlsSuppressed();
            else s_Instance.ReleaseControlsSuppression();
            if (FastMovementSuppressed)
                s_Instance.ApplyFastMovementSuppressed();
            else s_Instance.ReleaseFastMovementSuppression();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_ControlsSuppressed = false;
            CONTROL_SUPPRESSION_OWNERS.Clear();
            FAST_MOVEMENT_SUPPRESSION_OWNERS.Clear();
            FAST_MOVEMENT_BYPASS_OWNERS.Clear();
            SPRITES.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<FranklinMobileHud>() != null) return;

            FranklinAnimationBridge bridge = FindFirstObjectByType<FranklinAnimationBridge>();
            FranklinVehicleInteractionManager interaction =
                FindFirstObjectByType<FranklinVehicleInteractionManager>();
            if (bridge == null && interaction == null) return;

            GameObject hudObject = new GameObject(
                "Franklin Mobile HUD",
                typeof(RectTransform)
            );
            hudObject.AddComponent<FranklinMobileHud>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            if (!this.TryBindPrefabControls())
            {
                if (this.GetComponent<Canvas>() == null)
                {
                    this.BuildCanvas();
                }
                else
                {
                    Debug.LogError(
                        "CanvasPlayerControl is missing the Franklin mobile HUD controls.",
                        this
                    );
                }
            }
            this.EnsureEventSystem();
            this.RefreshReferences(true);
            if (ControlsSuppressed) this.ApplyControlsSuppressed();
        }

        private void OnDestroy()
        {
            this.BindBikeHealth(null);
            this.BindBikeFuel(null);
            this.UpdateBikeSpeed(null);
            this.ReleaseMovementInputs();
            this.ReleaseVehicleInputs(this.m_ActiveDriver);
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            this.RefreshReferences(false);
            this.UpdateBikeFuelFill();

            if (ControlsSuppressed)
            {
                this.ApplyControlsSuppressed();
                return;
            }
            if (this.m_HasAppliedSuppression) this.ReleaseControlsSuppression();
            if (FastMovementSuppressed) this.ApplyFastMovementSuppressed();
            else this.ReleaseFastMovementSuppression();

            bool isBikePassenger = this.TryGetPlayerBikePassenger(out _, out _);
            bool isBikeDriver = this.IsPlayerBikeDriver();
            bool isDriving = IsUsableDriver(this.m_ActiveDriver) &&
                             (this.m_ActiveDriver is not FranklinArcadeBikeDriver ||
                              isBikeDriver ||
                              isBikePassenger);
            bool isPassengerMode =
                (this.m_ActiveDriver is SimcadeCarDriver car &&
                 car.IsPassengerPresentationActive) ||
                isBikePassenger;
            if (!this.m_HasAppliedMode || isDriving != this.m_WasDriving)
            {
                this.ApplyMode(isDriving);
            }
            if (!this.m_HasAppliedMode || isPassengerMode != this.m_WasPassengerMode)
                this.ApplyPassengerControlVisibility(isPassengerMode);

            this.UpdateEnterVehicleButton(isDriving);
            this.UpdateBikeOnlyControls(isDriving);
            this.UpdateHelmetControls(isDriving);
        }

        private void ApplyControlsSuppressed()
        {
            if (!this.m_HasAppliedSuppression)
            {
                this.ReleaseMovementInputs();
                this.ReleaseVehicleInputs(this.m_ActiveDriver, true);
            }
            if (this.m_OnFootGroup != null) this.m_OnFootGroup.gameObject.SetActive(false);
            if (this.m_VehicleGroup != null) this.m_VehicleGroup.gameObject.SetActive(false);
            if (this.m_EnterVehicleButton != null)
                this.m_EnterVehicleButton.SetActive(false);
            if (this.m_TactileMoveStick != null) this.m_TactileMoveStick.SetActive(false);
            if (!this.m_UsesCanvasPlayerControl && this.m_TactileCanvas != null &&
                this.m_TactileCanvas != this.gameObject)
            {
                this.m_TactileCanvas.SetActive(false);
            }
            this.m_HasAppliedSuppression = true;
        }

        private void ReleaseControlsSuppression()
        {
            if (!this.m_HasAppliedSuppression) return;
            this.m_HasAppliedSuppression = false;
            this.m_HasAppliedMode = false;
            if (!this.m_UsesCanvasPlayerControl && this.m_TactileCanvas != null &&
                this.m_TactileCanvas != this.gameObject)
            {
                this.m_TactileCanvas.SetActive(true);
            }
        }

        private void ApplyFastMovementSuppressed()
        {
            bool bypassFastMovement = FastMovementBypassed;
            if (!this.m_HasAppliedFastMovementSuppression ||
                this.m_WasFastMovementBypassed && !bypassFastMovement)
            {
                this.ReleaseMovementInputs();
            }
            if (!this.m_HasAppliedFastMovementSuppression)
            {
                this.m_ObjectDirectionToggle?.SetObjectDirectionEnabled(false);
            }
            SetButtonActive(this.m_JogButton, bypassFastMovement);
            SetButtonActive(this.m_SprintButton, bypassFastMovement);
            SetButtonActive(this.m_PointDirectionButton, false);
            SetButtonActive(this.m_ObjectDirectionButton, false);
            this.SuppressMeleeButton();
            this.m_WasFastMovementBypassed = bypassFastMovement;
            this.m_HasAppliedFastMovementSuppression = true;
        }

        private void ReleaseFastMovementSuppression()
        {
            if (!this.m_HasAppliedFastMovementSuppression) return;
            this.m_HasAppliedFastMovementSuppression = false;
            this.m_WasFastMovementBypassed = false;
            SetButtonActive(this.m_JogButton, true);
            SetButtonActive(this.m_SprintButton, true);
            SetButtonActive(this.m_PointDirectionButton, true);
            SetButtonActive(this.m_ObjectDirectionButton, true);
            if (this.m_HasMeleeVisibilitySnapshot && this.m_MeleeButton != null)
                this.m_MeleeButton.SetActive(this.m_MeleeButtonWasActive);
            this.m_HasMeleeVisibilitySnapshot = false;
        }

        private void SuppressMeleeButton()
        {
            if (this.m_MeleeButton == null)
                this.m_MeleeButton = this.FindChild("Fight")?.gameObject;
            if (this.m_MeleeButton == null) return;

            if (!this.m_HasMeleeVisibilitySnapshot)
            {
                this.m_MeleeButtonWasActive = this.m_MeleeButton.activeSelf;
                this.m_HasMeleeVisibilitySnapshot = true;
            }
            this.m_MeleeButton.SetActive(false);
        }

        internal void SetAction(FranklinHudAction action, bool active)
        {
            switch (action)
            {
                case FranklinHudAction.Jog:
                    this.m_MovementBridge?.SetVirtualJogInput(active);
                    break;
                case FranklinHudAction.Sprint:
                    this.m_MovementBridge?.SetVirtualSprintInput(active);
                    break;
                case FranklinHudAction.Jump:
                    if (active) this.m_MovementBridge?.RequestVirtualJump();
                    break;
                case FranklinHudAction.PointDirection:
                    this.m_CameraPointing?.SetVirtualPointInput(active);
                    break;
                case FranklinHudAction.ObjectDirection:
                    this.m_ObjectDirectionToggle?.SetObjectDirectionEnabled(active);
                    break;
                case FranklinHudAction.Phone:
                    if (active) this.EventPhoneRequested?.Invoke();
                    break;
                case FranklinHudAction.Home:
                    if (active) this.EventHomeRequested?.Invoke();
                    break;
                case FranklinHudAction.Settings:
                    if (active) this.EventSettingsRequested?.Invoke();
                    break;
                case FranklinHudAction.SteerLeft:
                    this.m_ActiveDriver?.SetVirtualSteerLeftInput(active);
                    break;
                case FranklinHudAction.SteerRight:
                    this.m_ActiveDriver?.SetVirtualSteerRightInput(active);
                    break;
                case FranklinHudAction.Accelerate:
                    this.m_ActiveDriver?.SetVirtualAccelerateInput(active);
                    break;
                case FranklinHudAction.BrakeReverse:
                    this.m_ActiveDriver?.SetVirtualBrakeReverseInput(active);
                    break;
                case FranklinHudAction.Handbrake:
                    this.m_ActiveDriver?.SetVirtualHandbrakeInput(active);
                    break;
                case FranklinHudAction.SlowDrive:
                    this.m_ActiveDriver?.SetVirtualSlowAccelerateInput(active);
                    break;
                case FranklinHudAction.BikeHeadlight:
                    if (this.m_ActiveDriver is FranklinArcadeBikeDriver bikeDriver)
                    {
                        bikeDriver.SetHeadlightEnabled(active);
                    }
                    else if (this.m_ActiveDriver is SimcadeCarDriver carDriver &&
                             carDriver != null)
                    {
                        carDriver.SetHeadlightEnabled(active);
                    }
                    break;
                case FranklinHudAction.CarHorn:
                    if (this.m_ActiveDriver is FranklinArcadeBikeDriver hornBike)
                    {
                        hornBike.SetHornPressed(active);
                    }
                    else if (this.m_ActiveDriver is SimcadeCarDriver hornCar &&
                        hornCar != null)
                    {
                        hornCar.SetHornPressed(active);
                    }
                    break;
                case FranklinHudAction.CarRearView:
                    if (this.m_ActiveDriver is SimcadeCarDriver rearViewCar &&
                        rearViewCar != null)
                    {
                        rearViewCar.SetRearViewPressed(active);
                    }
                    break;
                case FranklinHudAction.CarCameraMode:
                    if (this.m_ActiveDriver is FranklinArcadeBikeDriver cameraModeBike)
                    {
                        cameraModeBike.SetFirstPersonView(active);
                    }
                    else if (this.m_ActiveDriver is SimcadeCarDriver cameraModeCar &&
                        cameraModeCar != null)
                    {
                        cameraModeCar.SetFirstPersonView(active);
                    }
                    break;
                case FranklinHudAction.BikeWheelie:
                    if (this.m_ActiveDriver is FranklinArcadeBikeDriver wheelieDriver)
                    {
                        wheelieDriver.SetVirtualWheelieInput(active);
                    }
                    break;
                case FranklinHudAction.BikeBurnout:
                    if (this.m_ActiveDriver is FranklinArcadeBikeDriver burnoutDriver)
                    {
                        burnoutDriver.SetVirtualBurnoutInput(active);
                    }
                    break;
                case FranklinHudAction.BikeHelmet:
                    if (active) this.ResolvePlayerHelmetController()?.ToggleHelmet();
                    break;
                case FranklinHudAction.VehicleInteraction:
                    if (!active) break;
                    if (this.TryGetPlayerBikePassenger(
                            out FranklinBikePassengerSeat bikePassengerSeat,
                            out Character bikePassenger))
                    {
                        bikePassengerSeat.RequestExit(bikePassenger);
                    }
                    else if (this.m_ActiveDriver is SimcadeCarDriver passengerCar &&
                        passengerCar.IsPassengerPresentationActive)
                    {
                        passengerCar.RequestExit();
                    }
                    else if (this.m_ActiveDriver != null &&
                             this.m_ActiveDriver.IsVehicleEnabled)
                    {
                        this.m_ActiveDriver.RequestExit();
                    }
                    else
                    {
                        this.m_VehicleInteraction?.RequestVehicleInteraction();
                    }
                    break;
            }
        }

        internal void SetButtonPointerState(FranklinHudAction action, bool pressed)
        {
            // Franklin Shooter Fire uses FranklinShooterTouchButton and therefore
            // never enters this path. Regular drive controls are filtered by the
            // exact UI touch in FranklinFirstPersonCameraInput, so they must not
            // suppress a second free finger that is orbiting. Rear View is the
            // only exclusive camera action and deliberately reserves all orbit.
            if (action != FranklinHudAction.CarRearView) return;

            if (this.m_ActiveDriver is FranklinArcadeBikeDriver bikeDriver &&
                bikeDriver.IsFirstPersonViewActive)
            {
                bikeDriver.SetFirstPersonOrbitSuppressed(pressed);
            }
            else if (this.m_ActiveDriver is SimcadeCarDriver carDriver &&
                     carDriver.IsFirstPersonViewActive)
            {
                carDriver.SetFirstPersonOrbitSuppressed(pressed);
            }
        }

        internal bool ShouldPreserveToggleStateOnDisable(FranklinHudAction action)
        {
            if (action != FranklinHudAction.CarCameraMode) return false;

            // Camera mode is a saved preference. Hiding the shared button during
            // exit, a modal menu or HUD rebuild must not synthesize a user toggle
            // to TPS and overwrite that preference.
            return this.m_ActiveDriver is FranklinArcadeBikeDriver ||
                   this.m_ActiveDriver is SimcadeCarDriver;
        }

        private void BuildCanvas()
        {
            Canvas canvas = this.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;

            CanvasScaler scaler = this.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            this.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = this.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            this.m_OnFootGroup = this.CreateGroup("On Foot Controls", canvasRect);
            this.m_VehicleGroup = this.CreateGroup("Vehicle Controls", canvasRect);

            this.m_JogButton = this.CreateButton(
                this.m_OnFootGroup,
                "Jog",
                "player-movement-0",
                FranklinHudAction.Jog,
                new Vector2(0f, 0f),
                new Vector2(369.2f, 568.43f),
                new Vector2(165f, 165f)
            );
            this.m_SprintButton = this.CreateButton(
                this.m_OnFootGroup,
                "Sprint",
                "player-movement-1",
                FranklinHudAction.Sprint,
                new Vector2(0f, 0f),
                new Vector2(173.4f, 561.13f),
                new Vector2(165f, 165f)
            );
            this.CreateButton(
                this.m_OnFootGroup,
                "Jump",
                "player-jump",
                FranklinHudAction.Jump,
                new Vector2(1f, 0f),
                new Vector2(-505f, 170f),
                new Vector2(165f, 165f)
            );
            this.m_PointDirectionButton = this.CreateButton(
                this.m_OnFootGroup,
                "Point Direction",
                "player-point-direction",
                FranklinHudAction.PointDirection,
                new Vector2(0f, 0f),
                new Vector2(565f, 568.43f),
                new Vector2(120f, 120f)
            );
            this.m_ObjectDirectionButton = this.CreateButton(
                this.m_OnFootGroup,
                "Object Direction",
                "player-object-direction",
                FranklinHudAction.ObjectDirection,
                new Vector2(0f, 0f),
                new Vector2(369.2f, 443f),
                new Vector2(120f, 120f),
                true
            );
            this.m_EnterVehicleButton = this.CreateButton(
                this.m_OnFootGroup,
                "Enter Vehicle",
                "vehicle-enter",
                FranklinHudAction.VehicleInteraction,
                new Vector2(0.72f, 0.4f),
                Vector2.zero,
                new Vector2(190f, 190f)
            ).gameObject;
            this.m_EnterVehicleButton.SetActive(false);
            this.m_OnFootHelmetButton = this.CreateButton(
                this.m_OnFootGroup,
                "Bike Helmet On Foot",
                "vehicle-control-helmet",
                FranklinHudAction.BikeHelmet,
                new Vector2(1f, 1f),
                new Vector2(-315f, -385f),
                new Vector2(165f, 165f)
            );
            this.m_OnFootHelmetButton.gameObject.SetActive(false);

            this.CreateButton(
                this.m_VehicleGroup,
                "Steer Left",
                "vehicle-control-0",
                FranklinHudAction.SteerLeft,
                new Vector2(0f, 0f),
                new Vector2(180f, 190f),
                new Vector2(263f, 263f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Steer Right",
                "vehicle-control-1",
                FranklinHudAction.SteerRight,
                new Vector2(0f, 0f),
                new Vector2(450f, 190f),
                new Vector2(263f, 263f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Accelerate",
                "vehicle-control-2",
                FranklinHudAction.Accelerate,
                new Vector2(1f, 0f),
                new Vector2(-155f, 305f),
                new Vector2(288f, 288f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Brake Reverse",
                "vehicle-control-3",
                FranklinHudAction.BrakeReverse,
                new Vector2(1f, 0f),
                new Vector2(-455f, 130f),
                new Vector2(225f, 225f)
            );
            this.m_BikeHelmetButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Helmet",
                "vehicle-control-helmet",
                FranklinHudAction.BikeHelmet,
                new Vector2(1f, 1f),
                new Vector2(-285f, -385f),
                new Vector2(155f, 155f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Handbrake",
                "vehicle-control-4",
                FranklinHudAction.Handbrake,
                new Vector2(1f, 0f),
                new Vector2(-455f, 330f),
                new Vector2(169f, 169f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Exit Vehicle",
                "vehicle-control-5",
                FranklinHudAction.VehicleInteraction,
                new Vector2(1f, 1f),
                new Vector2(-115f, -385f),
                new Vector2(155f, 155f)
            );
            this.m_CarHornButton = this.CreateButton(
                this.m_VehicleGroup,
                "Car Horn",
                "vehicle-control-horn",
                FranklinHudAction.CarHorn,
                new Vector2(1f, 1f),
                new Vector2(-625f, -575f),
                new Vector2(155f, 155f)
            );
            this.m_CarRearViewButton = this.CreateButton(
                this.m_VehicleGroup,
                "Car Rear View",
                "vehicle-control-rear-view",
                FranklinHudAction.CarRearView,
                new Vector2(1f, 1f),
                new Vector2(-295f, -385f),
                new Vector2(125f, 125f)
            );
            this.m_CarCameraModeButton = this.CreateButton(
                this.m_VehicleGroup,
                "Car Camera Mode",
                "vehicle-control-camera-mode",
                FranklinHudAction.CarCameraMode,
                new Vector2(1f, 1f),
                new Vector2(-455f, -385f),
                new Vector2(155f, 155f),
                true
            );
            this.m_SlowDriveButton = this.CreateButton(
                this.m_VehicleGroup,
                "Slow Drive",
                "vehicle-control-slow",
                FranklinHudAction.SlowDrive,
                new Vector2(1f, 0f),
                new Vector2(-155f, 82f),
                new Vector2(163f, 163f)
            );
            this.m_BikeHeadlightButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Headlight",
                "vehicle-control-headlight",
                FranklinHudAction.BikeHeadlight,
                new Vector2(1f, 1f),
                new Vector2(-115f, -575f),
                new Vector2(155f, 155f),
                true
            );
            this.m_BikeWheelieButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Wheelie",
                "vehicle-control-wheelie",
                FranklinHudAction.BikeWheelie,
                new Vector2(1f, 1f),
                new Vector2(-285f, -575f),
                new Vector2(155f, 155f)
            );
            this.m_BikeBurnoutButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Burnout",
                "vehicle-control-burnout",
                FranklinHudAction.BikeBurnout,
                new Vector2(1f, 1f),
                new Vector2(-455f, -575f),
                new Vector2(155f, 155f)
            );
            this.EnsureBikeSpeedUi();
            this.EnsureBikeHealthUi();
            this.EnsureBikeFuelUi();
        }

        private bool TryBindPrefabControls()
        {
            Transform onFootTransform = this.transform.Find("Franklin On Foot Controls");
            Transform vehicleTransform = this.transform.Find("Franklin Vehicle Controls");
            if (onFootTransform is not RectTransform onFoot ||
                vehicleTransform is not RectTransform vehicle)
            {
                return false;
            }

            this.m_OnFootGroup = onFoot;
            this.m_VehicleGroup = vehicle;
            this.m_JogButton = FindButton("Jog");
            this.m_SprintButton = FindButton("Sprint");
            this.m_PointDirectionButton = FindButton("Point Direction");
            this.m_ObjectDirectionButton = FindButton("Object Direction");
            this.m_MeleeButton = FindChild("Fight")?.gameObject;
            this.m_EnterVehicleButton = FindChild("Enter Vehicle")?.gameObject;
            this.m_SlowDriveButton = FindButton("Slow Drive");
            this.m_BikeHeadlightButton = FindButton("Bike Headlight");
            this.m_CarHornButton = FindButton("Car Horn");
            this.m_CarRearViewButton = FindButton("Car Rear View");
            this.m_CarCameraModeButton = FindButton("Car Camera Mode");
            this.m_BikeWheelieButton = FindButton("Bike Wheelie");
            this.m_BikeBurnoutButton = FindButton("Bike Burnout");
            this.m_BikeHelmetButton = FindButton("Bike Helmet");
            this.m_OnFootHelmetButton = this.m_OnFootGroup
                .Find("Bike Helmet On Foot")?.GetComponent<FranklinHudButton>();

            if (this.m_PointDirectionButton == null)
            {
                this.m_PointDirectionButton = this.CreateButton(
                    this.m_OnFootGroup,
                    "Point Direction",
                    "player-point-direction",
                    FranklinHudAction.PointDirection,
                    new Vector2(0f, 0f),
                    new Vector2(565f, 568.43f),
                    new Vector2(120f, 120f)
                );
            }

            if (this.m_ObjectDirectionButton == null)
            {
                this.m_ObjectDirectionButton = this.CreateButton(
                    this.m_OnFootGroup,
                    "Object Direction",
                    "player-object-direction",
                    FranklinHudAction.ObjectDirection,
                    new Vector2(0f, 0f),
                    new Vector2(369.2f, 443f),
                    new Vector2(120f, 120f),
                    true
                );
            }

            if (this.m_OnFootHelmetButton == null)
            {
                this.m_OnFootHelmetButton = this.CreateButton(
                this.m_OnFootGroup,
                    "Bike Helmet On Foot",
                    "vehicle-control-helmet",
                    FranklinHudAction.BikeHelmet,
                    new Vector2(1f, 1f),
                    new Vector2(-315f, -385f),
                    new Vector2(165f, 165f)
                );
                this.m_OnFootHelmetButton.gameObject.SetActive(false);
            }

            if (this.m_SlowDriveButton == null)
            {
                this.m_SlowDriveButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Slow Drive",
                    "vehicle-control-slow",
                    FranklinHudAction.SlowDrive,
                    new Vector2(1f, 0f),
                    new Vector2(-155f, 82f),
                    new Vector2(163f, 163f)
                );
            }

            if (this.m_BikeHeadlightButton == null)
            {
                this.m_BikeHeadlightButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Bike Headlight",
                    "vehicle-control-headlight",
                    FranklinHudAction.BikeHeadlight,
                    new Vector2(1f, 1f),
                    new Vector2(-115f, -575f),
                    new Vector2(155f, 155f),
                    true
                );
            }

            if (this.m_CarHornButton == null)
            {
                this.m_CarHornButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Car Horn",
                    "vehicle-control-horn",
                    FranklinHudAction.CarHorn,
                    new Vector2(1f, 1f),
                    new Vector2(-625f, -575f),
                    new Vector2(155f, 155f)
                );
            }

            if (this.m_CarRearViewButton == null)
            {
                this.m_CarRearViewButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Car Rear View",
                    "vehicle-control-rear-view",
                    FranklinHudAction.CarRearView,
                    new Vector2(1f, 1f),
                    new Vector2(-295f, -385f),
                    new Vector2(125f, 125f)
                );
            }

            if (this.m_CarCameraModeButton == null)
            {
                this.m_CarCameraModeButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Car Camera Mode",
                    "vehicle-control-camera-mode",
                    FranklinHudAction.CarCameraMode,
                    new Vector2(1f, 1f),
                    new Vector2(-455f, -385f),
                    new Vector2(155f, 155f),
                    true
                );
            }

            if (this.m_BikeWheelieButton == null)
            {
                this.m_BikeWheelieButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Bike Wheelie",
                    "vehicle-control-wheelie",
                    FranklinHudAction.BikeWheelie,
                    new Vector2(1f, 1f),
                    new Vector2(-285f, -575f),
                    new Vector2(155f, 155f)
                );
            }

            if (this.m_BikeBurnoutButton == null)
            {
                this.m_BikeBurnoutButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Bike Burnout",
                    "vehicle-control-burnout",
                    FranklinHudAction.BikeBurnout,
                    new Vector2(1f, 1f),
                    new Vector2(-455f, -575f),
                    new Vector2(155f, 155f)
                );
            }

            if (this.m_BikeHelmetButton == null)
            {
                this.m_BikeHelmetButton = this.CreateButton(
                    this.m_VehicleGroup,
                    "Bike Helmet",
                    "vehicle-control-helmet",
                    FranklinHudAction.BikeHelmet,
                    new Vector2(1f, 1f),
                    new Vector2(-285f, -385f),
                    new Vector2(155f, 155f)
                );
            }

            this.EnsureBikeSpeedUi();
            this.EnsureBikeHealthUi();
            this.EnsureBikeFuelUi();

            if (this.m_EnterVehicleButton == null || this.m_JogButton == null ||
                this.m_SprintButton == null || FindButton("Jump") == null)
            {
                return false;
            }

            FranklinHudButton[] buttons = this.GetComponentsInChildren<FranklinHudButton>(true);
            foreach (FranklinHudButton button in buttons)
            {
                button.Bind(this);
            }

            this.m_TactileCanvas = this.gameObject;
            this.m_TactileMoveStick = this.transform.Find("MoveStick")?.gameObject;
            this.m_UsesCanvasPlayerControl = true;
            return true;
        }

        private Transform FindChild(string childName)
        {
            foreach (Transform child in this.transform)
            {
                if (child.name == childName) return child;

                foreach (Transform nestedChild in child)
                {
                    if (nestedChild.name == childName) return nestedChild;
                }
            }

            return null;
        }

        private FranklinHudButton FindButton(string buttonName)
        {
            Transform child = this.FindChild(buttonName);
            return child != null ? child.GetComponent<FranklinHudButton>() : null;
        }

        private RectTransform CreateGroup(string groupName, Transform parent)
        {
            GameObject groupObject = new GameObject(groupName, typeof(RectTransform));
            RectTransform group = groupObject.GetComponent<RectTransform>();
            group.SetParent(parent, false);
            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.offsetMin = Vector2.zero;
            group.offsetMax = Vector2.zero;
            return group;
        }

        private FranklinHudButton CreateButton(
            Transform parent,
            string buttonName,
            string resourceName,
            FranklinHudAction action,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            bool isToggle = false)
        {
            GameObject buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(FranklinHudButton)
            );
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = LoadSprite(resourceName);
            image.preserveAspect = true;
            image.raycastTarget = true;

            FranklinHudButton button = buttonObject.GetComponent<FranklinHudButton>();
            button.Initialize(this, action, image, isToggle);
            return button;
        }

        private static Sprite LoadSprite(string resourceName)
        {
            if (SPRITES.TryGetValue(resourceName, out Sprite sprite)) return sprite;

            sprite = Resources.Load<Sprite>(RESOURCE_ROOT + resourceName);
            if (sprite != null)
            {
                SPRITES[resourceName] = sprite;
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(RESOURCE_ROOT + resourceName);
            if (texture == null)
            {
                Debug.LogWarning($"Franklin HUD sprite not found: {resourceName}");
                return null;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect
            );
            sprite.name = resourceName;
            SPRITES[resourceName] = sprite;
            return sprite;
        }

        private void RefreshReferences(bool force)
        {
            if (!force && Time.unscaledTime < this.m_NextReferenceRefresh) return;
            this.m_NextReferenceRefresh = Time.unscaledTime + REFERENCE_REFRESH_SECONDS;

            if (this.m_MovementBridge == null)
            {
                this.m_MovementBridge = FindFirstObjectByType<FranklinAnimationBridge>();
            }
            if (this.m_ObjectDirectionToggle == null && this.m_MovementBridge != null)
            {
                this.m_ObjectDirectionToggle =
                    this.m_MovementBridge.GetComponent<FranklinObjectDirectionToggle>();
                if (this.m_ObjectDirectionToggle == null)
                {
                    this.m_ObjectDirectionToggle = this.m_MovementBridge.gameObject
                        .AddComponent<FranklinObjectDirectionToggle>();
                }
            }
            if (this.m_CameraPointing == null)
            {
                this.m_CameraPointing = FindFirstObjectByType<FranklinCameraPointing>();
            }
            this.ResolvePlayerHelmetController();
            if (this.m_VehicleInteraction == null)
            {
                this.m_VehicleInteraction =
                    FindFirstObjectByType<FranklinVehicleInteractionManager>();
            }
            if (this.m_TactileCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas candidate in canvases)
                {
                    if (candidate != null && candidate.gameObject.name == "CanvasPlayerControl")
                    {
                        this.m_TactileCanvas = candidate.gameObject;
                        break;
                    }
                }
            }

            if (this.m_UsesCanvasPlayerControl && this.m_TactileMoveStick == null)
            {
                this.m_TactileMoveStick = this.transform.Find("MoveStick")?.gameObject;
            }

            IRvrVehicleInputController previousDriver = this.m_ActiveDriver;
            SimcadeCarDriver activeCar = null;
            foreach (SimcadeCarDriver driver in FindObjectsByType<SimcadeCarDriver>(
                         FindObjectsSortMode.None))
            {
                if (driver != null && (driver.IsVehicleEnabled ||
                                       driver.IsPassengerPresentationActive))
                {
                    activeCar = driver;
                    break;
                }
            }

            // A Car owns the shared vehicle HUD while its presentation is active. This also
            // prevents a Bike driver that is still enabled for one handoff frame from keeping
            // the Bike controls/telemetry bound over the Car HUD.
            if (activeCar != null)
            {
                this.m_ActiveDriver = activeCar;
            }
            else
            {
                Character player = ShortcutPlayer.Get<Character>();
                FranklinArcadeBikeDriver firstActiveBike = null;
                FranklinArcadeBikeDriver playerBike = null;
                foreach (FranklinArcadeBikeDriver driver in
                         FindObjectsByType<FranklinArcadeBikeDriver>(FindObjectsSortMode.None))
                {
                    if (driver == null || !driver.IsVehicleEnabled) continue;
                    firstActiveBike ??= driver;

                    BikeEntry entry = driver.GetComponent<BikeEntry>();
                    FranklinBikePassengerSeat passenger =
                        driver.GetComponent<FranklinBikePassengerSeat>();
                    if (player != null &&
                        ((entry != null && entry.SeatedCharacter == player) ||
                         (passenger != null && passenger.Passenger == player)))
                    {
                        playerBike = driver;
                        break;
                    }
                }

                // Prefer the Bike that actually contains Player. This keeps passenger
                // HUD/exit routing deterministic even when several NPC Bikes are enabled.
                this.m_ActiveDriver = playerBike ?? firstActiveBike;
            }

            if (previousDriver != null && previousDriver != this.m_ActiveDriver)
            {
                this.ReleaseVehicleInputs(previousDriver);
            }
        }

        private void ApplyMode(bool isDriving)
        {
            this.m_OnFootGroup.gameObject.SetActive(!isDriving);
            this.m_VehicleGroup.gameObject.SetActive(isDriving);
            if (this.m_UsesCanvasPlayerControl)
            {
                if (this.m_TactileMoveStick != null)
                {
                    this.m_TactileMoveStick.SetActive(!isDriving);
                }
            }
            else if (this.m_TactileCanvas != null)
            {
                this.m_TactileCanvas.SetActive(!isDriving);
            }

            if (isDriving) this.ReleaseMovementInputs();
            else this.ReleaseVehicleInputs(this.m_ActiveDriver);

            this.m_WasDriving = isDriving;
            this.m_HasAppliedMode = true;
        }

        private void UpdateEnterVehicleButton(bool isDriving)
        {
            if (this.m_EnterVehicleButton == null) return;

            bool shouldShow = !isDriving &&
                              this.m_VehicleInteraction != null &&
                              this.m_VehicleInteraction.CanRequestVehicleInteraction;
            if (this.m_EnterVehicleButton.activeSelf != shouldShow)
            {
                this.m_EnterVehicleButton.SetActive(shouldShow);
            }
        }

        private void UpdateBikeOnlyControls(bool isDriving)
        {
            FranklinArcadeBikeDriver bikeDriver = isDriving &&
                                                     !SimcadeCarDashboard.IsSharedHudActive
                ? this.m_ActiveDriver as FranklinArcadeBikeDriver
                : null;
            bool showBikeControls = bikeDriver != null && !this.m_WasPassengerMode;
            bool showVehicleHeadlight = showBikeControls ||
                isDriving && this.m_ActiveDriver is SimcadeCarDriver carDriver &&
                carDriver != null &&
                carDriver.IsVehicleEnabled;
            bool showVehicleHorn = showBikeControls ||
                isDriving && this.m_ActiveDriver is SimcadeCarDriver hornCar &&
                hornCar != null && hornCar.IsVehicleEnabled;
            bool showCarRearView = isDriving &&
                this.m_ActiveDriver is SimcadeCarDriver rearViewCar &&
                rearViewCar != null &&
                rearViewCar.IsVehicleEnabled;
            bool showCarCameraMode = isDriving &&
                (this.m_ActiveDriver is FranklinArcadeBikeDriver cameraModeBike &&
                 cameraModeBike.IsVehicleEnabled && !this.m_WasPassengerMode ||
                 this.m_ActiveDriver is SimcadeCarDriver cameraModeCar &&
                 cameraModeCar != null && cameraModeCar.IsVehicleEnabled);
            this.m_BikeTelemetrySuppressed = bikeDriver != null &&
                                             bikeDriver.IsFirstPersonViewActive;
            SetButtonActive(this.m_BikeHeadlightButton, showVehicleHeadlight);
            SetButtonActive(this.m_CarHornButton, showVehicleHorn);
            SetButtonActive(this.m_CarRearViewButton, showCarRearView);
            SetButtonActive(this.m_CarCameraModeButton, showCarCameraMode);
            this.m_CarCameraModeButton?.SetToggleState(
                bikeDriver != null && bikeDriver.IsFirstPersonViewActive ||
                this.m_ActiveDriver is SimcadeCarDriver cameraCar &&
                cameraCar != null && cameraCar.IsFirstPersonViewActive
            );
            SetButtonActive(this.m_BikeWheelieButton, showBikeControls);
            SetButtonActive(this.m_BikeBurnoutButton, showBikeControls);
            this.BindBikeHealth(bikeDriver);
            this.BindBikeFuel(bikeDriver);
            this.UpdateBikeSpeed(bikeDriver);
            this.UpdateBikeGaugePositions(bikeDriver);
        }

        private void UpdateHelmetControls(bool isDriving)
        {
            bool isDrivingBike = isDriving &&
                                 !SimcadeCarDashboard.IsSharedHudActive &&
                                 this.m_ActiveDriver is FranklinArcadeBikeDriver &&
                                 !this.m_WasPassengerMode;
            SetButtonActive(this.m_BikeHelmetButton, isDrivingBike);

            FranklinBikeHelmetController helmet = this.ResolvePlayerHelmetController();
            bool showOnFoot = !isDriving && helmet != null &&
                              (helmet.IsEquipped || helmet.IsTransitioning);
            if (showOnFoot && this.m_OnFootHelmetButton != null &&
                !this.m_OnFootHelmetButton.gameObject.activeSelf)
            {
                this.SyncOnFootHelmetButtonAppearance();
            }
            SetButtonActive(this.m_OnFootHelmetButton, showOnFoot);
        }

        /// <summary>
        /// The on-foot control is a persistence proxy because VehicleGroup is hidden
        /// after exit. Keep it visually identical to the authored Bike button so an
        /// equipped helmet never appears to jump, resize or change icon during exit.
        /// </summary>
        private void SyncOnFootHelmetButtonAppearance()
        {
            if (this.m_BikeHelmetButton == null ||
                this.m_OnFootHelmetButton == null)
            {
                return;
            }

            RectTransform source = this.m_BikeHelmetButton.transform as RectTransform;
            RectTransform target = this.m_OnFootHelmetButton.transform as RectTransform;
            if (source != null && target != null)
            {
                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition;
                target.sizeDelta = source.sizeDelta;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }

            Image sourceImage = this.m_BikeHelmetButton.GetComponent<Image>();
            Image targetImage = this.m_OnFootHelmetButton.GetComponent<Image>();
            if (sourceImage == null || targetImage == null) return;

            targetImage.sprite = sourceImage.sprite;
            targetImage.material = sourceImage.material;
            targetImage.preserveAspect = sourceImage.preserveAspect;
            targetImage.type = sourceImage.type;
            targetImage.color = sourceImage.color;
        }

        private bool TryGetPlayerBikePassenger(
            out FranklinBikePassengerSeat passengerSeat,
            out Character player)
        {
            passengerSeat = null;
            player = null;
            if (this.m_ActiveDriver is not FranklinArcadeBikeDriver bikeDriver)
                return false;

            player = ShortcutPlayer.Get<Character>();
            if (player == null) return false;
            passengerSeat = bikeDriver.GetComponent<FranklinBikePassengerSeat>();
            return passengerSeat != null &&
                   passengerSeat.Passenger == player;
        }

        private bool IsPlayerBikeDriver()
        {
            if (this.m_ActiveDriver is not FranklinArcadeBikeDriver bikeDriver)
                return false;

            Character player = ShortcutPlayer.Get<Character>();
            BikeEntry bikeEntry = bikeDriver.GetComponent<BikeEntry>();
            return player != null &&
                   bikeEntry != null &&
                   bikeEntry.SeatedCharacter == player;
        }

        private FranklinBikeHelmetController ResolvePlayerHelmetController()
        {
            if (this.m_PlayerHelmetController == null)
            {
                this.m_PlayerHelmetController =
                    FindFirstObjectByType<FranklinBikeHelmetController>();
            }
            return this.m_PlayerHelmetController;
        }

        private void EnsureBikeHealthUi()
        {
            if (this.m_VehicleGroup == null) return;

            Transform existingRoot = this.m_VehicleGroup.Find("Bike Health UI");
            if (existingRoot is RectTransform root)
            {
                this.m_BikeHealthRoot = root;
                Transform existingFill = root.Find("Health Arc Fill");
                if (existingFill is RectTransform fill)
                {
                    this.m_BikeHealthFillImage = fill.GetComponent<Image>();
                }

                if (this.m_BikeHealthFillImage != null)
                {
                    this.ConfigureBikeHealthFill(this.m_BikeHealthFillImage);
                    ApplyBikeHealthIconColor(this.m_BikeHealthRoot);
                    this.m_BikeHealthRoot.gameObject.SetActive(false);
                    return;
                }
            }

            if (this.m_BikeHealthRoot == null)
            {
                GameObject healthObject = new GameObject(
                    "Bike Health UI",
                    typeof(RectTransform)
                );
                this.m_BikeHealthRoot = healthObject.GetComponent<RectTransform>();
                this.m_BikeHealthRoot.SetParent(this.m_VehicleGroup, false);
            }

            this.m_BikeHealthRoot.anchorMin = new Vector2(0.5f, 0.5f);
            this.m_BikeHealthRoot.anchorMax = new Vector2(0.5f, 0.5f);
            this.m_BikeHealthRoot.pivot = new Vector2(0.5f, 0.5f);
            this.m_BikeHealthRoot.anchoredPosition = Vector2.zero;
            this.m_BikeHealthRoot.sizeDelta = new Vector2(
                BIKE_HEALTH_LAYOUT_WIDTH,
                BIKE_HEALTH_LAYOUT_HEIGHT
            );

            this.m_BikeHealthFillImage = CreateBikeGaugeImage(
                "Health Arc Fill",
                this.m_BikeHealthRoot,
                new Vector2(BIKE_HEALTH_BAR_OFFSET_X, BIKE_HEALTH_BAR_OFFSET_Y),
                new Vector2(BIKE_HEALTH_GAUGE_WIDTH, BIKE_HEALTH_GAUGE_HEIGHT),
                this.m_BikeGaugeSprite,
                VEHICLE_HEALTH_SKY_BLUE
            );
            this.ConfigureBikeHealthFill(this.m_BikeHealthFillImage);
            RectTransform iconRoot = CreateBikeGaugeRect(
                "Bike Health Icon",
                this.m_BikeHealthRoot,
                new Vector2(BIKE_HEALTH_BAR_OFFSET_X, 91f),
                new Vector2(34f, 34f)
            );
            CreateBikeSolidImage(
                "Health Icon Vertical",
                iconRoot,
                new Vector2(9f, 30f),
                VEHICLE_HEALTH_SKY_BLUE
            );
            CreateBikeSolidImage(
                "Health Icon Horizontal",
                iconRoot,
                new Vector2(30f, 9f),
                VEHICLE_HEALTH_SKY_BLUE
            );
            ApplyBikeHealthIconColor(this.m_BikeHealthRoot);
            this.m_BikeHealthRoot.gameObject.SetActive(false);
        }

        private static void ApplyBikeHealthIconColor(RectTransform healthRoot)
        {
            if (healthRoot == null) return;
            Transform iconRoot = healthRoot.Find("Bike Health Icon");
            if (iconRoot == null) return;

            Image vertical = iconRoot.Find("Health Icon Vertical")?.GetComponent<Image>();
            Image horizontal = iconRoot.Find("Health Icon Horizontal")?.GetComponent<Image>();
            if (vertical != null) vertical.color = VEHICLE_HEALTH_SKY_BLUE;
            if (horizontal != null) horizontal.color = VEHICLE_HEALTH_SKY_BLUE;
        }

        private void EnsureBikeFuelUi()
        {
            if (this.m_VehicleGroup == null) return;

            Transform existingRoot = this.m_VehicleGroup.Find("Bike Fuel UI");
            if (existingRoot is RectTransform root)
            {
                this.m_BikeFuelRoot = root;
                this.m_BikeFuelFillImage = root.Find("Fuel Arc Fill")?.GetComponent<Image>();
                if (this.m_BikeFuelFillImage != null)
                {
                    this.ConfigureBikeFuelFill(this.m_BikeFuelFillImage);
                    this.m_BikeFuelRoot.gameObject.SetActive(false);
                    return;
                }
            }

            if (this.m_BikeFuelRoot == null)
            {
                GameObject fuelObject = new GameObject("Bike Fuel UI", typeof(RectTransform));
                this.m_BikeFuelRoot = fuelObject.GetComponent<RectTransform>();
                this.m_BikeFuelRoot.SetParent(this.m_VehicleGroup, false);
            }

            this.m_BikeFuelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            this.m_BikeFuelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            this.m_BikeFuelRoot.pivot = new Vector2(0.5f, 0.5f);
            this.m_BikeFuelRoot.anchoredPosition = Vector2.zero;
            this.m_BikeFuelRoot.sizeDelta = new Vector2(
                BIKE_FUEL_GAUGE_WIDTH,
                BIKE_FUEL_GAUGE_HEIGHT
            );

            this.m_BikeFuelFillImage = CreateBikeGaugeImage(
                "Fuel Arc Fill",
                this.m_BikeFuelRoot,
                Vector2.zero,
                new Vector2(BIKE_FUEL_GAUGE_WIDTH, BIKE_FUEL_GAUGE_HEIGHT),
                this.m_BikeGaugeSprite,
                Color.white
            );
            this.ConfigureBikeFuelFill(this.m_BikeFuelFillImage);
            CreateBikeGaugeLabel(
                "Full",
                this.m_BikeFuelRoot,
                "F",
                new Vector2(29f, 59f),
                new Color(1f, 1f, 1f, 0.78f)
            );
            CreateBikeGaugeLabel(
                "Empty",
                this.m_BikeFuelRoot,
                "E",
                new Vector2(29f, -59f),
                new Color(1f, 1f, 1f, 0.62f)
            );
            this.m_BikeFuelRoot.gameObject.SetActive(false);
        }

        private void ConfigureBikeHealthFill(Image image)
        {
            image.sprite = this.m_BikeGaugeSprite;
            image.material = this.m_BikeGaugeAlphaTintMaterial;
            image.preserveAspect = false;
            image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)Image.OriginVertical.Bottom;
            image.fillClockwise = true;
            image.color = VEHICLE_HEALTH_SKY_BLUE;
            image.raycastTarget = false;
        }

        private void ConfigureBikeFuelFill(Image image)
        {
            image.sprite = this.m_BikeGaugeSprite;
            image.preserveAspect = true;
            image.rectTransform.localScale = Vector3.one;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)Image.OriginVertical.Bottom;
            image.fillClockwise = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static RectTransform CreateBikeGaugeRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateBikeGaugeImage(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateBikeSolidImage(
            string objectName,
            Transform parent,
            Vector2 size,
            Color color)
        {
            return CreateBikeGaugeImage(
                objectName,
                parent,
                Vector2.zero,
                size,
                null,
                color
            );
        }

        private void CreateBikeGaugeLabel(
            string objectName,
            Transform parent,
            string value,
            Vector2 position,
            Color color)
        {
            GameObject labelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(24f, 22f);
            Text label = labelObject.GetComponent<Text>();
            label.font = this.m_BikeSpeedFont != null
                ? this.m_BikeSpeedFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = value;
            label.raycastTarget = false;
        }

        private void BindBikeHealth(FranklinArcadeBikeDriver bikeDriver)
        {
            if (this.m_BikeHealthDriver == bikeDriver)
            {
                this.SetBikeHealthVisible(bikeDriver != null &&
                                          this.m_ActiveBikeHealth != null);
                return;
            }

            if (this.m_ActiveBikeHealth != null)
                this.m_ActiveBikeHealth.EventHealthChanged -= this.OnBikeHealthChanged;

            this.m_BikeHealthDriver = bikeDriver;
            this.m_ActiveBikeHealth = bikeDriver != null
                ? bikeDriver.GetComponent<FranklinBikeHealth>()
                : null;

            this.SetBikeHealthVisible(this.m_ActiveBikeHealth != null);

            if (this.m_ActiveBikeHealth == null) return;
            this.m_ActiveBikeHealth.EventHealthChanged += this.OnBikeHealthChanged;
            this.OnBikeHealthChanged(
                this.m_ActiveBikeHealth.CurrentHealth,
                this.m_ActiveBikeHealth.MaximumHealth
            );
        }

        private void OnBikeHealthChanged(float current, float maximum)
        {
            float ratio = maximum > 0.001f ? Mathf.Clamp01(current / maximum) : 0f;
            if (this.m_BikeHealthFillImage != null)
            {
                this.m_BikeHealthFillImage.fillAmount = ratio;
                this.m_BikeHealthFillImage.color = VEHICLE_HEALTH_SKY_BLUE;
            }
        }

        private void SetBikeHealthVisible(bool visible)
        {
            visible &= !SimcadeCarDashboard.IsSharedHudActive &&
                       !this.m_BikeTelemetrySuppressed;
            if (this.m_BikeHealthRoot != null &&
                this.m_BikeHealthRoot.gameObject.activeSelf != visible)
            {
                this.m_BikeHealthRoot.gameObject.SetActive(visible);
            }
        }

        private void BindBikeFuel(FranklinArcadeBikeDriver bikeDriver)
        {
            if (this.m_BikeFuelDriver == bikeDriver)
            {
                this.SetBikeFuelVisible(bikeDriver != null &&
                                        this.m_ActiveBikeFuel != null);
                return;
            }

            if (this.m_ActiveBikeFuel != null)
                this.m_ActiveBikeFuel.EventFuelChanged -= this.OnBikeFuelChanged;

            this.m_BikeFuelDriver = bikeDriver;
            this.m_ActiveBikeFuel = bikeDriver != null
                ? bikeDriver.GetComponent<FranklinBikeFuel>()
                : null;
            this.m_HasBikeFuelTarget = false;
            this.m_BikeFuelVelocity = 0f;

            this.SetBikeFuelVisible(this.m_ActiveBikeFuel != null);

            if (this.m_ActiveBikeFuel == null) return;
            this.m_ActiveBikeFuel.EventFuelChanged += this.OnBikeFuelChanged;
            this.OnBikeFuelChanged(
                this.m_ActiveBikeFuel.CurrentFuel,
                this.m_ActiveBikeFuel.MaximumFuel
            );
        }

        private void OnBikeFuelChanged(float current, float maximum)
        {
            float ratio = maximum > 0.001f ? Mathf.Clamp01(current / maximum) : 0f;
            this.m_BikeFuelTarget = ratio;
            if (this.m_HasBikeFuelTarget) return;

            this.m_HasBikeFuelTarget = true;
            if (this.m_BikeFuelFillImage != null)
                this.m_BikeFuelFillImage.fillAmount = ratio;
        }

        private void UpdateBikeFuelFill()
        {
            if (this.m_BikeTelemetrySuppressed ||
                !this.m_HasBikeFuelTarget ||
                this.m_BikeFuelFillImage == null)
            {
                return;
            }

            this.m_BikeFuelFillImage.fillAmount = Mathf.SmoothDamp(
                this.m_BikeFuelFillImage.fillAmount,
                this.m_BikeFuelTarget,
                ref this.m_BikeFuelVelocity,
                0.22f,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
            if (Mathf.Abs(this.m_BikeFuelFillImage.fillAmount - this.m_BikeFuelTarget) <
                0.0005f)
            {
                this.m_BikeFuelFillImage.fillAmount = this.m_BikeFuelTarget;
                this.m_BikeFuelVelocity = 0f;
            }
        }

        private void SetBikeFuelVisible(bool visible)
        {
            visible &= !SimcadeCarDashboard.IsSharedHudActive &&
                       !this.m_BikeTelemetrySuppressed;
            if (this.m_BikeFuelRoot != null &&
                this.m_BikeFuelRoot.gameObject.activeSelf != visible)
            {
                this.m_BikeFuelRoot.gameObject.SetActive(visible);
            }
        }

        private void EnsureBikeSpeedUi()
        {
            if (this.m_VehicleGroup == null) return;

            Transform existing = this.m_VehicleGroup.Find("Bike Speed UI");
            if (existing is RectTransform existingRect)
            {
                Text existingText = existingRect.GetComponent<Text>();
                if (existingText != null)
                {
                    this.m_BikeSpeedRoot = existingRect;
                    this.m_BikeSpeedText = existingText;
                    this.ApplyBikeSpeedStyle();
                    this.EnsureBikeSpeedBackground();
                    this.m_BikeSpeedRoot.gameObject.SetActive(false);
                    this.m_BikeSpeedBackground.gameObject.SetActive(false);
                    return;
                }
            }

            GameObject speedObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "Bike Speed UI",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );
            this.m_BikeSpeedRoot = speedObject.GetComponent<RectTransform>();
            this.m_BikeSpeedRoot.SetParent(this.m_VehicleGroup, false);
            this.m_BikeSpeedRoot.anchorMin = new Vector2(0.5f, 0.5f);
            this.m_BikeSpeedRoot.anchorMax = new Vector2(0.5f, 0.5f);
            this.m_BikeSpeedRoot.pivot = new Vector2(0.5f, 0.5f);
            this.m_BikeSpeedRoot.anchoredPosition = Vector2.zero;
            this.m_BikeSpeedRoot.sizeDelta = this.m_BikeSpeedRectSize;

            this.m_BikeSpeedText = speedObject.GetComponent<Text>() ??
                                   speedObject.AddComponent<Text>();
            this.ApplyBikeSpeedStyle();
            this.EnsureBikeSpeedBackground();
            this.m_BikeSpeedRoot.gameObject.SetActive(false);
            this.m_BikeSpeedBackground.gameObject.SetActive(false);
        }

        private void EnsureBikeSpeedBackground()
        {
            if (this.m_VehicleGroup == null || this.m_BikeSpeedRoot == null) return;

            Transform existing = this.m_VehicleGroup.Find("Bike Speed Background");
            Image backgroundImage = existing != null ? existing.GetComponent<Image>() : null;
            if (backgroundImage == null)
            {
                GameObject backgroundObject = new GameObject(
                    "Bike Speed Background",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                this.m_BikeSpeedBackground =
                    backgroundObject.GetComponent<RectTransform>();
                this.m_BikeSpeedBackground.SetParent(this.m_VehicleGroup, false);
                backgroundImage = backgroundObject.GetComponent<Image>();
            }
            else
            {
                this.m_BikeSpeedBackground = (RectTransform)existing;
            }

            backgroundImage.color = SPEED_BACKGROUND_COLOR;
            backgroundImage.raycastTarget = false;
            Outline border = backgroundImage.GetComponent<Outline>() ??
                             backgroundImage.gameObject.AddComponent<Outline>();
            border.effectColor = SPEED_BACKGROUND_BORDER_COLOR;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = true;

            this.SyncBikeSpeedBackgroundTransform();
            int speedIndex = this.m_BikeSpeedRoot.GetSiblingIndex();
            if (this.m_BikeSpeedBackground.GetSiblingIndex() < speedIndex)
                speedIndex--;
            this.m_BikeSpeedBackground.SetSiblingIndex(Mathf.Max(0, speedIndex));
        }

        private void SyncBikeSpeedBackgroundTransform()
        {
            if (this.m_BikeSpeedBackground == null || this.m_BikeSpeedRoot == null) return;

            this.m_BikeSpeedBackground.anchorMin = this.m_BikeSpeedRoot.anchorMin;
            this.m_BikeSpeedBackground.anchorMax = this.m_BikeSpeedRoot.anchorMax;
            this.m_BikeSpeedBackground.pivot = this.m_BikeSpeedRoot.pivot;
            this.m_BikeSpeedBackground.anchoredPosition =
                this.m_BikeSpeedRoot.anchoredPosition + SPEED_BACKGROUND_OFFSET;
            Vector2 speedSize = this.m_BikeSpeedRoot.sizeDelta;
            this.m_BikeSpeedBackground.sizeDelta = new Vector2(
                Mathf.Min(speedSize.x, SPEED_BACKGROUND_MAX_SIZE.x),
                Mathf.Min(speedSize.y, SPEED_BACKGROUND_MAX_SIZE.y)
            );
            this.m_BikeSpeedBackground.localRotation = this.m_BikeSpeedRoot.localRotation;
            this.m_BikeSpeedBackground.localScale = this.m_BikeSpeedRoot.localScale;
        }

        private void UpdateBikeSpeed(FranklinArcadeBikeDriver bikeDriver)
        {
            bool visible = bikeDriver != null &&
                           !this.m_BikeTelemetrySuppressed &&
                           !SimcadeCarDashboard.IsSharedHudActive;
            if (this.m_BikeSpeedRoot != null &&
                this.m_BikeSpeedRoot.gameObject.activeSelf != visible)
            {
                this.m_BikeSpeedRoot.gameObject.SetActive(visible);
            }
            if (this.m_BikeSpeedBackground != null &&
                this.m_BikeSpeedBackground.gameObject.activeSelf != visible)
            {
                this.m_BikeSpeedBackground.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                this.m_BikeSpeedDriver = null;
                this.m_HasBikeSpeedPosition = false;
                this.m_LastDisplayedBikeSpeed = int.MinValue;
                return;
            }

            if (this.m_BikeSpeedDriver != bikeDriver)
            {
                this.m_BikeSpeedDriver = bikeDriver;
                this.m_HasBikeSpeedPosition = false;
                this.m_BikeSpeedVelocity = Vector2.zero;
                this.m_LastDisplayedBikeSpeed = int.MinValue;
                this.m_NextBikeSpeedUpdate = 0f;
            }

            float now = Time.unscaledTime;
            if (now >= this.m_NextBikeSpeedUpdate)
            {
                this.m_NextBikeSpeedUpdate = now + BIKE_SPEED_UPDATE_SECONDS;
                int speed = Mathf.RoundToInt(bikeDriver.SpeedMetersPerSecond * 3.6f);
                if (speed != this.m_LastDisplayedBikeSpeed)
                {
                    this.m_LastDisplayedBikeSpeed = speed;
                    if (this.m_BikeSpeedText != null)
                        this.m_BikeSpeedText.text = this.FormatBikeSpeed(speed);
                }
            }

            if (this.m_BikeSpeedFollowBike)
            {
                this.UpdateBikeSpeedWorldFollow(bikeDriver);
            }
            else if (this.m_BikeSpeedRoot != null)
            {
                this.m_HasBikeSpeedPosition = false;
                this.m_BikeSpeedVelocity = Vector2.zero;
                this.m_BikeSpeedRoot.anchoredPosition =
                    this.m_BikeSpeedFixedScreenPosition;
            }

            this.SyncBikeSpeedBackgroundTransform();
        }

        private void UpdateBikeGaugePositions(FranklinArcadeBikeDriver bikeDriver)
        {
            if (bikeDriver == null || this.m_BikeTelemetrySuppressed)
            {
                this.m_HasBikeHealthPosition = false;
                this.m_BikeHealthVelocity = Vector2.zero;
                return;
            }

            if (this.m_BikeFuelRoot != null && this.m_BikeSpeedRoot != null)
            {
                this.m_BikeFuelRoot.anchoredPosition =
                    this.m_BikeSpeedRoot.anchoredPosition + this.m_BikeFuelGaugeOffset;
            }

            if (this.m_BikeHealthRoot == null || this.m_VehicleGroup == null) return;
            if (this.m_BikeSpeedCamera == null || !this.m_BikeSpeedCamera.isActiveAndEnabled)
                this.m_BikeSpeedCamera = Camera.main;
            if (this.m_BikeSpeedCamera == null) return;

            Vector3 worldTarget = bikeDriver.transform.position +
                                  Vector3.up * this.m_BikeHealthWorldHeight +
                                  this.m_BikeSpeedCamera.transform.right *
                                  this.m_BikeHealthWorldRightOffset;
            Vector3 screenPoint = this.m_BikeSpeedCamera.WorldToScreenPoint(worldTarget);
            if (screenPoint.z <= 0.01f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_VehicleGroup,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            float mirroredX = -(this.m_BikeSpeedScreenOffset.x +
                                this.m_BikeFuelGaugeOffset.x) -
                              BIKE_HEALTH_BAR_OFFSET_X;
            float alignedBottomY = this.m_BikeSpeedScreenOffset.y +
                                   this.m_BikeFuelGaugeOffset.y -
                                   BIKE_FUEL_GAUGE_HEIGHT * 0.5f -
                                   BIKE_HEALTH_BAR_OFFSET_Y +
                                   BIKE_HEALTH_GAUGE_HEIGHT * 0.5f;
            localPoint += new Vector2(mirroredX, alignedBottomY) +
                          this.m_BikeHealthScreenOffset;

            Rect canvasRect = this.GetBikeHudSafeRect();
            Vector2 half = this.m_BikeHealthRoot.rect.size * 0.5f;
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                canvasRect.xMin + half.x,
                canvasRect.xMax - half.x
            );
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                canvasRect.yMin + half.y,
                canvasRect.yMax - half.y
            );

            if (!this.m_HasBikeHealthPosition)
            {
                this.m_BikeHealthPosition = localPoint;
                this.m_HasBikeHealthPosition = true;
            }
            else
            {
                this.m_BikeHealthPosition = Vector2.SmoothDamp(
                    this.m_BikeHealthPosition,
                    localPoint,
                    ref this.m_BikeHealthVelocity,
                    this.m_BikeSpeedFollowSmooth,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }

            this.m_BikeHealthRoot.anchoredPosition = this.m_BikeHealthPosition;
        }

        private void ApplyBikeSpeedStyle()
        {
            if (this.m_BikeSpeedText == null) return;

            this.m_BikeSpeedText.font = this.m_BikeSpeedFont != null
                ? this.m_BikeSpeedFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            this.m_BikeSpeedText.fontSize = this.m_BikeSpeedValueFontSize;
            this.m_BikeSpeedText.fontStyle = this.m_BikeSpeedFontStyle;
            this.m_BikeSpeedText.alignment = TextAnchor.MiddleCenter;
            this.m_BikeSpeedText.color = this.m_BikeSpeedTextColor;
            this.m_BikeSpeedText.supportRichText = true;
            this.m_BikeSpeedText.raycastTarget = false;
            int speed = this.m_LastDisplayedBikeSpeed == int.MinValue
                ? 0
                : this.m_LastDisplayedBikeSpeed;
            this.m_BikeSpeedText.text = this.FormatBikeSpeed(speed);

            Shadow shadow = this.m_BikeSpeedText.GetComponent<Shadow>() ??
                            this.m_BikeSpeedText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = this.m_BikeSpeedShadowColor;
            shadow.effectDistance = this.m_BikeSpeedShadowOffset;
            shadow.useGraphicAlpha = true;
        }

        private string FormatBikeSpeed(int speed)
        {
            return $"{speed}<size={this.m_BikeSpeedUnitFontSize}> km/h</size>";
        }

        private void UpdateBikeSpeedWorldFollow(FranklinArcadeBikeDriver bikeDriver)
        {
            if (this.m_BikeSpeedRoot == null || this.m_VehicleGroup == null) return;

            if (this.m_BikeSpeedCamera == null || !this.m_BikeSpeedCamera.isActiveAndEnabled)
                this.m_BikeSpeedCamera = Camera.main;
            if (this.m_BikeSpeedCamera == null) return;

            Vector3 worldTarget = bikeDriver.transform.position +
                                  Vector3.up * this.m_BikeSpeedWorldHeight -
                                  this.m_BikeSpeedCamera.transform.right *
                                  this.m_BikeSpeedWorldLeftOffset;
            Vector3 screenPoint = this.m_BikeSpeedCamera.WorldToScreenPoint(worldTarget);
            if (screenPoint.z <= 0.01f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_VehicleGroup,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            localPoint += this.m_BikeSpeedScreenOffset;
            Rect canvasRect = this.GetBikeHudSafeRect();
            Vector2 half = this.m_BikeSpeedRoot.rect.size * 0.5f;
            float minimumX = Mathf.Max(
                canvasRect.xMin + half.x,
                canvasRect.xMin - this.m_BikeFuelGaugeOffset.x +
                BIKE_FUEL_GAUGE_WIDTH * 0.5f
            );
            float maximumX = Mathf.Min(
                canvasRect.xMax - half.x,
                canvasRect.xMax - this.m_BikeFuelGaugeOffset.x -
                BIKE_FUEL_GAUGE_WIDTH * 0.5f
            );
            float minimumY = Mathf.Max(
                canvasRect.yMin + half.y,
                canvasRect.yMin - this.m_BikeFuelGaugeOffset.y +
                BIKE_FUEL_GAUGE_HEIGHT * 0.5f
            );
            float maximumY = Mathf.Min(
                canvasRect.yMax - half.y,
                canvasRect.yMax - this.m_BikeFuelGaugeOffset.y -
                BIKE_FUEL_GAUGE_HEIGHT * 0.5f
            );
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                minimumX,
                Mathf.Max(minimumX, maximumX)
            );
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                minimumY,
                Mathf.Max(minimumY, maximumY)
            );

            if (!this.m_HasBikeSpeedPosition)
            {
                this.m_BikeSpeedPosition = localPoint;
                this.m_HasBikeSpeedPosition = true;
            }
            else
            {
                this.m_BikeSpeedPosition = Vector2.SmoothDamp(
                    this.m_BikeSpeedPosition,
                    localPoint,
                    ref this.m_BikeSpeedVelocity,
                    this.m_BikeSpeedFollowSmooth,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }
            this.m_BikeSpeedRoot.anchoredPosition = this.m_BikeSpeedPosition;
        }

        private Rect GetBikeHudSafeRect()
        {
            Rect fallback = this.m_VehicleGroup != null
                ? this.m_VehicleGroup.rect
                : new Rect(-960f, -540f, 1920f, 1080f);
            if (this.m_VehicleGroup == null) return fallback;

            Rect safeArea = Screen.safeArea;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_VehicleGroup,
                    safeArea.min,
                    null,
                    out Vector2 localMinimum) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_VehicleGroup,
                    safeArea.max,
                    null,
                    out Vector2 localMaximum))
            {
                return fallback;
            }

            return Rect.MinMaxRect(
                Mathf.Min(localMinimum.x, localMaximum.x),
                Mathf.Min(localMinimum.y, localMaximum.y),
                Mathf.Max(localMinimum.x, localMaximum.x),
                Mathf.Max(localMinimum.y, localMaximum.y)
            );
        }

        private static void SetButtonActive(FranklinHudButton button, bool state)
        {
            if (button != null && button.gameObject.activeSelf != state)
                button.gameObject.SetActive(state);
        }

        private void ApplyPassengerControlVisibility(bool passengerMode)
        {
            if (this.m_VehicleGroup == null) return;
            string[] drivingOnlyControls =
            {
                "Steer Left",
                "Steer Right",
                "Accelerate",
                "Brake Reverse",
                "Handbrake",
                "Slow Drive",
                "Bike Headlight",
                "Car Horn",
                "Car Rear View",
                "Car Camera Mode",
                "Bike Wheelie",
                "Bike Burnout",
                "Bike Helmet"
            };
            foreach (string controlName in drivingOnlyControls)
            {
                Transform control = this.m_VehicleGroup.Find(controlName);
                if (control != null) control.gameObject.SetActive(!passengerMode);
            }
            this.m_WasPassengerMode = passengerMode;
        }

        private void ReleaseMovementInputs()
        {
            this.m_MovementBridge?.SetVirtualJogInput(false);
            this.m_MovementBridge?.SetVirtualSprintInput(false);
            this.m_MovementBridge?.StopVirtualAutoRun();
            this.m_CameraPointing?.SetVirtualPointInput(false);
        }

        private void ReleaseVehicleInputs(
            IRvrVehicleInputController driver,
            bool preserveFirstPersonView = false)
        {
            if (driver == null) return;
            driver.SetVirtualAccelerateInput(false);
            driver.SetVirtualSlowAccelerateInput(false);
            driver.SetVirtualBrakeReverseInput(false);
            driver.SetVirtualSteerLeftInput(false);
            driver.SetVirtualSteerRightInput(false);
            driver.SetVirtualHandbrakeInput(false);
            if (driver is FranklinArcadeBikeDriver bikeDriver)
            {
                if (!preserveFirstPersonView)
                    bikeDriver.RestoreThirdPersonViewPreservingPreference();
                bikeDriver.SetHeadlightEnabled(false);
                bikeDriver.SetHornPressed(false);
                bikeDriver.SetVirtualWheelieInput(false);
                bikeDriver.SetVirtualBurnoutInput(false);
            }
            else if (driver is SimcadeCarDriver carDriver && carDriver != null)
            {
                carDriver.SetHeadlightEnabled(false);
                carDriver.SetHornPressed(false);
                carDriver.SetRearViewPressed(false);
                if (!preserveFirstPersonView)
                {
                    carDriver.RestoreThirdPersonViewPreservingPreference();
                }
            }
        }

        private static bool IsUsableDriver(IRvrVehicleInputController driver)
        {
            return driver is MonoBehaviour behaviour && behaviour != null &&
                   (driver.IsVehicleEnabled ||
                    driver is SimcadeCarDriver car &&
                    car.IsPassengerPresentationActive);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject eventSystemObject = new GameObject(
                "EventSystem (Franklin HUD)",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            DontDestroyOnLoad(eventSystemObject);
        }

        private void OnValidate()
        {
            this.m_BikeSpeedValueFontSize = Mathf.Clamp(
                this.m_BikeSpeedValueFontSize,
                16,
                96
            );
            this.m_BikeSpeedUnitFontSize = Mathf.Clamp(
                this.m_BikeSpeedUnitFontSize,
                10,
                64
            );
            this.m_BikeSpeedRectSize.x = Mathf.Max(64f, this.m_BikeSpeedRectSize.x);
            this.m_BikeSpeedRectSize.y = Mathf.Max(32f, this.m_BikeSpeedRectSize.y);
            this.m_BikeSpeedWorldLeftOffset = Mathf.Max(
                0.5f,
                this.m_BikeSpeedWorldLeftOffset
            );
            this.m_BikeSpeedFollowSmooth = Mathf.Clamp(
                this.m_BikeSpeedFollowSmooth,
                0.03f,
                0.3f
            );
            this.m_HasBikeSpeedPosition = false;
            this.m_BikeSpeedVelocity = Vector2.zero;
            this.ApplyBikeSpeedStyle();
        }
    }

    internal enum FranklinHudAction
    {
        Jog,
        Sprint,
        SteerLeft,
        SteerRight,
        Accelerate,
        BrakeReverse,
        Handbrake,
        VehicleInteraction,
        Jump,
        SlowDrive,
        BikeHeadlight,
        BikeWheelie,
        BikeBurnout,
        Phone,
        Home,
        Settings,
        BikeHelmet,
        PointDirection,
        CarHorn,
        ObjectDirection,
        CarRearView,
        CarCameraMode
    }

}
