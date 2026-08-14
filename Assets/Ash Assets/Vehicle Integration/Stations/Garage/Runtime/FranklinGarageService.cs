using System.Collections.Generic;
using System.Globalization;
using FranklinGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Shared Car/Bike garage service. It turns the prefab's GC2 Markers into
    /// parking triggers and offers paid full repair plus up to 25%-capacity fuel
    /// per visit through Franklin's existing health, fuel and Wallet APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinGarageService : MonoBehaviour
    {
        private sealed class TankPresence
        {
            public MonoBehaviour Behaviour;
            public IFranklinFuelTank Tank;
            public int Contacts;
        }

        private static readonly CultureInfo DISPLAY_CULTURE =
            CultureInfo.InvariantCulture;
        private static readonly Color PANEL_COLOR =
            new(0.025f, 0.045f, 0.065f, 0.96f);
        private static readonly Color BUTTON_COLOR =
            new(0.055f, 0.085f, 0.11f, 0.98f);
        private static readonly Color REPAIR_ACCENT =
            new(0.18f, 0.78f, 1f, 1f);
        private static readonly Color FUEL_ACCENT =
            new(1f, 0.68f, 0.08f, 1f);
        private static readonly Color BLOCKED_TEXT =
            new(1f, 0.66f, 0.3f, 1f);

        [Header("Garage Marker Triggers")]
        [SerializeField] private string m_MarkerNamePrefix = "Marker";
        [SerializeField] private Vector3 m_TriggerCenter = new(0f, 1.5f, 4f);
        [SerializeField] private Vector3 m_TriggerSize = new(6.3f, 3f, 12f);
        [SerializeField, Min(0f)] private float m_MaxServiceSpeedKph = 3f;

        [Header("Repair Price")]
        [SerializeField, Min(0)] private int m_RepairBasePrice = 75;
        [SerializeField, Min(0f)] private float m_RepairPricePerHealthUnit = 12f;
        [SerializeField, Min(0.5f)] private float m_MinRepairDuration = 2.5f;
        [SerializeField, Min(0.5f)] private float m_MaxRepairDuration = 12f;

        [Header("Limited Garage Fuel")]
        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Maximum fraction of tank capacity added during one garage visit.")]
        private float m_MaxFuelFractionPerVisit = 0.25f;
        [SerializeField, Min(1)] private int m_PricePerFuelUnit = 5;

        [Header("Mobile Service UI")]
        [SerializeField] private Vector2 m_PanelPosition = new(0f, 175f);
        [SerializeField] private Vector2 m_PanelSize = new(820f, 250f);
        [SerializeField, Range(18, 42)] private int m_ActionFontSize = 23;

        private readonly Dictionary<int, TankPresence> m_Tanks = new();
        private IFranklinFuelTank m_ActiveTank;
        private FranklinPlayerStatusHud m_Wallet;
        private SimcadeCarHealth m_CarHealth;
        private FranklinBikeHealth m_BikeHealth;
        private SimcadeCarDeformation m_CarDeformation;
        private SimcadeCarDoorDamage m_CarDoorDamage;
        private FranklinBikeDeformation m_BikeDeformation;
        private SimcadeCarDestruction m_CarDestruction;
        private FranklinBikeDestruction m_BikeDestruction;
        private float m_FuelAddedThisVisit;

        private GameObject m_CanvasRoot;
        private RectTransform m_PanelRoot;
        private Text m_TitleText;
        private Text m_StatusText;
        private Button m_CloseButton;
        private RectTransform m_RepairProgressRoot;
        private Image m_RepairProgressFill;
        private Text m_RepairProgressText;
        private Button m_RepairButton;
        private Text m_RepairText;
        private Button m_FuelButton;
        private Text m_FuelText;
        private float m_NextRefresh;
        private bool m_PanelDismissedForVisit;
        private bool m_OwnsControlSuppression;
        private bool m_IsRepairing;
        private float m_RepairElapsed;
        private float m_RepairDuration;
        private float m_RepairServiceStartHealth;
        private float m_RepairMaximumHealth;

        public IFranklinFuelTank ActiveVehicle => this.m_ActiveTank;
        public float MaximumFuelFractionPerVisit => this.m_MaxFuelFractionPerVisit;

        private void Awake()
        {
            this.CreateMarkerZones();
        }

        private void OnDisable()
        {
            if (this.m_IsRepairing)
            {
                this.m_CarHealth?.RepairFull();
                this.m_BikeHealth?.RepairFull();
                this.m_CarDeformation?.ResetDeformation(true);
                this.m_CarDoorDamage?.RepairAllDoors();
                this.m_BikeDeformation?.ResetDeformation();
                this.m_IsRepairing = false;
                this.SetRepairProgress(0f);
            }
            this.m_Tanks.Clear();
            this.SelectTarget(null);
            this.SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            this.ReleaseControlSuppression();
            SimcadeCarDashboard.SetRefuelPromptVisible(false);
            if (this.m_CanvasRoot != null) Destroy(this.m_CanvasRoot);
        }

        private void Update()
        {
            if (this.m_IsRepairing) this.TickRepair(Time.unscaledDeltaTime);
            if (Time.unscaledTime < this.m_NextRefresh) return;
            this.m_NextRefresh = Time.unscaledTime + 0.1f;
            this.SelectActiveTank();
            this.RefreshPanel();
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
            this.RefreshPanel();
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
            this.SelectActiveTank();
            this.RefreshPanel();
        }

        public void RequestFullRepair()
        {
            if (this.m_IsRepairing || !this.CanServiceNow() ||
                this.IsTerminalWreck()) return;

            float current = this.GetCurrentHealth();
            float maximum = this.GetMaximumHealth();
            bool hasDoorDamage = this.m_CarDoorDamage != null &&
                                 this.m_CarDoorDamage.HasDamagedDoors;
            if (maximum <= 0f ||
                (current >= maximum - 0.001f && !hasDoorDamage))
            {
                return;
            }

            this.ResolveWallet();
            int price = this.GetRepairPrice();
            if (this.m_Wallet == null || !this.m_Wallet.TrySpendMoney(price)) return;

            float missingRatio = Mathf.Clamp01((maximum - current) / maximum);
            this.m_RepairDuration = Mathf.Lerp(
                this.m_MinRepairDuration,
                this.m_MaxRepairDuration,
                Mathf.Pow(missingRatio, 0.8f)
            );
            this.m_RepairElapsed = 0f;
            this.m_RepairMaximumHealth = maximum;

            // Stabilize a critical vehicle immediately so its warning fire does
            // not turn into a terminal explosion while the paid repair runs.
            float safeHealth = Mathf.Min(maximum, maximum * 0.16f);
            if (current < safeHealth) this.RepairHealth(safeHealth - current);
            this.m_RepairServiceStartHealth = this.GetCurrentHealth();
            this.m_IsRepairing = true;
            this.SetRepairProgress(0f);
            this.RefreshPanel();
        }

        public void RequestLimitedRefuel()
        {
            if (this.m_IsRepairing || !this.CanServiceNow() ||
                this.m_ActiveTank == null) return;
            this.ResolveWallet();
            if (this.m_Wallet == null || this.m_Wallet.CurrentMoney <= 0) return;

            float allowance = this.GetRemainingFuelAllowance();
            float missing = Mathf.Max(
                0f,
                this.m_ActiveTank.MaximumFuel - this.m_ActiveTank.CurrentFuel
            );
            float affordable = this.m_Wallet.CurrentMoney /
                               (float)Mathf.Max(1, this.m_PricePerFuelUnit);
            float requested = Mathf.Min(allowance, missing, affordable);
            if (requested <= 0.0001f) return;

            float accepted = this.m_ActiveTank.TryRefuel(requested);
            if (accepted <= 0.0001f) return;

            int cost = Mathf.Clamp(
                Mathf.CeilToInt(accepted * this.m_PricePerFuelUnit - 0.0001f),
                1,
                this.m_Wallet.CurrentMoney
            );
            if (!this.m_Wallet.TrySpendMoney(cost)) return;
            this.m_FuelAddedThisVisit += accepted;
            this.RefreshPanel();
        }

        private bool CanServiceNow()
        {
            return this.m_ActiveTank != null &&
                   this.m_ActiveTank.IsPlayerControlled &&
                   this.m_ActiveTank.SpeedKph <= this.m_MaxServiceSpeedKph;
        }

        private void SelectActiveTank()
        {
            if (this.m_IsRepairing) return;
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
            if (!ReferenceEquals(this.m_ActiveTank, selected)) this.SelectTarget(selected);
        }

        private void SelectTarget(IFranklinFuelTank tank)
        {
            this.m_ActiveTank = tank;
            this.m_FuelAddedThisVisit = 0f;
            this.m_PanelDismissedForVisit = false;
            this.m_CarHealth = null;
            this.m_BikeHealth = null;
            this.m_CarDeformation = null;
            this.m_CarDoorDamage = null;
            this.m_BikeDeformation = null;
            this.m_CarDestruction = null;
            this.m_BikeDestruction = null;

            GameObject vehicle = tank?.VehicleObject;
            if (vehicle == null) return;
            this.m_CarHealth = vehicle.GetComponent<SimcadeCarHealth>();
            this.m_BikeHealth = vehicle.GetComponent<FranklinBikeHealth>();
            this.m_CarDeformation = vehicle.GetComponent<SimcadeCarDeformation>();
            this.m_CarDoorDamage = vehicle.GetComponent<SimcadeCarDoorDamage>();
            this.m_BikeDeformation = vehicle.GetComponent<FranklinBikeDeformation>();
            this.m_CarDestruction = vehicle.GetComponent<SimcadeCarDestruction>();
            this.m_BikeDestruction = vehicle.GetComponent<FranklinBikeDestruction>();
        }

        private float GetCurrentHealth()
        {
            if (this.m_CarHealth != null) return this.m_CarHealth.CurrentHealth;
            return this.m_BikeHealth != null ? this.m_BikeHealth.CurrentHealth : 0f;
        }

        private float GetMaximumHealth()
        {
            if (this.m_CarHealth != null) return this.m_CarHealth.MaximumHealth;
            return this.m_BikeHealth != null ? this.m_BikeHealth.MaximumHealth : 0f;
        }

        private bool IsTerminalWreck()
        {
            return this.m_CarDestruction != null && this.m_CarDestruction.IsDestroyed ||
                   this.m_BikeDestruction != null && this.m_BikeDestruction.IsDestroyed;
        }

        private int GetRepairPrice()
        {
            float missing = Mathf.Max(0f, this.GetMaximumHealth() - this.GetCurrentHealth());
            bool hasDoorDamage = this.m_CarDoorDamage != null &&
                                 this.m_CarDoorDamage.HasDamagedDoors;
            if (missing <= 0.001f && !hasDoorDamage) return 0;
            return Mathf.Max(
                this.m_RepairBasePrice,
                this.m_RepairBasePrice +
                Mathf.CeilToInt(missing * this.m_RepairPricePerHealthUnit)
            );
        }

        private float GetRemainingFuelAllowance()
        {
            if (this.m_ActiveTank == null) return 0f;
            float visitLimit = this.m_ActiveTank.MaximumFuel *
                               Mathf.Clamp(this.m_MaxFuelFractionPerVisit, 0.01f, 0.25f);
            return Mathf.Max(0f, visitLimit - this.m_FuelAddedThisVisit);
        }

        private int GetFuelPrice()
        {
            if (this.m_ActiveTank == null) return 0;
            float amount = Mathf.Min(
                this.GetRemainingFuelAllowance(),
                Mathf.Max(0f,
                    this.m_ActiveTank.MaximumFuel - this.m_ActiveTank.CurrentFuel)
            );
            return Mathf.Max(
                0,
                Mathf.CeilToInt(amount * this.m_PricePerFuelUnit - 0.0001f)
            );
        }

        private void RefreshPanel()
        {
            bool visible = this.m_IsRepairing ||
                           this.m_ActiveTank != null &&
                           this.m_ActiveTank.IsPlayerControlled &&
                           this.m_ActiveTank.SpeedKph <= this.m_MaxServiceSpeedKph &&
                           !this.m_PanelDismissedForVisit;
            this.SetPanelVisible(visible);
            if (!visible || this.m_PanelRoot == null) return;

            this.ResolveWallet();
            bool stopped = this.m_ActiveTank.SpeedKph <= this.m_MaxServiceSpeedKph;
            bool terminal = this.IsTerminalWreck();
            float maximumHealth = this.GetMaximumHealth();
            float healthPercent = maximumHealth > 0f
                ? Mathf.Clamp01(this.GetCurrentHealth() / maximumHealth) * 100f
                : 0f;
            float fuelPercent = Mathf.Clamp01(this.m_ActiveTank.FuelRatio) * 100f;
            string vehicleName = this.m_BikeHealth != null ? "BIKE" : "CAR";
            this.m_TitleText.text = $"AUTO GARAGE  •  {vehicleName}";
            this.m_StatusText.text = this.m_IsRepairing
                ? $"VUI LÒNG CHỜ  •  {Mathf.Max(0f, this.m_RepairDuration - this.m_RepairElapsed):0.0}s"
                : stopped
                ? $"HEALTH {healthPercent:0}%   •   FUEL {fuelPercent:0}%"
                : $"DỪNG XE DƯỚI {this.m_MaxServiceSpeedKph:0} KM/H";
            this.m_CloseButton.interactable = !this.m_IsRepairing;

            int repairPrice = this.GetRepairPrice();
            bool repairNeeded = repairPrice > 0;
            bool canPayRepair = this.m_Wallet != null &&
                                this.m_Wallet.CanAfford(repairPrice);
            this.m_RepairButton.interactable = !this.m_IsRepairing &&
                                               stopped && !terminal &&
                                               repairNeeded && canPayRepair;
            this.m_RepairText.color = this.m_RepairButton.interactable
                ? Color.white
                : BLOCKED_TEXT;
            this.m_RepairText.text = this.m_IsRepairing
                ? "ĐANG SỬA XE..."
                : terminal
                ? "WRECK KHÔNG THỂ SỬA"
                : !stopped
                    ? "DỪNG XE ĐỂ SỬA"
                    : !repairNeeded
                        ? "XE ĐÃ HOÀN HẢO"
                        : !canPayRepair
                            ? $"THIẾU TIỀN  •  ${FormatMoney(repairPrice)}"
                            : $"SỬA TOÀN BỘ  •  ${FormatMoney(repairPrice)}";

            float allowance = this.GetRemainingFuelAllowance();
            bool tankFull = this.m_ActiveTank.CurrentFuel >=
                            this.m_ActiveTank.MaximumFuel - 0.001f;
            bool visitLimitReached = allowance <= 0.001f;
            int fuelPrice = this.GetFuelPrice();
            bool hasMoney = this.m_Wallet != null && this.m_Wallet.CurrentMoney > 0;
            this.m_FuelButton.interactable = !this.m_IsRepairing && stopped && !tankFull &&
                                             !visitLimitReached && hasMoney;
            this.m_FuelText.color = this.m_FuelButton.interactable
                ? Color.white
                : BLOCKED_TEXT;
            this.m_FuelText.text = this.m_IsRepairing
                ? "CHỜ SỬA XE HOÀN TẤT"
                : !stopped
                ? "DỪNG XE ĐỂ ĐỔ XĂNG"
                : tankFull
                    ? "BÌNH XĂNG ĐÃ ĐẦY"
                    : visitLimitReached
                        ? "ĐÃ ĐỔ ĐỦ 25% LƯỢT NÀY"
                        : !hasMoney
                            ? "KHÔNG ĐỦ TIỀN"
                            : $"ĐỔ THÊM TỐI ĐA 25%  •  ${FormatMoney(fuelPrice)}";
        }

        private static string FormatMoney(int amount)
        {
            return Mathf.Max(0, amount).ToString("N0", DISPLAY_CULTURE);
        }

        private void SetPanelVisible(bool visible)
        {
            if (visible && this.m_PanelRoot == null) this.EnsurePanel();
            bool show = visible && this.m_PanelRoot != null;
            if (this.m_PanelRoot != null && this.m_PanelRoot.gameObject.activeSelf != show)
                this.m_PanelRoot.gameObject.SetActive(show);
            SimcadeCarDashboard.SetRefuelPromptVisible(show);
            if (show) this.AcquireControlSuppression();
            else this.ReleaseControlSuppression();
        }

        private void ClosePanelForCurrentVisit()
        {
            if (this.m_IsRepairing) return;
            this.m_PanelDismissedForVisit = true;
            this.SetPanelVisible(false);
        }

        private void TickRepair(float deltaTime)
        {
            if (!this.m_IsRepairing) return;
            this.m_RepairElapsed = Mathf.Min(
                this.m_RepairDuration,
                this.m_RepairElapsed + Mathf.Max(0f, deltaTime)
            );
            float progress = this.m_RepairDuration > 0.0001f
                ? Mathf.Clamp01(this.m_RepairElapsed / this.m_RepairDuration)
                : 1f;
            float desiredHealth = Mathf.Lerp(
                this.m_RepairServiceStartHealth,
                this.m_RepairMaximumHealth,
                progress
            );
            this.RepairHealth(Mathf.Max(0f, desiredHealth - this.GetCurrentHealth()));
            this.SetRepairProgress(progress);
            if (progress < 1f) return;

            this.m_CarHealth?.RepairFull();
            this.m_BikeHealth?.RepairFull();
            this.m_CarDeformation?.ResetDeformation(true);
            this.m_CarDoorDamage?.RepairAllDoors();
            this.m_BikeDeformation?.ResetDeformation();
            this.m_IsRepairing = false;
            this.SetRepairProgress(0f);
            this.RefreshPanel();
        }

        private void RepairHealth(float amount)
        {
            if (amount <= 0.0001f) return;
            this.m_CarHealth?.Repair(amount);
            this.m_BikeHealth?.Repair(amount);
        }

        private void SetRepairProgress(float progress)
        {
            if (this.m_RepairProgressRoot == null) return;
            bool visible = this.m_IsRepairing;
            if (this.m_RepairProgressRoot.gameObject.activeSelf != visible)
                this.m_RepairProgressRoot.gameObject.SetActive(visible);
            if (!visible) return;
            float normalized = Mathf.Clamp01(progress);
            RectTransform fillRect = this.m_RepairProgressFill.rectTransform;
            fillRect.sizeDelta = new Vector2(
                Mathf.Max(0f, this.m_RepairProgressRoot.rect.width - 4f) * normalized,
                Mathf.Max(0f, this.m_RepairProgressRoot.rect.height - 4f)
            );
            this.m_RepairProgressText.text =
                $"ĐANG SỬA  {Mathf.RoundToInt(normalized * 100f)}%";
        }

        private void AcquireControlSuppression()
        {
            if (this.m_OwnsControlSuppression) return;
            FranklinMobileHud.AcquireControlsSuppression(this);
            this.m_OwnsControlSuppression = true;
        }

        private void ReleaseControlSuppression()
        {
            if (!this.m_OwnsControlSuppression) return;
            FranklinMobileHud.ReleaseControlsSuppression(this);
            this.m_OwnsControlSuppression = false;
        }

        private void EnsurePanel()
        {
            this.m_CanvasRoot = new GameObject(
                "Garage Service Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            Canvas canvas = this.m_CanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1230;
            CanvasScaler scaler = this.m_CanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelObject = new(
                "Garage Service Panel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline)
            );
            this.m_PanelRoot = panelObject.GetComponent<RectTransform>();
            this.m_PanelRoot.SetParent(this.m_CanvasRoot.transform, false);
            this.m_PanelRoot.anchorMin = this.m_PanelRoot.anchorMax =
                new Vector2(0.5f, 0f);
            this.m_PanelRoot.pivot = new Vector2(0.5f, 0.5f);
            this.m_PanelRoot.anchoredPosition = this.m_PanelPosition;
            this.m_PanelRoot.sizeDelta = this.m_PanelSize;
            panelObject.GetComponent<Image>().color = PANEL_COLOR;
            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.18f, 0.78f, 1f, 0.45f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            this.m_TitleText = this.CreateLabel(
                "Title", this.m_PanelRoot, new Vector2(0f, 98f),
                new Vector2(760f, 30f), 22, REPAIR_ACCENT
            );
            this.m_StatusText = this.CreateLabel(
                "Status", this.m_PanelRoot, new Vector2(0f, 65f),
                new Vector2(760f, 30f), 19, Color.white
            );
            this.m_CloseButton = this.CreateCloseButton();
            this.m_CloseButton.onClick.AddListener(this.ClosePanelForCurrentVisit);
            this.CreateRepairProgressBar();
            this.CreateDecoration(
                this.m_PanelRoot, new Vector2(0f, 29f),
                new Vector2(770f, 2f), new Color(1f, 1f, 1f, 0.14f)
            );

            this.m_RepairButton = this.CreateActionButton(
                "Repair Vehicle", -198f, "garage-repair-icon", REPAIR_ACCENT,
                out this.m_RepairText
            );
            this.m_RepairButton.onClick.AddListener(this.RequestFullRepair);
            this.m_FuelButton = this.CreateActionButton(
                "Limited Fuel", 198f, "garage-fuel-25-icon", FUEL_ACCENT,
                out this.m_FuelText
            );
            this.m_FuelButton.onClick.AddListener(this.RequestLimitedRefuel);
            this.m_PanelRoot.gameObject.SetActive(false);
        }

        private void CreateRepairProgressBar()
        {
            GameObject rootObject = new(
                "Repair Progress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline)
            );
            this.m_RepairProgressRoot = rootObject.GetComponent<RectTransform>();
            this.m_RepairProgressRoot.SetParent(this.m_PanelRoot, false);
            this.m_RepairProgressRoot.anchorMin =
                this.m_RepairProgressRoot.anchorMax = new Vector2(0.5f, 0.5f);
            this.m_RepairProgressRoot.anchoredPosition = new Vector2(0f, 40f);
            this.m_RepairProgressRoot.sizeDelta = new Vector2(770f, 18f);
            rootObject.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.66f);
            Outline outline = rootObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.2f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject fillObject = new(
                "Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)
            );
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(this.m_RepairProgressRoot, false);
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.sizeDelta = new Vector2(0f, 14f);
            this.m_RepairProgressFill = fillObject.GetComponent<Image>();
            this.m_RepairProgressFill.color = REPAIR_ACCENT;

            this.m_RepairProgressText = this.CreateLabel(
                "Progress Text", this.m_RepairProgressRoot, Vector2.zero,
                new Vector2(760f, 28f), 16, Color.white
            );
            this.m_RepairProgressRoot.gameObject.SetActive(false);
        }

        private Button CreateCloseButton()
        {
            GameObject buttonObject = new(
                "Close Garage UI",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button)
            );
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(this.m_PanelRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(376f, 92f);
            rect.sizeDelta = new Vector2(58f, 58f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.15f, 0.18f, 0.98f);
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.38f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.7f, 0.76f, 0.82f, 1f);
            button.colors = colors;

            Text closeText = this.CreateLabel(
                "Close Symbol", rect, Vector2.zero, new Vector2(54f, 54f),
                34, Color.white
            );
            closeText.text = "×";
            return button;
        }

        private Button CreateActionButton(
            string name,
            float x,
            string iconResource,
            Color accent,
            out Text label)
        {
            GameObject buttonObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button)
            );
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(this.m_PanelRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -32f);
            rect.sizeDelta = new Vector2(375f, 122f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = BUTTON_COLOR;
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.82f, 1f);
            colors.disabledColor = new Color(0.36f, 0.38f, 0.4f, 0.72f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            GameObject iconObject = new(
                "Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)
            );
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-128f, 0f);
            iconRect.sizeDelta = new Vector2(92f, 92f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = LoadSprite(iconResource);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            label = this.CreateLabel(
                "Label", rect, new Vector2(40f, 0f),
                new Vector2(245f, 92f), this.m_ActionFontSize, Color.white
            );
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        private Text CreateLabel(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Color color)
        {
            GameObject labelObject = new(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)
            );
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = labelObject.GetComponent<Text>();
            text.font = SimcadeCarDashboard.SharedHudFont != null
                ? SimcadeCarDashboard.SharedHudFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateDecoration(
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject decoration = new(
                "Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)
            );
            RectTransform rect = decoration.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = decoration.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadSprite(string resourceName)
        {
            Sprite sprite = Resources.Load<Sprite>(
                "FranklinMobileUI/" + resourceName
            );
            if (sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(
                "FranklinMobileUI/" + resourceName
            );
            return texture != null
                ? Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0u,
                    SpriteMeshType.FullRect
                )
                : null;
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
            foreach (Transform child in this.GetComponentsInChildren<Transform>(true))
            {
                if (child == this.transform ||
                    !child.name.StartsWith(
                        this.m_MarkerNamePrefix,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BoxCollider trigger = child.GetComponent<BoxCollider>();
                if (trigger == null) trigger = child.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = this.m_TriggerCenter;
                trigger.size = this.m_TriggerSize;

                FranklinGarageZone zone = child.GetComponent<FranklinGarageZone>();
                if (zone == null) zone = child.gameObject.AddComponent<FranklinGarageZone>();
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
            foreach (MonoBehaviour candidate in
                     origin.GetComponentsInParent<MonoBehaviour>(true))
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
            this.m_MaxServiceSpeedKph = Mathf.Max(0f, this.m_MaxServiceSpeedKph);
            this.m_RepairBasePrice = Mathf.Max(0, this.m_RepairBasePrice);
            this.m_RepairPricePerHealthUnit =
                Mathf.Max(0f, this.m_RepairPricePerHealthUnit);
            this.m_MinRepairDuration = Mathf.Max(0.5f, this.m_MinRepairDuration);
            this.m_MaxRepairDuration = Mathf.Max(
                this.m_MinRepairDuration,
                this.m_MaxRepairDuration
            );
            this.m_MaxFuelFractionPerVisit =
                Mathf.Clamp(this.m_MaxFuelFractionPerVisit, 0.01f, 0.25f);
            this.m_PricePerFuelUnit = Mathf.Max(1, this.m_PricePerFuelUnit);
            this.m_ActionFontSize = Mathf.Clamp(this.m_ActionFontSize, 18, 42);
        }
#endif
    }
}
