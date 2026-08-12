using System.Collections;
using System.Collections.Generic;
using FranklinGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Discovers the GC2 Markers in a fuel-station prefab, creates lightweight
    /// trigger zones and performs paid, hold-to-refuel transactions for Cars/Bikes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinFuelStation : MonoBehaviour
    {
        private sealed class TankPresence
        {
            public MonoBehaviour Behaviour;
            public IFranklinFuelTank Tank;
            public int Contacts;
        }

        private static readonly Color READY_TEXT = Color.white;
        private static readonly Color BLOCKED_TEXT = new(1f, 0.72f, 0.3f, 1f);
        private static readonly Color PANEL_COLOR =
            new(0.035f, 0.055f, 0.075f, 0.94f);
        private static readonly Color PANEL_PRESSED_COLOR =
            new(0.11f, 0.085f, 0.03f, 0.98f);
        private static readonly Color FUEL_ACCENT =
            new(1f, 0.67f, 0.08f, 0.92f);

        [Header("Marker Trigger")]
        [SerializeField] private string m_MarkerNamePrefix = "Marker";
        [SerializeField] private Vector3 m_TriggerCenter = new(0f, 1.25f, 0f);
        [SerializeField] private Vector3 m_TriggerSize = new(5f, 2.5f, 5f);
        [SerializeField, Min(0f)] private float m_MaxRefuelSpeedKph = 3f;

        [Header("Price And Flow")]
        [SerializeField, Min(1)] private int m_PricePerFuelUnit = 5;
        [SerializeField, Min(0.1f)] private float m_FuelUnitsPerSecond = 5f;
        [SerializeField, Range(0.1f, 0.5f)] private float m_TransactionInterval = 0.2f;

        [Header("Mobile Button")]
        [SerializeField] private Vector2 m_ButtonPosition = new(0f, 70f);
        [SerializeField] private Vector2 m_ButtonSize = new(620f, 96f);
        [SerializeField, Range(18, 52)] private int m_ButtonFontSize = 24;

        private readonly Dictionary<int, TankPresence> m_Tanks = new();
        private IFranklinFuelTank m_ActiveTank;
        private FranklinPlayerStatusHud m_Wallet;
        private GameObject m_ButtonCanvasRoot;
        private GameObject m_ButtonRoot;
        private Text m_ButtonTitleText;
        private Text m_ButtonText;
        private Image m_ButtonProgressFill;
        private FranklinFuelStationHoldButton m_HoldButton;
        private Coroutine m_RefuelRoutine;
        private bool m_IsHeld;
        private float m_NextUiRefresh;

        public bool CanBeginRefueling => this.GetBlockReason() == RefuelBlock.None;

        private enum RefuelBlock
        {
            None,
            NoVehicle,
            Moving,
            Full,
            NoMoney,
            NoWallet
        }

        private void Awake()
        {
            this.CreateMarkerZones();
        }

        private void OnDisable()
        {
            this.SetRefuelHeld(false);
            this.SetButtonVisible(false);
            this.m_Tanks.Clear();
            this.m_ActiveTank = null;
        }

        private void OnDestroy()
        {
            SimcadeCarDashboard.SetRefuelPromptVisible(false);
            if (this.m_ButtonCanvasRoot != null) Destroy(this.m_ButtonCanvasRoot);
        }

        private void Update()
        {
            if (this.m_Tanks.Count == 0 && this.m_ActiveTank == null) return;
            if (Time.unscaledTime < this.m_NextUiRefresh) return;

            this.m_NextUiRefresh = Time.unscaledTime + 0.1f;
            this.SelectActiveTank();
            this.RefreshButton();
        }

        internal void RegisterContact(Collider other)
        {
            if (!TryResolveTank(other, out MonoBehaviour behaviour,
                    out IFranklinFuelTank tank))
            {
                return;
            }

            int id = behaviour.GetInstanceID();
            if (!this.m_Tanks.TryGetValue(id, out TankPresence presence))
            {
                presence = new TankPresence
                {
                    Behaviour = behaviour,
                    Tank = tank,
                    Contacts = 0
                };
                this.m_Tanks.Add(id, presence);
            }

            presence.Contacts++;
            this.SelectActiveTank();
            this.RefreshButton();
        }

        internal void UnregisterContact(Collider other)
        {
            if (!TryResolveTank(other, out MonoBehaviour behaviour,
                    out IFranklinFuelTank _))
            {
                return;
            }

            int id = behaviour.GetInstanceID();
            if (!this.m_Tanks.TryGetValue(id, out TankPresence presence)) return;

            presence.Contacts--;
            if (presence.Contacts <= 0) this.m_Tanks.Remove(id);

            if (ReferenceEquals(this.m_ActiveTank, presence.Tank))
            {
                this.SetRefuelHeld(false);
                this.m_ActiveTank = null;
            }

            this.SelectActiveTank();
            this.RefreshButton();
        }

        internal void SetRefuelHeld(bool held)
        {
            if (held && !this.CanBeginRefueling) return;
            this.m_IsHeld = held;

            if (held)
            {
                if (this.m_RefuelRoutine == null)
                    this.m_RefuelRoutine = this.StartCoroutine(this.RefuelWhileHeld());
            }
            else if (this.m_RefuelRoutine != null)
            {
                this.StopCoroutine(this.m_RefuelRoutine);
                this.m_RefuelRoutine = null;
            }
        }

        private IEnumerator RefuelWhileHeld()
        {
            float interval = Mathf.Clamp(this.m_TransactionInterval, 0.1f, 0.5f);
            WaitForSecondsRealtime wait = new(interval);

            while (this.m_IsHeld && this.CanBeginRefueling)
            {
                this.ProcessTransaction(interval);
                this.RefreshButton();
                yield return wait;
            }

            this.m_RefuelRoutine = null;
            this.m_IsHeld = false;
            this.m_HoldButton?.ForceRelease();
            this.RefreshButton();
        }

        private void ProcessTransaction(float interval)
        {
            if (this.m_ActiveTank == null || this.m_Wallet == null) return;

            float missing = Mathf.Max(
                0f,
                this.m_ActiveTank.MaximumFuel - this.m_ActiveTank.CurrentFuel
            );
            float affordable = this.m_Wallet.CurrentMoney /
                               (float)Mathf.Max(1, this.m_PricePerFuelUnit);
            float request = Mathf.Min(
                this.m_FuelUnitsPerSecond * interval,
                missing,
                affordable
            );
            if (request <= 0.0001f) return;

            float accepted = this.m_ActiveTank.TryRefuel(request);
            if (accepted <= 0.0001f) return;

            int cost = Mathf.Clamp(
                Mathf.CeilToInt(accepted * this.m_PricePerFuelUnit - 0.0001f),
                1,
                this.m_Wallet.CurrentMoney
            );
            this.m_Wallet.TrySpendMoney(cost);
        }

        private void SelectActiveTank()
        {
            IFranklinFuelTank selected = null;
            List<int> stale = null;

            foreach (KeyValuePair<int, TankPresence> pair in this.m_Tanks)
            {
                TankPresence presence = pair.Value;
                if (presence.Behaviour == null || presence.Contacts <= 0)
                {
                    stale ??= new List<int>();
                    stale.Add(pair.Key);
                    continue;
                }

                if (presence.Tank.IsPlayerControlled)
                {
                    selected = presence.Tank;
                    break;
                }
            }

            if (stale != null)
            {
                foreach (int id in stale) this.m_Tanks.Remove(id);
            }

            if (!ReferenceEquals(this.m_ActiveTank, selected))
            {
                this.SetRefuelHeld(false);
                this.m_ActiveTank = selected;
            }
        }

        private RefuelBlock GetBlockReason()
        {
            if (this.m_ActiveTank == null || !this.m_ActiveTank.IsPlayerControlled)
                return RefuelBlock.NoVehicle;
            if (this.m_ActiveTank.SpeedKph > this.m_MaxRefuelSpeedKph)
                return RefuelBlock.Moving;
            if (this.m_ActiveTank.MaximumFuel <= 0f ||
                this.m_ActiveTank.CurrentFuel >= this.m_ActiveTank.MaximumFuel - 0.001f)
                return RefuelBlock.Full;

            this.ResolveWallet();
            if (this.m_Wallet == null) return RefuelBlock.NoWallet;
            if (this.m_Wallet.CurrentMoney <= 0) return RefuelBlock.NoMoney;
            return RefuelBlock.None;
        }

        private void RefreshButton()
        {
            bool visible = this.m_ActiveTank != null &&
                           this.m_ActiveTank.IsPlayerControlled;
            this.SetButtonVisible(visible);
            if (!visible || this.m_ButtonText == null) return;

            RefuelBlock block = this.GetBlockReason();
            float percent = this.m_ActiveTank != null
                ? Mathf.Clamp01(this.m_ActiveTank.FuelRatio) * 100f
                : 0f;
            int remainingCost = this.GetRemainingFillCost();
            string costText = remainingCost.ToString(
                "N0",
                System.Globalization.CultureInfo.InvariantCulture
            );

            this.m_ButtonText.color = block == RefuelBlock.None
                ? READY_TEXT
                : BLOCKED_TEXT;
            if (this.m_ButtonTitleText != null)
                this.m_ButtonTitleText.text = $"FUEL STATION  •  {percent:0}%";
            if (this.m_ButtonProgressFill != null)
                this.m_ButtonProgressFill.fillAmount = Mathf.Clamp01(
                    this.m_ActiveTank.FuelRatio
                );
            this.m_ButtonText.text = block switch
            {
                RefuelBlock.Moving => "DỪNG XE ĐỂ ĐỔ XĂNG",
                RefuelBlock.Full => $"BÌNH XĂNG ĐÃ ĐẦY  •  {percent:0}%",
                RefuelBlock.NoMoney => "KHÔNG ĐỦ TIỀN",
                RefuelBlock.NoWallet => "CHƯA TÌM THẤY VÍ TIỀN",
                _ => this.m_IsHeld
                    ? $"ĐANG ĐỔ  •  {percent:0}%  •  CÒN ${costText}"
                    : $"GIỮ ĐỂ ĐỔ ĐẦY  •  ${costText}"
            };
        }

        private int GetRemainingFillCost()
        {
            if (this.m_ActiveTank == null) return 0;
            float missing = Mathf.Max(
                0f,
                this.m_ActiveTank.MaximumFuel - this.m_ActiveTank.CurrentFuel
            );
            return Mathf.Max(
                0,
                Mathf.CeilToInt(missing * this.m_PricePerFuelUnit - 0.0001f)
            );
        }

        private void SetButtonVisible(bool visible)
        {
            if (visible && this.m_ButtonRoot == null) this.EnsureButton();
            bool showPrompt = visible && this.m_ButtonRoot != null;
            if (this.m_ButtonRoot != null && this.m_ButtonRoot.activeSelf != showPrompt)
                this.m_ButtonRoot.SetActive(showPrompt);
            SimcadeCarDashboard.SetRefuelPromptVisible(showPrompt);
            if (!showPrompt) this.m_HoldButton?.ForceRelease();
        }

        private void EnsureButton()
        {
            this.m_ButtonCanvasRoot = new GameObject(
                "Fuel Station Prompt Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            Canvas canvas = this.m_ButtonCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1220;
            CanvasScaler scaler = this.m_ButtonCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            this.m_ButtonRoot = new GameObject(
                "Fuel Station Hold Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(FranklinFuelStationHoldButton)
            );
            RectTransform rect = this.m_ButtonRoot.GetComponent<RectTransform>();
            rect.SetParent(this.m_ButtonCanvasRoot.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = this.m_ButtonPosition;
            rect.sizeDelta = this.m_ButtonSize;

            Image background = this.m_ButtonRoot.GetComponent<Image>();
            background.color = PANEL_COLOR;
            background.raycastTarget = true;

            Outline outline = this.m_ButtonRoot.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 0.67f, 0.08f, 0.52f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            Sprite buttonSprite = Resources.Load<Sprite>(
                "FranklinMobileUI/fuel-pump-icon"
            );
            GameObject iconObject = new GameObject(
                "Fuel Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-267f, 0f);
            iconRect.sizeDelta = new Vector2(68f, 68f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = buttonSprite;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;

            this.CreateDecoration(
                "Fuel Divider",
                rect,
                new Vector2(-216f, 0f),
                new Vector2(2f, 54f),
                new Color(1f, 0.67f, 0.08f, 0.34f)
            );

            this.m_ButtonTitleText = this.CreateLabel(
                "Title",
                rect,
                new Vector2(46f, 19f),
                new Vector2(470f, 22f),
                16,
                TextAnchor.MiddleLeft,
                FUEL_ACCENT
            );
            this.m_ButtonTitleText.text = "FUEL STATION";

            this.m_ButtonText = this.CreateLabel(
                "Label",
                rect,
                new Vector2(46f, -10f),
                new Vector2(470f, 38f),
                this.m_ButtonFontSize,
                TextAnchor.MiddleLeft,
                READY_TEXT
            );

            this.CreateDecoration(
                "Fuel Progress Track",
                rect,
                new Vector2(0f, -45f),
                new Vector2(590f, 3f),
                new Color(1f, 1f, 1f, 0.14f)
            );
            this.m_ButtonProgressFill = this.CreateDecoration(
                "Fuel Progress Fill",
                rect,
                new Vector2(0f, -45f),
                new Vector2(590f, 3f),
                FUEL_ACCENT
            );
            this.m_ButtonProgressFill.type = Image.Type.Filled;
            this.m_ButtonProgressFill.fillMethod = Image.FillMethod.Horizontal;
            this.m_ButtonProgressFill.fillOrigin = 0;
            this.m_ButtonProgressFill.fillAmount = 0f;

            this.m_HoldButton =
                this.m_ButtonRoot.GetComponent<FranklinFuelStationHoldButton>();
            this.m_HoldButton.Initialize(
                this,
                background,
                PANEL_COLOR,
                PANEL_PRESSED_COLOR
            );
            this.m_ButtonRoot.SetActive(false);
        }

        private Text CreateLabel(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject labelObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = position;
            labelRect.sizeDelta = size;

            Text label = labelObject.GetComponent<Text>();
            label.font = SimcadeCarDashboard.SharedHudFont != null
                ? SimcadeCarDashboard.SharedHudFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private Image CreateDecoration(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject decorationObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform decorationRect =
                decorationObject.GetComponent<RectTransform>();
            decorationRect.SetParent(parent, false);
            decorationRect.anchorMin = decorationRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            decorationRect.pivot = new Vector2(0.5f, 0.5f);
            decorationRect.anchoredPosition = position;
            decorationRect.sizeDelta = size;
            Image decoration = decorationObject.GetComponent<Image>();
            decoration.color = color;
            decoration.raycastTarget = false;
            return decoration;
        }

        private void ResolveWallet()
        {
            if (this.m_Wallet != null) return;
            this.m_Wallet = FranklinPlayerStatusHud.Instance != null
                ? FranklinPlayerStatusHud.Instance
                : FindFirstObjectByType<FranklinPlayerStatusHud>();
        }

        private void CreateMarkerZones()
        {
            Transform[] children = this.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child == this.transform ||
                    !child.name.StartsWith(this.m_MarkerNamePrefix,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BoxCollider trigger = child.GetComponent<BoxCollider>();
                if (trigger == null) trigger = child.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = this.m_TriggerCenter;
                trigger.size = this.m_TriggerSize;

                FranklinFuelStationZone zone =
                    child.GetComponent<FranklinFuelStationZone>();
                if (zone == null)
                    zone = child.gameObject.AddComponent<FranklinFuelStationZone>();
                zone.Initialize(this);
            }
        }

        private static bool TryResolveTank(
            Collider collider,
            out MonoBehaviour behaviour,
            out IFranklinFuelTank tank)
        {
            behaviour = null;
            tank = null;
            if (collider == null) return false;

            Transform origin = collider.attachedRigidbody != null
                ? collider.attachedRigidbody.transform
                : collider.transform;
            MonoBehaviour[] candidates = origin.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour candidate in candidates)
            {
                if (candidate is not IFranklinFuelTank fuelTank) continue;
                behaviour = candidate;
                tank = fuelTank;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(this.m_MarkerNamePrefix))
                this.m_MarkerNamePrefix = "Marker";
            this.m_TriggerSize.x = Mathf.Max(0.5f, this.m_TriggerSize.x);
            this.m_TriggerSize.y = Mathf.Max(0.5f, this.m_TriggerSize.y);
            this.m_TriggerSize.z = Mathf.Max(0.5f, this.m_TriggerSize.z);
            this.m_MaxRefuelSpeedKph = Mathf.Max(0f, this.m_MaxRefuelSpeedKph);
            this.m_PricePerFuelUnit = Mathf.Max(1, this.m_PricePerFuelUnit);
            this.m_FuelUnitsPerSecond = Mathf.Max(0.1f, this.m_FuelUnitsPerSecond);
            this.m_TransactionInterval = Mathf.Clamp(
                this.m_TransactionInterval,
                0.1f,
                0.5f
            );
            this.m_ButtonFontSize = Mathf.Clamp(this.m_ButtonFontSize, 18, 52);
        }
#endif
    }
}
