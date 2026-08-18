using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// One persistent touch HUD. It uses the scene EventSystem and never creates a
    /// second input module. Decorative graphics do not participate in raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DroneMobileHud : MonoBehaviour
    {
        [Header("Serialized Canvas Hierarchy")]
        [SerializeField] private RectTransform m_SafeArea;
        [SerializeField] private GameObject m_ConnectButton;
        [SerializeField] private GameObject m_FlightGroup;
        [SerializeField] private DroneHudActionButton m_ConnectAction;
        [SerializeField] private DroneHudActionButton m_ExitAction;
        [SerializeField] private DroneVirtualStick m_MoveStick;
        [SerializeField] private RectTransform m_MoveStickKnob;
        [SerializeField] private DroneHoldButton m_AscendButton;
        [SerializeField] private DroneHoldButton m_DescendButton;
        [SerializeField] private DroneOrbitArea m_OrbitArea;

        private IAirHudActionHandler m_Owner;
        private Canvas m_Canvas;
        private GraphicRaycaster m_Raycaster;
        private bool m_ShowTouchControls;
        private bool m_HasAppliedState;
        private bool m_LastCanConnect;
        private bool m_LastIsControlling;
        private Rect m_LastSafeArea;
        private int m_LastScreenWidth = -1;
        private int m_LastScreenHeight = -1;

        public Vector2 MoveInput => this.m_MoveStick != null
            ? this.m_MoveStick.Value
            : Vector2.zero;

        public float LiftInput =>
            (this.m_AscendButton != null && this.m_AscendButton.IsHeld ? 1f : 0f) -
            (this.m_DescendButton != null && this.m_DescendButton.IsHeld ? 1f : 0f);

        public static DroneMobileHud Create(
            DroneMobileHud prefab,
            IAirHudActionHandler owner,
            Transform host,
            bool showTouchControls)
        {
            if (prefab == null || host == null) return null;

            DroneMobileHud hud = Instantiate(prefab, host, false);
            hud.name = "Canvas Air Control";
            hud.Initialize(owner, showTouchControls);
            return hud;
        }

        private void Initialize(IAirHudActionHandler owner, bool showTouchControls)
        {
            this.m_Owner = owner;
            this.m_ShowTouchControls = showTouchControls;

            RectTransform rootRect = this.transform as RectTransform;
            Stretch(rootRect);

            this.m_Canvas = this.GetComponent<Canvas>();
            this.m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            this.m_Canvas.pixelPerfect = false;
            this.m_Canvas.sortingOrder = 250;
            this.m_Canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            CanvasScaler scaler = this.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            this.m_Raycaster = this.GetComponent<GraphicRaycaster>();
            this.m_Raycaster.ignoreReversedGraphics = true;
            this.m_Raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            Stretch(this.m_SafeArea);
            if (this.m_ConnectAction != null)
                this.m_ConnectAction.Initialize(this.m_Owner, DroneHudAction.Connect);
            if (this.m_ExitAction != null)
                this.m_ExitAction.Initialize(this.m_Owner, DroneHudAction.Exit);
            if (this.m_MoveStick != null)
                this.m_MoveStick.Initialize(this.m_MoveStickKnob, 0.32f, 0.08f);

            this.UpdateSafeArea(true);
            this.SetState(false, false);
        }

        private void Update()
        {
            if (this.m_Canvas != null && this.m_Canvas.enabled)
            {
                this.UpdateSafeArea(false);
            }
        }

        private void OnDisable()
        {
            this.ResetInput();
        }

        public void SetState(bool canConnect, bool isControlling)
        {
            if (this.m_HasAppliedState && this.m_LastCanConnect == canConnect &&
                this.m_LastIsControlling == isControlling)
            {
                return;
            }
            this.m_HasAppliedState = true;
            this.m_LastCanConnect = canConnect;
            this.m_LastIsControlling = isControlling;

            bool visible = this.m_ShowTouchControls && (canConnect || isControlling);
            if (this.m_Canvas != null) this.m_Canvas.enabled = visible;
            if (this.m_Raycaster != null) this.m_Raycaster.enabled = visible;
            if (this.m_ConnectButton != null)
            {
                this.m_ConnectButton.SetActive(visible && canConnect && !isControlling);
            }
            if (this.m_FlightGroup != null)
            {
                this.m_FlightGroup.SetActive(visible && isControlling);
            }
            if (!isControlling) this.ResetInput();
            if (visible) this.UpdateSafeArea(true);
            // Remove this HUD from Unity's Update loop while it is invisible.
            this.enabled = visible;
        }

        public void ResetInput()
        {
            if (this.m_MoveStick != null) this.m_MoveStick.ResetInput();
            if (this.m_AscendButton != null) this.m_AscendButton.ResetInput();
            if (this.m_DescendButton != null) this.m_DescendButton.ResetInput();
            if (this.m_OrbitArea != null) this.m_OrbitArea.ResetInput();
            if (this.m_ConnectAction != null) this.m_ConnectAction.ResetVisual();
            if (this.m_ExitAction != null) this.m_ExitAction.ResetVisual();
        }

        private void UpdateSafeArea(bool force)
        {
            if (this.m_SafeArea == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safeArea = Screen.safeArea;
            if (!force && this.m_LastScreenWidth == Screen.width &&
                this.m_LastScreenHeight == Screen.height && this.m_LastSafeArea == safeArea)
            {
                return;
            }

            this.m_LastScreenWidth = Screen.width;
            this.m_LastScreenHeight = Screen.height;
            this.m_LastSafeArea = safeArea;
            this.m_SafeArea.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height
            );
            this.m_SafeArea.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height
            );
            this.m_SafeArea.offsetMin = Vector2.zero;
            this.m_SafeArea.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

    }
}
