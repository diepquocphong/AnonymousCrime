using System.Collections.Generic;
using FranklinGame.Animations;
using FranklinGame.Vehicles;
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
        private const float BIKE_HEALTH_FILL_WIDTH = 732f;
        private const float BIKE_SPEED_UPDATE_SECONDS = 0.1f;

        private static readonly Color BIKE_HEALTH_GREEN =
            new Color(0.55f, 0.96f, 0.16f, 1f);
        private static readonly Color BIKE_HEALTH_YELLOW =
            new Color(1f, 0.68f, 0.05f, 1f);
        private static readonly Color BIKE_HEALTH_RED =
            new Color(0.96f, 0.08f, 0.08f, 1f);

        private static readonly Dictionary<string, Sprite> SPRITES = new();
        private static FranklinMobileHud s_Instance;
        private static bool s_ControlsSuppressed;

        [Header("Bike Health UI")]
        [SerializeField] private Sprite m_BikeHealthFrameSprite;

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
        private RectTransform m_BikeHealthRoot;
        private RectTransform m_BikeHealthFill;
        private Image m_BikeHealthFillImage;
        private RectTransform m_BikeSpeedRoot;
        private Text m_BikeSpeedText;
        private GameObject m_EnterVehicleButton;
        private FranklinHudButton m_SlowDriveButton;
        private FranklinHudButton m_BikeHeadlightButton;
        private FranklinHudButton m_BikeWheelieButton;
        private FranklinHudButton m_BikeBurnoutButton;
        private FranklinAnimationBridge m_MovementBridge;
        private FranklinVehicleInteractionManager m_VehicleInteraction;
        private IRvrVehicleInputController m_ActiveDriver;
        private FranklinArcadeBikeDriver m_BikeHealthDriver;
        private FranklinBikeHealth m_ActiveBikeHealth;
        private FranklinArcadeBikeDriver m_BikeSpeedDriver;
        private Camera m_BikeSpeedCamera;
        private GameObject m_TactileCanvas;
        private GameObject m_TactileMoveStick;
        private float m_NextReferenceRefresh;
        private float m_NextBikeSpeedUpdate;
        private int m_LastDisplayedBikeSpeed = int.MinValue;
        private Vector2 m_BikeSpeedPosition;
        private Vector2 m_BikeSpeedVelocity;
        private bool m_WasDriving;
        private bool m_WasPassengerMode;
        private bool m_HasAppliedMode;
        private bool m_UsesCanvasPlayerControl;
        private bool m_HasAppliedSuppression;
        private bool m_HasBikeSpeedPosition;

        public static bool IsActive => s_Instance != null &&
                                       s_Instance.isActiveAndEnabled;
        public static bool ControlsSuppressed => s_ControlsSuppressed;

        public static void SetControlsSuppressed(bool suppressed)
        {
            s_ControlsSuppressed = suppressed;
            if (s_Instance == null) return;
            if (suppressed) s_Instance.ApplyControlsSuppressed();
            else s_Instance.ReleaseControlsSuppression();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_ControlsSuppressed = false;
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
            if (s_ControlsSuppressed) this.ApplyControlsSuppressed();
        }

        private void OnDestroy()
        {
            this.BindBikeHealth(null);
            this.UpdateBikeSpeed(null);
            this.ReleaseMovementInputs();
            this.ReleaseVehicleInputs(this.m_ActiveDriver);
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            this.RefreshReferences(false);

            if (s_ControlsSuppressed)
            {
                this.ApplyControlsSuppressed();
                return;
            }
            if (this.m_HasAppliedSuppression) this.ReleaseControlsSuppression();

            bool isDriving = IsUsableDriver(this.m_ActiveDriver);
            bool isPassengerMode = this.m_ActiveDriver is SimcadeCarDriver car &&
                car.IsPassengerPresentationActive;
            if (!this.m_HasAppliedMode || isDriving != this.m_WasDriving)
            {
                this.ApplyMode(isDriving);
            }
            if (!this.m_HasAppliedMode || isPassengerMode != this.m_WasPassengerMode)
                this.ApplyPassengerControlVisibility(isPassengerMode);

            this.UpdateEnterVehicleButton(isDriving);
            this.UpdateBikeOnlyControls(isDriving);
        }

        private void ApplyControlsSuppressed()
        {
            if (!this.m_HasAppliedSuppression)
            {
                this.ReleaseMovementInputs();
                this.ReleaseVehicleInputs(this.m_ActiveDriver);
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
                case FranklinHudAction.VehicleInteraction:
                    if (!active) break;
                    if (this.m_ActiveDriver is SimcadeCarDriver passengerCar &&
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

            this.CreateButton(
                this.m_OnFootGroup,
                "Jog",
                "player-movement-0",
                FranklinHudAction.Jog,
                new Vector2(0f, 0f),
                new Vector2(369.2f, 568.43f),
                new Vector2(165f, 165f)
            );
            this.CreateButton(
                this.m_OnFootGroup,
                "Sprint",
                "player-movement-1",
                FranklinHudAction.Sprint,
                new Vector2(0f, 0f),
                new Vector2(173.4f, 561.13f),
                new Vector2(215f, 215f)
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
                new Vector2(-455f, 190f),
                new Vector2(225f, 225f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Handbrake",
                "vehicle-control-4",
                FranklinHudAction.Handbrake,
                new Vector2(1f, 0f),
                new Vector2(-455f, 430f),
                new Vector2(169f, 169f)
            );
            this.CreateButton(
                this.m_VehicleGroup,
                "Exit Vehicle",
                "vehicle-control-5",
                FranklinHudAction.VehicleInteraction,
                new Vector2(1f, 1f),
                new Vector2(-115f, -120f),
                new Vector2(190f, 190f)
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
                new Vector2(-115f, -315f),
                new Vector2(155f, 155f),
                true
            );
            this.m_BikeWheelieButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Wheelie",
                "vehicle-control-wheelie",
                FranklinHudAction.BikeWheelie,
                new Vector2(1f, 1f),
                new Vector2(-285f, -315f),
                new Vector2(155f, 155f)
            );
            this.m_BikeBurnoutButton = this.CreateButton(
                this.m_VehicleGroup,
                "Bike Burnout",
                "vehicle-control-burnout",
                FranklinHudAction.BikeBurnout,
                new Vector2(1f, 1f),
                new Vector2(-455f, -315f),
                new Vector2(155f, 155f)
            );
            this.EnsureBikeHealthUi();
            this.EnsureBikeSpeedUi();
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
            this.m_EnterVehicleButton = FindChild("Enter Vehicle")?.gameObject;
            this.m_SlowDriveButton = FindButton("Slow Drive");
            this.m_BikeHeadlightButton = FindButton("Bike Headlight");
            this.m_BikeWheelieButton = FindButton("Bike Wheelie");
            this.m_BikeBurnoutButton = FindButton("Bike Burnout");

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
                    new Vector2(-115f, -315f),
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
                    new Vector2(-285f, -315f),
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
                    new Vector2(-455f, -315f),
                    new Vector2(155f, 155f)
                );
            }

            this.EnsureBikeHealthUi();
            this.EnsureBikeSpeedUi();

            if (this.m_EnterVehicleButton == null || FindButton("Jog") == null ||
                FindButton("Sprint") == null || FindButton("Jump") == null)
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
            if (!IsUsableDriver(this.m_ActiveDriver))
            {
                this.m_ActiveDriver = null;
                foreach (SimcadeCarDriver driver in FindObjectsByType<SimcadeCarDriver>(
                    FindObjectsSortMode.None))
                {
                    if (driver != null && (driver.IsVehicleEnabled ||
                                           driver.IsPassengerPresentationActive))
                    {
                        this.m_ActiveDriver = driver;
                        break;
                    }
                }

                if (this.m_ActiveDriver == null)
                {
                    foreach (FranklinArcadeBikeDriver driver in
                             FindObjectsByType<FranklinArcadeBikeDriver>(FindObjectsSortMode.None))
                    {
                        if (driver != null && driver.IsVehicleEnabled)
                        {
                            this.m_ActiveDriver = driver;
                            break;
                        }
                    }
                }
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
            FranklinArcadeBikeDriver bikeDriver = isDriving
                ? this.m_ActiveDriver as FranklinArcadeBikeDriver
                : null;
            bool shouldShow = bikeDriver != null;
            SetButtonActive(this.m_BikeHeadlightButton, shouldShow);
            SetButtonActive(this.m_BikeWheelieButton, shouldShow);
            SetButtonActive(this.m_BikeBurnoutButton, shouldShow);
            this.BindBikeHealth(bikeDriver);
            this.UpdateBikeSpeed(bikeDriver);
        }

        private void EnsureBikeHealthUi()
        {
            if (this.m_VehicleGroup == null) return;

            Transform existingRoot = this.m_VehicleGroup.Find("Bike Health UI");
            if (existingRoot is RectTransform root)
            {
                this.m_BikeHealthRoot = root;
                Transform background = root.Find("Background");
                if (background != null) background.gameObject.SetActive(false);
                Transform existingFill = root.Find("Fill");
                if (existingFill is RectTransform fill)
                {
                    this.m_BikeHealthFill = fill;
                    this.m_BikeHealthFillImage = fill.GetComponent<Image>();
                }

                if (this.m_BikeHealthFill != null && this.m_BikeHealthFillImage != null)
                {
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

            this.m_BikeHealthRoot.anchorMin = new Vector2(0.5f, 0f);
            this.m_BikeHealthRoot.anchorMax = new Vector2(0.5f, 0f);
            this.m_BikeHealthRoot.pivot = new Vector2(0.5f, 0.5f);
            this.m_BikeHealthRoot.anchoredPosition = new Vector2(0f, 48f);
            this.m_BikeHealthRoot.sizeDelta = new Vector2(760f, 74f);

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            this.m_BikeHealthFill = fillObject.GetComponent<RectTransform>();
            this.m_BikeHealthFill.SetParent(this.m_BikeHealthRoot, false);
            this.m_BikeHealthFill.anchorMin = new Vector2(0f, 0.5f);
            this.m_BikeHealthFill.anchorMax = new Vector2(0f, 0.5f);
            this.m_BikeHealthFill.pivot = new Vector2(0f, 0.5f);
            this.m_BikeHealthFill.anchoredPosition = new Vector2(14f, 0f);
            this.m_BikeHealthFill.sizeDelta = new Vector2(BIKE_HEALTH_FILL_WIDTH, 38f);
            this.m_BikeHealthFillImage = fillObject.GetComponent<Image>();
            this.m_BikeHealthFillImage.color = BIKE_HEALTH_GREEN;
            this.m_BikeHealthFillImage.raycastTarget = false;

            CreateBikeHealthImage(
                "Generated Health Frame",
                this.m_BikeHealthRoot,
                new Vector2(760f, 74f),
                this.m_BikeHealthFrameSprite,
                this.m_BikeHealthFrameSprite != null ? Color.white : Color.clear
            );
            this.m_BikeHealthRoot.gameObject.SetActive(false);
        }

        private static Image CreateBikeHealthImage(
            string objectName,
            Transform parent,
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
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private void BindBikeHealth(FranklinArcadeBikeDriver bikeDriver)
        {
            if (this.m_BikeHealthDriver == bikeDriver) return;

            if (this.m_ActiveBikeHealth != null)
                this.m_ActiveBikeHealth.EventHealthChanged -= this.OnBikeHealthChanged;

            this.m_BikeHealthDriver = bikeDriver;
            this.m_ActiveBikeHealth = bikeDriver != null
                ? bikeDriver.GetComponent<FranklinBikeHealth>()
                : null;

            bool visible = this.m_ActiveBikeHealth != null;
            if (this.m_BikeHealthRoot != null &&
                this.m_BikeHealthRoot.gameObject.activeSelf != visible)
            {
                this.m_BikeHealthRoot.gameObject.SetActive(visible);
            }

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
            if (this.m_BikeHealthFill != null)
            {
                this.m_BikeHealthFill.sizeDelta = new Vector2(
                    BIKE_HEALTH_FILL_WIDTH * ratio,
                    this.m_BikeHealthFill.sizeDelta.y
                );
            }
            if (this.m_BikeHealthFillImage != null)
            {
                this.m_BikeHealthFillImage.color = ratio > 0.5f
                    ? Color.Lerp(BIKE_HEALTH_YELLOW, BIKE_HEALTH_GREEN, (ratio - 0.5f) * 2f)
                    : Color.Lerp(BIKE_HEALTH_RED, BIKE_HEALTH_YELLOW, ratio * 2f);
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
                    this.m_BikeSpeedRoot.gameObject.SetActive(false);
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
            this.m_BikeSpeedRoot.gameObject.SetActive(false);
        }

        private void UpdateBikeSpeed(FranklinArcadeBikeDriver bikeDriver)
        {
            bool visible = bikeDriver != null;
            if (this.m_BikeSpeedRoot != null &&
                this.m_BikeSpeedRoot.gameObject.activeSelf != visible)
            {
                this.m_BikeSpeedRoot.gameObject.SetActive(visible);
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
        }

        private void ApplyBikeSpeedStyle()
        {
            if (this.m_BikeSpeedRoot != null)
                this.m_BikeSpeedRoot.sizeDelta = this.m_BikeSpeedRectSize;
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
            Rect canvasRect = this.m_VehicleGroup.rect;
            Vector2 half = this.m_BikeSpeedRoot.rect.size * 0.5f;
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
                "Bike Wheelie",
                "Bike Burnout"
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
        }

        private void ReleaseVehicleInputs(IRvrVehicleInputController driver)
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
                bikeDriver.SetHeadlightEnabled(false);
                bikeDriver.SetVirtualWheelieInput(false);
                bikeDriver.SetVirtualBurnoutInput(false);
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
        BikeBurnout
    }

}
