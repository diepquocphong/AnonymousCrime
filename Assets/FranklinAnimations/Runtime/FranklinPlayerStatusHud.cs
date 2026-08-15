using System;
using System.Globalization;
using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.UI
{
    /// <summary>
    /// Presents the player's top-right status HUD. The prefab ships with demo values, while
    /// the public setters provide the hand-off point for real gameplay data later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinPlayerStatusHud : MonoBehaviour
    {
        private static FranklinPlayerStatusHud s_Instance;

        [Header("Demo Data")]
        [SerializeField, Min(0)] private int m_Money = 12480;
        [SerializeField, Range(0f, 1f)] private float m_NormalizedHealth = 0.78f;
        [SerializeField, Range(0f, 1f)] private float m_NormalizedArmor = 1f;
        [SerializeField, Min(0f)] private float m_ArmorValue = 100f;
        [SerializeField] private string m_WeaponName = "PISTOL";
        [SerializeField, Min(0)] private int m_AmmoInClip = 12;
        [SerializeField, Min(0)] private int m_AmmoReserve = 48;

        [Header("Money Presentation")]
        [SerializeField, Range(0.05f, 1f)] private float m_MoneyAnimationSmoothTime = 0.28f;

        [Header("Weapon Presentation")]
        [SerializeField] private string m_UnarmedLabel = "FISTS";
        [SerializeField] private Vector2 m_ArmedCardPosition = new(-255f, -83f);
        [SerializeField] private Vector2 m_ArmedCardSize = new(430f, 86f);
        [SerializeField] private Vector2 m_UnarmedCardPosition = new(-170f, -83f);
        [SerializeField] private Vector2 m_UnarmedCardSize = new(260f, 86f);
        [SerializeField, Min(0f)] private float m_WeaponLayoutSpeed = 16f;

        [Header("Game Creator 2 Player Health")]
        [SerializeField]
        [Tooltip("Traits Attribute ID used as the Player's current health")]
        private string m_HealthAttributeId = "hp";
        [SerializeField, Min(0.1f)] private float m_PlayerLookupInterval = 0.5f;

        [Header("UI References")]
        [SerializeField] private Text m_MoneyText;
        [SerializeField] private Image m_HealthFill;
        [SerializeField] private Text m_HealthText;
        [SerializeField] private Image m_ArmorFill;
        [SerializeField] private Text m_ArmorText;
        [SerializeField] private RectTransform m_WeaponCard;
        [SerializeField] private Image m_WeaponIcon;
        [SerializeField] private Sprite m_ArmedWeaponSprite;
        [SerializeField] private Sprite m_UnarmedWeaponSprite;
        [SerializeField] private Text m_WeaponNameText;
        [SerializeField] private Text m_AmmoText;
        [SerializeField] private Button m_GrenadeButton;
        [SerializeField] private Button m_MolotovButton;
        [SerializeField] private Image m_GrenadeSelection;
        [SerializeField] private Image m_MolotovSelection;

        private static readonly CultureInfo DISPLAY_CULTURE = CultureInfo.InvariantCulture;
        private bool m_QuickItemListenersBound;
        private bool m_MolotovSelected;
        private Traits m_PlayerTraits;
        private RuntimeAttributeData m_PlayerHealth;
        private float m_NextPlayerLookupTime;
        private bool m_PlayerHealthBound;
        private bool m_HasWarnedMissingHealth;
        private FranklinArmor m_PlayerArmor;
        private float m_NextArmorLookupTime;
        private bool m_PlayerArmorBound;
        private Character m_PlayerCharacter;
        private ShooterWeapon m_PlayerWeapon;
        private Sprite m_RuntimeWeaponSprite;
        private float m_NextPlayerWeaponRefreshTime;
        private bool m_PlayerWeaponBound;
        private bool m_IsUnarmed;
        private bool m_IsAmmoInfinite;
        private bool m_WeaponLayoutInitialized;
        private Vector2 m_TargetWeaponCardPosition;
        private Vector2 m_TargetWeaponCardSize;
        private Vector2 m_TargetWeaponIconPosition;
        private Vector2 m_TargetWeaponNamePosition;
        private float m_DisplayedMoney;
        private float m_MoneyDisplayVelocity;
        private bool m_MoneyDisplayInitialized;

        public static FranklinPlayerStatusHud Instance => s_Instance;
        public int CurrentMoney => this.m_Money;
        public event Action<int> EventMoneyChanged;

        /// <summary>
        /// Hides the complete weapon group without changing its saved layout.
        /// This includes the weapon card and the armor/grenade/molotov rail.
        /// </summary>
        public void SetWeaponHudVisible(bool visible)
        {
            if (this.m_WeaponCard != null &&
                this.m_WeaponCard.gameObject.activeSelf != visible)
            {
                this.m_WeaponCard.gameObject.SetActive(visible);
            }

            Transform quickItemRail = this.m_WeaponCard != null
                ? this.m_WeaponCard.parent?.Find("Quick Item Rail")
                : null;
            if (quickItemRail != null && quickItemRail.gameObject.activeSelf != visible)
            {
                quickItemRail.gameObject.SetActive(visible);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
        }

        private void Awake()
        {
            s_Instance = this;
            this.m_DisplayedMoney = this.m_Money;
            this.m_MoneyDisplayInitialized = true;
            this.RefreshView();
        }

        private void OnEnable()
        {
            this.BindQuickItemButtons();
            this.TryBindPlayerHealth(true);
            this.TryBindPlayerArmor(true);
            this.TryBindPlayerWeapon(true);
            this.RefreshView();
        }

        private void OnDisable()
        {
            this.UnbindPlayerHealth();
            this.UnbindPlayerArmor();
            this.UnbindPlayerWeapon();
            this.UnbindQuickItemButtons();
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            this.UpdateMoneyAnimation();

            GameObject player = ShortcutPlayer.Instance;
            if (this.m_PlayerHealthBound &&
                (this.m_PlayerTraits == null || player != this.m_PlayerTraits.gameObject))
            {
                this.UnbindPlayerHealth();
            }

            if (!this.m_PlayerHealthBound && Time.unscaledTime >= this.m_NextPlayerLookupTime)
            {
                this.TryBindPlayerHealth(false);
            }

            if (this.m_PlayerArmorBound &&
                (this.m_PlayerArmor == null || player != this.m_PlayerArmor.gameObject))
            {
                this.UnbindPlayerArmor();
            }

            if (!this.m_PlayerArmorBound && Time.unscaledTime >= this.m_NextArmorLookupTime)
            {
                this.TryBindPlayerArmor(false);
            }

            if (this.m_PlayerWeaponBound &&
                (this.m_PlayerCharacter == null || player != this.m_PlayerCharacter.gameObject))
            {
                this.UnbindPlayerWeapon();
            }

            if (!this.m_PlayerWeaponBound && Time.unscaledTime >= this.m_NextPlayerWeaponRefreshTime)
            {
                this.TryBindPlayerWeapon(false);
            }
            else if (this.m_PlayerWeaponBound &&
                     Time.unscaledTime >= this.m_NextPlayerWeaponRefreshTime)
            {
                this.m_NextPlayerWeaponRefreshTime =
                    Time.unscaledTime + this.m_PlayerLookupInterval;
                this.RefreshWeaponFromPlayer();
            }

            this.UpdateWeaponLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_Money = Mathf.Max(0, this.m_Money);
            this.m_MoneyAnimationSmoothTime = Mathf.Clamp(
                this.m_MoneyAnimationSmoothTime,
                0.05f,
                1f
            );
            if (!Application.isPlaying)
            {
                this.m_DisplayedMoney = this.m_Money;
                this.m_MoneyDisplayInitialized = true;
            }
            this.m_NormalizedHealth = Mathf.Clamp01(this.m_NormalizedHealth);
            this.m_NormalizedArmor = Mathf.Clamp01(this.m_NormalizedArmor);
            this.m_ArmorValue = Mathf.Max(0f, this.m_ArmorValue);
            this.m_AmmoInClip = Mathf.Max(0, this.m_AmmoInClip);
            this.m_AmmoReserve = Mathf.Max(0, this.m_AmmoReserve);
            if (string.IsNullOrWhiteSpace(this.m_HealthAttributeId))
            {
                this.m_HealthAttributeId = "hp";
            }
            if (string.IsNullOrWhiteSpace(this.m_UnarmedLabel))
            {
                this.m_UnarmedLabel = "FISTS";
            }
            this.m_PlayerLookupInterval = Mathf.Max(0.1f, this.m_PlayerLookupInterval);
            this.m_WeaponLayoutSpeed = Mathf.Max(0f, this.m_WeaponLayoutSpeed);
            this.RefreshView();
        }
#endif

        public void SetMoney(int money)
        {
            int value = Mathf.Max(0, money);
            if (this.m_Money == value) return;
            this.m_Money = value;
            if (!this.m_MoneyDisplayInitialized)
            {
                this.m_DisplayedMoney = value;
                this.m_MoneyDisplayInitialized = true;
            }
            this.EventMoneyChanged?.Invoke(this.m_Money);
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && this.m_Money >= amount;
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount < 0 || !this.CanAfford(amount)) return false;
            if (amount == 0) return true;
            this.SetMoney(this.m_Money - amount);
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            long total = (long)this.m_Money + amount;
            this.SetMoney((int)Math.Min(int.MaxValue, total));
        }

        public void SetHealth(float normalizedHealth)
        {
            this.m_NormalizedHealth = Mathf.Clamp01(normalizedHealth);
            this.RefreshHealth();
        }

        public void SetWeapon(string weaponName, int ammoInClip, int ammoReserve)
        {
            this.m_IsUnarmed = string.IsNullOrWhiteSpace(weaponName) ||
                               string.Equals(weaponName, "UNARMED",
                                   StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(weaponName, "FISTS",
                                   StringComparison.OrdinalIgnoreCase);
            this.m_WeaponName = this.m_IsUnarmed
                ? this.m_UnarmedLabel
                : weaponName.Trim().ToUpperInvariant();
            this.m_AmmoInClip = Mathf.Max(0, ammoInClip);
            this.m_AmmoReserve = Mathf.Max(0, ammoReserve);
            this.m_IsAmmoInfinite = false;
            this.m_RuntimeWeaponSprite = null;
            this.RefreshWeapon();
        }

        public void SetUnarmed()
        {
            this.SetWeapon(this.m_UnarmedLabel, 0, 0);
        }

        public void SelectGrenade()
        {
            this.m_MolotovSelected = false;
            this.RefreshQuickItem();
        }

        public void SelectMolotov()
        {
            this.m_MolotovSelected = true;
            this.RefreshQuickItem();
        }

        private void RefreshView()
        {
            this.RefreshMoney();
            this.RefreshHealth();
            this.RefreshArmor();
            this.RefreshWeapon();
            this.RefreshQuickItem();
        }

        private void RefreshMoney()
        {
            if (this.m_MoneyText == null) return;
            int displayed = this.m_MoneyDisplayInitialized
                ? Mathf.Max(0, Mathf.RoundToInt(this.m_DisplayedMoney))
                : this.m_Money;
            this.m_MoneyText.text = displayed.ToString("N0", DISPLAY_CULTURE);
        }

        private void UpdateMoneyAnimation()
        {
            if (!this.m_MoneyDisplayInitialized)
            {
                this.m_DisplayedMoney = this.m_Money;
                this.m_MoneyDisplayInitialized = true;
                this.RefreshMoney();
                return;
            }

            int previous = Mathf.RoundToInt(this.m_DisplayedMoney);
            if (Mathf.Abs(this.m_DisplayedMoney - this.m_Money) <= 0.05f)
            {
                this.m_DisplayedMoney = this.m_Money;
                this.m_MoneyDisplayVelocity = 0f;
            }
            else
            {
                this.m_DisplayedMoney = Mathf.SmoothDamp(
                    this.m_DisplayedMoney,
                    this.m_Money,
                    ref this.m_MoneyDisplayVelocity,
                    Mathf.Max(0.05f, this.m_MoneyAnimationSmoothTime),
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }

            if (Mathf.RoundToInt(this.m_DisplayedMoney) != previous)
                this.RefreshMoney();
        }

        private void RefreshHealth()
        {
            if (this.m_HealthFill != null)
            {
                this.m_HealthFill.fillAmount = this.m_NormalizedHealth;
            }

            if (this.m_HealthText != null)
            {
                this.m_HealthText.text = Mathf.RoundToInt(this.m_NormalizedHealth * 100f) + "%";
            }
        }

        private void RefreshArmor()
        {
            if (this.m_ArmorFill != null)
                this.m_ArmorFill.fillAmount = this.m_NormalizedArmor;

            if (this.m_ArmorText != null)
                this.m_ArmorText.text = Mathf.CeilToInt(this.m_ArmorValue).ToString(
                    DISPLAY_CULTURE
                );
        }

        private void TryBindPlayerHealth(bool immediate)
        {
            if (this.m_PlayerHealthBound) return;
            if (!immediate && Time.unscaledTime < this.m_NextPlayerLookupTime) return;
            this.m_NextPlayerLookupTime = Time.unscaledTime + this.m_PlayerLookupInterval;

            Traits traits = ShortcutPlayer.Get<Traits>();
            if (traits == null) return;

            RuntimeAttributeData health;
            try
            {
                health = traits.RuntimeAttributes.Get(this.m_HealthAttributeId);
            }
            catch (Exception exception)
            {
                if (!this.m_HasWarnedMissingHealth)
                {
                    this.m_HasWarnedMissingHealth = true;
                    Debug.LogWarning(
                        $"Player HUD could not read GC2 Traits Attribute " +
                        $"'{this.m_HealthAttributeId}': {exception.Message}",
                        this
                    );
                }
                return;
            }

            if (health == null) return;

            this.m_PlayerTraits = traits;
            this.m_PlayerHealth = health;
            this.m_PlayerHealth.EventChange += this.OnPlayerHealthChanged;
            this.m_PlayerTraits.RuntimeStats.EventChange += this.OnPlayerMaximumHealthChanged;
            this.m_PlayerHealthBound = true;
            this.m_HasWarnedMissingHealth = false;
            this.RefreshHealthFromTraits();
        }

        private void UnbindPlayerHealth()
        {
            if (this.m_PlayerHealth != null)
            {
                this.m_PlayerHealth.EventChange -= this.OnPlayerHealthChanged;
            }
            if (this.m_PlayerTraits != null)
            {
                this.m_PlayerTraits.RuntimeStats.EventChange -=
                    this.OnPlayerMaximumHealthChanged;
            }

            this.m_PlayerHealth = null;
            this.m_PlayerTraits = null;
            this.m_PlayerHealthBound = false;
        }

        private void TryBindPlayerArmor(bool immediate)
        {
            if (this.m_PlayerArmorBound) return;
            if (!immediate && Time.unscaledTime < this.m_NextArmorLookupTime) return;
            this.m_NextArmorLookupTime = Time.unscaledTime + this.m_PlayerLookupInterval;

            GameObject player = ShortcutPlayer.Instance;
            FranklinArmor armor = FranklinArmorAPI.Get(player);
            if (armor == null)
            {
                if (player != null) this.SetArmorPresentation(0f, 100f);
                return;
            }

            this.m_PlayerArmor = armor;
            this.m_PlayerArmor.EventArmorChanged += this.OnPlayerArmorChanged;
            this.m_PlayerArmorBound = true;
            this.RefreshArmorFromPlayer();
        }

        private void UnbindPlayerArmor()
        {
            if (this.m_PlayerArmor != null)
                this.m_PlayerArmor.EventArmorChanged -= this.OnPlayerArmorChanged;

            this.m_PlayerArmor = null;
            this.m_PlayerArmorBound = false;
            this.SetArmorPresentation(0f, 100f);
        }

        private void OnPlayerArmorChanged(float currentArmor, float maxArmor)
        {
            this.SetArmorPresentation(currentArmor, maxArmor);
        }

        private void RefreshArmorFromPlayer()
        {
            if (this.m_PlayerArmor == null) return;
            this.SetArmorPresentation(
                this.m_PlayerArmor.CurrentArmor,
                this.m_PlayerArmor.MaxArmor
            );
        }

        private void SetArmorPresentation(float currentArmor, float maxArmor)
        {
            float maximum = Mathf.Max(1f, maxArmor);
            this.m_ArmorValue = Mathf.Clamp(currentArmor, 0f, maximum);
            this.m_NormalizedArmor = Mathf.Clamp01(this.m_ArmorValue / maximum);
            this.RefreshArmor();
        }

        private void OnPlayerHealthChanged(IdString attributeId, double change)
        {
            if (!string.Equals(
                    attributeId.String,
                    this.m_HealthAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.RefreshHealthFromTraits();
        }

        private void OnPlayerMaximumHealthChanged(IdString statId)
        {
            this.RefreshHealthFromTraits();
        }

        private void RefreshHealthFromTraits()
        {
            if (this.m_PlayerHealth == null) return;

            double range = this.m_PlayerHealth.MaxValue - this.m_PlayerHealth.MinValue;
            this.m_NormalizedHealth = range > double.Epsilon
                ? Mathf.Clamp01((float)(
                    (this.m_PlayerHealth.Value - this.m_PlayerHealth.MinValue) / range
                ))
                : 0f;
            this.RefreshHealth();
        }

        private void RefreshWeapon()
        {
            if (this.m_WeaponIcon != null)
            {
                this.m_WeaponIcon.overrideSprite = this.m_IsUnarmed
                    ? this.m_UnarmedWeaponSprite
                    : this.m_RuntimeWeaponSprite != null
                        ? this.m_RuntimeWeaponSprite
                        : this.m_ArmedWeaponSprite;
            }

            if (this.m_WeaponNameText != null)
            {
                this.m_WeaponNameText.text = this.m_IsUnarmed ||
                                             string.IsNullOrWhiteSpace(this.m_WeaponName)
                    ? this.m_UnarmedLabel
                    : this.m_WeaponName.Trim().ToUpperInvariant();
            }

            if (this.m_AmmoText != null)
            {
                this.m_AmmoText.gameObject.SetActive(!this.m_IsUnarmed);
                this.m_AmmoText.text = this.m_AmmoInClip + " / " +
                                       (this.m_IsAmmoInfinite ? "∞" : this.m_AmmoReserve);
            }

            this.SetWeaponLayoutTarget(this.m_IsUnarmed);
        }

        private void TryBindPlayerWeapon(bool immediate)
        {
            if (this.m_PlayerWeaponBound) return;
            if (!immediate && Time.unscaledTime < this.m_NextPlayerWeaponRefreshTime) return;
            this.m_NextPlayerWeaponRefreshTime =
                Time.unscaledTime + this.m_PlayerLookupInterval;

            Character character = ShortcutPlayer.Get<Character>();
            if (character == null) return;

            this.m_PlayerCharacter = character;
            this.m_PlayerCharacter.Combat.EventEquip += this.OnPlayerWeaponChanged;
            this.m_PlayerCharacter.Combat.EventUnequip += this.OnPlayerWeaponChanged;
            this.m_PlayerWeaponBound = true;
            this.RefreshWeaponFromPlayer();
        }

        private void UnbindPlayerWeapon()
        {
            if (this.m_PlayerCharacter != null)
            {
                this.m_PlayerCharacter.Combat.EventEquip -= this.OnPlayerWeaponChanged;
                this.m_PlayerCharacter.Combat.EventUnequip -= this.OnPlayerWeaponChanged;
            }

            this.m_PlayerCharacter = null;
            this.m_PlayerWeapon = null;
            this.m_PlayerWeaponBound = false;
            this.m_NextPlayerWeaponRefreshTime =
                Time.unscaledTime + this.m_PlayerLookupInterval;
        }

        private void OnPlayerWeaponChanged(IWeapon weapon, GameObject instance)
        {
            if (weapon is not ShooterWeapon) return;
            this.RefreshWeaponFromPlayer();
        }

        private void RefreshWeaponFromPlayer()
        {
            if (this.m_PlayerCharacter == null) return;

            ShooterWeapon weapon =
                this.m_PlayerCharacter.Combat.GetActiveWeapon<ShooterWeapon>();
            this.m_PlayerWeapon = weapon;

            if (weapon == null)
            {
                this.m_IsUnarmed = true;
                this.m_WeaponName = this.m_UnarmedLabel;
                this.m_AmmoInClip = 0;
                this.m_AmmoReserve = 0;
                this.m_IsAmmoInfinite = false;
                this.m_RuntimeWeaponSprite = null;
                this.RefreshWeapon();
                return;
            }

            GameObject prop = this.m_PlayerCharacter.Combat.GetProp(weapon);
            Args args = new Args(this.m_PlayerCharacter.gameObject, prop);
            string weaponName = weapon.GetName(args);

            this.m_IsUnarmed = false;
            this.m_WeaponName = string.IsNullOrWhiteSpace(weaponName)
                ? weapon.name
                : weaponName;
            this.m_RuntimeWeaponSprite = weapon.GetSprite(args);

            if (this.m_PlayerCharacter.Combat.RequestMunition(weapon) is
                ShooterMunition munition)
            {
                int totalAmmo = weapon.Magazine.GetTotalAmmo(args);
                this.m_AmmoInClip = munition.InMagazine;
                this.m_IsAmmoInfinite = totalAmmo >= int.MaxValue;
                this.m_AmmoReserve = this.m_IsAmmoInfinite
                    ? 0
                    : Mathf.Max(0, totalAmmo - munition.InMagazine);
            }

            this.RefreshWeapon();
        }

        private void SetWeaponLayoutTarget(bool unarmed)
        {
            this.m_TargetWeaponCardPosition = unarmed
                ? this.m_UnarmedCardPosition
                : this.m_ArmedCardPosition;
            this.m_TargetWeaponCardSize = unarmed
                ? this.m_UnarmedCardSize
                : this.m_ArmedCardSize;
            this.m_TargetWeaponIconPosition = unarmed
                ? new Vector2(-76f, 0f)
                : new Vector2(-169f, 0f);
            this.m_TargetWeaponNamePosition = unarmed
                ? new Vector2(42f, 0f)
                : new Vector2(-66f, 0f);

            if (this.m_WeaponLayoutInitialized && Application.isPlaying) return;

            this.ApplyWeaponLayout(1f);
            this.m_WeaponLayoutInitialized = true;
        }

        private void UpdateWeaponLayout()
        {
            if (!this.m_WeaponLayoutInitialized) return;
            float blend = this.m_WeaponLayoutSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-this.m_WeaponLayoutSpeed * Time.unscaledDeltaTime);
            this.ApplyWeaponLayout(blend);
        }

        private void ApplyWeaponLayout(float blend)
        {
            if (this.m_WeaponCard != null)
            {
                this.m_WeaponCard.anchoredPosition = Vector2.Lerp(
                    this.m_WeaponCard.anchoredPosition,
                    this.m_TargetWeaponCardPosition,
                    blend
                );
                this.m_WeaponCard.sizeDelta = Vector2.Lerp(
                    this.m_WeaponCard.sizeDelta,
                    this.m_TargetWeaponCardSize,
                    blend
                );
            }

            if (this.m_WeaponIcon != null)
            {
                this.m_WeaponIcon.rectTransform.anchoredPosition = Vector2.Lerp(
                    this.m_WeaponIcon.rectTransform.anchoredPosition,
                    this.m_TargetWeaponIconPosition,
                    blend
                );
            }

            if (this.m_WeaponNameText != null)
            {
                this.m_WeaponNameText.rectTransform.anchoredPosition = Vector2.Lerp(
                    this.m_WeaponNameText.rectTransform.anchoredPosition,
                    this.m_TargetWeaponNamePosition,
                    blend
                );
            }
        }

        private void BindQuickItemButtons()
        {
            if (this.m_QuickItemListenersBound) return;
            if (this.m_GrenadeButton != null)
            {
                this.m_GrenadeButton.onClick.AddListener(this.SelectGrenade);
            }
            if (this.m_MolotovButton != null)
            {
                this.m_MolotovButton.onClick.AddListener(this.SelectMolotov);
            }
            this.m_QuickItemListenersBound = true;
        }

        private void UnbindQuickItemButtons()
        {
            if (!this.m_QuickItemListenersBound) return;
            if (this.m_GrenadeButton != null)
            {
                this.m_GrenadeButton.onClick.RemoveListener(this.SelectGrenade);
            }
            if (this.m_MolotovButton != null)
            {
                this.m_MolotovButton.onClick.RemoveListener(this.SelectMolotov);
            }
            this.m_QuickItemListenersBound = false;
        }

        private void RefreshQuickItem()
        {
            if (this.m_GrenadeSelection != null)
            {
                this.m_GrenadeSelection.enabled = !this.m_MolotovSelected;
            }
            if (this.m_MolotovSelection != null)
            {
                this.m_MolotovSelection.enabled = this.m_MolotovSelected;
            }
        }
    }
}
