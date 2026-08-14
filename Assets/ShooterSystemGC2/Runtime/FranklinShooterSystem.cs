using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FranklinGame.Animations;
using FranklinGame.UI;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Runtime integration between the Franklin mobile HUD, the GC2 Shooter API and the
    /// low-poly weapon props. Everything is built from the project-local catalog in Resources.
    /// </summary>
    [DefaultExecutionOrder(620)]
    [DisallowMultipleComponent]
    public sealed class FranklinShooterSystem : MonoBehaviour
    {
        private enum BikeSeatRole
        {
            None,
            Driver,
            Passenger
        }

        private const string CATALOG_RESOURCE = "FranklinShooter/Franklin Shooter Catalog";
        private const string BACKGROUND_RESOURCE = "FranklinShooter/UI/weapon-wheel-background";
        private const string BIKE_DRIVER_MASK_RESOURCE =
            "FranklinShooter/Animations/Franklin Bike Driver Seat And Left Hand";
        private const string CAUTIOUS_LOCOMOTION_RESOURCE =
            "FranklinShooter/Animations/Franklin Shooter Upper Body Locomotion";
        private const int SHOOTER_LOCOMOTION_LAYER = 7;
        private const float SHOOTER_LOCOMOTION_TRANSITION = 0.25f;
        private const float AIM_POSE_READY_DELAY = 0.30f;
        private const float OBJECT_DIRECTION_IDLE_SECONDS = 5f;
        private const float PLAYER_RETRY_SECONDS = 0.4f;
        private const float CROSSHAIR_EXPANSION_SCALE = 0.5f;
        private const float CROSSHAIR_SCAN_SECONDS = 0.1f;
        private static readonly Vector2 ON_FOOT_FIRE_POSITION =
            new(-273.1f, 254.9f);
        private static readonly Vector2 ON_FOOT_RELOAD_POSITION =
            new(-113f, 94f);
        private static readonly Vector2 BIKE_FIRE_POSITION =
            new(-690f, 190f);

        private const BindingFlags CROSSHAIR_FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo CROSSHAIR_ACCURACY_POSITION =
            typeof(CrosshairUI).GetField("m_AccuracyPosition", CROSSHAIR_FIELD_FLAGS);
        private static readonly FieldInfo CROSSHAIR_POSITION_X =
            typeof(CrosshairUI).GetField("m_PositionX", CROSSHAIR_FIELD_FLAGS);
        private static readonly FieldInfo CROSSHAIR_POSITION_Y =
            typeof(CrosshairUI).GetField("m_PositionY", CROSSHAIR_FIELD_FLAGS);

        private static readonly Color CYAN = new(0.13f, 0.82f, 1f, 1f);
        private static readonly Color LIME = new(0.63f, 1f, 0.18f, 1f);
        private static readonly Color OFF_WHITE = new(0.92f, 0.94f, 0.94f, 1f);
        private static readonly Color WHEEL_TINT = new(0.48f, 0.51f, 0.55f, 0.90f);
        // Saturated navy remains dark, but separates clearly from the charcoal wheel art.
        private static readonly Color SELECTED_SECTOR = new(0.035f, 0.23f, 0.48f, 1f);

        private static FranklinShooterSystem s_Instance;
        private static AvatarMask s_BikeDriverAnimationMask;
        private static bool s_PhoneUseActive;

        private FranklinShooterCatalog m_Catalog;
        private StateBasicLocomotion m_CautiousLocomotion;
        private Character m_Player;
        private float m_NextPlayerLookup;
        private int m_SelectedIndex = -1;
        private bool m_IsSwitching;
        private bool m_FireHeld;
        private bool m_TriggerPulled;
        private int m_FireRequestId;
        private bool m_Aiming;
        private ShooterWeapon m_SingleRepeatWeapon;
        private int m_LastSingleRepeatShotFrame = int.MinValue;
        private bool m_CautiousWalk;
        private bool m_HudTapBound;
        private float m_NextHudTapLookup;
        private float m_NextControlLookup;
        private float m_NextCrosshairScan;
        private bool m_MeleeSuppressed;
        private bool m_MeleeFightWasActive;
        private bool m_SidestepVisibilityWasEnabled;
        private bool m_OwnsIdleVariationSuppression;
        private bool m_OwnsObjectDirection;
        private float m_ObjectDirectionReturnAt = -1f;
        private ShooterWeapon m_ObservedShotWeapon;
        private int m_LastObservedShotFrame = int.MinValue;
        private BikeSeatRole m_BikeSeatRole;
        private Component m_ActiveBikeSeat;
        private FranklinBikeMainShotAim m_BikeCameraAim;
        private FranklinArcadeBikeDriver m_BikeReloadDriver;
        private CharacterIKSetter m_BikeIkSetter;
        private CharacterIKSetter.HandIKState m_OriginalBikeHandIk;
        private bool m_HasOriginalBikeHandIk;
        private Character m_BikeSuspendedPlayer;
        private ShooterWeapon m_BikeSuspendedWeapon;
        private GameObject m_BikeSuspendedProp;
        private int m_BikeSuspendedWeaponIndex = -1;
        private int m_BikeWeaponTransitionVersion;
        private bool m_BikeWeaponRestoreRequested;
        private Task m_BikeWeaponTask = Task.CompletedTask;
        private bool m_PhoneUseActive;
        private int m_PhoneSuspendedWeaponIndex = -1;
        private Character m_PhoneSuspendedPlayer;
        private ShooterWeapon m_PhoneSuspendedWeapon;
        private GameObject m_PhoneSuspendedProp;
        private int m_PhoneTransitionVersion;
        private Task m_PhoneSuspendTask = Task.CompletedTask;

        private Canvas m_Canvas;
        private GameObject m_MenuRoot;
        private GameObject m_ControlsRoot;
        private GameObject m_HudTapTarget;
        private GameObject m_ReloadIndicatorRoot;
        private Transform m_OnFootControls;
        private GameObject m_MeleeFight;
        private GameObject m_MeleeSidestepLeft;
        private GameObject m_MeleeSidestepRight;
        private Behaviour m_SidestepVisibility;
        private FranklinAnimationBridge m_AnimationBridge;
        private FranklinObjectDirectionToggle m_ObjectDirectionToggle;
        private Image m_SelectedIcon;
        private Image m_MeleeSwitchImage;
        private FranklinShooterTouchButton m_CautiousWalkButton;
        private RectTransform m_FireButtonRect;
        private RectTransform m_ReloadButtonRect;
        private Text m_SelectedName;
        private Text m_SelectedCategory;
        private Text m_SelectedAmmo;
        private Text m_Hint;
        private Image m_ReloadProgressImage;
        private Texture2D m_ReloadRingTexture;
        private Sprite m_ReloadRingSprite;
        private readonly List<Graphic> m_SlotFrames = new();
        private readonly List<Image> m_SlotIcons = new();
        private readonly List<Button> m_SlotButtons = new();
        private readonly HashSet<int> m_CompactCrosshairs = new();

        public static FranklinShooterSystem Instance => s_Instance;
        public static AvatarMask BikeDriverAnimationMask =>
            s_BikeDriverAnimationMask ??=
                Resources.Load<AvatarMask>(BIKE_DRIVER_MASK_RESOURCE);
        public bool IsWeaponMenuOpen => this.m_MenuRoot != null && this.m_MenuRoot.activeSelf;

        /// <summary>
        /// Normalizes the shooter state before BikeEntry starts its full-body entry gesture.
        /// Heavy/two-hand weapons are temporarily unequipped while the character is still on
        /// foot, so their layer-7 state cannot override the authored bike transition.
        /// </summary>
        public static Task PrepareForBikeDriverEntry(Character character)
        {
            return s_Instance != null
                ? s_Instance.PrepareForBikeDriverEntryInternal(character)
                : Task.CompletedTask;
        }

        /// <summary>Restores a heavy weapon when a Bike entry was cancelled.</summary>
        public static Task CancelBikeDriverEntry(Character character)
        {
            return s_Instance != null
                ? s_Instance.RequestBikeDriverWeaponRestoreInternal(character)
                : Task.CompletedTask;
        }

        /// <summary>Restores a suspended heavy weapon after the exit animation.</summary>
        public static Task CompleteBikeDriverExit(Character character)
        {
            return s_Instance != null
                ? s_Instance.RequestBikeDriverWeaponRestoreInternal(character)
                : Task.CompletedTask;
        }

        /// <summary>
        /// Temporarily puts the active Shooter weapon away while the physical phone
        /// owns the right hand. Closing the phone restores the same catalog weapon.
        /// </summary>
        public static void SetPhoneUseActive(bool active)
        {
            s_PhoneUseActive = active;
            s_Instance?.SetPhoneUseActiveInternal(active);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_BikeDriverAnimationMask = null;
            s_PhoneUseActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<FranklinShooterSystem>() != null) return;
            GameObject instance = new("Franklin Shooter System");
            instance.AddComponent<FranklinShooterSystem>();
        }

        private async void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(this.gameObject);
            this.m_Catalog = Resources.Load<FranklinShooterCatalog>(CATALOG_RESOURCE);
            this.m_CautiousLocomotion =
                Resources.Load<StateBasicLocomotion>(CAUTIOUS_LOCOMOTION_RESOURCE);
            if (this.m_Catalog == null)
            {
                Debug.LogError(
                    $"Franklin Shooter Catalog is missing at Resources/{CATALOG_RESOURCE}. " +
                    "Run Tools/Franklin Game/Shooter System GC2/Install or Repair.",
                    this
                );
                return;
            }

            this.BuildCanvas();
            this.EnsureEventSystem();
            this.m_PhoneUseActive = s_PhoneUseActive;
            this.m_HudTapBound = this.BindWeaponHudTap();
            await this.TryBindPlayer(true);
        }

        private void OnDestroy()
        {
            FranklinMobileHud.ReleaseControlsSuppression(this);
            this.SetMeleeControlsSuppressed(false);
            this.UnbindPlayer();
            if (this.m_ReloadRingSprite != null) Destroy(this.m_ReloadRingSprite);
            if (this.m_ReloadRingTexture != null) Destroy(this.m_ReloadRingTexture);
            if (s_Instance == this) s_Instance = null;
        }

        private async void Update()
        {
            if (this.m_Player == null && Time.unscaledTime >= this.m_NextPlayerLookup)
            {
                await this.TryBindPlayer(false);
            }

            if (!this.m_HudTapBound && Time.unscaledTime >= this.m_NextHudTapLookup)
            {
                this.m_NextHudTapLookup = Time.unscaledTime + PLAYER_RETRY_SECONDS;
                this.m_HudTapBound = this.BindWeaponHudTap();
            }

            if (this.m_OnFootControls == null && Time.unscaledTime >= this.m_NextControlLookup)
            {
                this.m_NextControlLookup = Time.unscaledTime + PLAYER_RETRY_SECONDS;
                this.TryBindOnFootControls();
            }

            this.RefreshBikeSeatContext();
            this.RefreshBikeReloadState();
            this.RefreshReloadIndicator();
            this.RefreshHeldSingleFire();
            this.RefreshControlMode();
            this.TrackShotActivityAndDirectionTimeout();

            if (this.m_Aiming && Time.unscaledTime >= this.m_NextCrosshairScan)
            {
                this.m_NextCrosshairScan = Time.unscaledTime + CROSSHAIR_SCAN_SECONDS;
                this.CompactActiveCrosshairs();
            }

            if (Keyboard.current != null)
            {
                if (!this.m_PhoneUseActive &&
                    Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    if (this.IsWeaponMenuOpen) this.CloseWeaponMenu();
                    else this.OpenWeaponMenu();
                }

                if (Keyboard.current.escapeKey.wasPressedThisFrame && this.IsWeaponMenuOpen)
                    this.CloseWeaponMenu();

                if (!this.m_PhoneUseActive &&
                    !this.IsWeaponMenuOpen &&
                    this.m_Player != null)
                {
                    if (Keyboard.current.rKey.wasPressedThisFrame &&
                        this.m_BikeSeatRole != BikeSeatRole.Driver)
                    {
                        this.Reload();
                    }
                    if (Keyboard.current.digit1Key.wasPressedThisFrame) await this.SelectWeapon(0);
                    if (Keyboard.current.digit2Key.wasPressedThisFrame) await this.SelectWeapon(1);
                    if (Keyboard.current.digit3Key.wasPressedThisFrame) await this.SelectWeapon(2);
                    if (Keyboard.current.digit4Key.wasPressedThisFrame) await this.SelectWeapon(3);
                    if (Keyboard.current.digit5Key.wasPressedThisFrame) await this.SelectWeapon(4);
                    if (Keyboard.current.digit6Key.wasPressedThisFrame) await this.SelectWeapon(5);
                    if (Keyboard.current.digit7Key.wasPressedThisFrame) await this.SelectWeapon(6);
                    if (Keyboard.current.digit8Key.wasPressedThisFrame) await this.SelectWeapon(7);
                }
            }

            if (this.IsWeaponMenuOpen) this.RefreshSelectedPanel();
        }

        public void OpenWeaponMenu()
        {
            if (this.m_MenuRoot == null || this.m_Catalog == null ||
                this.m_PhoneUseActive) return;
            if ((this.m_FireHeld || this.m_TriggerPulled) && this.m_Player != null)
            {
                ShooterWeapon active = this.GetActiveWeapon();
                if (active != null)
                {
                    ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
                    this.CancelFireRequest(active, stance, true);
                }
                else
                {
                    this.InvalidateFireRequest();
                }
            }
            this.RefreshSelectedIndex();
            this.RefreshMenuSelection();
            this.m_MenuRoot.SetActive(true);
            if (this.m_HudTapTarget != null) this.m_HudTapTarget.SetActive(false);
            FranklinMobileHud.AcquireControlsSuppression(this);
            this.RefreshControlMode();
        }

        public void CloseWeaponMenu()
        {
            if (this.m_MenuRoot != null) this.m_MenuRoot.SetActive(false);
            if (this.m_HudTapTarget != null) this.m_HudTapTarget.SetActive(true);
            FranklinMobileHud.ReleaseControlsSuppression(this);
            this.RefreshControlMode();
        }

        public async void SelectWeaponAndClose(int index)
        {
            if (await this.SelectWeapon(index)) this.CloseWeaponMenu();
        }

        public void SetTouchAction(FranklinShooterTouchButton.Action action, bool active)
        {
            if (this.m_Player == null || this.IsWeaponMenuOpen ||
                this.m_PhoneUseActive) return;
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;
            if (!this.CanUseWeaponInCurrentSeat(weapon)) return;
            if (this.m_BikeSeatRole != BikeSeatRole.None &&
                (action == FranklinShooterTouchButton.Action.Melee ||
                 action == FranklinShooterTouchButton.Action.CautiousWalk ||
                 action == FranklinShooterTouchButton.Action.Reload &&
                 this.m_BikeSeatRole == BikeSeatRole.Driver))
            {
                return;
            }

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            switch (action)
            {
                case FranklinShooterTouchButton.Action.Fire:
                    if (active)
                    {
                        if (this.m_FireHeld) break;
                        this.m_FireHeld = true;
                        this.m_TriggerPulled = false;
                        int requestId = ++this.m_FireRequestId;
                        if (this.ShouldReloadBeforeFire(weapon, stance))
                            this.ReloadThenBeginFire(weapon, stance, requestId);
                        else
                            this.BeginFireWhenAimReady(weapon, stance, requestId);
                    }
                    else
                    {
                        this.CancelFireRequest(weapon, stance, true);
                    }
                    break;
                case FranklinShooterTouchButton.Action.Reload:
                    if (active) this.Reload();
                    break;
                case FranklinShooterTouchButton.Action.Melee:
                    if (active) this.SwitchToMelee();
                    break;
                case FranklinShooterTouchButton.Action.CautiousWalk:
                    if (active) this.SetCautiousWalk(!this.m_CautiousWalk);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private async Task<bool> TryBindPlayer(bool immediate)
        {
            if (this.m_Catalog == null) return false;
            if (!immediate && Time.unscaledTime < this.m_NextPlayerLookup) return false;
            this.m_NextPlayerLookup = Time.unscaledTime + PLAYER_RETRY_SECONDS;
            Character player = ShortcutPlayer.Get<Character>();
            if (player == null) return false;

            this.UnbindPlayer();
            this.m_Player = player;
            this.m_AnimationBridge = this.m_Player.GetComponentInChildren<FranklinAnimationBridge>(true);
            this.m_BikeCameraAim =
                this.m_Player.GetComponentInChildren<FranklinBikeMainShotAim>(true);
            this.m_Player.Combat.EventEquip += this.OnWeaponChanged;
            this.m_Player.Combat.EventUnequip += this.OnWeaponChanged;
            this.RefreshSelectedIndex();

            if (this.m_PhoneUseActive)
            {
                this.m_PhoneSuspendTask = this.SuspendWeaponForPhone();
                await this.m_PhoneSuspendTask;
            }

            // Starting without an equipped Shooter weapon intentionally keeps the
            // Player in melee. DefaultWeaponIndex is only the initial wheel cursor.
            this.SetCautiousWalk(false, true);
            this.RefreshControlMode();

            return true;
        }

        private void UnbindPlayer()
        {
            ShooterWeapon activeWeapon = this.GetActiveWeapon();
            if (this.m_Player != null && activeWeapon != null)
            {
                ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
                this.CancelFireRequest(activeWeapon, stance, true);
            }
            else
            {
                this.InvalidateFireRequest();
            }

            this.SetCautiousWalk(false, true);
            this.SetObjectDirectionForShooting(false);
            this.ResetShotDirectionTracking();
            this.SetIdleAnimationSuppressed(false);
            if (this.m_Player != null)
            {
                this.m_Player.Combat.EventEquip -= this.OnWeaponChanged;
                this.m_Player.Combat.EventUnequip -= this.OnWeaponChanged;
            }

            this.ClearPhoneSuspendedWeapon(true);
            this.ClearBikeSuspendedWeapon(true);
            this.m_Player = null;
            this.m_AnimationBridge = null;
            this.m_ObjectDirectionToggle = null;
            ++this.m_PhoneTransitionVersion;
            ++this.m_BikeWeaponTransitionVersion;
            this.m_PhoneSuspendedWeaponIndex = -1;
            this.m_PhoneSuspendTask = Task.CompletedTask;
            this.m_BikeWeaponTask = Task.CompletedTask;
            this.ClearBikeSeatContext();
            this.m_FireHeld = false;
            this.m_Aiming = false;
            this.m_CautiousWalk = false;
            this.RefreshCautiousWalkButton();
            this.RefreshControlMode();
        }

        private async Task<bool> SelectWeapon(
            int index,
            bool initializeEmptyMagazine = true)
        {
            if (this.m_IsSwitching || this.m_Player == null ||
                this.m_PhoneUseActive) return false;
            FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(index);
            if (entry?.Weapon == null || entry.PropPrefab == null) return false;
            if (this.m_BikeSeatRole == BikeSeatRole.Driver &&
                !IsDriverWeaponEntryAllowed(entry))
            {
                return false;
            }
            if (this.m_BikeSuspendedWeapon != null &&
                this.m_BikeSuspendedWeapon != entry.Weapon)
            {
                ++this.m_BikeWeaponTransitionVersion;
                this.ClearBikeSuspendedWeapon(true);
            }

            this.m_IsSwitching = true;
            try
            {
                ShooterWeapon active = this.GetActiveWeapon();
                if (active != null && (this.m_FireHeld || this.m_TriggerPulled))
                {
                    ShooterStance activeStance =
                        this.m_Player.Combat.RequestStance<ShooterStance>();
                    this.CancelFireRequest(active, activeStance, true);
                }
                if (active == entry.Weapon)
                {
                    this.m_SelectedIndex = index;
                    this.RefreshMenuSelection();
                    return true;
                }

                if (active != null)
                {
                    GameObject oldProp = this.m_Player.Combat.GetProp(active);
                    await this.m_Player.Combat.Unequip(active, new Args(this.m_Player.gameObject));
                    if (oldProp != null) this.m_Player.Props.RemoveInstance(oldProp);
                }

                GameObject prop = Instantiate(entry.PropPrefab);
                prop.name = entry.DisplayName + " Weapon Prop";
                prop.transform.localScale = Vector3.Scale(prop.transform.localScale, entry.LocalScale);
                this.m_Player.Props.AttachInstance(
                    new Bone(HumanBodyBones.RightHand), prop,
                    entry.LocalPosition, entry.LocalRotation
                );
                await this.m_Player.Combat.Equip(
                    entry.Weapon,
                    prop,
                    new Args(this.m_Player.gameObject, prop)
                );

                if (initializeEmptyMagazine &&
                    this.m_Player.Combat.RequestMunition(entry.Weapon) is ShooterMunition munition &&
                    munition.InMagazine <= 0)
                {
                    Args args = new(this.m_Player.gameObject, prop);
                    int capacity = entry.Weapon.Magazine.GetMagazineSize(args);
                    int available = entry.Weapon.Magazine.GetTotalAmmo(args);
                    munition.InMagazine = Mathf.Min(
                        entry.StartingMagazine > 0 ? entry.StartingMagazine : capacity,
                        capacity,
                        available
                    );
                }

                this.m_SelectedIndex = index;
                this.m_Aiming = false;
                this.SetCautiousWalk(false, true);
                this.RefreshMenuSelection();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not equip Franklin shooter weapon: {exception}", this);
                return false;
            }
            finally
            {
                this.m_IsSwitching = false;
                this.RefreshControlMode();
            }
        }

        private void SetPhoneUseActiveInternal(bool active)
        {
            if (this.m_PhoneUseActive == active)
            {
                if (!active && this.m_PhoneSuspendedWeapon != null)
                {
                    int retryVersion = ++this.m_PhoneTransitionVersion;
                    Task pending = this.m_PhoneSuspendTask;
                    this.m_PhoneSuspendTask =
                        this.RestoreWeaponAfterPhone(retryVersion, pending);
                }
                if (!active && this.m_BikeWeaponRestoreRequested)
                {
                    this.QueueBikeDriverWeaponRestore(
                        this.m_Player,
                        this.m_PhoneSuspendTask
                    );
                }
                return;
            }

            this.m_PhoneUseActive = active;
            int version = ++this.m_PhoneTransitionVersion;
            if (active)
            {
                if (this.IsWeaponMenuOpen) this.CloseWeaponMenu();
                this.m_PhoneSuspendTask = this.SuspendWeaponForPhone();
            }
            else
            {
                Task pending = this.m_PhoneSuspendTask;
                this.m_PhoneSuspendTask =
                    this.RestoreWeaponAfterPhone(version, pending);
                if (this.m_BikeWeaponRestoreRequested)
                {
                    this.QueueBikeDriverWeaponRestore(
                        this.m_Player,
                        this.m_PhoneSuspendTask
                    );
                }
            }

            this.RefreshControlMode();
        }

        private async Task SuspendWeaponForPhone()
        {
            while (this.m_IsSwitching && this.m_PhoneUseActive && this != null)
                await Task.Yield();
            if (this == null || !this.m_PhoneUseActive || this.m_Player == null)
                return;

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;

            Character player = this.m_Player;
            GameObject prop = player.Combat.GetProp(weapon);
            int catalogIndex = this.m_Catalog?.IndexOf(weapon) ?? -1;
            ShooterStance stance = player.Combat.RequestStance<ShooterStance>();
            if (this.m_FireHeld || this.m_TriggerPulled || this.m_Aiming)
                this.CancelFireRequest(weapon, stance, true);
            this.SetCautiousWalk(false, true);
            this.SetObjectDirectionForShooting(false);

            this.m_PhoneSuspendedPlayer = player;
            this.m_PhoneSuspendedWeapon = weapon;
            this.m_PhoneSuspendedProp = prop;
            this.m_PhoneSuspendedWeaponIndex = catalogIndex;
            if (prop != null) prop.SetActive(false);

            this.m_IsSwitching = true;
            bool unequipped = false;
            try
            {
                await player.Combat.Unequip(
                    weapon,
                    new Args(player.gameObject)
                );
                unequipped = !player.Combat.IsEquipped(weapon);
            }
            catch (Exception exception)
            {
                unequipped = !player.Combat.IsEquipped(weapon);
                Debug.LogError(
                    $"Could not temporarily unequip weapon for Phone: {exception}",
                    this
                );
            }
            finally
            {
                this.m_IsSwitching = false;
                if (!unequipped)
                {
                    if (prop != null) prop.SetActive(true);
                    this.ClearPhoneSuspendedWeapon(false);
                }
                this.m_Aiming = false;
                this.RefreshMenuSelection();
                this.RefreshControlMode();
            }
        }

        private async Task RestoreWeaponAfterPhone(int version, Task pendingSuspend)
        {
            if (pendingSuspend != null)
            {
                try
                {
                    await pendingSuspend;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            while (this.m_IsSwitching && !this.m_PhoneUseActive && this != null)
                await Task.Yield();
            if (this == null ||
                this.m_PhoneUseActive ||
                version != this.m_PhoneTransitionVersion ||
                this.m_Player == null ||
                this.m_PhoneSuspendedWeapon == null)
            {
                return;
            }

            if (this.GetActiveWeapon() != null)
            {
                // Another system intentionally equipped a weapon while Phone was open.
                // Keep that newer choice and discard only our hidden cached prop.
                this.ClearPhoneSuspendedWeapon(true);
                this.RefreshControlMode();
                return;
            }

            if (this.m_BikeSeatRole == BikeSeatRole.Driver &&
                !IsDriverWeaponEntryAllowed(
                    this.GetCatalogEntry(this.m_PhoneSuspendedWeapon)))
            {
                this.TransferPhoneSuspendedWeaponToBike();
                this.RefreshMenuSelection();
                this.RefreshControlMode();
                return;
            }

            Character player = this.m_PhoneSuspendedPlayer;
            ShooterWeapon weapon = this.m_PhoneSuspendedWeapon;
            GameObject prop = this.m_PhoneSuspendedProp;
            int catalogIndex = this.m_PhoneSuspendedWeaponIndex;
            if (player == null || player != this.m_Player || prop == null)
            {
                this.ClearPhoneSuspendedWeapon(true);
                if (catalogIndex >= 0)
                    await this.SelectWeapon(catalogIndex, false);
                return;
            }

            this.m_IsSwitching = true;
            bool restored = false;
            try
            {
                prop.SetActive(true);
                await player.Combat.Equip(
                    weapon,
                    prop,
                    new Args(player.gameObject, prop)
                );
                restored = player.Combat.IsEquipped(weapon);
                if (restored)
                {
                    this.m_SelectedIndex = catalogIndex;
                    this.m_Aiming = false;
                    this.SetCautiousWalk(false, true);
                    this.ClearPhoneSuspendedWeapon(false);
                }
            }
            catch (Exception exception)
            {
                restored = player != null && player.Combat.IsEquipped(weapon);
                if (restored)
                {
                    this.m_SelectedIndex = catalogIndex;
                    this.m_Aiming = false;
                    this.SetCautiousWalk(false, true);
                    this.ClearPhoneSuspendedWeapon(false);
                }
                Debug.LogError(
                    $"Could not restore weapon after Phone: {exception}",
                    this
                );
            }
            finally
            {
                this.m_IsSwitching = false;
                if (!restored && prop != null) prop.SetActive(false);
                this.RefreshMenuSelection();
                this.RefreshControlMode();
            }
        }

        private void ClearPhoneSuspendedWeapon(bool removeProp)
        {
            if (removeProp &&
                this.m_PhoneSuspendedPlayer != null &&
                this.m_PhoneSuspendedProp != null)
            {
                this.m_PhoneSuspendedPlayer.Props.RemoveInstance(
                    this.m_PhoneSuspendedProp
                );
            }

            this.m_PhoneSuspendedPlayer = null;
            this.m_PhoneSuspendedWeapon = null;
            this.m_PhoneSuspendedProp = null;
            this.m_PhoneSuspendedWeaponIndex = -1;
        }

        private void TransferPhoneSuspendedWeaponToBike()
        {
            ++this.m_BikeWeaponTransitionVersion;
            this.ClearBikeSuspendedWeapon(true);
            this.m_BikeSuspendedPlayer = this.m_PhoneSuspendedPlayer;
            this.m_BikeSuspendedWeapon = this.m_PhoneSuspendedWeapon;
            this.m_BikeSuspendedProp = this.m_PhoneSuspendedProp;
            this.m_BikeSuspendedWeaponIndex = this.m_PhoneSuspendedWeaponIndex;
            this.m_BikeWeaponRestoreRequested = false;
            this.ClearPhoneSuspendedWeapon(false);
        }

        private Task PrepareForBikeDriverEntryInternal(Character character)
        {
            if (character == null || character != this.m_Player)
                return Task.CompletedTask;

            this.m_BikeWeaponRestoreRequested = false;
            int version = ++this.m_BikeWeaponTransitionVersion;
            Task pending = this.m_BikeWeaponTask;
            this.m_BikeWeaponTask = this.SuspendHeavyWeaponForBikeDriver(
                character,
                version,
                pending
            );
            return this.m_BikeWeaponTask;
        }

        private async Task SuspendHeavyWeaponForBikeDriver(
            Character character,
            int version,
            Task pending)
        {
            if (pending != null)
            {
                try
                {
                    await pending;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            while (this.m_IsSwitching && this != null)
                await Task.Yield();
            if (this == null ||
                version != this.m_BikeWeaponTransitionVersion ||
                character == null ||
                character != this.m_Player)
            {
                return;
            }

            if (this.m_BikeSuspendedWeapon != null) return;

            ShooterWeapon active = this.GetActiveWeapon();
            if (active == null) return;
            if (IsDriverWeaponEntryAllowed(this.GetCatalogEntry(active))) return;

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            if (this.m_FireHeld || this.m_TriggerPulled || this.m_Aiming)
                this.CancelFireRequest(active, stance, true);
            this.SetCautiousWalk(false, true);
            this.SetObjectDirectionForShooting(false);

            GameObject prop = character.Combat.GetProp(active);
            this.m_BikeSuspendedPlayer = character;
            this.m_BikeSuspendedWeapon = active;
            this.m_BikeSuspendedProp = prop;
            this.m_BikeSuspendedWeaponIndex = this.m_Catalog?.IndexOf(active) ?? -1;
            if (prop != null) prop.SetActive(false);

            this.m_IsSwitching = true;
            bool unequipped = false;
            try
            {
                await character.Combat.Unequip(
                    active,
                    new Args(character.gameObject)
                );
                unequipped = !character.Combat.IsEquipped(active);
            }
            catch (Exception exception)
            {
                unequipped = !character.Combat.IsEquipped(active);
                Debug.LogError(
                    $"Could not temporarily unequip heavy Bike weapon: {exception}",
                    this
                );
            }
            finally
            {
                this.m_IsSwitching = false;
                if (!unequipped)
                {
                    if (prop != null) prop.SetActive(true);
                    this.ClearBikeSuspendedWeapon(false);
                }
                this.m_Aiming = false;
                this.RefreshMenuSelection();
                this.RefreshControlMode();
            }
        }

        private Task RequestBikeDriverWeaponRestoreInternal(Character character)
        {
            if (character == null || character != this.m_Player)
                return Task.CompletedTask;

            this.m_BikeWeaponRestoreRequested = true;
            return this.QueueBikeDriverWeaponRestore(character, null);
        }

        private Task QueueBikeDriverWeaponRestore(
            Character character,
            Task prerequisite)
        {
            if (character == null || character != this.m_Player)
                return Task.CompletedTask;

            int version = ++this.m_BikeWeaponTransitionVersion;
            Task pending = this.m_BikeWeaponTask;
            this.m_BikeWeaponTask = this.RestoreHeavyWeaponAfterBike(
                character,
                version,
                pending,
                prerequisite
            );
            return this.m_BikeWeaponTask;
        }

        private async Task RestoreHeavyWeaponAfterBike(
            Character character,
            int version,
            Task pending,
            Task prerequisite)
        {
            foreach (Task task in new[] { pending, prerequisite })
            {
                if (task == null) continue;
                try
                {
                    await task;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            while (this.m_IsSwitching && !this.m_PhoneUseActive && this != null)
                await Task.Yield();
            if (this == null ||
                version != this.m_BikeWeaponTransitionVersion ||
                !this.m_BikeWeaponRestoreRequested ||
                this.m_PhoneUseActive ||
                character == null ||
                character != this.m_Player)
            {
                return;
            }

            if (this.m_BikeSuspendedWeapon == null)
            {
                this.m_BikeWeaponRestoreRequested = false;
                return;
            }

            BikeEntry driverSeat = character.GetComponentInParent<BikeEntry>();
            if (driverSeat != null && driverSeat.SeatedCharacter == character)
                return;

            if (this.GetActiveWeapon() != null)
            {
                // A newer explicit weapon choice wins over the cached heavy weapon.
                this.ClearBikeSuspendedWeapon(true);
                this.RefreshControlMode();
                return;
            }

            Character cachedPlayer = this.m_BikeSuspendedPlayer;
            ShooterWeapon weapon = this.m_BikeSuspendedWeapon;
            GameObject prop = this.m_BikeSuspendedProp;
            int catalogIndex = this.m_BikeSuspendedWeaponIndex;
            if (cachedPlayer == null || cachedPlayer != character || prop == null)
            {
                this.ClearBikeSuspendedWeapon(true);
                if (catalogIndex >= 0)
                    await this.SelectWeapon(catalogIndex, false);
                return;
            }

            this.m_IsSwitching = true;
            bool restored = false;
            try
            {
                prop.SetActive(true);
                await character.Combat.Equip(
                    weapon,
                    prop,
                    new Args(character.gameObject, prop)
                );
                restored = character.Combat.IsEquipped(weapon);
                if (restored)
                {
                    this.m_SelectedIndex = catalogIndex;
                    this.m_Aiming = false;
                    this.SetCautiousWalk(false, true);
                    this.ClearBikeSuspendedWeapon(false);
                }
            }
            catch (Exception exception)
            {
                restored = character != null && character.Combat.IsEquipped(weapon);
                if (restored)
                {
                    this.m_SelectedIndex = catalogIndex;
                    this.m_Aiming = false;
                    this.SetCautiousWalk(false, true);
                    this.ClearBikeSuspendedWeapon(false);
                }
                Debug.LogError(
                    $"Could not restore heavy weapon after Bike exit: {exception}",
                    this
                );
            }
            finally
            {
                this.m_IsSwitching = false;
                if (!restored && prop != null) prop.SetActive(false);
                this.RefreshMenuSelection();
                this.RefreshControlMode();
            }
        }

        private void ClearBikeSuspendedWeapon(bool removeProp)
        {
            if (removeProp &&
                this.m_BikeSuspendedPlayer != null &&
                this.m_BikeSuspendedProp != null)
            {
                this.m_BikeSuspendedPlayer.Props.RemoveInstance(
                    this.m_BikeSuspendedProp
                );
            }

            this.m_BikeSuspendedPlayer = null;
            this.m_BikeSuspendedWeapon = null;
            this.m_BikeSuspendedProp = null;
            this.m_BikeSuspendedWeaponIndex = -1;
            this.m_BikeWeaponRestoreRequested = false;
        }

        private void RefreshBikeSeatContext()
        {
            BikeSeatRole nextRole = BikeSeatRole.None;
            Component nextSeat = null;

            if (this.m_Player != null)
            {
                BikeEntry driverSeat = this.m_Player.GetComponentInParent<BikeEntry>();
                if (driverSeat != null &&
                    driverSeat.SeatedCharacter == this.m_Player &&
                    !driverSeat.IsTransitioning)
                {
                    nextRole = BikeSeatRole.Driver;
                    nextSeat = driverSeat;
                }
                else
                {
                    FranklinBikePassengerSeat passengerSeat =
                        this.m_Player.GetComponentInParent<FranklinBikePassengerSeat>();
                    if (passengerSeat != null &&
                        passengerSeat.Passenger == this.m_Player &&
                        !passengerSeat.IsTransitioning)
                    {
                        nextRole = BikeSeatRole.Passenger;
                        nextSeat = passengerSeat;
                    }
                }
            }

            if (nextRole != this.m_BikeSeatRole || nextSeat != this.m_ActiveBikeSeat)
            {
                ShooterWeapon active = this.GetActiveWeapon();
                if (active != null &&
                    (this.m_FireHeld || this.m_TriggerPulled || this.m_Aiming))
                {
                    ShooterStance stance =
                        this.m_Player.Combat.RequestStance<ShooterStance>();
                    this.CancelFireRequest(active, stance, true);
                }

                // BikeEntry/passenger-seat owns cleanup during transitions. Do not restore
                // an old handle target after that owner has started releasing the rider.
                this.ClearBikeHandIk(false);
                this.SetBikeShooterAim(false);
                this.SetBikeReloadSteeringLocked(false);
                this.m_BikeSeatRole = nextRole;
                this.m_ActiveBikeSeat = nextSeat;
                this.m_BikeReloadDriver = nextRole == BikeSeatRole.Driver
                    ? nextSeat?.GetComponentInParent<FranklinArcadeBikeDriver>()
                    : null;
                this.SetCautiousWalk(false, true);
                this.SetObjectDirectionForShooting(false);
                this.ResetShotDirectionTracking();
                this.RefreshMenuSelection();
                this.RefreshBikeControlLayout();
            }

            if (this.m_BikeSeatRole == BikeSeatRole.None) return;

            this.SetObjectDirectionForShooting(false);
            this.SetBikeShooterAim(this.m_Aiming);
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (this.m_BikeSeatRole == BikeSeatRole.Driver &&
                weapon != null &&
                !this.CanUseWeaponInCurrentSeat(weapon))
            {
                this.SuspendInvalidDriverWeapon();
            }

            this.RefreshBikeWeaponHandIk();
        }

        private async void SuspendInvalidDriverWeapon()
        {
            if (this.m_BikeSeatRole != BikeSeatRole.Driver ||
                this.m_Player == null ||
                this.m_BikeSuspendedWeapon != null ||
                !this.m_BikeWeaponTask.IsCompleted)
            {
                return;
            }

            try
            {
                await this.PrepareForBikeDriverEntryInternal(this.m_Player);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void RefreshBikeWeaponHandIk()
        {
            if (this.m_Player == null || this.m_BikeSeatRole == BikeSeatRole.None)
            {
                this.ClearBikeHandIk(false);
                return;
            }

            Animator animator = this.m_Player.GetComponentInChildren<Animator>(true);
            CharacterIKSetter ikSetter = animator != null
                ? animator.GetComponent<CharacterIKSetter>()
                : null;
            if (ikSetter != this.m_BikeIkSetter)
            {
                this.ClearBikeHandIk(false);
                this.m_BikeIkSetter = ikSetter;
            }
            if (this.m_BikeIkSetter == null) return;

            ShooterWeapon weapon = this.GetActiveWeapon();
            bool useWeaponPose = !this.m_IsSwitching &&
                                 weapon != null &&
                                 this.CanUseWeaponInCurrentSeat(weapon);
            if (!useWeaponPose)
            {
                if (this.m_HasOriginalBikeHandIk)
                {
                    this.m_BikeIkSetter.RestoreHandIKState(this.m_OriginalBikeHandIk);
                    this.m_HasOriginalBikeHandIk = false;
                }
                return;
            }

            if (!this.m_HasOriginalBikeHandIk)
            {
                this.m_OriginalBikeHandIk = this.m_BikeIkSetter.CaptureHandIKState();
                this.m_HasOriginalBikeHandIk = true;
            }

            CharacterIKSetter.HandIKState original = this.m_OriginalBikeHandIk;
            if (this.m_BikeSeatRole == BikeSeatRole.Driver)
            {
                // The left hand remains vehicle-owned. The right hand follows the
                // layer-7/8 one-hand gun pose and therefore remains free to fire.
                this.m_BikeIkSetter.SetIKTargets(
                    original.LeftTarget,
                    null,
                    original.LeftWeight,
                    0f,
                    original.LeftRotationWeight,
                    0f
                );
            }
            else
            {
                // A rear passenger does not drive: both hands belong to the selected
                // weapon, while the passenger seat continues to own both foot targets.
                this.m_BikeIkSetter.SetIKTargets(null, null, 0f, 0f, 0f, 0f);
            }
        }

        private void ClearBikeHandIk(bool restore)
        {
            if (restore && this.m_HasOriginalBikeHandIk && this.m_BikeIkSetter != null)
                this.m_BikeIkSetter.RestoreHandIKState(this.m_OriginalBikeHandIk);
            this.m_HasOriginalBikeHandIk = false;
            this.m_BikeIkSetter = null;
        }

        private void ClearBikeSeatContext()
        {
            this.SetBikeShooterAim(false);
            this.SetBikeReloadSteeringLocked(false);
            this.ClearBikeHandIk(false);
            this.m_BikeSeatRole = BikeSeatRole.None;
            this.m_ActiveBikeSeat = null;
            this.m_BikeCameraAim = null;
            this.m_BikeReloadDriver = null;
            this.RefreshBikeControlLayout();
            this.RefreshMenuSelection();
        }

        private void SetBikeShooterAim(bool active)
        {
            if (this.m_BikeCameraAim == null && this.m_Player != null)
            {
                this.m_BikeCameraAim = this.m_Player
                    .GetComponentInChildren<FranklinBikeMainShotAim>(true);
            }

            this.m_BikeCameraAim?.SetShooterAimActive(
                active && this.m_BikeSeatRole != BikeSeatRole.None
            );
        }

        private void RefreshBikeReloadState()
        {
            if (this.m_BikeSeatRole != BikeSeatRole.Driver ||
                this.m_Player == null)
            {
                this.SetBikeReloadSteeringLocked(false);
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null || !this.CanUseWeaponInCurrentSeat(weapon))
            {
                this.SetBikeReloadSteeringLocked(false);
                return;
            }

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            bool isReloading = stance.Reloading.IsReloading &&
                               stance.Reloading.WeaponReloading == weapon;
            this.SetBikeReloadSteeringLocked(isReloading);
        }

        private void RefreshReloadIndicator()
        {
            if (this.m_ReloadIndicatorRoot == null) return;

            ShooterWeapon weapon = this.GetActiveWeapon();
            ShooterStance stance = this.m_Player != null
                ? this.m_Player.Combat.RequestStance<ShooterStance>()
                : null;
            bool isReloading = weapon != null &&
                               stance != null &&
                               stance.Reloading.IsReloading &&
                               stance.Reloading.WeaponReloading == weapon;

            if (this.m_ReloadIndicatorRoot.activeSelf != isReloading)
                this.m_ReloadIndicatorRoot.SetActive(isReloading);
            if (!isReloading) return;

            float progress = Mathf.Clamp01(stance.Reloading.Ratio);
            if (this.m_ReloadProgressImage != null)
                this.m_ReloadProgressImage.fillAmount = progress;
        }

        private bool ShouldReloadBeforeFire(
            ShooterWeapon weapon,
            ShooterStance stance)
        {
            WeaponData data = stance.Get(weapon);
            if (data == null ||
                !weapon.Magazine.GetHasMagazine(data.WeaponArgs) ||
                this.m_Player.Combat.RequestMunition(weapon) is not ShooterMunition munition)
            {
                return false;
            }

            return munition.InMagazine <= 0 &&
                   weapon.Magazine.GetTotalAmmo(data.WeaponArgs) > 0 &&
                   weapon.GetReload(this.m_Player, false) != null;
        }

        private async void ReloadThenBeginFire(
            ShooterWeapon weapon,
            ShooterStance stance,
            int requestId)
        {
            bool reloadStartedHere = !stance.Reloading.IsReloading;

            try
            {
                if (reloadStartedHere)
                {
                    Task reloadTask = stance.Reload(weapon);
                    this.SetBikeReloadSteeringLocked(
                        this.m_BikeSeatRole == BikeSeatRole.Driver &&
                        stance.Reloading.IsReloading &&
                        stance.Reloading.WeaponReloading == weapon
                    );
                    await reloadTask;
                }
                else
                {
                    while (this != null &&
                           stance.Reloading.IsReloading &&
                           stance.Reloading.WeaponReloading == weapon)
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }
            finally
            {
                bool stillReloading = this.m_BikeSeatRole == BikeSeatRole.Driver &&
                                      stance.Reloading.IsReloading &&
                                      stance.Reloading.WeaponReloading == weapon;
                this.SetBikeReloadSteeringLocked(stillReloading);
            }

            if (!this.IsPreAimFireRequestValid(weapon, requestId)) return;
            this.BeginFireWhenAimReady(weapon, stance, requestId);
        }

        private bool IsPreAimFireRequestValid(
            ShooterWeapon weapon,
            int requestId)
        {
            return this != null &&
                   requestId == this.m_FireRequestId &&
                   this.m_FireHeld &&
                   !this.m_IsSwitching &&
                   !this.IsWeaponMenuOpen &&
                   !this.m_PhoneUseActive &&
                   this.m_Player != null &&
                   this.GetActiveWeapon() == weapon &&
                   this.CanUseWeaponInCurrentSeat(weapon);
        }

        private void SetBikeReloadSteeringLocked(bool active)
        {
            if (this.m_BikeReloadDriver == null &&
                this.m_BikeSeatRole == BikeSeatRole.Driver &&
                this.m_ActiveBikeSeat != null)
            {
                this.m_BikeReloadDriver = this.m_ActiveBikeSeat
                    .GetComponentInParent<FranklinArcadeBikeDriver>();
            }

            this.m_BikeReloadDriver?.SetShooterReloadSteeringLocked(active);
        }

        private FranklinShooterCatalog.Entry GetCatalogEntry(ShooterWeapon weapon)
        {
            int index = this.m_Catalog?.IndexOf(weapon) ?? -1;
            return this.m_Catalog?.Get(index);
        }

        private bool CanUseWeaponInCurrentSeat(ShooterWeapon weapon)
        {
            return this.m_BikeSeatRole != BikeSeatRole.Driver ||
                   IsDriverWeaponEntryAllowed(this.GetCatalogEntry(weapon));
        }

        private static bool IsDriverWeaponEntryAllowed(
            FranklinShooterCatalog.Entry entry)
        {
            return entry != null &&
                   (string.Equals(entry.Id, "m1911", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Id, "uzi", StringComparison.OrdinalIgnoreCase));
        }

        private async void BeginFireWhenAimReady(
            ShooterWeapon weapon,
            ShooterStance stance,
            int requestId)
        {
            this.SetObjectDirectionForShooting(this.m_BikeSeatRole == BikeSeatRole.None);
            if (!this.SetAimActive(weapon, stance, true))
            {
                if (requestId == this.m_FireRequestId) this.m_FireHeld = false;
                return;
            }

            WeaponData initialData = stance.Get(weapon);
            if (initialData == null)
            {
                if (requestId == this.m_FireRequestId) this.m_FireHeld = false;
                return;
            }

            IdString firingSightId = initialData.SightId;
            SightItem firingSight = weapon.Sights.Get(firingSightId);
            float readyDelay = Mathf.Max(
                AIM_POSE_READY_DELAY,
                firingSight?.Sight != null ? firingSight.Sight.SmoothTime : 0f
            );
            float readyAt = Time.time + readyDelay;

            while (Time.time < readyAt)
            {
                if (!this.IsFireRequestValid(weapon, stance, firingSightId, requestId))
                    return;
                await Task.Yield();
            }

            // Require one fully evaluated frame after the 0.25 second GC2 sight blend.
            await Task.Yield();
            if (!this.IsFireRequestValid(weapon, stance, firingSightId, requestId)) return;

            this.m_TriggerPulled = true;
            this.PrepareHeldSingleFire(weapon, stance);
            stance.PullTrigger(weapon);
            this.ArmObjectDirectionReturn();
        }

        private void PrepareHeldSingleFire(
            ShooterWeapon weapon,
            ShooterStance stance)
        {
            if (weapon.Fire.Mode != ShootMode.Single)
            {
                this.ResetHeldSingleFire();
                return;
            }

            this.m_SingleRepeatWeapon = weapon;
            this.m_LastSingleRepeatShotFrame =
                stance.Get(weapon)?.LastShotFrame ?? int.MinValue;
        }

        private void RefreshHeldSingleFire()
        {
            if (!this.m_FireHeld || !this.m_TriggerPulled || !this.m_Aiming ||
                this.m_Player == null || this.m_IsSwitching ||
                this.IsWeaponMenuOpen || this.m_PhoneUseActive)
            {
                this.ResetHeldSingleFire();
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null || weapon.Fire.Mode != ShootMode.Single ||
                !this.CanUseWeaponInCurrentSeat(weapon))
            {
                this.ResetHeldSingleFire();
                return;
            }

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            if (stance.Reloading.IsReloading) return;

            WeaponData data = stance.Get(weapon);
            if (data == null) return;
            if (this.m_SingleRepeatWeapon != weapon)
            {
                this.m_SingleRepeatWeapon = weapon;
                this.m_LastSingleRepeatShotFrame = data.LastShotFrame;
                return;
            }

            if (data.LastShotFrame < 0 ||
                data.LastShotFrame == this.m_LastSingleRepeatShotFrame)
            {
                return;
            }

            float fireRate = weapon.Fire.FireRate(data.WeaponArgs);
            if (fireRate <= float.Epsilon ||
                data.Character.Time.Time < data.LastShotTime + 1f / fireRate)
            {
                return;
            }

            this.m_LastSingleRepeatShotFrame = data.LastShotFrame;
            if (weapon.Magazine.GetHasMagazine(data.WeaponArgs) &&
                (this.m_Player.Combat.RequestMunition(weapon) is not ShooterMunition munition ||
                 munition.InMagazine <= 0))
            {
                // Do not turn the same hold into an automatic reload. The user must
                // release and press Fire again, as required by the mobile flow.
                return;
            }

            stance.ReleaseTrigger(weapon);
            stance.PullTrigger(weapon);
            this.ArmObjectDirectionReturn();
        }

        private void ResetHeldSingleFire()
        {
            this.m_SingleRepeatWeapon = null;
            this.m_LastSingleRepeatShotFrame = int.MinValue;
        }

        private bool IsFireRequestValid(
            ShooterWeapon weapon,
            ShooterStance stance,
            IdString firingSightId,
            int requestId)
        {
            if (this == null ||
                requestId != this.m_FireRequestId ||
                !this.m_FireHeld ||
                !this.m_Aiming ||
                this.m_IsSwitching ||
                this.IsWeaponMenuOpen ||
                this.m_PhoneUseActive ||
                this.m_Player == null ||
                this.GetActiveWeapon() != weapon ||
                !this.CanUseWeaponInCurrentSeat(weapon))
            {
                return false;
            }

            WeaponData data = stance.Get(weapon);
            return data != null && data.SightId == firingSightId;
        }

        private void CancelFireRequest(
            ShooterWeapon weapon,
            ShooterStance stance,
            bool exitAim)
        {
            ++this.m_FireRequestId;
            this.m_FireHeld = false;
            this.ResetHeldSingleFire();
            if (this.m_TriggerPulled)
            {
                stance.ReleaseTrigger(weapon);
                this.m_TriggerPulled = false;
            }

            if (exitAim) this.SetAimActive(weapon, stance, false);
        }

        private void InvalidateFireRequest()
        {
            ++this.m_FireRequestId;
            this.m_FireHeld = false;
            this.m_TriggerPulled = false;
            this.ResetHeldSingleFire();
        }

        private bool SetAimActive(ShooterWeapon weapon, ShooterStance stance, bool active)
        {
            if (!active) this.SetBikeShooterAim(false);

            WeaponData data = stance.Get(weapon);
            if (data == null) return false;

            if (!active)
            {
                if (this.m_Aiming)
                {
                    stance.ExitSight(weapon);
                    this.m_Aiming = false;
                    if (!this.m_CautiousWalk)
                        this.ApplyDefaultSightPose(weapon, false);
                }
                return true;
            }

            if (this.m_Aiming) return true;

            string[] preferred = { "aim-ads", "aim-scope-1", "aim-scope-2" };
            foreach (string id in preferred)
            {
                IdString sightId = new(id);
                if (!weapon.Sights.Contains(sightId)) continue;
                stance.EnterSight(weapon, sightId);
                this.m_Aiming = true;
                this.SetBikeShooterAim(true);
                this.CompactActiveCrosshairs();
                return true;
            }

            return false;
        }

        private void SetCautiousWalk(bool active, bool force = false)
        {
            if (!force && this.m_CautiousWalk == active) return;

            this.m_CautiousWalk = active;
            this.RefreshCautiousWalkButton();
            if (this.m_Player == null) return;

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;

            if (active && this.m_CautiousLocomotion != null)
            {
                _ = this.m_Player.States.SetState(
                    this.m_CautiousLocomotion,
                    SHOOTER_LOCOMOTION_LAYER,
                    BlendMode.Blend,
                    new ConfigState(
                        0f,
                        1f,
                        1f,
                        SHOOTER_LOCOMOTION_TRANSITION,
                        SHOOTER_LOCOMOTION_TRANSITION
                    )
                );
            }
            else
            {
                this.m_Player.States.Stop(
                    SHOOTER_LOCOMOTION_LAYER,
                    0f,
                    SHOOTER_LOCOMOTION_TRANSITION
                );
            }

            if (!this.m_Aiming) this.ApplyDefaultSightPose(weapon, active);
        }

        // GC2 enters the default lowered-gun sight automatically. Normal walking keeps its
        // logical sight id for shooting, but removes that sight's visual state until requested.
        private void ApplyDefaultSightPose(ShooterWeapon weapon, bool active)
        {
            if (this.m_Player == null || weapon == null) return;

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            WeaponData data = stance.Get(weapon);
            if (data == null) return;

            IdString defaultId = weapon.Sights.DefaultId;
            SightItem defaultSight = weapon.Sights.Get(defaultId);
            if (defaultSight == null) return;

            if (data.SightId != defaultId)
            {
                if (active) stance.ExitSight(weapon);
                return;
            }

            if (active) defaultSight.Enter(this.m_Player, weapon);
            else defaultSight.Exit(this.m_Player, weapon);
        }

        private void RefreshCautiousWalkButton()
        {
            this.m_CautiousWalkButton?.SetToggled(this.m_CautiousWalk);
        }

        private void CompactActiveCrosshairs()
        {
            if (CROSSHAIR_ACCURACY_POSITION == null ||
                CROSSHAIR_POSITION_X == null ||
                CROSSHAIR_POSITION_Y == null)
            {
                return;
            }

            CrosshairUI[] crosshairs = FindObjectsByType<CrosshairUI>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            foreach (CrosshairUI crosshair in crosshairs)
            {
                if (crosshair == null ||
                    !IsFranklinWeaponCrosshair(crosshair.transform) ||
                    CROSSHAIR_ACCURACY_POSITION.GetValue(crosshair) is not RectTransform)
                {
                    continue;
                }

                int instanceId = crosshair.GetInstanceID();
                if (!this.m_CompactCrosshairs.Add(instanceId)) continue;

                Vector2 positionX = (Vector2) CROSSHAIR_POSITION_X.GetValue(crosshair);
                Vector2 positionY = (Vector2) CROSSHAIR_POSITION_Y.GetValue(crosshair);
                CROSSHAIR_POSITION_X.SetValue(crosshair, CompactCrosshairRange(positionX));
                CROSSHAIR_POSITION_Y.SetValue(crosshair, CompactCrosshairRange(positionY));
            }
        }

        private static bool IsFranklinWeaponCrosshair(Transform item)
        {
            while (item != null)
            {
                string itemName = item.name;
                if (itemName.StartsWith("Ak_Crosshair", StringComparison.Ordinal) ||
                    itemName.StartsWith("Pistol_Crosshair", StringComparison.Ordinal) ||
                    itemName.StartsWith("Shotgun_Crosshair", StringComparison.Ordinal))
                {
                    return true;
                }

                item = item.parent;
            }

            return false;
        }

        private static Vector2 CompactCrosshairRange(Vector2 range)
        {
            return new Vector2(
                range.x,
                Mathf.Lerp(range.x, range.y, CROSSHAIR_EXPANSION_SCALE)
            );
        }

        private async void Reload()
        {
            if (this.m_Player == null ||
                this.m_BikeSeatRole == BikeSeatRole.Driver) return;
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null || !this.CanUseWeaponInCurrentSeat(weapon)) return;
            await this.m_Player.Combat.RequestStance<ShooterStance>().Reload(weapon);
        }

        private async void SwitchToMelee()
        {
            if (this.m_IsSwitching || this.m_Player == null ||
                this.m_BikeSeatRole != BikeSeatRole.None) return;
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;

            this.m_IsSwitching = true;
            try
            {
                ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
                this.CancelFireRequest(weapon, stance, true);
                this.SetCautiousWalk(false, true);

                GameObject prop = this.m_Player.Combat.GetProp(weapon);
                await this.m_Player.Combat.Unequip(
                    weapon,
                    new Args(this.m_Player.gameObject)
                );
                if (prop != null) this.m_Player.Props.RemoveInstance(prop);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not switch Franklin shooter to melee: {exception}", this);
            }
            finally
            {
                this.m_IsSwitching = false;
                this.m_Aiming = false;
                this.RefreshControlMode();
            }
        }

        private async void OnWeaponChanged(IWeapon weapon, GameObject prop)
        {
            if (weapon is ShooterWeapon shooterWeapon)
            {
                this.RefreshSelectedIndex();
                if (!this.m_IsSwitching)
                {
                    this.InvalidateFireRequest();
                    this.m_Aiming = false;
                    await Task.Yield();
                    if (this.GetActiveWeapon() == shooterWeapon)
                        this.SetCautiousWalk(false, true);
                    else if (this.GetActiveWeapon() == null)
                    {
                        this.m_CautiousWalk = false;
                        this.RefreshCautiousWalkButton();
                    }
                }
            }
            this.RefreshControlMode();
        }

        private ShooterWeapon GetActiveWeapon()
        {
            return this.m_Player?.Combat.GetActiveWeapon<ShooterWeapon>();
        }

        private bool HasActiveWeapon() => this.GetActiveWeapon() != null;

        private void RefreshSelectedIndex()
        {
            int index = this.m_Catalog?.IndexOf(this.GetActiveWeapon()) ?? -1;
            if (index >= 0) this.m_SelectedIndex = index;
            else if (this.m_SelectedIndex < 0) this.m_SelectedIndex = this.m_Catalog?.DefaultWeaponIndex ?? -1;
        }

        private void RefreshMenuSelection()
        {
            for (int i = 0; i < this.m_SlotFrames.Count; ++i)
            {
                bool selected = i == this.m_SelectedIndex;
                bool available = this.m_BikeSeatRole != BikeSeatRole.Driver ||
                                 IsDriverWeaponEntryAllowed(this.m_Catalog?.Get(i));
                Graphic frame = this.m_SlotFrames[i];
                frame.color = SELECTED_SECTOR;
                frame.gameObject.SetActive(selected);
                if (selected)
                {
                    frame.SetVerticesDirty();
                    frame.SetMaterialDirty();
                }
                if (i < this.m_SlotButtons.Count)
                    this.m_SlotButtons[i].interactable = available;
                this.m_SlotIcons[i].color = !available
                    ? new Color(0.52f, 0.56f, 0.6f, 0.22f)
                    : selected
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.56f);
            }

            if (this.m_Hint != null)
            {
                this.m_Hint.text = this.m_BikeSeatRole switch
                {
                    BikeSeatRole.Driver =>
                        "DRIVER: M1911 / UZI ONLY  •  TAP OUTSIDE TO CLOSE",
                    BikeSeatRole.Passenger =>
                        "PASSENGER: ALL WEAPONS AVAILABLE  •  TAP OUTSIDE TO CLOSE",
                    _ => "TAP A WEAPON TO EQUIP  •  TAP OUTSIDE TO CLOSE"
                };
            }

            this.RefreshSelectedPanel();
        }

        private void RefreshSelectedPanel()
        {
            FranklinShooterCatalog.Entry entry = this.m_Catalog?.Get(this.m_SelectedIndex);
            if (entry == null) return;

            if (this.m_SelectedIcon != null) this.m_SelectedIcon.overrideSprite = entry.Icon;
            if (this.m_SelectedName != null) this.m_SelectedName.text = entry.DisplayName.ToUpperInvariant();
            if (this.m_SelectedCategory != null) this.m_SelectedCategory.text = entry.Category.ToUpperInvariant();

            if (this.m_SelectedAmmo != null)
            {
                string ammo = "READY";
                if (this.m_Player != null && entry.Weapon != null &&
                    this.m_Player.Combat.RequestMunition(entry.Weapon) is ShooterMunition munition)
                {
                    GameObject prop = this.m_Player.Combat.GetProp(entry.Weapon);
                    Args args = new(this.m_Player.gameObject, prop);
                    int total = entry.Weapon.Magazine.GetTotalAmmo(args);
                    ammo = munition.InMagazine + " / " + (total >= int.MaxValue ? "∞" : total.ToString());
                }

                this.m_SelectedAmmo.text = ammo;
            }
        }

        private bool BindWeaponHudTap()
        {
            FranklinPlayerStatusHud hud = FranklinPlayerStatusHud.Instance ??
                                         FindFirstObjectByType<FranklinPlayerStatusHud>();
            if (hud == null) return false;

            Transform weaponCard = hud.transform.Find("Weapon Card") ??
                                   FindDeepChild(hud.transform, "Weapon Card");
            if (weaponCard == null) return false;

            Image image = weaponCard.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;
            if (weaponCard.GetComponent<FranklinWeaponHudTap>() == null)
                weaponCard.gameObject.AddComponent<FranklinWeaponHudTap>();
            return true;
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new(
                "Franklin Shooter UI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)
            );
            canvasObject.transform.SetParent(this.transform, false);
            this.m_Canvas = canvasObject.GetComponent<Canvas>();
            this.m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            this.m_Canvas.sortingOrder = 95;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            this.BuildWeaponMenu(canvasObject.transform);
            this.BuildShooterControls(canvasObject.transform);
            this.BuildHudTapTarget(canvasObject.transform);
            this.BuildReloadIndicator(canvasObject.transform);
            this.TryBindOnFootControls();
        }

        private void BuildReloadIndicator(Transform parent)
        {
            RectTransform root = CreateRect(
                "Reload Progress Indicator",
                parent,
                Vector2.zero,
                new Vector2(170f, 170f)
            );
            this.m_ReloadIndicatorRoot = root.gameObject;
            Sprite ringSprite = this.CreateReloadRingSprite();

            Image shadow = CreateImage(
                "Outline Shadow",
                root,
                ringSprite,
                new Color(0f, 0f, 0f, 0.72f)
            );
            SetRect(shadow.rectTransform, Vector2.zero, new Vector2(164f, 164f));
            shadow.preserveAspect = true;
            shadow.raycastTarget = false;

            Image track = CreateImage(
                "Yellow Outline",
                root,
                ringSprite,
                new Color(1f, 0.72f, 0.04f, 0.30f)
            );
            SetRect(track.rectTransform, Vector2.zero, new Vector2(150f, 150f));
            track.preserveAspect = true;
            track.raycastTarget = false;

            this.m_ReloadProgressImage = CreateImage(
                "Radial Fill",
                root,
                ringSprite,
                new Color(1f, 0.72f, 0.04f, 1f)
            );
            SetRect(this.m_ReloadProgressImage.rectTransform, Vector2.zero, new Vector2(150f, 150f));
            this.m_ReloadProgressImage.preserveAspect = true;
            this.m_ReloadProgressImage.raycastTarget = false;
            this.m_ReloadProgressImage.type = Image.Type.Filled;
            this.m_ReloadProgressImage.fillMethod = Image.FillMethod.Radial360;
            this.m_ReloadProgressImage.fillOrigin = (int) Image.Origin360.Top;
            this.m_ReloadProgressImage.fillClockwise = true;
            this.m_ReloadProgressImage.fillAmount = 0f;

            this.m_ReloadIndicatorRoot.SetActive(false);
        }

        private Sprite CreateReloadRingSprite()
        {
            const int size = 128;
            const float innerRadius = 48f;
            const float outerRadius = 61f;
            float center = (size - 1f) * 0.5f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(center, center)
                    );
                    float outerEdge = Mathf.Clamp01(outerRadius + 0.75f - distance);
                    float innerEdge = Mathf.Clamp01(distance - innerRadius + 0.75f);
                    byte alpha = (byte) Mathf.RoundToInt(
                        255f * Mathf.Min(outerEdge, innerEdge)
                    );
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            this.m_ReloadRingTexture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true
            )
            {
                name = "Franklin Reload Ring Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            this.m_ReloadRingTexture.SetPixels32(pixels);
            this.m_ReloadRingTexture.Apply(false, true);

            this.m_ReloadRingSprite = Sprite.Create(
                this.m_ReloadRingTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
            this.m_ReloadRingSprite.name = "Franklin Reload Ring Runtime";
            return this.m_ReloadRingSprite;
        }

        private void BuildWeaponMenu(Transform parent)
        {
            this.m_MenuRoot = CreateRect("Weapon Selection Menu", parent, Vector2.zero, Vector2.zero).gameObject;
            Stretch(this.m_MenuRoot.GetComponent<RectTransform>());

            Image blocker = this.m_MenuRoot.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.06f);
            blocker.raycastTarget = true;
            Button closeButton = this.m_MenuRoot.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(this.CloseWeaponMenu);

            Image background = CreateImage(
                "ImageGen Weapon Wheel", this.m_MenuRoot.transform,
                Resources.Load<Sprite>(BACKGROUND_RESOURCE), WHEEL_TINT
            );
            SetRect(background.rectTransform, Vector2.zero, new Vector2(900f, 900f));
            background.preserveAspect = true;
            background.raycastTarget = false;

            Text title = CreateText("Title", this.m_MenuRoot.transform, "WEAPONS", 30, OFF_WHITE, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 475f), new Vector2(460f, 50f));
            title.fontStyle = FontStyle.Bold;
            AddTextOutline(title, 0.9f);

            Vector2[] positions =
            {
                new(0f, 330f), new(234f, 234f), new(330f, 0f), new(234f, -234f),
                new(0f, -330f), new(-234f, -234f), new(-330f, 0f), new(-234f, 234f)
            };

            for (int i = 0; i < this.m_Catalog.Count; ++i)
            {
                FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(i);
                int index = i;
                RectTransform slot = CreateRect(
                    "Weapon Slot " + (i + 1), this.m_MenuRoot.transform,
                    positions[i], new Vector2(220f, 160f)
                );
                RectTransform sector = CreateRect(
                    "Selected Sector " + (i + 1),
                    this.m_MenuRoot.transform,
                    Vector2.zero,
                    new Vector2(900f, 900f)
                );
                sector.SetSiblingIndex(background.rectTransform.GetSiblingIndex() + 1);
                sector.localRotation = Quaternion.Euler(0f, 0f, -45f * i);
                FranklinWeaponSectorHighlight frame =
                    sector.gameObject.AddComponent<FranklinWeaponSectorHighlight>();
                frame.color = SELECTED_SECTOR;
                frame.raycastTarget = false;
                sector.gameObject.SetActive(false);

                Image hitArea = slot.gameObject.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                hitArea.raycastTarget = true;
                Button button = slot.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = hitArea;
                button.colors = CreateButtonColors(CYAN);
                button.onClick.AddListener(() => this.SelectWeaponAndClose(index));

                Image icon = CreateImage("Icon", slot, entry.Icon, new Color(1f, 1f, 1f, 0.72f));
                SetRect(icon.rectTransform, new Vector2(0f, 14f), new Vector2(210f, 104f));
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                Text label = CreateText("Label", slot, entry.DisplayName.ToUpperInvariant(), 20, OFF_WHITE, TextAnchor.MiddleCenter);
                SetRect(label.rectTransform, new Vector2(0f, -60f), new Vector2(218f, 34f));
                label.fontStyle = FontStyle.Bold;
                label.raycastTarget = false;
                AddTextOutline(label, 0.82f);

                this.m_SlotFrames.Add(frame);
                this.m_SlotIcons.Add(icon);
                this.m_SlotButtons.Add(button);
            }

            RectTransform center = CreateRect(
                "Selected Weapon", this.m_MenuRoot.transform,
                Vector2.zero, new Vector2(330f, 238f)
            );
            Image centerPanel = center.gameObject.AddComponent<Image>();
            centerPanel.color = new Color(0.015f, 0.02f, 0.025f, 0.12f);
            centerPanel.raycastTarget = false;

            this.m_SelectedIcon = CreateImage("Selected Icon", center, null, Color.white);
            SetRect(this.m_SelectedIcon.rectTransform, new Vector2(0f, 54f), new Vector2(270f, 108f));
            this.m_SelectedIcon.preserveAspect = true;
            this.m_SelectedIcon.raycastTarget = false;

            this.m_SelectedName = CreateText("Selected Name", center, string.Empty, 30, OFF_WHITE, TextAnchor.MiddleCenter);
            SetRect(this.m_SelectedName.rectTransform, new Vector2(0f, -24f), new Vector2(324f, 44f));
            this.m_SelectedName.fontStyle = FontStyle.Bold;
            AddTextOutline(this.m_SelectedName, 0.9f);

            this.m_SelectedCategory = CreateText("Selected Category", center, string.Empty, 16, CYAN, TextAnchor.MiddleCenter);
            SetRect(this.m_SelectedCategory.rectTransform, new Vector2(0f, -64f), new Vector2(324f, 28f));
            AddTextOutline(this.m_SelectedCategory, 0.82f);

            this.m_SelectedAmmo = CreateText("Selected Ammo", center, string.Empty, 22, LIME, TextAnchor.MiddleCenter);
            SetRect(this.m_SelectedAmmo.rectTransform, new Vector2(0f, -96f), new Vector2(324f, 32f));
            this.m_SelectedAmmo.fontStyle = FontStyle.Bold;
            AddTextOutline(this.m_SelectedAmmo, 0.82f);

            this.m_Hint = CreateText(
                "Hint", this.m_MenuRoot.transform,
                "TAP A WEAPON TO EQUIP  •  TAP OUTSIDE TO CLOSE",
                18, new Color(OFF_WHITE.r, OFF_WHITE.g, OFF_WHITE.b, 0.78f), TextAnchor.MiddleCenter
            );
            SetRect(this.m_Hint.rectTransform, new Vector2(0f, -485f), new Vector2(900f, 38f));
            AddTextOutline(this.m_Hint, 0.8f);

            this.m_MenuRoot.SetActive(false);
        }

        private void BuildShooterControls(Transform parent)
        {
            this.m_ControlsRoot = CreateRect("Shooter Touch Controls", parent, Vector2.zero, Vector2.zero).gameObject;
            Stretch(this.m_ControlsRoot.GetComponent<RectTransform>());

            Image fireImage = CreateTouchButton(
                "Fire", this.m_ControlsRoot.transform,
                "FranklinShooter/UI/Controls/fire",
                ON_FOOT_FIRE_POSITION, new Vector2(185f, 185f),
                FranklinShooterTouchButton.Action.Fire
            );
            this.m_FireButtonRect = fireImage.rectTransform;
            this.m_MeleeSwitchImage = CreateTouchButton(
                "Switch To Melee", this.m_ControlsRoot.transform,
                null,
                new Vector2(-83.1f, 254.9f), new Vector2(140f, 140f),
                FranklinShooterTouchButton.Action.Melee
            );
            Image reloadImage = CreateTouchButton(
                "Reload", this.m_ControlsRoot.transform,
                "FranklinShooter/UI/Controls/reload",
                ON_FOOT_RELOAD_POSITION, new Vector2(126f, 126f),
                FranklinShooterTouchButton.Action.Reload
            );
            this.m_ReloadButtonRect = reloadImage.rectTransform;
            Image cautiousWalkImage = CreateTouchButton(
                "Cautious Walk", this.m_ControlsRoot.transform,
                "FranklinShooter/UI/Controls/cautious-walk",
                new Vector2(-273f, 94f), new Vector2(126f, 126f),
                FranklinShooterTouchButton.Action.CautiousWalk
            );
            this.m_CautiousWalkButton =
                cautiousWalkImage.GetComponent<FranklinShooterTouchButton>();
            this.RefreshCautiousWalkButton();
            this.RefreshBikeControlLayout();
            this.m_ControlsRoot.SetActive(false);
        }

        private bool TryBindOnFootControls()
        {
            FranklinMobileHud hud = FindFirstObjectByType<FranklinMobileHud>();
            Transform onFoot = hud != null
                ? hud.transform.Find("Franklin On Foot Controls")
                : null;
            if (onFoot == null) return false;
            if (this.m_OnFootControls == onFoot) return true;

            this.SetMeleeControlsSuppressed(false);
            this.m_OnFootControls = onFoot;
            this.m_MeleeFight = onFoot.Find("Fight")?.gameObject;
            this.m_MeleeSidestepLeft = onFoot.Find("Sidestep Left")?.gameObject;
            this.m_MeleeSidestepRight = onFoot.Find("Sidestep Right")?.gameObject;
            Image fightImage = this.m_MeleeFight != null
                ? this.m_MeleeFight.GetComponent<Image>()
                : null;
            if (this.m_MeleeSwitchImage != null && fightImage != null)
                this.m_MeleeSwitchImage.sprite = fightImage.sprite;
            this.m_MeleeFightWasActive = this.m_MeleeFight != null &&
                                         this.m_MeleeFight.activeSelf;

            this.m_SidestepVisibility = null;
            foreach (MonoBehaviour behaviour in onFoot.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null ||
                    behaviour.GetType().FullName !=
                    "FranklinGame.Melee.FranklinSidestepVisibility") continue;
                this.m_SidestepVisibility = behaviour;
                break;
            }

            this.m_SidestepVisibilityWasEnabled = this.m_SidestepVisibility != null &&
                                                  this.m_SidestepVisibility.enabled;
            this.RefreshControlMode();
            return true;
        }

        private void RefreshControlMode()
        {
            bool hasShooterWeapon = this.HasActiveWeapon() || this.m_IsSwitching;
            ShooterWeapon activeWeapon = this.GetActiveWeapon();
            bool hasUsableWeapon = activeWeapon != null &&
                                   this.CanUseWeaponInCurrentSeat(activeWeapon);
            if (!hasShooterWeapon || this.m_BikeSeatRole != BikeSeatRole.None)
                this.SetObjectDirectionForShooting(false);
            this.SetIdleAnimationSuppressed(hasShooterWeapon);
            this.SetMeleeControlsSuppressed(hasShooterWeapon);
            this.RefreshBikeControlLayout();

            if (this.m_ControlsRoot == null) return;
            bool onFootVisible = this.m_OnFootControls == null ||
                                 this.m_OnFootControls.gameObject.activeInHierarchy;
            bool hasVisibleControlMode = onFootVisible ||
                                         this.m_BikeSeatRole != BikeSeatRole.None;
            bool showShooterControls = hasUsableWeapon &&
                                       !this.m_IsSwitching &&
                                       !this.m_PhoneUseActive &&
                                       !this.IsWeaponMenuOpen &&
                                       !FranklinMobileHud.ControlsSuppressed &&
                                       hasVisibleControlMode;
            if (this.m_ControlsRoot.activeSelf != showShooterControls)
                this.m_ControlsRoot.SetActive(showShooterControls);
        }

        private void RefreshBikeControlLayout()
        {
            bool isOnBike = this.m_BikeSeatRole != BikeSeatRole.None;
            bool showReload = this.m_BikeSeatRole != BikeSeatRole.Driver;
            if (this.m_FireButtonRect != null)
                this.m_FireButtonRect.anchoredPosition = isOnBike
                    ? BIKE_FIRE_POSITION
                    : ON_FOOT_FIRE_POSITION;
            if (this.m_ReloadButtonRect != null)
            {
                this.m_ReloadButtonRect.anchoredPosition = ON_FOOT_RELOAD_POSITION;
                if (this.m_ReloadButtonRect.gameObject.activeSelf != showReload)
                    this.m_ReloadButtonRect.gameObject.SetActive(showReload);
            }
            if (this.m_MeleeSwitchImage != null &&
                this.m_MeleeSwitchImage.gameObject.activeSelf == isOnBike)
            {
                this.m_MeleeSwitchImage.gameObject.SetActive(!isOnBike);
            }
            if (this.m_CautiousWalkButton != null &&
                this.m_CautiousWalkButton.gameObject.activeSelf == isOnBike)
            {
                this.m_CautiousWalkButton.gameObject.SetActive(!isOnBike);
            }
        }

        private void TrackShotActivityAndDirectionTimeout()
        {
            if (this.m_BikeSeatRole != BikeSeatRole.None)
            {
                this.SetObjectDirectionForShooting(false);
                this.ResetShotDirectionTracking();
                return;
            }

            if (this.m_Player == null)
            {
                this.ResetShotDirectionTracking();
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null)
            {
                // Weapon switching briefly has no active weapon. Keep the pending timeout
                // so changing guns cannot leave Object Direction enabled indefinitely.
                if (!this.m_IsSwitching) this.ResetShotDirectionTracking();
                return;
            }

            ShooterStance stance = this.m_Player.Combat.RequestStance<ShooterStance>();
            WeaponData data = stance.Get(weapon);
            if (this.m_ObservedShotWeapon != weapon)
            {
                this.m_ObservedShotWeapon = weapon;
                this.m_LastObservedShotFrame = data?.LastShotFrame ?? int.MinValue;
            }
            else if (data != null && data.LastShotFrame != this.m_LastObservedShotFrame)
            {
                this.m_LastObservedShotFrame = data.LastShotFrame;
                if (data.LastShotFrame >= 0) this.ArmObjectDirectionReturn();
            }

            if (this.m_ObjectDirectionReturnAt < 0f ||
                Time.time < this.m_ObjectDirectionReturnAt)
            {
                return;
            }

            this.m_ObjectDirectionReturnAt = -1f;
            this.SetObjectDirectionForShooting(false);
        }

        private void ArmObjectDirectionReturn()
        {
            this.m_ObjectDirectionReturnAt = Time.time + OBJECT_DIRECTION_IDLE_SECONDS;
        }

        private void ResetShotDirectionTracking()
        {
            this.m_ObjectDirectionReturnAt = -1f;
            this.m_ObservedShotWeapon = null;
            this.m_LastObservedShotFrame = int.MinValue;
        }

        private void SetObjectDirectionForShooting(bool active)
        {
            if (active)
            {
                if (this.m_OwnsObjectDirection &&
                    this.m_ObjectDirectionToggle != null &&
                    this.m_ObjectDirectionToggle.IsObjectDirectionEnabled &&
                    this.m_Player?.Facing is UnitFacingObjectDirection)
                {
                    return;
                }

                if (this.m_OwnsObjectDirection)
                {
                    this.m_ObjectDirectionToggle?.SetObjectDirectionEnabled(false);
                    this.m_OwnsObjectDirection = false;
                }
                if (this.m_AnimationBridge == null && this.m_Player != null)
                {
                    this.m_AnimationBridge = this.m_Player
                        .GetComponentInChildren<FranklinAnimationBridge>(true);
                }
                if (this.m_AnimationBridge == null) return;

                this.m_ObjectDirectionToggle ??=
                    this.m_AnimationBridge.GetComponent<FranklinObjectDirectionToggle>();
                if (this.m_ObjectDirectionToggle == null)
                {
                    this.m_ObjectDirectionToggle = this.m_AnimationBridge.gameObject
                        .AddComponent<FranklinObjectDirectionToggle>();
                }

                if (this.m_ObjectDirectionToggle.IsObjectDirectionEnabled) return;
                this.m_OwnsObjectDirection =
                    this.m_ObjectDirectionToggle.SetObjectDirectionEnabled(true);
                return;
            }

            if (!this.m_OwnsObjectDirection) return;
            this.m_ObjectDirectionToggle?.SetObjectDirectionEnabled(false);
            this.m_OwnsObjectDirection = false;
        }

        private void SetIdleAnimationSuppressed(bool suppressed)
        {
            if (suppressed)
            {
                if (this.m_OwnsIdleVariationSuppression) return;
                if (this.m_AnimationBridge == null && this.m_Player != null)
                {
                    this.m_AnimationBridge = this.m_Player
                        .GetComponentInChildren<FranklinAnimationBridge>(true);
                }
                if (this.m_AnimationBridge == null) return;

                this.m_AnimationBridge.AcquireIdleVariationsSuppression(this);
                this.m_OwnsIdleVariationSuppression = true;
                return;
            }

            if (!this.m_OwnsIdleVariationSuppression) return;
            this.m_AnimationBridge?.ReleaseIdleVariationsSuppression(this);
            this.m_OwnsIdleVariationSuppression = false;
        }

        private void SetMeleeControlsSuppressed(bool suppressed)
        {
            if (suppressed)
            {
                if (this.m_MeleeFight != null && this.m_MeleeFight.activeSelf)
                    this.m_MeleeFight.SetActive(false);
                if (this.m_SidestepVisibility != null && this.m_SidestepVisibility.enabled)
                    this.m_SidestepVisibility.enabled = false;
                if (this.m_MeleeSidestepLeft != null && this.m_MeleeSidestepLeft.activeSelf)
                    this.m_MeleeSidestepLeft.SetActive(false);
                if (this.m_MeleeSidestepRight != null && this.m_MeleeSidestepRight.activeSelf)
                    this.m_MeleeSidestepRight.SetActive(false);
                this.m_MeleeSuppressed = true;
                return;
            }

            if (!this.m_MeleeSuppressed) return;
            if (this.m_MeleeFight != null &&
                this.m_MeleeFight.activeSelf != this.m_MeleeFightWasActive)
            {
                this.m_MeleeFight.SetActive(this.m_MeleeFightWasActive);
            }
            if (this.m_SidestepVisibility != null &&
                this.m_SidestepVisibility.enabled != this.m_SidestepVisibilityWasEnabled)
            {
                this.m_SidestepVisibility.enabled = this.m_SidestepVisibilityWasEnabled;
            }
            this.m_MeleeSuppressed = false;
        }

        private void BuildHudTapTarget(Transform parent)
        {
            RectTransform rect = CreateRect(
                "HUD Weapon Tap Target", parent,
                new Vector2(-255f, -83f), new Vector2(430f, 86f)
            );
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            hitArea.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;
            button.onClick.AddListener(this.OpenWeaponMenu);
            this.m_HudTapTarget = rect.gameObject;
        }

        private static Image CreateTouchButton(
            string name, Transform parent, string resource,
            Vector2 bottomRightOffset, Vector2 size,
            FranklinShooterTouchButton.Action action)
        {
            RectTransform rect = CreateRect(name, parent, bottomRightOffset, size);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = string.IsNullOrEmpty(resource)
                ? null
                : Resources.Load<Sprite>(resource);
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.94f);
            image.raycastTarget = true;

            FranklinShooterTouchButton button = rect.gameObject.AddComponent<FranklinShooterTouchButton>();
            button.Initialize(action, image);
            return image;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject instance = new(name, typeof(RectTransform));
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, position, size);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int size,
            Color color, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void AddTextOutline(Text text, float alpha)
        {
            if (text == null) return;
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static ColorBlock CreateButtonColors(Color highlight)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(highlight.r, highlight.g, highlight.b, 0.88f);
            colors.pressedColor = new Color(highlight.r, highlight.g, highlight.b, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject instance = new("EventSystem", typeof(EventSystem));
                eventSystem = instance.GetComponent<EventSystem>();
            }

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null) legacy.enabled = false;
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
