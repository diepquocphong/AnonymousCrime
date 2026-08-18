using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FranklinGame.Animations;
using FranklinGame.Menu;
using FranklinGame.Melee;
using FranklinGame.UI;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
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
        private const string FIRST_PERSON_ICON_RESOURCE =
            "FranklinShooter/UI/Controls/first-person-camera";
        private const string MOVEMENT_FIRST_PERSON_PREFERENCE_KEY =
            "Franklin.Camera.OnFoot.Movement.FirstPerson";
        private const string SHOOTER_FIRST_PERSON_PREFERENCE_KEY =
            "Franklin.Camera.OnFoot.Shooter.FirstPerson";
        private const string BIKE_DRIVER_MASK_RESOURCE =
            "FranklinShooter/Animations/Franklin Bike Driver Seat And Left Hand";
        private const string SHOOTER_UPPER_BODY_MASK_RESOURCE =
            "FranklinShooter/Animations/Franklin Shooter Upper Body";
        private const string BIKE_DRIVER_SHOOTER_MASK_RESOURCE =
            "FranklinShooter/Animations/Franklin Bike Driver Shooter Upper Body";
        private const string SHOOTER_LOCOMOTION_RESOURCE =
            "FranklinShooter/Animations/Franklin Shooter Upper Body Locomotion";
        private const int SHOOTER_LOCOMOTION_LAYER = 7;
        private const float SHOOTER_LOCOMOTION_TRANSITION = 0.25f;
        private const float AIM_POSE_READY_DELAY = 0.30f;
        private const float RPG_FIRE_INTERVAL_SECONDS = 2f;
        private const float OBJECT_DIRECTION_IDLE_SECONDS = 1f;
        private const float PLAYER_RETRY_SECONDS = 0.4f;
        private const float CROSSHAIR_EXPANSION_SCALE = 0.5f;
        private const int WEAPON_WHEEL_SECTOR_COUNT = 8;
        private const int WEAPON_MENU_THROWABLE_PAGE = 1;
        private static readonly Vector2 ON_FOOT_FIRE_POSITION =
            new(-273.1f, 254.9f);
        private static readonly Vector2 ON_FOOT_RELOAD_POSITION =
            new(-113f, 94f);
        private static readonly Vector2 BIKE_FIRE_POSITION =
            new(-690f, 190f);
        private static readonly Vector2 BIKE_MELEE_POSITION =
            new(-875f, 190f);
        private static readonly Vector2 ON_FOOT_MELEE_POSITION =
            new(-83.1f, 254.9f);
        private static readonly Vector2 ON_FOOT_FIRST_PERSON_POSITION =
            new(-83f, 410f);

        private const BindingFlags CROSSHAIR_FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo CROSSHAIR_ACCURACY_POSITION =
            typeof(CrosshairUI).GetField("m_AccuracyPosition", CROSSHAIR_FIELD_FLAGS);
        private static readonly FieldInfo CROSSHAIR_POSITION_X =
            typeof(CrosshairUI).GetField("m_PositionX", CROSSHAIR_FIELD_FLAGS);
        private static readonly FieldInfo CROSSHAIR_POSITION_Y =
            typeof(CrosshairUI).GetField("m_PositionY", CROSSHAIR_FIELD_FLAGS);
        private static readonly IdString BIKE_DRIVER_AIM_SIGHT =
            new("bike-driver-aim");
        private static readonly IdString[] PREFERRED_AIM_SIGHTS =
        {
            new("throw"),
            new("aim-ads"),
            new("aim-scope-1"),
            new("aim-scope-2")
        };
        private static readonly Color CYAN = new(0.13f, 0.82f, 1f, 1f);
        private static readonly Color LIME = new(0.63f, 1f, 0.18f, 1f);
        private static readonly Color OFF_WHITE = new(0.92f, 0.94f, 0.94f, 1f);
        private static readonly Color WHEEL_TINT = new(0.48f, 0.51f, 0.55f, 0.90f);
        // Saturated navy remains dark, but separates clearly from the charcoal wheel art.
        private static readonly Color SELECTED_SECTOR = new(0.035f, 0.23f, 0.48f, 1f);

        private static FranklinShooterSystem s_Instance;
        private static AvatarMask s_BikeDriverAnimationMask;
        private static bool s_PhoneUseActive;

        [Header("Mobile Runtime Budget")]
        [SerializeField, Min(0.02f)]
        private float m_PassiveControlRefreshSeconds = 0.1f;
        [SerializeField, Min(0.05f)]
        private float m_MenuPanelRefreshSeconds = 0.2f;
        [SerializeField, Min(0.05f)]
        private float m_CrosshairScanSeconds = 0.1f;
        [SerializeField, Min(0.2f)]
        private float m_CrosshairDiscoverySeconds = 1f;
        [SerializeField, Min(0.1f)]
        private float m_HierarchyFallbackRefreshSeconds = 0.5f;

        [Header("Throwable TPS Camera")]
        [Tooltip(
            "Additive horizontal shoulder offset applied to the native GC2 Third Person " +
            "Camera Shot while a throwable fire button is held."
        )]
        [SerializeField, Range(-1.5f, 1.5f)]
        private float m_ThrowableAimShoulderOffset = 0.3f;
        [Tooltip(
            "Additive GC2 Camera Shot radius while a throwable is held. Negative values " +
            "move the camera closer without changing the authored TPS radius."
        )]
        [SerializeField, Range(-2.5f, 0f)]
        private float m_ThrowableAimRadiusOffset = -1.15f;
        [Tooltip(
            "Critically damped smoothing time for both shoulder and radius. This avoids " +
            "the hard initial velocity of GC2's default one-shot Quad-Out Aim blend."
        )]
        [SerializeField, Min(0.01f)]
        private float m_ThrowableAimCameraSmoothTime = 0.14f;

        private FranklinShooterCatalog m_Catalog;
        private StateBasicLocomotion m_ShooterLocomotion;
        private AvatarMask m_ShooterUpperBodyMask;
        private AvatarMask m_BikeDriverShooterMask;
        private Character m_Player;
        private ShooterWeapon m_ActiveWeaponCache;
        private bool m_ActiveWeaponCacheValid;
        private ShooterStance m_ShooterStanceCache;
        private ShooterWeapon m_CatalogEntryWeaponCache;
        private FranklinShooterCatalog.Entry m_CatalogEntryCache;
        private Animator m_PlayerAnimatorCache;
        private CharacterIKSetter m_PlayerIkSetterCache;
        private bool m_PlayerIkSetterResolved;
        private Character m_HierarchyPlayerCache;
        private Transform m_HierarchyParentCache;
        private BikeEntry m_BikeDriverSeatCache;
        private FranklinBikePassengerSeat m_BikePassengerSeatCache;
        private CarEntry m_CarEntryCache;
        private float m_NextPlayerLookup;
        private int m_SelectedIndex = -1;
        private bool m_IsSwitching;
        private bool m_FireHeld;
        private bool m_TriggerPulled;
        private int m_FireRequestId;
        private bool m_Aiming;
        private bool m_WasSuppressingCameraRecoil;
        private bool m_RagdollInputSuppressed;
        private ShotSystemThirdPerson m_ThrowableAimCamera;
        private bool m_ThrowableAimCameraActive;
        private bool m_ThrowableAimCameraRequested;
        private float m_ThrowableAimShoulderCurrent;
        private float m_ThrowableAimShoulderVelocity;
        private float m_ThrowableAimRadiusCurrent;
        private float m_ThrowableAimRadiusVelocity;
        private ShooterWeapon m_SingleRepeatWeapon;
        private int m_LastSingleRepeatShotFrame = int.MinValue;
        private bool m_CautiousWalk;
        private bool m_MovementFirstPersonPreferred;
        private bool m_ShooterFirstPersonPreferred;
        private bool m_FirstPersonUsesShooterPreference;
        private bool m_HudTapBound;
        private float m_NextHudTapLookup;
        private float m_NextControlLookup;
        private float m_NextPassiveControlRefresh;
        private float m_NextMenuPanelRefresh;
        private float m_NextHierarchyFallbackRefresh;
        private float m_NextCrosshairScan;
        private float m_CrosshairScanUntil;
        private bool m_MeleeSuppressed;
        private bool m_MeleeFightWasActive;
        private bool m_SidestepVisibilityWasEnabled;
        private bool m_OwnsIdleVariationSuppression;
        private bool m_OwnsFireMovementSuppression;
        private bool m_OwnsObjectDirection;
        private float m_ObjectDirectionReturnAt = -1f;
        private ShooterWeapon m_ObservedShotWeapon;
        private int m_LastObservedShotFrame = int.MinValue;
        private BikeSeatRole m_BikeSeatRole;
        private Component m_ActiveBikeSeat;
        private FranklinBikeMainShotAim m_BikeCameraAim;
        private bool m_BikeCameraAimResolved;
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
        private bool m_BikeStuntActive;
        private Character m_BikeStuntSuspendedPlayer;
        private ShooterWeapon m_BikeStuntSuspendedWeapon;
        private GameObject m_BikeStuntSuspendedProp;
        private int m_BikeStuntSuspendedWeaponIndex = -1;
        private int m_BikeStuntTransitionVersion;
        private Task m_BikeStuntTask = Task.CompletedTask;
        private bool m_PhoneUseActive;
        private int m_PhoneSuspendedWeaponIndex = -1;
        private Character m_PhoneSuspendedPlayer;
        private ShooterWeapon m_PhoneSuspendedWeapon;
        private GameObject m_PhoneSuspendedProp;
        private int m_PhoneTransitionVersion;
        private Task m_PhoneSuspendTask = Task.CompletedTask;

        private Canvas m_Canvas;
        private GameObject m_MenuRoot;
        private GameObject m_MenuNavigationRoot;
        private GameObject m_ControlsRoot;
        private GameObject m_HudTapTarget;
        private GameObject m_ReloadIndicatorRoot;
        private Transform m_OnFootControls;
        private GameObject m_MeleeFight;
        private GameObject m_MeleeSidestepLeft;
        private GameObject m_MeleeSidestepRight;
        private Behaviour m_SidestepVisibility;
        private FranklinMeleeController m_MeleeController;
        private FranklinAnimationBridge m_AnimationBridge;
        private FranklinObjectDirectionToggle m_ObjectDirectionToggle;
        private FranklinShooterFirstPersonCamera m_FirstPersonCamera;
        private FranklinThrowableAimPose m_ThrowableAimPose;
        private GameObject m_FirstPersonControlRoot;
        private FranklinShooterTouchButton m_FirstPersonButton;
        private Image m_SelectedIcon;
        private Image m_MeleeSwitchImage;
        private FranklinShooterTouchButton m_CautiousWalkButton;
        private RectTransform m_FireButtonRect;
        private RectTransform m_ReloadButtonRect;
        private Text m_SelectedName;
        private Text m_SelectedCategory;
        private Text m_SelectedAmmo;
        private Text m_Hint;
        private Text m_MenuPageText;
        private Text m_MenuPrevLabel;
        private Text m_MenuNextLabel;
        private Button m_MenuPrevButton;
        private Button m_MenuNextButton;
        private int m_MenuPageIndex;
        private int m_MenuPageCount = 1;
        private Image m_ReloadProgressImage;
        private Texture2D m_ReloadRingTexture;
        private Sprite m_ReloadRingSprite;
        private int m_RenderedSelectionIndex = int.MinValue;
        private int m_RenderedAmmoInMagazine = int.MinValue;
        private int m_RenderedAmmoTotal = int.MinValue;
        private bool m_RenderedAmmoAvailable;
        private Args m_SelectedAmmoArgs;
        private readonly List<GameObject> m_SlotRoots = new();
        private readonly List<int> m_SlotPages = new();
        private readonly List<Graphic> m_SlotFrames = new();
        private readonly List<Image> m_SlotIcons = new();
        private readonly List<Button> m_SlotButtons = new();
        // Keep the actual UI wrappers rather than monotonically increasing instance IDs.
        // GC2 destroys these objects on every unequip, so dead entries can be pruned and do
        // not turn repeated weapon switching into a process-lifetime collection.
        private readonly HashSet<CrosshairUI> m_CompactedCrosshairs = new();
        private readonly HashSet<int> m_InitializedReserveWeapons = new();

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
        /// Temporarily unequips the rider's exact Shooter weapon while Wheelie or
        /// Burnout owns both hands. Releasing the stunt restores the cached weapon
        /// and prop instead of creating a replacement instance.
        /// </summary>
        public static void SetBikeStuntWeaponSuppressed(
            Character character,
            bool active)
        {
            s_Instance?.SetBikeStuntWeaponSuppressedInternal(character, active);
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
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_Instance = null;
            s_BikeDriverAnimationMask = null;
            s_PhoneUseActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSpawned();
        }

        private static void OnSceneLoaded(
            Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureSpawned();
        }

        private static void EnsureSpawned()
        {
            if (FranklinMenuRuntimeGate.IsMenuSceneActive)
            {
                if (s_Instance != null)
                {
                    GameObject target = s_Instance.gameObject;
                    target.SetActive(false);
                    Destroy(target);
                }
                return;
            }

            FranklinShooterSystem existing = FindFirstObjectByType<FranklinShooterSystem>(
                FindObjectsInactive.Include
            );
            if (existing != null)
            {
                s_Instance = existing;
                existing.gameObject.SetActive(true);
                return;
            }

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
            this.m_FirstPersonCamera =
                this.GetComponent<FranklinShooterFirstPersonCamera>() ??
                this.gameObject.AddComponent<FranklinShooterFirstPersonCamera>();
            this.m_ThrowableAimPose =
                this.GetComponent<FranklinThrowableAimPose>() ??
                this.gameObject.AddComponent<FranklinThrowableAimPose>();
            this.m_MovementFirstPersonPreferred = PlayerPrefs.GetInt(
                MOVEMENT_FIRST_PERSON_PREFERENCE_KEY,
                0
            ) != 0;
            this.m_ShooterFirstPersonPreferred = PlayerPrefs.GetInt(
                SHOOTER_FIRST_PERSON_PREFERENCE_KEY,
                0
            ) != 0;
            this.m_Catalog = Resources.Load<FranklinShooterCatalog>(CATALOG_RESOURCE);
            this.m_ShooterLocomotion =
                Resources.Load<StateBasicLocomotion>(SHOOTER_LOCOMOTION_RESOURCE);
            this.m_ShooterUpperBodyMask =
                Resources.Load<AvatarMask>(SHOOTER_UPPER_BODY_MASK_RESOURCE);
            this.m_BikeDriverShooterMask =
                Resources.Load<AvatarMask>(BIKE_DRIVER_SHOOTER_MASK_RESOURCE);
            if (this.m_ShooterLocomotion == null)
            {
                Debug.LogError(
                    $"Shooter_Locomotion is missing at Resources/{SHOOTER_LOCOMOTION_RESOURCE}. " +
                    "Run Tools/Franklin Game/Shooter System GC2/Install or Repair.",
                    this
                );
            }
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

        private void OnValidate()
        {
            this.m_ThrowableAimShoulderOffset = Mathf.Clamp(
                this.m_ThrowableAimShoulderOffset,
                -1.5f,
                1.5f
            );
            this.m_ThrowableAimRadiusOffset = Mathf.Clamp(
                this.m_ThrowableAimRadiusOffset,
                -2.5f,
                0f
            );
            this.m_ThrowableAimCameraSmoothTime = Mathf.Max(
                0.01f,
                this.m_ThrowableAimCameraSmoothTime
            );

            // Runtime Inspector tuning continues through the same damped path instead of
            // restarting GC2's one-shot easing and introducing a visible velocity step.
            if (!Application.isPlaying || !this.m_ThrowableAimCameraActive ||
                this.m_ThrowableAimCamera == null)
            {
                return;
            }

            this.RefreshThrowableTpsCameraBlend();
        }

        private void OnDestroy()
        {
            this.ResetThrowableTpsCameraAim(true);
            FranklinMobileHud.ReleaseControlsSuppression(this);
            this.SetMeleeControlsSuppressed(false);
            this.UnbindPlayer();
            if (this.m_ReloadRingSprite != null) Destroy(this.m_ReloadRingSprite);
            if (this.m_ReloadRingTexture != null) Destroy(this.m_ReloadRingTexture);
            if (s_Instance == this) s_Instance = null;
        }

        private async void Update()
        {
            this.SuppressCameraRecoilDuringFireHold();

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
            this.RefreshRagdollInputSuppression();
            this.RefreshThrowableTpsCameraBlend();

            // GC2 states may be stopped by another transition while the fire button remains
            // held. Re-enter the exact Shooter_Locomotion as soon as layer 7 becomes free.
            // The seated vehicle state remains authoritative while riding.
            ShooterWeapon heldWeapon = this.GetActiveWeapon();
            if (!this.IsPlayerRagdolledOrDead() &&
                this.GetCatalogEntry(heldWeapon)?.IsThrowable != true &&
                (this.m_BikeSeatRole == BikeSeatRole.None ||
                 this.m_BikeSeatRole == BikeSeatRole.Driver) &&
                (this.m_Aiming || this.m_FireHeld || this.m_TriggerPulled) &&
                this.m_Player != null &&
                this.m_ShooterLocomotion != null &&
                this.m_Player.States.IsAvailable(SHOOTER_LOCOMOTION_LAYER))
            {
                this.SetShooterLocomotionActive(true);
            }

            this.RefreshBikeReloadState();
            this.RefreshReloadIndicator();
            this.RefreshHeldSingleFire();
            this.RefreshControlModeBudgeted();
            this.TrackShotActivityAndDirectionTimeout();

            float unscaledNow = Time.unscaledTime;
            if (this.m_Aiming &&
                unscaledNow <= this.m_CrosshairScanUntil &&
                unscaledNow >= this.m_NextCrosshairScan)
            {
                this.m_NextCrosshairScan =
                    unscaledNow + Mathf.Max(0.05f, this.m_CrosshairScanSeconds);
                if (this.CompactActiveCrosshairs())
                    this.m_CrosshairScanUntil = 0f;
            }

            if (Keyboard.current != null)
            {
                if (!this.IsPlayerRagdolledOrDead() &&
                    !this.m_PhoneUseActive &&
                    Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    if (this.IsWeaponMenuOpen) this.CloseWeaponMenu();
                    else this.OpenWeaponMenu();
                }

                if (Keyboard.current.escapeKey.wasPressedThisFrame && this.IsWeaponMenuOpen)
                    this.CloseWeaponMenu();

                if (!this.IsPlayerRagdolledOrDead() &&
                    !this.m_PhoneUseActive &&
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

            if (this.IsWeaponMenuOpen && unscaledNow >= this.m_NextMenuPanelRefresh)
            {
                this.m_NextMenuPanelRefresh =
                    unscaledNow + Mathf.Max(0.05f, this.m_MenuPanelRefreshSeconds);
                this.RefreshSelectedPanel();
            }

        }

        private void LateUpdate()
        {
            // ShooterStance can author recoil after the active ShotCamera has already
            // updated. Clear that pending value in the same frame so it cannot surface
            // as a delayed kick on the next touch frame.
            this.SuppressCameraRecoilDuringFireHold();
        }

        private void SuppressCameraRecoilDuringFireHold()
        {
            bool suppress = this.m_Player != null &&
                            (this.m_FireHeld || this.m_TriggerPulled);

            // GC2 Shooter may schedule a shot after Camera Shot has updated for the
            // current frame. Flush once more after Fire ends so that recoil authored
            // during the last held frame cannot appear one frame late.
            bool shouldFlush = suppress || this.m_WasSuppressingCameraRecoil;
            this.m_WasSuppressingCameraRecoil = suppress;
            if (!shouldFlush) return;

            MainCamera mainCamera = ShortcutMainCamera.Get<MainCamera>();
            ShotCamera shot = mainCamera?.Transition.CurrentShotCamera;

            // Use GC2's public recoil API. This only neutralizes Camera Shot recoil;
            // Shooter's HumanRecoil continues to animate the hands, arms and weapon.
            shot?.ShotType?.Recoil?.Run(0f, Vector2.zero);
        }

        public void OpenWeaponMenu()
        {
            if (this.m_MenuRoot == null || this.m_Catalog == null ||
                this.m_PhoneUseActive || this.m_BikeStuntActive ||
                this.IsPlayerRagdolledOrDead()) return;
            if ((this.m_FireHeld || this.m_TriggerPulled) && this.m_Player != null)
            {
                ShooterWeapon active = this.GetActiveWeapon();
                if (active != null)
                {
                    ShooterStance stance = this.GetShooterStance();
                    this.CancelFireRequest(active, stance, true);
                }
                else
                {
                    this.InvalidateFireRequest();
                }
            }
            this.RefreshSelectedIndex();
            this.SetWeaponMenuPage(
                this.GetWeaponMenuPage(this.m_SelectedIndex),
                false
            );
            this.RefreshMenuSelection();
            this.m_NextMenuPanelRefresh =
                Time.unscaledTime + Mathf.Max(0.05f, this.m_MenuPanelRefreshSeconds);
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

        private void ShowPreviousWeaponMenuPage()
        {
            this.SetWeaponMenuPage(this.m_MenuPageIndex - 1, true);
        }

        private void ShowNextWeaponMenuPage()
        {
            this.SetWeaponMenuPage(this.m_MenuPageIndex + 1, true);
        }

        private int GetWeaponMenuPage(int catalogIndex)
        {
            if (this.m_MenuPageCount <= 1) return 0;
            if (catalogIndex >= 0 && catalogIndex < this.m_SlotPages.Count)
            {
                return this.m_SlotPages[catalogIndex];
            }

            return this.m_Catalog?.Get(catalogIndex)?.IsThrowable == true
                ? WEAPON_MENU_THROWABLE_PAGE
                : 0;
        }

        private void SetWeaponMenuPage(int page, bool refreshSelection)
        {
            int pageCount = Mathf.Max(1, this.m_MenuPageCount);
            int nextPage = Mathf.Clamp(page, 0, pageCount - 1);
            bool changed = this.m_MenuPageIndex != nextPage;
            this.m_MenuPageIndex = nextPage;

            for (int i = 0; i < this.m_SlotRoots.Count; ++i)
            {
                bool visible = i >= this.m_SlotPages.Count ||
                               this.m_SlotPages[i] == nextPage;
                if (this.m_SlotRoots[i].activeSelf != visible)
                {
                    this.m_SlotRoots[i].SetActive(visible);
                }
            }

            bool hasMultiplePages = pageCount > 1;
            if (this.m_MenuNavigationRoot != null &&
                this.m_MenuNavigationRoot.activeSelf != hasMultiplePages)
            {
                this.m_MenuNavigationRoot.SetActive(hasMultiplePages);
            }

            if (this.m_MenuPrevButton != null)
            {
                this.m_MenuPrevButton.interactable = nextPage > 0;
            }
            if (this.m_MenuNextButton != null)
            {
                this.m_MenuNextButton.interactable = nextPage < pageCount - 1;
            }
            if (this.m_MenuPrevLabel != null)
            {
                this.m_MenuPrevLabel.color = nextPage > 0
                    ? OFF_WHITE
                    : new Color(OFF_WHITE.r, OFF_WHITE.g, OFF_WHITE.b, 0.28f);
            }
            if (this.m_MenuNextLabel != null)
            {
                this.m_MenuNextLabel.color = nextPage < pageCount - 1
                    ? OFF_WHITE
                    : new Color(OFF_WHITE.r, OFF_WHITE.g, OFF_WHITE.b, 0.28f);
            }
            if (this.m_MenuPageText != null)
            {
                string value = (nextPage + 1) + " / " + pageCount;
                if (this.m_MenuPageText.text != value) this.m_MenuPageText.text = value;
            }

            if (refreshSelection && changed) this.RefreshMenuSelection();
        }

        public async void SelectWeaponById(string weaponId)
        {
            if (string.IsNullOrWhiteSpace(weaponId) || this.m_Catalog == null) return;
            for (int i = 0; i < this.m_Catalog.Count; ++i)
            {
                FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(i);
                if (entry == null || !string.Equals(
                        entry.Id,
                        weaponId,
                        StringComparison.OrdinalIgnoreCase
                    )) continue;

                await this.SelectWeapon(i);
                return;
            }
        }

        public int GetRemainingAmmo(string weaponId)
        {
            if (this.m_Player == null || this.m_Catalog == null ||
                string.IsNullOrWhiteSpace(weaponId)) return 0;

            for (int i = 0; i < this.m_Catalog.Count; ++i)
            {
                FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(i);
                if (entry?.Weapon == null || !string.Equals(
                        entry.Id,
                        weaponId,
                        StringComparison.OrdinalIgnoreCase
                    )) continue;

                if (this.m_Player.Combat.RequestMunition(entry.Weapon) is not
                    ShooterMunition munition) return 0;

                int weaponHash = entry.Weapon.Id.Hash;
                return munition.Total <= 0 &&
                       !this.m_InitializedReserveWeapons.Contains(weaponHash)
                    ? entry.StartingMagazine
                    : Mathf.Max(0, munition.Total);
            }

            return 0;
        }

        public void SetTouchAction(FranklinShooterTouchButton.Action action, bool active)
        {
            if (this.m_Player == null || this.IsWeaponMenuOpen ||
                this.m_PhoneUseActive || this.IsPlayerRagdolledOrDead()) return;
            if (action == FranklinShooterTouchButton.Action.FirstPersonCamera)
            {
                if (active)
                {
                    bool shooterPreference = this.ResolveFirstPersonPreferenceMode();
                    bool preferred = shooterPreference
                        ? !this.m_ShooterFirstPersonPreferred
                        : !this.m_MovementFirstPersonPreferred;
                    this.SetFirstPersonPreference(shooterPreference, preferred);
                    this.ApplyFirstPersonPreference();
                    this.RefreshFirstPersonControl();
                }
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;
            if (!this.CanUseWeaponInCurrentSeat(weapon)) return;
            if (this.m_BikeSeatRole != BikeSeatRole.None &&
                (action == FranklinShooterTouchButton.Action.CautiousWalk ||
                 action == FranklinShooterTouchButton.Action.Reload &&
                 this.m_BikeSeatRole == BikeSeatRole.Driver))
            {
                return;
            }

            ShooterStance stance = this.GetShooterStance();
            switch (action)
            {
                case FranklinShooterTouchButton.Action.Fire:
                    if (active)
                    {
                        if (this.m_FireHeld) break;
                        this.m_FireHeld = true;
                        this.m_TriggerPulled = false;

                        // Throwable camera framing belongs to the physical hold itself,
                        // not the delayed charge/throw animation. This keeps the mobile
                        // response immediate even when GC2 first needs to reload.
                        this.SetThrowableTpsCameraAim(
                            this.GetCatalogEntry(weapon)?.IsThrowable == true
                        );

                        // PointerDown owns GC2 Object Direction immediately. Do not wait
                        // for an empty throwable magazine to reload or for the authored
                        // Sight pose to finish blending before turning the Character.
                        // Vehicle seats keep their own facing authority.
                        this.SetObjectDirectionForShooting(
                            this.m_BikeSeatRole == BikeSeatRole.None
                        );

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

        public void SetBikeFirstPersonOrbitSuppressed(bool suppressed)
        {
            this.m_FirstPersonCamera?.SetOrbitSuppressed(suppressed);
            if (this.m_BikeSeatRole == BikeSeatRole.None) return;
            this.ResolveBikeCameraAim()?.SetFirstPersonOrbitSuppressed(suppressed);
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
            this.m_ActiveWeaponCacheValid = false;
            this.m_ShooterStanceCache = null;
            this.m_CatalogEntryWeaponCache = null;
            this.m_CatalogEntryCache = null;
            this.m_PlayerAnimatorCache = this.m_Player.Animim?.Animator ??
                                         this.m_Player.GetComponentInChildren<Animator>(true);
            this.m_PlayerIkSetterCache = this.m_PlayerAnimatorCache != null
                ? this.m_PlayerAnimatorCache.GetComponent<CharacterIKSetter>()
                : null;
            // BikeEntry creates CharacterIKSetter only after Player mounts the seat.
            // A missing pre-seat component is therefore not a final lookup result: keep
            // retrying when Bike hand arbitration becomes active so the handlebar IK can
            // release the weapon hand to GC2's per-weapon Sight state.
            this.m_PlayerIkSetterResolved = this.m_PlayerIkSetterCache != null;
            this.RefreshPlayerHierarchyCache(true);
            this.m_FirstPersonCamera?.Initialize(this.m_Player);
            this.m_ThrowableAimPose?.Initialize(this.m_Player);
            this.m_MeleeController =
                this.m_Player.GetComponent<FranklinMeleeController>();
            this.m_AnimationBridge = this.m_Player.GetComponentInChildren<FranklinAnimationBridge>(true);
            this.m_BikeCameraAim =
                this.m_Player.GetComponentInChildren<FranklinBikeMainShotAim>(true);
            this.m_BikeCameraAimResolved = true;
            this.m_Player.Combat.EventEquip += this.OnWeaponChanged;
            this.m_Player.Combat.EventUnequip += this.OnWeaponChanged;
            this.RefreshSelectedIndex();
            if (this.GetActiveWeapon() != null)
            {
                this.CancelMeleeForShooterEquip();
            }

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
            this.ResetThrowableTpsCameraAim(true);
            this.m_ThrowableAimPose?.Initialize(null);
            this.m_FirstPersonCamera?.Initialize(null);
            ShooterWeapon activeWeapon = this.GetActiveWeapon();
            if (this.m_Player != null && activeWeapon != null)
            {
                ShooterStance stance = this.GetShooterStance();
                this.CancelFireRequest(activeWeapon, stance, true);
            }
            else
            {
                this.InvalidateFireRequest();
            }

            this.SetCautiousWalk(false, true);
            this.SetShooterLocomotionActive(false);
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
            this.ClearBikeStuntSuspendedWeapon(true);
            this.m_Player = null;
            this.m_ActiveWeaponCache = null;
            this.m_ActiveWeaponCacheValid = false;
            this.m_ShooterStanceCache = null;
            this.m_CatalogEntryWeaponCache = null;
            this.m_CatalogEntryCache = null;
            this.m_CompactedCrosshairs.Clear();
            this.m_InitializedReserveWeapons.Clear();
            this.m_PlayerAnimatorCache = null;
            this.m_PlayerIkSetterCache = null;
            this.m_PlayerIkSetterResolved = false;
            this.ClearPlayerHierarchyCache();
            this.m_MeleeController = null;
            this.m_AnimationBridge = null;
            this.m_ObjectDirectionToggle = null;
            this.m_BikeCameraAimResolved = false;
            ++this.m_PhoneTransitionVersion;
            ++this.m_BikeWeaponTransitionVersion;
            ++this.m_BikeStuntTransitionVersion;
            this.m_BikeStuntActive = false;
            this.m_RagdollInputSuppressed = false;
            this.m_PhoneSuspendedWeaponIndex = -1;
            this.m_PhoneSuspendTask = Task.CompletedTask;
            this.m_BikeWeaponTask = Task.CompletedTask;
            this.m_BikeStuntTask = Task.CompletedTask;
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
                this.m_PhoneUseActive || this.m_BikeStuntActive ||
                this.IsPlayerRagdolledOrDead()) return false;
            FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(index);
            if (entry?.Weapon == null || entry.PropPrefab == null) return false;
            // A depleted grenade slot cannot equip a fresh hand prop. Ammo pickups can
            // make the slot selectable again through the existing munition API.
            if (entry.IsThrowable && this.GetRemainingAmmo(entry.Id) <= 0)
                return false;
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
            this.CancelMeleeForShooterEquip();
            this.RefreshControlMode();
            try
            {
                ShooterWeapon active = this.GetActiveWeapon();
                if (active != null && (this.m_FireHeld || this.m_TriggerPulled))
                {
                    ShooterStance activeStance = this.GetShooterStance();
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
                    this.InvalidateActiveWeaponCache();
                    await this.m_Player.Combat.Unequip(active, new Args(this.m_Player.gameObject));
                    if (oldProp != null) this.m_Player.Props.RemoveInstance(oldProp);
                }

                GameObject prop = new(entry.DisplayName + " Weapon Gameplay Root")
                {
                    layer = entry.PropPrefab.layer
                };
                prop.transform.localScale = Vector3.Scale(
                    entry.PropPrefab.transform.localScale,
                    entry.LocalScale
                );
                FranklinWeaponModelPose modelPose =
                    prop.AddComponent<FranklinWeaponModelPose>();
                modelPose.Initialize(
                    entry.PropPrefab,
                    entry.ModelLocalPosition,
                    entry.ModelLocalRotation,
                    entry.ModelLocalScale
                );
                this.m_Player.Props.AttachInstance(
                    new Bone(HumanBodyBones.RightHand), prop,
                    entry.LocalPosition, entry.LocalRotation
                );
                this.InvalidateActiveWeaponCache();
                await this.m_Player.Combat.Equip(
                    entry.Weapon,
                    prop,
                    new Args(this.m_Player.gameObject, prop)
                );
                modelPose.BindAnimator();

                if (initializeEmptyMagazine &&
                    this.m_Player.Combat.RequestMunition(entry.Weapon) is ShooterMunition munition &&
                    munition.InMagazine <= 0)
                {
                    Args args = new(this.m_Player.gameObject, prop);
                    bool hasMagazine = entry.Weapon.Magazine.GetHasMagazine(args);
                    if (!hasMagazine)
                    {
                        int weaponHash = entry.Weapon.Id.Hash;
                        if (entry.StartingMagazine > 0 &&
                            this.m_InitializedReserveWeapons.Add(weaponHash) &&
                            munition.Total <= 0)
                        {
                            munition.Total = entry.StartingMagazine;
                        }
                    }

                    int capacity = entry.Weapon.Magazine.GetMagazineSize(args);
                    int available = entry.Weapon.Magazine.GetTotalAmmo(args);
                    if (hasMagazine)
                    {
                        munition.InMagazine = Mathf.Min(
                            entry.StartingMagazine > 0 ? entry.StartingMagazine : capacity,
                            capacity,
                            available
                        );
                    }
                }

                if (string.Equals(entry.Id, "rpg7", StringComparison.OrdinalIgnoreCase))
                {
                    FranklinRpgLoadedRocketVisual loadedRocket =
                        prop.GetComponent<FranklinRpgLoadedRocketVisual>();
                    if (loadedRocket == null)
                        loadedRocket = prop.AddComponent<FranklinRpgLoadedRocketVisual>();
                    loadedRocket.Initialize(this.m_Player, entry.Weapon, modelPose);
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
                if (!active &&
                    !this.m_BikeStuntActive &&
                    this.m_BikeStuntSuspendedWeapon != null)
                {
                    this.QueueBikeStuntWeaponRestore(
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
                if (!this.m_BikeStuntActive &&
                    this.m_BikeStuntSuspendedWeapon != null)
                {
                    this.QueueBikeStuntWeaponRestore(
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
                this.InvalidateActiveWeaponCache();
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
            this.CancelMeleeForShooterEquip();
            this.RefreshControlMode();
            bool restored = false;
            try
            {
                prop.SetActive(true);
                this.InvalidateActiveWeaponCache();
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

        private void SetBikeStuntWeaponSuppressedInternal(
            Character character,
            bool active)
        {
            if (character == null || character != this.m_Player) return;

            if (this.m_BikeStuntActive == active)
            {
                if (!active && this.m_BikeStuntSuspendedWeapon != null)
                    this.QueueBikeStuntWeaponRestore(character, null);
                return;
            }

            this.m_BikeStuntActive = active;
            int version = ++this.m_BikeStuntTransitionVersion;
            Task pending = this.m_BikeStuntTask;
            this.m_BikeStuntTask = active
                ? this.SuspendWeaponForBikeStunt(character, version, pending)
                : this.RestoreWeaponAfterBikeStunt(
                    character,
                    version,
                    pending,
                    null
                );
            this.RefreshControlMode();
        }

        private async Task SuspendWeaponForBikeStunt(
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

            while (this.m_IsSwitching && this.m_BikeStuntActive && this != null)
                await Task.Yield();
            if (this == null ||
                version != this.m_BikeStuntTransitionVersion ||
                !this.m_BikeStuntActive ||
                character == null ||
                character != this.m_Player ||
                this.m_BikeStuntSuspendedWeapon != null)
            {
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;

            ShooterStance stance = character.Combat.RequestStance<ShooterStance>();
            this.CancelFireRequest(weapon, stance, true);
            this.SetCautiousWalk(false, true);
            this.SetObjectDirectionForShooting(false);
            this.SetBikeShooterAim(false);

            GameObject prop = character.Combat.GetProp(weapon);
            this.m_BikeStuntSuspendedPlayer = character;
            this.m_BikeStuntSuspendedWeapon = weapon;
            this.m_BikeStuntSuspendedProp = prop;
            this.m_BikeStuntSuspendedWeaponIndex =
                this.m_Catalog?.IndexOf(weapon) ?? -1;
            if (prop != null) prop.SetActive(false);

            this.m_IsSwitching = true;
            bool unequipped = false;
            try
            {
                this.InvalidateActiveWeaponCache();
                await character.Combat.Unequip(
                    weapon,
                    new Args(character.gameObject)
                );
                unequipped = !character.Combat.IsEquipped(weapon);
            }
            catch (Exception exception)
            {
                unequipped = !character.Combat.IsEquipped(weapon);
                Debug.LogError(
                    $"Could not temporarily unequip Bike stunt weapon: {exception}",
                    this
                );
            }
            finally
            {
                this.m_IsSwitching = false;
                if (!unequipped)
                {
                    if (prop != null) prop.SetActive(true);
                    this.ClearBikeStuntSuspendedWeapon(false);
                }
                this.m_Aiming = false;
                this.RefreshMenuSelection();
                this.RefreshControlMode();
            }
        }

        private void QueueBikeStuntWeaponRestore(
            Character character,
            Task prerequisite)
        {
            if (character == null || character != this.m_Player) return;

            int version = ++this.m_BikeStuntTransitionVersion;
            Task pending = this.m_BikeStuntTask;
            this.m_BikeStuntTask = this.RestoreWeaponAfterBikeStunt(
                character,
                version,
                pending,
                prerequisite
            );
        }

        private async Task RestoreWeaponAfterBikeStunt(
            Character character,
            int version,
            Task pending,
            Task prerequisite)
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

            if (prerequisite != null)
            {
                try
                {
                    await prerequisite;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            while (this.m_IsSwitching && !this.m_BikeStuntActive && this != null)
                await Task.Yield();
            if (this == null ||
                version != this.m_BikeStuntTransitionVersion ||
                this.m_BikeStuntActive ||
                this.m_PhoneUseActive ||
                character == null ||
                character != this.m_Player ||
                this.m_BikeStuntSuspendedWeapon == null)
            {
                return;
            }

            if (this.GetActiveWeapon() != null)
            {
                // A newer explicit selection wins over the cached stunt weapon.
                this.ClearBikeStuntSuspendedWeapon(true);
                this.RefreshControlMode();
                return;
            }

            Character cachedPlayer = this.m_BikeStuntSuspendedPlayer;
            ShooterWeapon weapon = this.m_BikeStuntSuspendedWeapon;
            GameObject prop = this.m_BikeStuntSuspendedProp;
            int catalogIndex = this.m_BikeStuntSuspendedWeaponIndex;
            if (cachedPlayer == null || cachedPlayer != character || prop == null)
            {
                this.ClearBikeStuntSuspendedWeapon(true);
                if (catalogIndex >= 0)
                    await this.SelectWeapon(catalogIndex, false);
                return;
            }

            this.m_IsSwitching = true;
            this.CancelMeleeForShooterEquip();
            this.RefreshControlMode();
            bool restored = false;
            try
            {
                prop.SetActive(true);
                this.InvalidateActiveWeaponCache();
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
                    this.ClearBikeStuntSuspendedWeapon(false);
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
                    this.ClearBikeStuntSuspendedWeapon(false);
                }
                Debug.LogError(
                    $"Could not restore Bike stunt weapon: {exception}",
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

        private void ClearBikeStuntSuspendedWeapon(bool removeProp)
        {
            if (removeProp &&
                this.m_BikeStuntSuspendedPlayer != null &&
                this.m_BikeStuntSuspendedProp != null)
            {
                this.m_BikeStuntSuspendedPlayer.Props.RemoveInstance(
                    this.m_BikeStuntSuspendedProp
                );
            }

            this.m_BikeStuntSuspendedPlayer = null;
            this.m_BikeStuntSuspendedWeapon = null;
            this.m_BikeStuntSuspendedProp = null;
            this.m_BikeStuntSuspendedWeaponIndex = -1;
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

            ShooterStance stance = this.GetShooterStance();
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
                this.InvalidateActiveWeaponCache();
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

            if (character.IsDead)
            {
                this.m_BikeWeaponRestoreRequested = false;
                this.ClearBikeSuspendedWeapon(true);
                return;
            }

            // Bike entry can be interrupted by a Car/explosion before it takes full
            // ownership of Player control. Keep the cached prop hidden and let the
            // ragdoll state transition queue this same restore after recovery.
            if (character.Ragdoll.IsRagdoll) return;

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
            this.CancelMeleeForShooterEquip();
            this.RefreshControlMode();
            bool restored = false;
            try
            {
                prop.SetActive(true);
                this.InvalidateActiveWeaponCache();
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

            this.RefreshPlayerHierarchyCache();
            if (this.m_Player != null)
            {
                BikeEntry driverSeat = this.m_BikeDriverSeatCache;
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
                        this.m_BikePassengerSeatCache;
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
                    ShooterStance stance = this.GetShooterStance();
                    this.CancelFireRequest(active, stance, true);
                }

                // BikeEntry/passenger-seat owns cleanup during transitions. Do not restore
                // an old handle target after that owner has started releasing the rider.
                this.ClearBikeHandIk(false);
                this.SetBikeShooterAim(false);
                this.SetBikeReloadSteeringLocked(false);
                this.m_BikeSeatRole = nextRole;
                this.m_ActiveBikeSeat = nextSeat;
                if (nextRole != BikeSeatRole.None && this.m_PlayerIkSetterCache == null)
                    this.m_PlayerIkSetterResolved = false;
                this.m_BikeReloadDriver = nextRole == BikeSeatRole.Driver
                    ? nextSeat?.GetComponentInParent<FranklinArcadeBikeDriver>()
                    : null;
                this.SetBikeShooterAim(this.m_Aiming);
                this.SetCautiousWalk(false, true);
                this.SetObjectDirectionForShooting(false);
                this.ResetShotDirectionTracking();
                this.RefreshMenuSelection();
                this.RefreshBikeControlLayout();
            }

            if (this.m_BikeSeatRole == BikeSeatRole.None) return;

            this.SetObjectDirectionForShooting(false);
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

            CharacterIKSetter ikSetter = this.ResolvePlayerIkSetter();
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
            this.m_BikeCameraAimResolved = false;
            this.m_BikeReloadDriver = null;
            this.RefreshBikeControlLayout();
            this.RefreshMenuSelection();
        }

        private CharacterIKSetter ResolvePlayerIkSetter()
        {
            if (this.m_Player == null) return null;

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator != null && animator != this.m_PlayerAnimatorCache)
            {
                this.m_PlayerAnimatorCache = animator;
                this.m_PlayerIkSetterResolved = false;
                this.m_PlayerIkSetterCache = null;
            }

            if (!this.m_PlayerIkSetterResolved || this.m_PlayerIkSetterCache == null)
            {
                this.m_PlayerIkSetterCache = this.m_PlayerAnimatorCache != null
                    ? this.m_PlayerAnimatorCache.GetComponent<CharacterIKSetter>()
                    : null;
                this.m_PlayerIkSetterResolved = this.m_PlayerIkSetterCache != null;
            }

            return this.m_PlayerIkSetterCache;
        }

        private void RefreshPlayerHierarchyCache(bool force = false)
        {
            if (this.m_Player == null)
            {
                this.ClearPlayerHierarchyCache();
                return;
            }

            Transform parent = this.m_Player.transform.parent;
            float now = Time.unscaledTime;
            if (!force &&
                this.m_HierarchyPlayerCache == this.m_Player &&
                this.m_HierarchyParentCache == parent &&
                now < this.m_NextHierarchyFallbackRefresh)
            {
                return;
            }

            this.m_HierarchyPlayerCache = this.m_Player;
            this.m_HierarchyParentCache = parent;
            this.m_BikeDriverSeatCache = this.m_Player.GetComponentInParent<BikeEntry>();
            this.m_BikePassengerSeatCache =
                this.m_Player.GetComponentInParent<FranklinBikePassengerSeat>();
            this.m_CarEntryCache = this.m_Player.GetComponentInParent<CarEntry>();
            this.m_NextHierarchyFallbackRefresh =
                now + Mathf.Max(0.1f, this.m_HierarchyFallbackRefreshSeconds);
        }

        private void ClearPlayerHierarchyCache()
        {
            this.m_HierarchyPlayerCache = null;
            this.m_HierarchyParentCache = null;
            this.m_BikeDriverSeatCache = null;
            this.m_BikePassengerSeatCache = null;
            this.m_CarEntryCache = null;
            this.m_NextHierarchyFallbackRefresh = 0f;
        }

        private void SetBikeShooterAim(bool active)
        {
            bool seatedAim = active && this.m_BikeSeatRole != BikeSeatRole.None;
            this.ResolveBikeCameraAim()?.SetShooterAimActive(seatedAim);

            // Bike_01's authored rider fit adds a manual spine bend after GC2 evaluates
            // its weapon state. Suppress only that post-state offset during driver ADS/fire
            // so Pistol_Aim/AK_Aim remains authoritative; the seat still owns pelvis/legs.
            if (this.m_BikeSeatRole == BikeSeatRole.Driver &&
                this.m_ActiveBikeSeat is BikeEntry bikeEntry)
            {
                bikeEntry.SetShooterPoseActive(seatedAim);
            }
        }

        private FranklinBikeMainShotAim ResolveBikeCameraAim()
        {
            if (!this.m_BikeCameraAimResolved && this.m_Player != null)
            {
                this.m_BikeCameraAim = this.m_Player
                    .GetComponentInChildren<FranklinBikeMainShotAim>(true);
                this.m_BikeCameraAimResolved = true;
            }

            return this.m_BikeCameraAim;
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

            ShooterStance stance = this.GetShooterStance();
            bool isReloading = stance.Reloading.IsReloading &&
                               stance.Reloading.WeaponReloading == weapon;
            this.SetBikeReloadSteeringLocked(isReloading);
        }

        private void RefreshReloadIndicator()
        {
            if (this.m_ReloadIndicatorRoot == null) return;

            if (this.IsPlayerRagdolledOrDead())
            {
                if (this.m_ReloadIndicatorRoot.activeSelf)
                    this.m_ReloadIndicatorRoot.SetActive(false);
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            ShooterStance stance = this.GetShooterStance();
            bool isReloading = weapon != null &&
                               stance != null &&
                               stance.Reloading.IsReloading &&
                               stance.Reloading.WeaponReloading == weapon;

            if (this.m_ReloadIndicatorRoot.activeSelf != isReloading)
                this.m_ReloadIndicatorRoot.SetActive(isReloading);
            if (!isReloading) return;

            float progress = Mathf.Clamp01(stance.Reloading.Ratio);
            if (this.m_ReloadProgressImage != null &&
                !Mathf.Approximately(this.m_ReloadProgressImage.fillAmount, progress))
            {
                this.m_ReloadProgressImage.fillAmount = progress;
            }
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
            if (weapon == this.m_CatalogEntryWeaponCache)
                return this.m_CatalogEntryCache;

            this.m_CatalogEntryWeaponCache = weapon;
            int index = this.m_Catalog?.IndexOf(weapon) ?? -1;
            this.m_CatalogEntryCache = this.m_Catalog?.Get(index);
            return this.m_CatalogEntryCache;
        }

        private bool IsRpgWeapon(ShooterWeapon weapon)
        {
            return string.Equals(
                this.GetCatalogEntry(weapon)?.Id,
                "rpg7",
                StringComparison.OrdinalIgnoreCase
            );
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
            this.SetFireMovementStatesSuppressed(true);
            this.SetObjectDirectionForShooting(this.m_BikeSeatRole == BikeSeatRole.None);
            if (!this.SetAimActive(weapon, stance, true))
            {
                if (requestId == this.m_FireRequestId)
                {
                    this.m_FireHeld = false;
                    this.SetThrowableTpsCameraAim(false);
                    this.SetFireMovementStatesSuppressed(false);
                }
                return;
            }

            WeaponData initialData = stance.Get(weapon);
            if (initialData == null)
            {
                if (requestId == this.m_FireRequestId)
                {
                    this.m_FireHeld = false;
                    this.SetThrowableTpsCameraAim(false);
                    this.SetFireMovementStatesSuppressed(false);
                }
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
                {
                    if (requestId == this.m_FireRequestId)
                    {
                        this.SetThrowableTpsCameraAim(false);
                        this.SetFireMovementStatesSuppressed(false);
                    }
                    return;
                }
                await Task.Yield();
            }

            // Require one fully evaluated frame after the 0.25 second GC2 sight blend.
            await Task.Yield();
            if (!this.IsFireRequestValid(weapon, stance, firingSightId, requestId))
            {
                if (requestId == this.m_FireRequestId)
                {
                    this.SetThrowableTpsCameraAim(false);
                    this.SetFireMovementStatesSuppressed(false);
                }
                return;
            }

            if (this.IsRpgWeapon(weapon))
            {
                float nextRocketTime =
                    initialData.LastShotTime + RPG_FIRE_INTERVAL_SECONDS;
                while (initialData.Character.Time.Time < nextRocketTime)
                {
                    if (!this.IsFireRequestValid(
                            weapon,
                            stance,
                            firingSightId,
                            requestId))
                    {
                        if (requestId == this.m_FireRequestId)
                        {
                            this.SetThrowableTpsCameraAim(false);
                            this.SetFireMovementStatesSuppressed(false);
                        }
                        return;
                    }
                    await Task.Yield();
                }
            }

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

            ShooterStance stance = this.GetShooterStance();
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
                this.IsPlayerRagdolledOrDead() ||
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
            this.SetThrowableTpsCameraAim(false);
            this.ResetHeldSingleFire();
            if (this.m_TriggerPulled)
            {
                WeaponData data = stance.Get(weapon);
                int previousShotFrame = data?.LastShotFrame ?? int.MinValue;
                stance.ReleaseTrigger(weapon);
                this.m_TriggerPulled = false;
                if (data != null && data.LastShotFrame != previousShotFrame)
                    this.HandleDepletedThrowableAfterShot(weapon, stance, data);
            }

            if (exitAim) this.SetAimActive(weapon, stance, false);
            this.SetFireMovementStatesSuppressed(false);
        }

        private void HandleDepletedThrowableAfterShot(
            ShooterWeapon weapon,
            ShooterStance stance,
            WeaponData data)
        {
            if (this.GetCatalogEntry(weapon)?.IsThrowable != true ||
                weapon.Magazine.GetTotalAmmo(data.WeaponArgs) > 0 ||
                this.m_Player == null)
            {
                return;
            }

            // The projectile has already been spawned synchronously by ReleaseTrigger.
            // Hide the consumed hand prop immediately, then let GC2 finish the authored
            // throw gesture before returning to the project's default melee state.
            GameObject prop = this.m_Player.Combat.GetProp(weapon);
            if (prop != null) prop.SetActive(false);
            this.ReturnToMeleeAfterDepletedThrowable(weapon, stance, prop);
        }

        private async void ReturnToMeleeAfterDepletedThrowable(
            ShooterWeapon weapon,
            ShooterStance stance,
            GameObject hiddenProp)
        {
            await Task.Yield();
            while (this != null &&
                   this.GetActiveWeapon() == weapon &&
                   stance.Shooting.IsShootingAnimation)
            {
                await Task.Yield();
            }

            if (this == null || this.m_Player == null ||
                this.GetActiveWeapon() != weapon)
            {
                return;
            }

            WeaponData data = stance.Get(weapon);
            if (data != null && weapon.Magazine.GetTotalAmmo(data.WeaponArgs) > 0)
            {
                if (hiddenProp != null) hiddenProp.SetActive(true);
                return;
            }

            this.SwitchToMelee();
        }

        private void InvalidateFireRequest()
        {
            ++this.m_FireRequestId;
            this.m_FireHeld = false;
            this.m_TriggerPulled = false;
            this.SetThrowableTpsCameraAim(false);
            this.ResetHeldSingleFire();
            this.SetFireMovementStatesSuppressed(false);
        }

        private bool IsPlayerRagdolledOrDead()
        {
            return this.m_Player != null &&
                   (this.m_Player.IsDead || this.m_Player.Ragdoll.IsRagdoll);
        }

        private void RefreshRagdollInputSuppression()
        {
            bool suppress = this.IsPlayerRagdolledOrDead();
            if (suppress == this.m_RagdollInputSuppressed) return;

            this.m_RagdollInputSuppressed = suppress;
            if (!suppress)
            {
                if (this.m_HudTapTarget != null && !this.IsWeaponMenuOpen)
                    this.m_HudTapTarget.SetActive(true);
                if (this.m_BikeWeaponRestoreRequested &&
                    this.m_BikeSuspendedWeapon != null)
                {
                    this.QueueBikeDriverWeaponRestore(this.m_Player, null);
                }
                this.RefreshControlMode();
                return;
            }

            ShooterWeapon weapon = this.GetActiveWeapon();
            ShooterStance stance = weapon != null ? this.GetShooterStance() : null;
            if (weapon != null && stance?.Reloading.IsReloading == true)
                stance.StopReload(weapon, CancelReason.ForceStop);
            if (weapon != null && stance != null)
                this.CancelFireRequest(weapon, stance, true);
            else
                this.InvalidateFireRequest();

            this.m_Aiming = false;
            this.SetCautiousWalk(false, true);
            this.SetShooterLocomotionActive(false);
            this.SetObjectDirectionForShooting(false);
            this.ResetShotDirectionTracking();
            this.ResetThrowableTpsCameraAim(true);

            if (this.IsWeaponMenuOpen) this.CloseWeaponMenu();
            if (this.m_HudTapTarget != null) this.m_HudTapTarget.SetActive(false);
            this.RefreshControlMode();
        }

        private void SetThrowableTpsCameraAim(bool active)
        {
            if (!active)
            {
                this.m_ThrowableAimPose?.SetRequested(false);
                if (!this.m_ThrowableAimCameraRequested) return;
                this.m_ThrowableAimCameraRequested = false;
                // The regular Update performs exactly one damped step this frame. Doing
                // it here as well would make PointerUp advance twice and feel uneven.
                return;
            }

            if (!this.CanUseThrowableTpsCamera())
            {
                this.m_ThrowableAimPose?.SetRequested(false);
                this.ResetThrowableTpsCameraAim(true);
                return;
            }

            if (!TryGetCurrentThirdPersonCamera(out ShotSystemThirdPerson thirdPerson))
            {
                this.m_ThrowableAimPose?.SetRequested(false);
                this.ResetThrowableTpsCameraAim(true);
                return;
            }

            if (this.m_ThrowableAimCameraActive &&
                this.m_ThrowableAimCamera == thirdPerson &&
                this.m_ThrowableAimCameraRequested)
            {
                this.m_ThrowableAimPose?.SetRequested(true);
                return;
            }

            // A camera transition during the hold must not leave an additive offset on
            // the previous Shot. The new active TPS Shot then receives this hold only.
            if (this.m_ThrowableAimCameraActive &&
                this.m_ThrowableAimCamera != null)
            {
                this.m_ThrowableAimCamera.Aim(0f, 0f, 0f, 0f);
            }

            this.m_ThrowableAimCamera = thirdPerson;
            this.m_ThrowableAimCameraActive = true;
            this.m_ThrowableAimCameraRequested = true;
            this.m_ThrowableAimPose?.SetRequested(true);
        }

        private void RefreshThrowableTpsCameraBlend()
        {
            if (!this.m_ThrowableAimCameraActive ||
                this.m_ThrowableAimCamera == null)
            {
                return;
            }

            if (!this.CanUseThrowableTpsCamera() ||
                !TryGetCurrentThirdPersonCamera(out ShotSystemThirdPerson current) ||
                current != this.m_ThrowableAimCamera)
            {
                this.ResetThrowableTpsCameraAim(true);
                return;
            }

            float targetShoulder = this.m_ThrowableAimCameraRequested
                ? this.m_ThrowableAimShoulderOffset
                : 0f;
            float targetRadius = this.m_ThrowableAimCameraRequested
                ? this.m_ThrowableAimRadiusOffset
                : 0f;
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            float smoothTime = Mathf.Max(0.01f, this.m_ThrowableAimCameraSmoothTime);

            this.m_ThrowableAimShoulderCurrent = Mathf.SmoothDamp(
                this.m_ThrowableAimShoulderCurrent,
                targetShoulder,
                ref this.m_ThrowableAimShoulderVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );
            this.m_ThrowableAimRadiusCurrent = Mathf.SmoothDamp(
                this.m_ThrowableAimRadiusCurrent,
                targetRadius,
                ref this.m_ThrowableAimRadiusVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );

            if (Mathf.Abs(this.m_ThrowableAimShoulderCurrent - targetShoulder) < 0.0005f &&
                Mathf.Abs(this.m_ThrowableAimRadiusCurrent - targetRadius) < 0.0005f)
            {
                this.m_ThrowableAimShoulderCurrent = targetShoulder;
                this.m_ThrowableAimRadiusCurrent = targetRadius;
                this.m_ThrowableAimShoulderVelocity = 0f;
                this.m_ThrowableAimRadiusVelocity = 0f;
            }

            // GC2 remains the only writer of the actual Camera Shot. Our damped values
            // are supplied as instantaneous additive offsets, avoiding nested tweens.
            this.m_ThrowableAimCamera.Aim(
                this.m_ThrowableAimShoulderCurrent,
                0f,
                this.m_ThrowableAimRadiusCurrent,
                0f
            );

            if (!this.m_ThrowableAimCameraRequested &&
                this.m_ThrowableAimShoulderCurrent == 0f &&
                this.m_ThrowableAimRadiusCurrent == 0f)
            {
                this.ResetThrowableTpsCameraAim(false);
            }
        }

        private void ResetThrowableTpsCameraAim(bool restoreCamera)
        {
            this.m_ThrowableAimPose?.SetRequested(false);
            if (restoreCamera && this.m_ThrowableAimCamera != null)
                this.m_ThrowableAimCamera.Aim(0f, 0f, 0f, 0f);

            this.m_ThrowableAimCamera = null;
            this.m_ThrowableAimCameraActive = false;
            this.m_ThrowableAimCameraRequested = false;
            this.m_ThrowableAimShoulderCurrent = 0f;
            this.m_ThrowableAimShoulderVelocity = 0f;
            this.m_ThrowableAimRadiusCurrent = 0f;
            this.m_ThrowableAimRadiusVelocity = 0f;
        }

        private bool CanUseThrowableTpsCamera()
        {
            if (this.m_Player == null ||
                this.IsPlayerRagdolledOrDead() ||
                this.m_FirstPersonCamera?.IsActive == true ||
                this.m_BikeSeatRole != BikeSeatRole.None)
            {
                return false;
            }

            this.RefreshPlayerHierarchyCache();
            return this.m_CarEntryCache == null &&
                   this.m_BikeDriverSeatCache == null &&
                   this.m_BikePassengerSeatCache == null;
        }

        private static bool TryGetCurrentThirdPersonCamera(
            out ShotSystemThirdPerson thirdPerson)
        {
            thirdPerson = null;
            MainCamera mainCamera = ShortcutMainCamera.Get<MainCamera>();
            ShotCamera shot = mainCamera?.Transition.CurrentShotCamera;
            if (shot?.ShotType is not ShotTypeThirdPerson shotType) return false;

            thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            return thirdPerson != null;
        }

        private bool SetAimActive(ShooterWeapon weapon, ShooterStance stance, bool active)
        {
            bool isThrowable = this.GetCatalogEntry(weapon)?.IsThrowable == true;
            this.SetThrowableTpsCameraAim(isThrowable && active && this.m_FireHeld);
            WeaponData data = stance.Get(weapon);
            if (data == null)
            {
                if (!active)
                {
                    this.SetBikeShooterAim(false);
                    this.SetShooterLocomotionActive(
                        !isThrowable && this.m_CautiousWalk
                    );
                }
                return false;
            }

            if (!active)
            {
                bool exitedSight = false;
                if (this.m_Aiming)
                {
                    stance.ExitSight(weapon);
                    this.m_Aiming = false;
                    exitedSight = true;
                    this.m_CrosshairScanUntil = 0f;
                    if (!this.m_CautiousWalk && !this.IsRpgWeapon(weapon))
                        this.ApplyDefaultSightPose(weapon, false);
                }
                // GC2 Sight.Exit can author a hard-coded FOV tween. Notify the
                // Bike camera only after ExitSight has started that tween so the
                // Bike FPS profile can cancel and replace it with its own FOV.
                this.SetBikeShooterAim(false);
                // On-foot FPS owns the same GC2 viewport. Restore it explicitly after the
                // Sight exit instead of relying on the passive 10 Hz control refresh to
                // rebuild every reflected profile property for the entire play session.
                if (exitedSight)
                    this.m_FirstPersonCamera?.ReapplyActiveProfile();
                this.SetShooterLocomotionActive(
                    !isThrowable && this.m_CautiousWalk
                );
                return true;
            }

            // Shooter_Locomotion is the moving armed-pose foundation for firearms.
            // Throwable weapons keep normal locomotion and let the native Grenade Sight plus
            // charge/fire animation own the throwing pose only during this hold.
            this.SetShooterLocomotionActive(!isThrowable);

            if (this.m_Aiming)
            {
                SightItem currentSight = weapon.Sights.Get(data.SightId);
                if (currentSight?.Sight != null) return true;

                // Never keep the Franklin state flagged as aiming when GC2 is actually
                // pointing at a missing Sight asset. This can happen after an asset copy
                // loses one of its object references.
                this.m_Aiming = false;
            }

            // A Bike driver keeps the left hand on the handlebar. These two local
            // sights retain the native Pistol/AK ADS state and Shooter rig, but their
            // authored mask and biomechanics only own the torso and gun-side arm.
            if (this.m_BikeSeatRole == BikeSeatRole.Driver)
            {
                SightItem bikeDriverSight = weapon.Sights.Get(BIKE_DRIVER_AIM_SIGHT);
                if (bikeDriverSight?.Sight != null)
                {
                    stance.EnterSight(weapon, BIKE_DRIVER_AIM_SIGHT);
                    this.m_Aiming = true;
                    this.SetBikeShooterAim(true);
                    this.BeginCrosshairDiscovery();
                    return true;
                }
            }

            foreach (IdString sightId in PREFERRED_AIM_SIGHTS)
            {
                SightItem sightItem = weapon.Sights.Get(sightId);
                if (sightItem?.Sight == null) continue;

                stance.EnterSight(weapon, sightId);
                this.m_Aiming = true;
                this.SetBikeShooterAim(true);
                this.BeginCrosshairDiscovery();
                return true;
            }

            // Some GC2 weapon templates (for example Grenade, used by the RPG-7)
            // deliberately expose only their default Sight. It is already entered when
            // the weapon is equipped and is still a valid firing/aiming pose.
            IdString fallbackSightId = data.SightId;
            SightItem fallbackSight = weapon.Sights.Get(fallbackSightId);
            if (fallbackSight?.Sight == null)
            {
                fallbackSightId = weapon.Sights.DefaultId;
                fallbackSight = weapon.Sights.Get(fallbackSightId);
            }

            if (fallbackSight?.Sight != null)
            {
                if (data.SightId != fallbackSightId)
                    stance.EnterSight(weapon, fallbackSightId);
                else if (isThrowable)
                    fallbackSight.Enter(this.m_Player, weapon);

                this.m_Aiming = true;
                this.SetBikeShooterAim(true);
                this.BeginCrosshairDiscovery();
                return true;
            }

            this.SetShooterLocomotionActive(
                !isThrowable && this.m_CautiousWalk
            );
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

            this.SetShooterLocomotionActive(active || this.m_Aiming);

            if (!this.m_Aiming)
                this.ApplyDefaultSightPose(
                    weapon,
                    active || this.IsRpgWeapon(weapon)
                );
        }

        private void SetShooterLocomotionActive(bool active)
        {
            if (this.m_Player == null)
            {
                return;
            }

            if (active && this.m_ShooterLocomotion != null)
            {
                // The native locomotion asset is full-body. On Bike use its exact controller
                // through GC2's masked state overload, so layer 1 remains authoritative for
                // the seated pelvis and legs. Driver additionally leaves the steering arm out.
                if (this.m_BikeSeatRole != BikeSeatRole.None)
                {
                    AvatarMask bikeShooterMask = this.m_BikeSeatRole == BikeSeatRole.Driver
                        ? this.m_BikeDriverShooterMask
                        : this.m_ShooterUpperBodyMask;
                    if (bikeShooterMask == null)
                    {
                        this.m_Player.States.Stop(
                            SHOOTER_LOCOMOTION_LAYER,
                            0f,
                            SHOOTER_LOCOMOTION_TRANSITION
                        );
                        return;
                    }

                    _ = this.m_Player.States.SetState(
                        this.m_ShooterLocomotion.StateController,
                        bikeShooterMask,
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
                    return;
                }
            }

            if (active && this.m_ShooterLocomotion != null)
            {
                _ = this.m_Player.States.SetState(
                    this.m_ShooterLocomotion,
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
                return;
            }

            this.m_Player.States.Stop(
                SHOOTER_LOCOMOTION_LAYER,
                0f,
                SHOOTER_LOCOMOTION_TRANSITION
            );
        }

        // GC2 enters the default lowered-gun sight automatically. Normal walking keeps its
        // logical sight id for shooting, but removes that sight's visual state until requested.
        private void ApplyDefaultSightPose(ShooterWeapon weapon, bool active)
        {
            if (this.m_Player == null || weapon == null) return;

            ShooterStance stance = this.GetShooterStance();
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

        private bool CompactActiveCrosshairs()
        {
            if (CROSSHAIR_ACCURACY_POSITION == null ||
                CROSSHAIR_POSITION_X == null ||
                CROSSHAIR_POSITION_Y == null)
            {
                return true;
            }

            CrosshairUI[] crosshairs = FindObjectsByType<CrosshairUI>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            bool foundFranklinCrosshair = false;
            foreach (CrosshairUI crosshair in crosshairs)
            {
                if (crosshair == null ||
                    !IsFranklinWeaponCrosshair(crosshair.transform) ||
                    CROSSHAIR_ACCURACY_POSITION.GetValue(crosshair) is not RectTransform)
                {
                    continue;
                }

                foundFranklinCrosshair = true;
                if (!this.m_CompactedCrosshairs.Add(crosshair)) continue;

                Vector2 positionX = (Vector2) CROSSHAIR_POSITION_X.GetValue(crosshair);
                Vector2 positionY = (Vector2) CROSSHAIR_POSITION_Y.GetValue(crosshair);
                CROSSHAIR_POSITION_X.SetValue(crosshair, CompactCrosshairRange(positionX));
                CROSSHAIR_POSITION_Y.SetValue(crosshair, CompactCrosshairRange(positionY));
            }

            return foundFranklinCrosshair;
        }

        private void BeginCrosshairDiscovery()
        {
            // CrosshairData destroys its instances on Shooter unequip. Unity's destroyed
            // object comparison lets us release the managed wrappers before scanning the
            // next Sight, while still remembering inactive crosshairs owned by this weapon.
            this.m_CompactedCrosshairs.RemoveWhere(IsDestroyedCrosshair);

            float now = Time.unscaledTime;
            this.m_CrosshairScanUntil =
                now + Mathf.Max(0.2f, this.m_CrosshairDiscoverySeconds);
            this.m_NextCrosshairScan =
                now + Mathf.Max(0.05f, this.m_CrosshairScanSeconds);
            if (this.CompactActiveCrosshairs())
                this.m_CrosshairScanUntil = 0f;
        }

        private static bool IsDestroyedCrosshair(CrosshairUI crosshair)
        {
            return crosshair == null;
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
                this.IsPlayerRagdolledOrDead() ||
                this.m_BikeSeatRole == BikeSeatRole.Driver) return;
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null || !this.CanUseWeaponInCurrentSeat(weapon)) return;
            await this.GetShooterStance().Reload(weapon);
        }

        private async void SwitchToMelee()
        {
            if (this.m_IsSwitching || this.m_Player == null ||
                this.m_BikeStuntActive || this.IsPlayerRagdolledOrDead()) return;
            ShooterWeapon weapon = this.GetActiveWeapon();
            if (weapon == null) return;

            this.m_IsSwitching = true;
            try
            {
                ShooterStance stance = this.GetShooterStance();
                this.CancelFireRequest(weapon, stance, true);
                this.SetCautiousWalk(false, true);

                GameObject prop = this.m_Player.Combat.GetProp(weapon);
                this.InvalidateActiveWeaponCache();
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
            this.InvalidateActiveWeaponCache();
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
                        this.SetShooterLocomotionActive(false);
                        this.m_CautiousWalk = false;
                        this.RefreshCautiousWalkButton();
                    }
                }
            }
            this.RefreshControlMode();
        }

        private ShooterWeapon GetActiveWeapon()
        {
            if (this.m_Player == null) return null;
            if (!this.m_ActiveWeaponCacheValid)
            {
                this.m_ActiveWeaponCache =
                    this.m_Player.Combat.GetActiveWeapon<ShooterWeapon>();
                this.m_ActiveWeaponCacheValid = true;
            }

            return this.m_ActiveWeaponCache;
        }

        private bool HasActiveWeapon() => this.GetActiveWeapon() != null;

        private ShooterStance GetShooterStance()
        {
            if (this.m_Player == null) return null;
            return this.m_ShooterStanceCache ??=
                this.m_Player.Combat.RequestStance<ShooterStance>();
        }

        private void InvalidateActiveWeaponCache()
        {
            this.m_ActiveWeaponCache = null;
            this.m_ActiveWeaponCacheValid = false;
            this.m_CatalogEntryWeaponCache = null;
            this.m_CatalogEntryCache = null;
        }

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
                bool onCurrentPage = i >= this.m_SlotPages.Count ||
                                     this.m_SlotPages[i] == this.m_MenuPageIndex;
                if (i < this.m_SlotRoots.Count &&
                    this.m_SlotRoots[i].activeSelf != onCurrentPage)
                {
                    this.m_SlotRoots[i].SetActive(onCurrentPage);
                }

                bool selected = onCurrentPage && i == this.m_SelectedIndex;
                bool available = this.m_BikeSeatRole != BikeSeatRole.Driver ||
                                 IsDriverWeaponEntryAllowed(this.m_Catalog?.Get(i));
                Graphic frame = this.m_SlotFrames[i];
                bool frameChanged = false;
                if (frame.color != SELECTED_SECTOR)
                {
                    frame.color = SELECTED_SECTOR;
                    frameChanged = true;
                }
                if (frame.gameObject.activeSelf != selected)
                {
                    frame.gameObject.SetActive(selected);
                    frameChanged = true;
                }
                if (selected && frameChanged)
                {
                    frame.SetVerticesDirty();
                    frame.SetMaterialDirty();
                }
                if (i < this.m_SlotButtons.Count &&
                    this.m_SlotButtons[i].interactable != available)
                {
                    this.m_SlotButtons[i].interactable = available;
                }
                Color iconColor = !available
                    ? new Color(0.52f, 0.56f, 0.6f, 0.22f)
                    : selected
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.56f);
                if (this.m_SlotIcons[i].color != iconColor)
                    this.m_SlotIcons[i].color = iconColor;
            }

            if (this.m_Hint != null)
            {
                string hint = this.m_BikeSeatRole switch
                {
                    BikeSeatRole.Driver =>
                        "DRIVER: M1911 / UZI ONLY  •  TAP OUTSIDE TO CLOSE",
                    BikeSeatRole.Passenger =>
                        "PASSENGER: ALL WEAPONS AVAILABLE  •  TAP OUTSIDE TO CLOSE",
                    _ => "TAP A WEAPON TO EQUIP  •  TAP OUTSIDE TO CLOSE"
                };
                if (this.m_Hint.text != hint) this.m_Hint.text = hint;
            }

            this.RefreshSelectedPanel();
        }

        private void RefreshSelectedPanel()
        {
            FranklinShooterCatalog.Entry entry = this.m_Catalog?.Get(this.m_SelectedIndex);
            if (entry == null) return;

            bool selectionChanged = this.m_RenderedSelectionIndex != this.m_SelectedIndex;
            if (selectionChanged)
            {
                if (this.m_SelectedIcon != null &&
                    this.m_SelectedIcon.overrideSprite != entry.Icon)
                {
                    this.m_SelectedIcon.overrideSprite = entry.Icon;
                }
                if (this.m_SelectedName != null)
                {
                    string displayName = entry.DisplayName.ToUpperInvariant();
                    if (this.m_SelectedName.text != displayName)
                        this.m_SelectedName.text = displayName;
                }
                if (this.m_SelectedCategory != null)
                {
                    string category = entry.Category.ToUpperInvariant();
                    if (this.m_SelectedCategory.text != category)
                        this.m_SelectedCategory.text = category;
                }

                this.m_RenderedSelectionIndex = this.m_SelectedIndex;
            }

            if (this.m_SelectedAmmo != null)
            {
                bool ammoAvailable = false;
                int inMagazine = int.MinValue;
                int total = int.MinValue;
                bool usesMagazine = false;
                if (this.m_Player != null && entry.Weapon != null &&
                    this.m_Player.Combat.RequestMunition(entry.Weapon) is ShooterMunition munition)
                {
                    GameObject prop = this.m_Player.Combat.GetProp(entry.Weapon);
                    this.m_SelectedAmmoArgs ??=
                        new Args(this.m_Player.gameObject, prop);
                    this.m_SelectedAmmoArgs.ChangeSelf(this.m_Player.gameObject);
                    this.m_SelectedAmmoArgs.ChangeTarget(prop);
                    ammoAvailable = true;
                    inMagazine = munition.InMagazine;
                    usesMagazine = entry.Weapon.Magazine.GetHasMagazine(
                        this.m_SelectedAmmoArgs
                    );
                    total = entry.Weapon.Magazine.GetTotalAmmo(
                        this.m_SelectedAmmoArgs
                    );
                }

                if (selectionChanged ||
                    this.m_RenderedAmmoAvailable != ammoAvailable ||
                    this.m_RenderedAmmoInMagazine != inMagazine ||
                    this.m_RenderedAmmoTotal != total)
                {
                    string ammo = ammoAvailable
                        ? usesMagazine
                            ? inMagazine + " / " +
                              (total >= int.MaxValue ? "∞" : total.ToString())
                            : total >= int.MaxValue ? "∞" : total.ToString()
                        : "READY";
                    if (this.m_SelectedAmmo.text != ammo)
                        this.m_SelectedAmmo.text = ammo;
                    this.m_RenderedAmmoAvailable = ammoAvailable;
                    this.m_RenderedAmmoInMagazine = inMagazine;
                    this.m_RenderedAmmoTotal = total;
                }
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
            this.BuildFirstPersonControl(canvasObject.transform);
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

            RectTransform safeArea = CreateRect(
                "Safe Area Content",
                this.m_MenuRoot.transform,
                Vector2.zero,
                Vector2.zero
            );
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<FranklinSafeArea>();

            RectTransform layoutRoot = CreateRect(
                "Weapon Menu Layout",
                safeArea,
                Vector2.zero,
                new Vector2(960f, 1080f)
            );
            safeArea.gameObject.AddComponent<FranklinWeaponMenuSafeAreaFitter>().Initialize(
                layoutRoot,
                new Vector2(960f, 1080f)
            );

            RectTransform wheelRoot = CreateRect(
                "Weapon Wheel",
                layoutRoot,
                new Vector2(0f, 20f),
                new Vector2(900f, 900f)
            );

            Image background = CreateImage(
                "ImageGen Weapon Wheel", wheelRoot,
                Resources.Load<Sprite>(BACKGROUND_RESOURCE), WHEEL_TINT
            );
            SetRect(background.rectTransform, Vector2.zero, new Vector2(900f, 900f));
            background.preserveAspect = true;
            background.raycastTarget = true;
            background.gameObject.AddComponent<FranklinCircularRaycastFilter>();
            Button wheelInputShield = background.gameObject.AddComponent<Button>();
            wheelInputShield.transition = Selectable.Transition.None;
            wheelInputShield.targetGraphic = background;

            Text title = CreateText(
                "Title", layoutRoot, "WEAPONS", 30, OFF_WHITE, TextAnchor.MiddleCenter
            );
            SetRect(title.rectTransform, new Vector2(0f, 495f), new Vector2(460f, 50f));
            title.fontStyle = FontStyle.Bold;
            AddTextOutline(title, 0.9f);

            int firearmCount = 0;
            int throwableCount = 0;
            for (int i = 0; i < this.m_Catalog.Count; ++i)
            {
                if (this.m_Catalog.Get(i)?.IsThrowable == true) ++throwableCount;
                else ++firearmCount;
            }

            this.m_MenuPageCount = firearmCount > 0 && throwableCount > 0 ? 2 : 1;
            int pageZeroOrdinal = 0;
            int pageOneOrdinal = 0;
            const float sectorStep = 360f / WEAPON_WHEEL_SECTOR_COUNT;
            const float slotRadius = 330f;
            Vector2 slotSize = new(220f, 160f);

            for (int i = 0; i < this.m_Catalog.Count; ++i)
            {
                FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(i);
                int index = i;
                bool throwablePage = this.m_MenuPageCount > 1 && entry.IsThrowable;
                int page = throwablePage ? WEAPON_MENU_THROWABLE_PAGE : 0;
                int pageOrdinal = page == WEAPON_MENU_THROWABLE_PAGE
                    ? pageOneOrdinal++
                    : pageZeroOrdinal++;
                int pageEntryCount = page == WEAPON_MENU_THROWABLE_PAGE
                    ? throwableCount
                    : this.m_MenuPageCount == 1 ? this.m_Catalog.Count : firearmCount;
                int physicalSlot = GetWeaponWheelSlot(pageOrdinal, pageEntryCount);
                float slotAngle = (90f - sectorStep * physicalSlot) * Mathf.Deg2Rad;
                Vector2 slotPosition = new(
                    Mathf.Cos(slotAngle) * slotRadius,
                    Mathf.Sin(slotAngle) * slotRadius
                );
                RectTransform slot = CreateRect(
                    "Weapon Slot " + (i + 1), wheelRoot,
                    slotPosition, slotSize
                );
                RectTransform sector = CreateRect(
                    "Selected Sector " + (i + 1),
                    wheelRoot,
                    Vector2.zero,
                    new Vector2(900f, 900f)
                );
                sector.SetSiblingIndex(background.rectTransform.GetSiblingIndex() + 1);
                sector.localRotation = Quaternion.Euler(0f, 0f, -sectorStep * physicalSlot);
                FranklinWeaponSectorHighlight frame =
                    sector.gameObject.AddComponent<FranklinWeaponSectorHighlight>();
                frame.SetSectorCount(WEAPON_WHEEL_SECTOR_COUNT);
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
                SetRect(
                    icon.rectTransform,
                    new Vector2(0f, 13f),
                    new Vector2(210f, 104f)
                );
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                Text label = CreateText(
                    "Label", slot, entry.DisplayName.ToUpperInvariant(),
                    20, OFF_WHITE, TextAnchor.MiddleCenter
                );
                SetRect(
                    label.rectTransform,
                    new Vector2(0f, -60f),
                    new Vector2(slotSize.x - 2f, 34f)
                );
                label.fontStyle = FontStyle.Bold;
                label.raycastTarget = false;
                AddTextOutline(label, 0.82f);

                this.m_SlotRoots.Add(slot.gameObject);
                this.m_SlotPages.Add(page);
                this.m_SlotFrames.Add(frame);
                this.m_SlotIcons.Add(icon);
                this.m_SlotButtons.Add(button);
            }

            RectTransform center = CreateRect(
                "Selected Weapon", wheelRoot,
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

            RectTransform navigation = CreateRect(
                "Weapon Pages",
                layoutRoot,
                new Vector2(0f, -474f),
                new Vector2(500f, 68f)
            );
            this.m_MenuNavigationRoot = navigation.gameObject;
            this.m_MenuPrevButton = CreateWeaponMenuPageButton(
                "Previous Page",
                navigation,
                new Vector2(-132f, 0f),
                "<  PREV",
                out this.m_MenuPrevLabel
            );
            this.m_MenuPrevButton.onClick.AddListener(this.ShowPreviousWeaponMenuPage);
            this.m_MenuNextButton = CreateWeaponMenuPageButton(
                "Next Page",
                navigation,
                new Vector2(132f, 0f),
                "NEXT  >",
                out this.m_MenuNextLabel
            );
            this.m_MenuNextButton.onClick.AddListener(this.ShowNextWeaponMenuPage);

            this.m_MenuPageText = CreateText(
                "Page Number",
                navigation,
                string.Empty,
                18,
                new Color(OFF_WHITE.r, OFF_WHITE.g, OFF_WHITE.b, 0.86f),
                TextAnchor.MiddleCenter
            );
            SetRect(this.m_MenuPageText.rectTransform, Vector2.zero, new Vector2(76f, 38f));
            this.m_MenuPageText.fontStyle = FontStyle.Bold;
            AddTextOutline(this.m_MenuPageText, 0.75f);

            this.m_Hint = CreateText(
                "Hint", layoutRoot,
                "TAP A WEAPON TO EQUIP  •  TAP OUTSIDE TO CLOSE",
                18, new Color(OFF_WHITE.r, OFF_WHITE.g, OFF_WHITE.b, 0.78f), TextAnchor.MiddleCenter
            );
            SetRect(this.m_Hint.rectTransform, new Vector2(0f, -525f), new Vector2(900f, 30f));
            AddTextOutline(this.m_Hint, 0.8f);

            this.SetWeaponMenuPage(0, false);
            this.m_MenuRoot.SetActive(false);
        }

        private static int GetWeaponWheelSlot(int pageOrdinal, int pageEntryCount)
        {
            if (pageEntryCount <= 1) return 0;
            if (pageEntryCount == 2) return pageOrdinal == 0 ? 7 : 1;
            return Mathf.RoundToInt(
                       pageOrdinal * (WEAPON_WHEEL_SECTOR_COUNT / (float) pageEntryCount)
                   ) % WEAPON_WHEEL_SECTOR_COUNT;
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
                ON_FOOT_MELEE_POSITION, new Vector2(140f, 140f),
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

        private void BuildFirstPersonControl(Transform parent)
        {
            this.m_FirstPersonControlRoot = CreateRect(
                "First Person Camera Control",
                parent,
                Vector2.zero,
                Vector2.zero
            ).gameObject;
            Stretch(this.m_FirstPersonControlRoot.GetComponent<RectTransform>());

            Image firstPersonImage = CreateTouchButton(
                "First Person Camera",
                this.m_FirstPersonControlRoot.transform,
                FIRST_PERSON_ICON_RESOURCE,
                ON_FOOT_FIRST_PERSON_POSITION,
                new Vector2(112f, 112f),
                FranklinShooterTouchButton.Action.FirstPersonCamera
            );
            this.m_FirstPersonButton =
                firstPersonImage.GetComponent<FranklinShooterTouchButton>();
            this.m_FirstPersonControlRoot.SetActive(false);
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

        private void RefreshControlModeBudgeted()
        {
            if (Time.unscaledTime < this.m_NextPassiveControlRefresh) return;
            this.RefreshControlMode();
        }

        private void RefreshControlMode()
        {
            this.m_NextPassiveControlRefresh =
                Time.unscaledTime +
                Mathf.Max(0.02f, this.m_PassiveControlRefreshSeconds);
            this.ApplyFirstPersonPreference();
            this.RefreshFirstPersonControl();

            bool hasShooterWeapon = this.HasActiveWeapon() ||
                                    this.m_IsSwitching ||
                                    this.m_BikeStuntActive ||
                                    this.m_BikeStuntSuspendedWeapon != null;
            ShooterWeapon activeWeapon = this.GetActiveWeapon();
            bool hasUsableWeapon = activeWeapon != null &&
                                   this.CanUseWeaponInCurrentSeat(activeWeapon);
            this.RefreshObjectDirectionMode();
            this.SetIdleAnimationSuppressed(hasShooterWeapon);
            this.SetMeleeControlsSuppressed(hasShooterWeapon);
            this.RefreshBikeControlLayout();

            if (this.m_ControlsRoot == null) return;
            bool onFootVisible = this.m_OnFootControls == null ||
                                 this.m_OnFootControls.gameObject.activeInHierarchy;
            bool hasVisibleControlMode = onFootVisible ||
                                         this.m_BikeSeatRole != BikeSeatRole.None;
            bool showShooterControls = hasUsableWeapon &&
                                       !this.IsPlayerRagdolledOrDead() &&
                                       !this.m_IsSwitching &&
                                       !this.m_PhoneUseActive &&
                                       !this.IsWeaponMenuOpen &&
                                       !FranklinMobileHud.ControlsSuppressed &&
                                       hasVisibleControlMode;
            if (this.m_ControlsRoot.activeSelf != showShooterControls)
                this.m_ControlsRoot.SetActive(showShooterControls);
        }

        private bool CanPresentOnFootFirstPerson()
        {
            if (this.m_Player == null || this.m_PhoneUseActive ||
                this.IsPlayerRagdolledOrDead() ||
                this.m_BikeSeatRole != BikeSeatRole.None ||
                this.IsWeaponMenuOpen || FranklinMobileHud.ControlsSuppressed)
            {
                return false;
            }

            if (this.m_Player.Player == null || !this.m_Player.Player.IsControllable)
                return false;

            // Melee is TPS-only. IsReady avoids a TPS flash during the brief
            // no-active-weapon gap while switching between Shooter weapons; the
            // fallback covers characters that do not carry the melee component.
            bool meleeMode = this.m_MeleeController?.IsReady == true ||
                             this.GetActiveWeapon() == null && !this.m_IsSwitching;
            if (meleeMode) return false;

            // Entry components become parents of the Character during their authored
            // transition. Yield before the Car/Bike camera selects its own FPS/TPS shot.
            this.RefreshPlayerHierarchyCache();
            return this.m_CarEntryCache == null &&
                   this.m_BikeDriverSeatCache == null;
        }

        private void ApplyFirstPersonPreference()
        {
            if (this.m_FirstPersonCamera == null) return;

            bool shooterPreference = this.ResolveFirstPersonPreferenceMode();
            bool preferred = shooterPreference
                ? this.m_ShooterFirstPersonPreferred
                : this.m_MovementFirstPersonPreferred;
            this.m_FirstPersonCamera.SetRequested(
                preferred,
                this.CanPresentOnFootFirstPerson(),
                shooterPreference
                    ? FranklinFirstPersonCameraManager.Context.Shooter
                    : FranklinFirstPersonCameraManager.Context.PlayerMovement
            );
        }

        private bool ResolveFirstPersonPreferenceMode()
        {
            if (this.GetActiveWeapon() != null ||
                this.m_PhoneSuspendedWeapon != null ||
                this.m_BikeSuspendedWeapon != null ||
                this.m_BikeStuntSuspendedWeapon != null)
            {
                this.m_FirstPersonUsesShooterPreference = true;
                return true;
            }

            bool shooterStateIsTemporarilySuspended =
                this.m_IsSwitching ||
                this.m_PhoneUseActive;
            if (!shooterStateIsTemporarilySuspended)
                this.m_FirstPersonUsesShooterPreference = false;

            return this.m_FirstPersonUsesShooterPreference;
        }

        private void SetFirstPersonPreference(bool shooterPreference, bool preferred)
        {
            if (shooterPreference)
            {
                this.m_ShooterFirstPersonPreferred = preferred;
                PlayerPrefs.SetInt(
                    SHOOTER_FIRST_PERSON_PREFERENCE_KEY,
                    preferred ? 1 : 0
                );
            }
            else
            {
                this.m_MovementFirstPersonPreferred = preferred;
                PlayerPrefs.SetInt(
                    MOVEMENT_FIRST_PERSON_PREFERENCE_KEY,
                    preferred ? 1 : 0
                );
            }

            PlayerPrefs.Save();
        }

        private void RefreshFirstPersonControl()
        {
            if (this.m_FirstPersonControlRoot == null) return;

            bool onFootControlsVisible = this.m_OnFootControls == null ||
                                         this.m_OnFootControls.gameObject.activeInHierarchy;
            bool show = this.CanPresentOnFootFirstPerson() &&
                        !this.IsWeaponMenuOpen &&
                        !FranklinMobileHud.ControlsSuppressed &&
                        onFootControlsVisible;
            if (this.m_FirstPersonControlRoot.activeSelf != show)
                this.m_FirstPersonControlRoot.SetActive(show);

            bool shooterPreference = this.ResolveFirstPersonPreferenceMode();
            this.m_FirstPersonButton?.SetToggled(
                shooterPreference
                    ? this.m_ShooterFirstPersonPreferred
                    : this.m_MovementFirstPersonPreferred
            );
        }

        private void RefreshBikeControlLayout()
        {
            bool isOnBike = this.m_BikeSeatRole != BikeSeatRole.None;
            FranklinShooterCatalog.Entry activeEntry =
                this.GetCatalogEntry(this.GetActiveWeapon());
            bool showReload = this.m_BikeSeatRole != BikeSeatRole.Driver &&
                              (activeEntry == null || !activeEntry.IsThrowable);
            if (this.m_FireButtonRect != null)
            {
                Vector2 firePosition = isOnBike
                    ? BIKE_FIRE_POSITION
                    : ON_FOOT_FIRE_POSITION;
                if (this.m_FireButtonRect.anchoredPosition != firePosition)
                    this.m_FireButtonRect.anchoredPosition = firePosition;
            }
            if (this.m_ReloadButtonRect != null)
            {
                if (this.m_ReloadButtonRect.anchoredPosition != ON_FOOT_RELOAD_POSITION)
                {
                    this.m_ReloadButtonRect.anchoredPosition =
                        ON_FOOT_RELOAD_POSITION;
                }
                if (this.m_ReloadButtonRect.gameObject.activeSelf != showReload)
                    this.m_ReloadButtonRect.gameObject.SetActive(showReload);
            }
            if (this.m_MeleeSwitchImage != null)
            {
                Vector2 meleePosition = isOnBike
                    ? BIKE_MELEE_POSITION
                    : ON_FOOT_MELEE_POSITION;
                if (this.m_MeleeSwitchImage.rectTransform.anchoredPosition != meleePosition)
                {
                    this.m_MeleeSwitchImage.rectTransform.anchoredPosition =
                        meleePosition;
                }
                if (!this.m_MeleeSwitchImage.gameObject.activeSelf)
                    this.m_MeleeSwitchImage.gameObject.SetActive(true);
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

            ShooterStance stance = this.GetShooterStance();
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
            this.RefreshObjectDirectionMode();
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

        private void RefreshObjectDirectionMode()
        {
            bool firstPersonOwnsFacing =
                this.m_BikeSeatRole == BikeSeatRole.None &&
                this.m_FirstPersonCamera != null &&
                this.m_FirstPersonCamera.IsActive &&
                this.CanPresentOnFootFirstPerson();
            bool shooterOwnsFacing =
                this.m_BikeSeatRole == BikeSeatRole.None &&
                this.GetActiveWeapon() != null &&
                (this.m_Aiming || this.m_FireHeld || this.m_TriggerPulled ||
                 this.m_ObjectDirectionReturnAt >= 0f &&
                 Time.time < this.m_ObjectDirectionReturnAt);

            // GC2 Object Direction reads Main Camera forward. In FPS this makes
            // the complete Character body align to orbit yaw, while the camera's
            // own pitch remains independent. Vehicle seats never enter this path.
            this.SetObjectDirectionForShooting(
                firstPersonOwnsFacing || shooterOwnsFacing
            );
        }

        private void SetObjectDirectionForShooting(bool active)
        {
            if (active)
            {
                if (this.m_OwnsObjectDirection &&
                    this.m_ObjectDirectionToggle != null &&
                    this.m_ObjectDirectionToggle.IsObjectDirectionEnabled &&
                    this.m_ObjectDirectionToggle.OwnsCurrentFacing)
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
                    this.m_AnimationBridge
                        .GetComponent<FranklinObjectDirectionToggle>();
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

        private void SetFireMovementStatesSuppressed(bool suppressed)
        {
            if (suppressed)
            {
                // Vehicle animation owns the seated body. This arbitration is only for the
                // competing on-foot Walk/Jog/Sprint pipeline.
                if (this.m_BikeSeatRole != BikeSeatRole.None ||
                    this.m_OwnsFireMovementSuppression)
                {
                    return;
                }

                if (this.m_AnimationBridge == null && this.m_Player != null)
                {
                    this.m_AnimationBridge = this.m_Player
                        .GetComponentInChildren<FranklinAnimationBridge>(true);
                }
                if (this.m_AnimationBridge == null) return;

                this.m_AnimationBridge.AcquireFastLocomotionSuppression(this);
                this.m_OwnsFireMovementSuppression = true;
                return;
            }

            if (!this.m_OwnsFireMovementSuppression) return;
            this.m_AnimationBridge?.ReleaseFastLocomotionSuppression(this);
            this.m_OwnsFireMovementSuppression = false;
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

        private void CancelMeleeForShooterEquip()
        {
            if (this.m_MeleeController == null && this.m_Player != null)
            {
                this.m_MeleeController =
                    this.m_Player.GetComponent<FranklinMeleeController>();
            }

            this.m_MeleeController?.CancelAllMeleeStates();
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

        private static Button CreateWeaponMenuPageButton(
            string name,
            Transform parent,
            Vector2 position,
            string value,
            out Text label)
        {
            RectTransform rect = CreateRect(name, parent, position, new Vector2(178f, 58f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            Outline border = rect.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.43f, 0.51f, 0.56f, 0.62f);
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.035f, 0.085f, 0.125f, 0.96f);
            colors.highlightedColor = new Color(0.04f, 0.20f, 0.32f, 1f);
            colors.pressedColor = new Color(0.025f, 0.30f, 0.48f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.025f, 0.045f, 0.06f, 0.32f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            label = CreateText(
                "Label",
                rect,
                value,
                20,
                OFF_WHITE,
                TextAnchor.MiddleCenter
            );
            Stretch(label.rectTransform);
            label.fontStyle = FontStyle.Bold;
            AddTextOutline(label, 0.72f);
            return button;
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
