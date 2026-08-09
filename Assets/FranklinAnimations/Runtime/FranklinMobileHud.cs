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
    /// existing Tactile movement stick while on foot and swaps to Sim-Cade controls in a car.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class FranklinMobileHud : MonoBehaviour
    {
        private const float REFERENCE_REFRESH_SECONDS = 0.5f;
        private const string RESOURCE_ROOT = "FranklinMobileUI/";

        private static readonly Dictionary<string, Sprite> SPRITES = new();
        private static FranklinMobileHud s_Instance;

        private RectTransform m_OnFootGroup;
        private RectTransform m_VehicleGroup;
        private GameObject m_EnterVehicleButton;
        private FranklinHudButton m_SlowDriveButton;
        private FranklinAnimationBridge m_MovementBridge;
        private FranklinVehicleInteractionManager m_VehicleInteraction;
        private SimcadeCarDriver m_ActiveDriver;
        private GameObject m_TactileCanvas;
        private GameObject m_TactileMoveStick;
        private float m_NextReferenceRefresh;
        private bool m_WasDriving;
        private bool m_HasAppliedMode;
        private bool m_UsesCanvasPlayerControl;

        public static bool IsActive => s_Instance != null &&
                                       s_Instance.isActiveAndEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
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
        }

        private void OnDestroy()
        {
            this.ReleaseMovementInputs();
            this.ReleaseVehicleInputs(this.m_ActiveDriver);
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            this.RefreshReferences(false);

            bool isDriving = this.m_ActiveDriver != null &&
                             this.m_ActiveDriver.IsVehicleEnabled;
            if (!this.m_HasAppliedMode || isDriving != this.m_WasDriving)
            {
                this.ApplyMode(isDriving);
            }

            this.UpdateEnterVehicleButton(isDriving);
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
                case FranklinHudAction.VehicleInteraction:
                    if (!active) break;
                    if (this.m_ActiveDriver != null && this.m_ActiveDriver.IsVehicleEnabled)
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
            Vector2 size)
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
            button.Initialize(this, action, image);
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

            SimcadeCarDriver previousDriver = this.m_ActiveDriver;
            if (this.m_ActiveDriver == null || !this.m_ActiveDriver.IsVehicleEnabled)
            {
                this.m_ActiveDriver = null;
                foreach (SimcadeCarDriver driver in FindObjectsByType<SimcadeCarDriver>(
                    FindObjectsSortMode.None))
                {
                    if (driver != null && driver.IsVehicleEnabled)
                    {
                        this.m_ActiveDriver = driver;
                        break;
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

        private void ReleaseMovementInputs()
        {
            this.m_MovementBridge?.SetVirtualJogInput(false);
            this.m_MovementBridge?.SetVirtualSprintInput(false);
            this.m_MovementBridge?.StopVirtualAutoRun();
        }

        private void ReleaseVehicleInputs(SimcadeCarDriver driver)
        {
            if (driver == null) return;
            driver.SetVirtualAccelerateInput(false);
            driver.SetVirtualSlowAccelerateInput(false);
            driver.SetVirtualBrakeReverseInput(false);
            driver.SetVirtualSteerLeftInput(false);
            driver.SetVirtualSteerRightInput(false);
            driver.SetVirtualHandbrakeInput(false);
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
        SlowDrive
    }

}
