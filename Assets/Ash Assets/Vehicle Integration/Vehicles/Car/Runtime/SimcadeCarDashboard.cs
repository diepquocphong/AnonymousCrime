using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Lightweight, prewarmed mobile Car HUD. Speed follows a projected world target,
    /// health is event-driven and radio audio is streamed from one non-spatial source.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(SimcadeCarDriver),
        typeof(SimcadeCarHealth),
        typeof(SimcadeCarFuel))]
    public sealed class SimcadeCarDashboard : MonoBehaviour
    {
        private static readonly Color HEALTH_SKY_BLUE = new Color(0.16f, 0.72f, 1f, 1f);
        private static readonly Color SPEED_BACKGROUND_COLOR =
            new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color SPEED_BACKGROUND_BORDER_COLOR =
            new Color(1f, 1f, 1f, 0.12f);
        private static readonly Vector2 SPEED_BACKGROUND_OFFSET =
            new Vector2(0f, 3f);
        private const string HEALTH_TINT_SHADER = "UI/Franklin Alpha Tint";
        private static readonly Color RADIO_ACTIVE = Color.white;
        private static readonly Color RADIO_INACTIVE = new Color(0.58f, 0.6f, 0.64f, 0.9f);
        private static readonly Color RADIO_PANEL = new(0.035f, 0.055f, 0.075f, 0.94f);
        private static readonly Color RADIO_ACCENT = new(0.16f, 0.72f, 1f, 0.78f);
        private const float FUEL_GAUGE_WIDTH = 74f;
        private const float FUEL_GAUGE_HEIGHT = 152f;
        private const float HEALTH_GAUGE_LAYOUT_WIDTH = 104f;
        private const float HEALTH_GAUGE_LAYOUT_HEIGHT = 230f;
        private const float HEALTH_GAUGE_WIDTH = 40f;
        private const float HEALTH_GAUGE_HEIGHT = 184f;
        private const float HEALTH_GAUGE_BAR_OFFSET_X = 18f;
        private const float HEALTH_GAUGE_BAR_OFFSET_Y = -23f;

        [Header("Telemetry")]
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private SimcadeCarHealth m_Health;
        [SerializeField] private SimcadeCarFuel m_Fuel;
        [SerializeField, Range(0.05f, 0.5f)] private float m_UpdateInterval = 0.1f;
        [Tooltip("Giới hạn phép chiếu 8 góc body Car; 20 Hz đủ mượt cho HUD mobile.")]
        [SerializeField, Range(0.033f, 0.1f)]
        private float m_WorldFollowInterval = 0.05f;

        [Header("Speed Position (Editable)")]
        [Tooltip("Khoảng cách world-space sang trái thân xe.")]
        [SerializeField, Min(0.5f)] private float m_SpeedWorldLeftOffset = 1.9f;
        [Tooltip("Độ cao world-space của target; 0.82 nằm ngang tầm kính sedan.")]
        [SerializeField] private float m_SpeedWorldHeight = 0.82f;
        [Tooltip("Tinh chỉnh cuối theo pixel Canvas sau khi project world target.")]
        [SerializeField] private Vector2 m_SpeedScreenOffset = Vector2.zero;
        [SerializeField, Range(0.03f, 0.3f)] private float m_SpeedFollowSmooth = 0.11f;

        [Header("Fuel Gauge Under Speed (Left of Car)")]
        [SerializeField] private Vector2 m_FuelGaugeOffset = new Vector2(-45f, -112f);

        [Header("Health Position (Right of Car)")]
        [Tooltip("Khoảng cách world-space sang phải thân xe.")]
        [SerializeField, Min(0.5f)] private float m_HealthWorldRightOffset = 1.9f;
        [SerializeField] private float m_HealthWorldHeight = 0.82f;
        [Tooltip("Bù pixel bổ sung sau khi tự mirror theo thanh xăng; mặc định (0,0).")]
        [SerializeField] private Vector2 m_HealthScreenOffset = Vector2.zero;

        [Header("Shared Telemetry Spacing")]
        [Tooltip("Khoảng cách pixel giữa mép thân Car và thanh xăng/máu.")]
        [SerializeField, Min(0f)] private float m_TelemetrySideGap = 45f;

        [Header("Radio")]
        [SerializeField] private AudioSource m_RadioSource;
        [SerializeField] private AudioSource m_RadioTuningSource;
        [SerializeField] private AudioClip m_RadioTuningClip;
        [SerializeField] private AudioClip[] m_RadioTracks = Array.Empty<AudioClip>();
        [SerializeField] private string[] m_StationNames = Array.Empty<string>();
        [SerializeField, Range(0f, 1f)] private float m_RadioVolume = 0.55f;
        [SerializeField] private bool m_StopRadioWhenLeavingCar = true;

        [Header("Generated HUD Sprites")]
        [SerializeField] private Font m_HudFont;
        [SerializeField] private Sprite m_RadioDiscSprite;
        [SerializeField] private Sprite m_RadioPowerSprite;
        [SerializeField] private Sprite m_RadioPreviousSprite;
        [SerializeField] private Sprite m_RadioPlaySprite;
        [SerializeField] private Sprite m_RadioPauseSprite;
        [SerializeField] private Sprite m_RadioNextSprite;
        [SerializeField] private Sprite m_FuelGaugeSprite;

        private static SimcadeCarDashboard s_ActiveDashboard;
        private static GameObject s_CanvasRoot;
        private static RectTransform s_SafeAreaRoot;
        private static RectTransform s_SpeedBackgroundRect;
        private static RectTransform s_SpeedRect;
        private static RectTransform s_HealthRect;
        private static RectTransform s_RadioDiscRect;
        private static RectTransform s_RadioRoot;
        private static Text s_SpeedText;
        private static Text s_RadioStatusText;
        private static Image s_RadioDiscImage;
        private static Image s_RadioPlayImage;
        private static Image s_HealthFillImage;
        private static Image s_HealthIconVertical;
        private static Image s_HealthIconHorizontal;
        private static Image s_FuelFillImage;
        private static Material s_HealthGaugeAlphaTintMaterial;
        private static bool s_RefuelPromptVisible;
        private static bool s_TelemetryVisible = true;
        private Camera m_HudCamera;
        private BoxCollider m_BodyCollider;
        private Vector3 m_VisualCenterLocal;
        private Vector3 m_MirrorCenterLocal;
        private Rect m_ChassisScreenRect;
        private bool m_HasChassisScreenRect;
        private Vector2 m_SpeedPosition;
        private Vector2 m_SpeedTargetPosition;
        private Vector2 m_SpeedVelocity;
        private Vector2 m_HealthPosition;
        private Vector2 m_HealthTargetPosition;
        private Vector2 m_HealthVelocity;
        private int m_CurrentTrackIndex;
        private int m_LastDisplayedSpeed = int.MinValue;
        private float m_NextTelemetryUpdate;
        private float m_NextWorldFollowUpdate;
        private float m_NextSafeAreaUpdate;
        private float m_FuelFillTarget;
        private float m_FuelFillVelocity;
        private bool m_HasFuelFillValue;
        private Rect m_LastSafeArea;
        private Vector2Int m_LastScreenSize;
        private bool m_IsRadioPlaying;
        private bool m_HasSpeedPosition;
        private bool m_HasHealthPosition;
        private bool m_HasFuelFillTarget;
        private bool m_WasRearViewPressed;

        public bool IsConfigured => m_Driver != null && m_Health != null && m_Fuel != null &&
            m_RadioSource != null && m_RadioTuningSource != null &&
            m_RadioTuningClip != null && m_RadioTracks != null &&
            m_RadioTracks.Length >= 3 && m_RadioTracks[0] != null &&
            m_HudFont != null &&
            m_RadioDiscSprite != null && m_RadioPowerSprite != null &&
            m_RadioPreviousSprite != null && m_RadioPlaySprite != null &&
            m_RadioPauseSprite != null && m_RadioNextSprite != null &&
            m_FuelGaugeSprite != null;
        public bool IsVisible => s_ActiveDashboard == this &&
            s_CanvasRoot != null && s_CanvasRoot.activeSelf;
        public static bool IsSharedHudActive => s_ActiveDashboard != null &&
            s_CanvasRoot != null && s_CanvasRoot.activeSelf;
        public static Font SharedHudFont => s_ActiveDashboard != null
            ? s_ActiveDashboard.m_HudFont
            : null;
        public bool IsRadioPlaying => m_IsRadioPlaying;
        public int CurrentTrackIndex => m_CurrentTrackIndex;
        public int RadioTrackCount => m_RadioTracks?.Length ?? 0;
        public float SpeedWorldLeftOffset => m_SpeedWorldLeftOffset;
        public float SpeedWorldHeight => m_SpeedWorldHeight;
        public Vector2 SpeedScreenOffset => m_SpeedScreenOffset;
        public Vector2 FuelGaugeOffset => m_FuelGaugeOffset;
        public float HealthWorldRightOffset => m_HealthWorldRightOffset;
        public float HealthWorldHeight => m_HealthWorldHeight;
        public Vector2 HealthScreenOffset => m_HealthScreenOffset;

        public static void SetRefuelPromptVisible(bool visible)
        {
            s_RefuelPromptVisible = visible;
            RefreshRadioRootVisibility();
        }

        private void Awake()
        {
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
            if (m_Health == null) m_Health = GetComponent<SimcadeCarHealth>();
            if (m_Fuel == null) m_Fuel = GetComponent<SimcadeCarFuel>();
            CacheVisualBounds();
            ConfigureAudioSources();
            BuildHud();
            SetPresentationActive(false);
        }

        private void OnEnable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged += OnHealthChanged;
            if (m_Fuel != null)
                m_Fuel.EventFuelChanged += OnFuelChanged;
        }

        private void Start()
        {
            RefreshHealth();
            RefreshFuel();
            RefreshRadioStatus();
        }

        private void OnDisable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged -= OnHealthChanged;
            if (m_Fuel != null)
                m_Fuel.EventFuelChanged -= OnFuelChanged;
            SetPresentationActive(false);
        }

        private void OnDestroy()
        {
            if (s_ActiveDashboard == this) SetPresentationActive(false);
        }

        private void Update()
        {
            if (s_ActiveDashboard != this || !IsVisible) return;

            float now = Time.unscaledTime;
            bool showTelemetry = m_Driver == null ||
                !m_Driver.IsFirstPersonViewActive;
            SetDrivingTelemetryVisible(showTelemetry);
            if (showTelemetry && now >= m_NextTelemetryUpdate)
            {
                m_NextTelemetryUpdate = now + m_UpdateInterval;
                RefreshSpeed(false);
            }

            bool rearViewChanged = m_Driver != null &&
                m_Driver.IsRearViewPressed != m_WasRearViewPressed;
            if (showTelemetry &&
                (now >= m_NextWorldFollowUpdate || !m_HasSpeedPosition ||
                 rearViewChanged))
            {
                m_NextWorldFollowUpdate = now + Mathf.Clamp(
                    m_WorldFollowInterval,
                    0.033f,
                    0.1f
                );
                UpdateSpeedWorldFollow();
                UpdateHealthWorldFollow();
                m_WasRearViewPressed = m_Driver != null &&
                    m_Driver.IsRearViewPressed;
            }
            if (showTelemetry)
            {
                SmoothWorldFollow();
                UpdateFuelFill();
            }

            if (m_IsRadioPlaying && s_RadioDiscRect != null)
                s_RadioDiscRect.Rotate(0f, 0f, -38f * Time.unscaledDeltaTime);

            if (now >= m_NextSafeAreaUpdate)
            {
                m_NextSafeAreaUpdate = now + 0.5f;
                ApplySafeArea(false);
            }
        }

        public void SetPresentationActive(bool active)
        {
            if (active)
            {
                BuildHud();
                if (s_ActiveDashboard != null && s_ActiveDashboard != this &&
                    s_ActiveDashboard.m_StopRadioWhenLeavingCar)
                {
                    s_ActiveDashboard.SetRadioEnabled(false, false);
                }

                s_ActiveDashboard = this;
                if (s_CanvasRoot != null && !s_CanvasRoot.activeSelf)
                    s_CanvasRoot.SetActive(true);
                m_HasSpeedPosition = false;
                m_HasHealthPosition = false;
                m_SpeedVelocity = Vector2.zero;
                m_HealthVelocity = Vector2.zero;
                m_NextTelemetryUpdate = 0f;
                m_NextWorldFollowUpdate = 0f;
                m_NextSafeAreaUpdate = 0f;
                m_WasRearViewPressed = m_Driver != null &&
                    m_Driver.IsRearViewPressed;
                RefreshSpeed(true);
                RefreshHealth();
                RefreshFuel();
                RefreshRadioStatus();
                RefreshRadioRootVisibility();
                ApplySafeArea(true);
                SetDrivingTelemetryVisible(
                    m_Driver == null || !m_Driver.IsFirstPersonViewActive,
                    true
                );
            }
            else
            {
                if (s_ActiveDashboard != this)
                {
                    if (s_ActiveDashboard == null && s_CanvasRoot != null)
                        s_CanvasRoot.SetActive(false);
                    return;
                }

                if (m_StopRadioWhenLeavingCar) SetRadioEnabled(false, false);
                s_ActiveDashboard = null;
                if (s_CanvasRoot != null) s_CanvasRoot.SetActive(false);
            }
        }

        public void ToggleRadio()
        {
            SetRadioEnabled(!m_IsRadioPlaying, true);
        }

        public void SetRadioEnabled(bool enabled)
        {
            SetRadioEnabled(enabled, true);
        }

        public void PreviousTrack()
        {
            ChangeTrack(-1);
        }

        public void NextTrack()
        {
            ChangeTrack(1);
        }

        public void SelectTrack(int index, bool play)
        {
            if (m_RadioTracks == null || m_RadioTracks.Length == 0) return;
            m_CurrentTrackIndex = Mathf.Clamp(index, 0, m_RadioTracks.Length - 1);
            if (play || m_IsRadioPlaying) SetRadioEnabled(true, true);
            else RefreshRadioStatus();
        }

        public void SetSpeedHudPosition(
            float worldLeftOffset,
            float worldHeight,
            Vector2 screenOffset)
        {
            m_SpeedWorldLeftOffset = Mathf.Max(0.5f, worldLeftOffset);
            m_SpeedWorldHeight = worldHeight;
            m_SpeedScreenOffset = screenOffset;
            m_HasSpeedPosition = false;
            if (IsVisible)
            {
                UpdateSpeedWorldFollow();
                UpdateHealthWorldFollow();
                SmoothWorldFollow();
            }
        }

        public void SetHealthHudPosition(
            float worldRightOffset,
            float worldHeight,
            Vector2 screenOffset)
        {
            m_HealthWorldRightOffset = Mathf.Max(0.5f, worldRightOffset);
            m_HealthWorldHeight = worldHeight;
            m_HealthScreenOffset = screenOffset;
            m_HasHealthPosition = false;
            if (IsVisible)
            {
                UpdateHealthWorldFollow();
                SmoothWorldFollow();
            }
        }

        /// <summary>
        /// Shows or hides only speed, fuel and health. Radio and driving controls
        /// remain available in FPS. The static HUD state follows the active Car.
        /// </summary>
        public void SetDrivingTelemetryVisible(bool visible)
        {
            SetDrivingTelemetryVisible(visible, false);
        }

        private void SetDrivingTelemetryVisible(bool visible, bool force)
        {
            if (s_ActiveDashboard != this) return;
            bool changed = s_TelemetryVisible != visible;
            if (!force && !changed) return;

            s_TelemetryVisible = visible;
            if (s_SpeedRect != null && s_SpeedRect.gameObject.activeSelf != visible)
                s_SpeedRect.gameObject.SetActive(visible);
            if (s_SpeedBackgroundRect != null &&
                s_SpeedBackgroundRect.gameObject.activeSelf != visible)
            {
                s_SpeedBackgroundRect.gameObject.SetActive(visible);
            }
            if (s_HealthRect != null && s_HealthRect.gameObject.activeSelf != visible)
                s_HealthRect.gameObject.SetActive(visible);

            if (!visible) return;

            m_HasSpeedPosition = false;
            m_HasHealthPosition = false;
            m_SpeedVelocity = Vector2.zero;
            m_HealthVelocity = Vector2.zero;
            m_NextTelemetryUpdate = 0f;
            m_NextWorldFollowUpdate = 0f;
            RefreshSpeed(true);
            RefreshHealth();
            RefreshFuel();
            UpdateSpeedWorldFollow();
            UpdateHealthWorldFollow();
            SmoothWorldFollow();
        }

        public void Configure(
            SimcadeCarDriver driver,
            SimcadeCarHealth health,
            SimcadeCarFuel fuel,
            AudioSource radioSource,
            AudioSource tuningSource,
            AudioClip tuningClip,
            AudioClip[] radioTracks,
            string[] stationNames,
            Font hudFont,
            Sprite radioDisc,
            Sprite radioPower,
            Sprite radioPrevious,
            Sprite radioPlay,
            Sprite radioPause,
            Sprite radioNext,
            Sprite fuelGauge)
        {
            m_Driver = driver;
            m_Health = health;
            m_Fuel = fuel;
            m_RadioSource = radioSource;
            m_RadioTuningSource = tuningSource;
            m_RadioTuningClip = tuningClip;
            m_RadioTracks = radioTracks ?? Array.Empty<AudioClip>();
            m_StationNames = stationNames ?? Array.Empty<string>();
            m_HudFont = hudFont;
            m_RadioDiscSprite = radioDisc;
            m_RadioPowerSprite = radioPower;
            m_RadioPreviousSprite = radioPrevious;
            m_RadioPlaySprite = radioPlay;
            m_RadioPauseSprite = radioPause;
            m_RadioNextSprite = radioNext;
            m_FuelGaugeSprite = fuelGauge;
            ConfigureAudioSources();
        }

        private void SetRadioEnabled(bool enabled, bool playTuning)
        {
            if (!enabled)
            {
                if (m_RadioSource != null) m_RadioSource.Stop();
                if (playTuning) PlayTuningCue();
                m_IsRadioPlaying = false;
                RefreshRadioStatus();
                return;
            }

            AudioClip clip = GetCurrentTrack();
            if (m_RadioSource == null || clip == null)
            {
                m_IsRadioPlaying = false;
                RefreshRadioStatus();
                return;
            }

            m_RadioSource.Stop();
            m_RadioSource.clip = clip;
            m_RadioSource.volume = m_RadioVolume;
            m_RadioSource.loop = true;
            if (playTuning && m_RadioTuningClip != null)
            {
                PlayTuningCue();
                m_RadioSource.PlayDelayed(0.16f);
            }
            else
            {
                m_RadioSource.Play();
            }
            m_IsRadioPlaying = true;
            RefreshRadioStatus();
        }

        private void PlayTuningCue()
        {
            if (m_RadioTuningSource == null || m_RadioTuningClip == null) return;
            m_RadioTuningSource.Stop();
            m_RadioTuningSource.PlayOneShot(m_RadioTuningClip, 0.72f);
        }

        private void ChangeTrack(int direction)
        {
            int count = m_RadioTracks?.Length ?? 0;
            if (count == 0) return;
            m_CurrentTrackIndex = (m_CurrentTrackIndex + direction + count) % count;
            SetRadioEnabled(true, true);
        }

        private AudioClip GetCurrentTrack()
        {
            int count = m_RadioTracks?.Length ?? 0;
            if (count == 0) return null;
            m_CurrentTrackIndex = Mathf.Clamp(m_CurrentTrackIndex, 0, count - 1);
            return m_RadioTracks[m_CurrentTrackIndex];
        }

        private void ConfigureAudioSources()
        {
            ConfigureAudioSource(m_RadioSource, true, m_RadioVolume, 64);
            ConfigureAudioSource(m_RadioTuningSource, false, 0.72f, 48);
        }

        private static void ConfigureAudioSource(
            AudioSource source,
            bool loop,
            float volume,
            int priority)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.priority = priority;
        }

        private void CacheVisualBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Bounds localBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; ++i)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 minimum = worldBounds.min;
                Vector3 maximum = worldBounds.max;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 worldPoint = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z
                    );
                    Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else localBounds.Encapsulate(localPoint);
                }
            }

            m_VisualCenterLocal = hasBounds ? localBounds.center : Vector3.zero;
            // The model/renderers include offset auxiliaries, while the root body
            // collider is authored around the actual visible chassis. Use its
            // stable center/edges so effects cannot pull telemetry apart.
            m_BodyCollider = GetComponent<BoxCollider>();
            m_MirrorCenterLocal = m_BodyCollider != null &&
                                  m_BodyCollider.enabled &&
                                  !m_BodyCollider.isTrigger
                ? m_BodyCollider.center
                : m_VisualCenterLocal;
        }

        private Vector3 GetHudWorldCenter(float worldHeight)
        {
            Vector3 worldCenter = transform.TransformPoint(new Vector3(
                m_VisualCenterLocal.x,
                0f,
                m_VisualCenterLocal.z
            ));
            worldCenter.y = transform.position.y + worldHeight;
            return worldCenter;
        }

        private Vector3 GetHudMirrorWorldCenter(float worldHeight)
        {
            Vector3 worldCenter = transform.TransformPoint(new Vector3(
                m_MirrorCenterLocal.x,
                0f,
                m_MirrorCenterLocal.z
            ));
            worldCenter.y = transform.position.y + worldHeight;
            return worldCenter;
        }

        private bool TryGetChassisScreenRect(out Rect screenRect)
        {
            screenRect = default;
            if (m_BodyCollider == null || !m_BodyCollider.enabled ||
                s_SafeAreaRoot == null || m_HudCamera == null)
            {
                return false;
            }

            Vector3 center = m_BodyCollider.center;
            Vector3 extents = m_BodyCollider.size * 0.5f;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            bool hasPoint = false;

            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 localCorner = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z
                );
                Vector3 screenPoint = m_HudCamera.WorldToScreenPoint(
                    m_BodyCollider.transform.TransformPoint(localCorner)
                );
                if (screenPoint.z <= 0.01f) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        s_SafeAreaRoot,
                        screenPoint,
                        null,
                        out Vector2 localPoint))
                {
                    continue;
                }

                minimumX = Mathf.Min(minimumX, localPoint.x);
                maximumX = Mathf.Max(maximumX, localPoint.x);
                minimumY = Mathf.Min(minimumY, localPoint.y);
                maximumY = Mathf.Max(maximumY, localPoint.y);
                hasPoint = true;
            }

            if (!hasPoint) return false;
            screenRect = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
            return true;
        }

        private void RefreshSpeed(bool force)
        {
            int speed = m_Driver != null ? Mathf.RoundToInt(m_Driver.SpeedKph) : 0;
            if (!force && speed == m_LastDisplayedSpeed) return;
            m_LastDisplayedSpeed = speed;
            if (s_SpeedText != null)
                s_SpeedText.text = $"{speed}<size=29> km/h</size>";
        }

        private void UpdateSpeedWorldFollow()
        {
            if (s_SpeedRect == null || s_SafeAreaRoot == null) return;

            if (m_HudCamera == null || !m_HudCamera.isActiveAndEnabled)
                m_HudCamera = Camera.main;
            if (m_HudCamera == null) return;

            Vector3 worldTarget = GetHudMirrorWorldCenter(m_SpeedWorldHeight);
            Vector3 screenPoint = m_HudCamera.WorldToScreenPoint(worldTarget);
            if (screenPoint.z <= 0.01f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    s_SafeAreaRoot,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            m_HasChassisScreenRect = TryGetChassisScreenRect(
                out m_ChassisScreenRect
            );
            if (m_HasChassisScreenRect)
            {
                float fuelBarCenterX = m_ChassisScreenRect.xMin -
                    m_TelemetrySideGap - FUEL_GAUGE_WIDTH * 0.5f;
                localPoint.x = fuelBarCenterX - m_FuelGaugeOffset.x +
                    m_SpeedScreenOffset.x;
            }
            else
            {
                localPoint.x -= Mathf.Max(
                    m_SpeedWorldLeftOffset,
                    m_HealthWorldRightOffset
                ) * 100f;
                localPoint.x += m_SpeedScreenOffset.x;
            }
            localPoint.y += m_SpeedScreenOffset.y;

            Rect safeRect = s_SafeAreaRoot.rect;
            Vector2 half = s_SpeedRect.rect.size * 0.5f;
            float leftExtent = Mathf.Max(
                half.x,
                -m_FuelGaugeOffset.x + FUEL_GAUGE_WIDTH * 0.5f
            );
            float rightExtent = Mathf.Max(
                half.x,
                m_FuelGaugeOffset.x + FUEL_GAUGE_WIDTH * 0.5f
            );
            float lowerExtent = Mathf.Max(
                half.y,
                -m_FuelGaugeOffset.y + FUEL_GAUGE_HEIGHT * 0.5f
            );
            float upperExtent = Mathf.Max(
                half.y,
                m_FuelGaugeOffset.y + FUEL_GAUGE_HEIGHT * 0.5f
            );
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                safeRect.xMin + leftExtent,
                safeRect.xMax - rightExtent
            );
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                safeRect.yMin + lowerExtent,
                safeRect.yMax - upperExtent
            );

            bool snap = !m_HasSpeedPosition ||
                (m_Driver != null &&
                 m_Driver.IsRearViewPressed != m_WasRearViewPressed);
            m_SpeedTargetPosition = localPoint;
            if (snap)
            {
                m_SpeedPosition = localPoint;
                m_SpeedVelocity = Vector2.zero;
                m_HasSpeedPosition = true;
            }
        }

        private void UpdateHealthWorldFollow()
        {
            if (s_HealthRect == null || s_SafeAreaRoot == null ||
                !m_HasSpeedPosition)
            {
                return;
            }

            if (m_HudCamera == null || !m_HudCamera.isActiveAndEnabled)
                m_HudCamera = Camera.main;
            if (m_HudCamera == null) return;

            if (!m_HasChassisScreenRect &&
                !TryGetChassisScreenRect(out m_ChassisScreenRect))
            {
                return;
            }
            m_HasChassisScreenRect = true;

            Vector2 fuelBarCenter = m_SpeedTargetPosition + m_FuelGaugeOffset;
            float healthBarCenterX = m_ChassisScreenRect.xMax +
                m_TelemetrySideGap + HEALTH_GAUGE_WIDTH * 0.5f;
            float healthBarCenterY = fuelBarCenter.y - FUEL_GAUGE_HEIGHT * 0.5f +
                HEALTH_GAUGE_HEIGHT * 0.5f;
            Vector2 localPoint = new Vector2(
                healthBarCenterX - HEALTH_GAUGE_BAR_OFFSET_X,
                healthBarCenterY - HEALTH_GAUGE_BAR_OFFSET_Y
            ) + m_HealthScreenOffset;
            Rect safeRect = s_SafeAreaRoot.rect;
            Vector2 half = s_HealthRect.rect.size * 0.5f;
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                safeRect.xMin + half.x,
                safeRect.xMax - half.x
            );
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                safeRect.yMin + half.y,
                safeRect.yMax - half.y
            );

            bool snap = !m_HasHealthPosition ||
                (m_Driver != null &&
                 m_Driver.IsRearViewPressed != m_WasRearViewPressed);
            m_HealthTargetPosition = localPoint;
            if (snap)
            {
                m_HealthPosition = localPoint;
                m_HealthVelocity = Vector2.zero;
                m_HasHealthPosition = true;
            }
        }

        private void SmoothWorldFollow()
        {
            if (m_HasSpeedPosition && s_SpeedRect != null)
            {
                m_SpeedPosition = Vector2.SmoothDamp(
                    m_SpeedPosition,
                    m_SpeedTargetPosition,
                    ref m_SpeedVelocity,
                    m_SpeedFollowSmooth,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
                if ((s_SpeedRect.anchoredPosition - m_SpeedPosition).sqrMagnitude >
                    0.0625f)
                {
                    s_SpeedRect.anchoredPosition = m_SpeedPosition;
                }

                Vector2 backgroundPosition =
                    m_SpeedPosition + SPEED_BACKGROUND_OFFSET;
                if (s_SpeedBackgroundRect != null &&
                    (s_SpeedBackgroundRect.anchoredPosition -
                     backgroundPosition).sqrMagnitude > 0.0625f)
                {
                    s_SpeedBackgroundRect.anchoredPosition =
                        backgroundPosition;
                }
            }

            if (!m_HasHealthPosition || s_HealthRect == null) return;

            m_HealthPosition = Vector2.SmoothDamp(
                m_HealthPosition,
                m_HealthTargetPosition,
                ref m_HealthVelocity,
                m_SpeedFollowSmooth,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
            if ((s_HealthRect.anchoredPosition - m_HealthPosition).sqrMagnitude >
                0.0625f)
            {
                s_HealthRect.anchoredPosition = m_HealthPosition;
            }
        }

        private void RefreshHealth()
        {
            float current = m_Health != null ? m_Health.CurrentHealth : 0f;
            float maximum = m_Health != null ? m_Health.MaximumHealth : 0f;
            OnHealthChanged(current, maximum);
        }

        private void OnHealthChanged(float current, float maximum)
        {
            if (s_ActiveDashboard != this) return;
            float ratio = maximum > 0.001f ? Mathf.Clamp01(current / maximum) : 0f;
            if (s_HealthFillImage != null)
            {
                s_HealthFillImage.fillAmount = ratio;
                s_HealthFillImage.color = HEALTH_SKY_BLUE;
                if (s_HealthIconVertical != null)
                    s_HealthIconVertical.color = HEALTH_SKY_BLUE;
                if (s_HealthIconHorizontal != null)
                    s_HealthIconHorizontal.color = HEALTH_SKY_BLUE;
            }
        }

        private void RefreshFuel()
        {
            float current = m_Fuel != null ? m_Fuel.CurrentFuel : 0f;
            float maximum = m_Fuel != null ? m_Fuel.MaximumFuel : 0f;
            OnFuelChanged(current, maximum);
        }

        private void OnFuelChanged(float current, float maximum)
        {
            if (s_ActiveDashboard != this) return;
            float ratio = maximum > 0.001f ? Mathf.Clamp01(current / maximum) : 0f;
            m_FuelFillTarget = ratio;
            if (!m_HasFuelFillValue)
            {
                m_HasFuelFillValue = true;
                m_HasFuelFillTarget = false;
                m_FuelFillVelocity = 0f;
                if (s_FuelFillImage != null) s_FuelFillImage.fillAmount = ratio;
                return;
            }
            m_HasFuelFillTarget = true;
        }

        private void UpdateFuelFill()
        {
            if (!m_HasFuelFillTarget || s_FuelFillImage == null) return;

            s_FuelFillImage.fillAmount = Mathf.SmoothDamp(
                s_FuelFillImage.fillAmount,
                m_FuelFillTarget,
                ref m_FuelFillVelocity,
                0.22f,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
            if (Mathf.Abs(s_FuelFillImage.fillAmount - m_FuelFillTarget) < 0.0005f)
            {
                s_FuelFillImage.fillAmount = m_FuelFillTarget;
                m_FuelFillVelocity = 0f;
                m_HasFuelFillTarget = false;
            }
        }

        private void RefreshRadioStatus()
        {
            if (s_ActiveDashboard != this) return;
            AudioClip clip = GetCurrentTrack();
            string station = m_CurrentTrackIndex < (m_StationNames?.Length ?? 0)
                ? m_StationNames[m_CurrentTrackIndex]
                : clip != null ? clip.name : "No station";
            if (s_RadioStatusText != null)
            {
                string state = m_IsRadioPlaying
                    ? "<color=#2FE5FF>PLAYING</color>"
                    : "<color=#9AA5B1>PAUSED</color>";
                s_RadioStatusText.text =
                    $"<size=14><color=#29B8FF>RADIO</color>  •  {state}</size>\n{station}";
            }
            if (s_RadioDiscImage != null)
                s_RadioDiscImage.color = m_IsRadioPlaying ? RADIO_ACTIVE : RADIO_INACTIVE;
            if (s_RadioPlayImage != null)
            {
                s_RadioPlayImage.sprite = m_IsRadioPlaying
                    ? m_RadioPauseSprite ?? m_RadioPlaySprite
                    : m_RadioPlaySprite;
                s_RadioPlayImage.color = RADIO_ACTIVE;
            }
        }

        private static void RefreshRadioRootVisibility()
        {
            if (s_RadioRoot != null)
                s_RadioRoot.gameObject.SetActive(!s_RefuelPromptVisible);
        }

        private void BuildHud()
        {
            if (s_CanvasRoot != null) return;

            s_CanvasRoot = new GameObject(
                "Sim-Cade Shared Car Dashboard",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            DontDestroyOnLoad(s_CanvasRoot);
            Canvas canvas = s_CanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1210;

            CanvasScaler scaler = s_CanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            s_SafeAreaRoot = CreateRect(
                "Safe Area",
                s_CanvasRoot.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero
            );
            s_SafeAreaRoot.offsetMin = Vector2.zero;
            s_SafeAreaRoot.offsetMax = Vector2.zero;

            Image speedBackground = CreateSolidImage(
                "Speed Background - Soft Square",
                s_SafeAreaRoot,
                SPEED_BACKGROUND_OFFSET,
                new Vector2(176f, 48f),
                SPEED_BACKGROUND_COLOR
            );
            s_SpeedBackgroundRect = speedBackground.rectTransform;
            Outline speedBorder = speedBackground.gameObject.AddComponent<Outline>();
            speedBorder.effectColor = SPEED_BACKGROUND_BORDER_COLOR;
            speedBorder.effectDistance = new Vector2(2f, -2f);
            speedBorder.useGraphicAlpha = true;

            s_SpeedText = CreateText(
                "Speed",
                s_SafeAreaRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(286f, 78f),
                56,
                TextAnchor.MiddleCenter,
                FontStyle.Normal,
                m_HudFont
            );
            s_SpeedRect = s_SpeedText.rectTransform;
            Shadow speedShadow = s_SpeedText.gameObject.AddComponent<Shadow>();
            speedShadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
            speedShadow.effectDistance = new Vector2(1f, -1f);

            BuildFuelGauge();
            BuildHealthGauge();
            s_TelemetryVisible = true;

            RectTransform radioRoot = CreateRect(
                "Radio Controls",
                s_SafeAreaRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f),
                new Vector2(620f, 96f)
            );
            s_RadioRoot = radioRoot;
            Image radioPanel = radioRoot.gameObject.AddComponent<Image>();
            radioPanel.color = RADIO_PANEL;
            radioPanel.raycastTarget = false;
            Outline radioOutline = radioRoot.gameObject.AddComponent<Outline>();
            radioOutline.effectColor = new Color(0.16f, 0.72f, 1f, 0.42f);
            radioOutline.effectDistance = new Vector2(2f, -2f);
            radioOutline.useGraphicAlpha = true;

            CreateSolidImage(
                "Radio Accent",
                radioRoot,
                new Vector2(0f, -46f),
                new Vector2(590f, 3f),
                RADIO_ACCENT
            );

            s_RadioDiscImage = CreateSpriteImage(
                "Spinning Radio Disc",
                radioRoot,
                new Vector2(-270f, 0f),
                new Vector2(74f, 74f),
                m_RadioDiscSprite
            );
            s_RadioDiscRect = s_RadioDiscImage.rectTransform;

            s_RadioStatusText = CreateText(
                "Station",
                radioRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-220f, 0f),
                new Vector2(205f, 70f),
                23,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                m_HudFont
            );
            Shadow radioTextShadow = s_RadioStatusText.gameObject.AddComponent<Shadow>();
            radioTextShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            radioTextShadow.effectDistance = new Vector2(1f, -1f);

            CreateSolidImage(
                "Radio Divider",
                radioRoot,
                new Vector2(-10f, 0f),
                new Vector2(2f, 54f),
                new Color(0.24f, 0.72f, 0.9f, 0.34f)
            );
            CreateSpriteButton(
                "Previous",
                radioRoot,
                new Vector2(65f, 0f),
                m_RadioPreviousSprite,
                PreviousSharedTrack
            );
            s_RadioPlayImage = CreateSpriteButton(
                "Play Pause",
                radioRoot,
                new Vector2(160f, 0f),
                m_RadioPlaySprite,
                ToggleSharedRadio
            );
            CreateSpriteButton(
                "Next",
                radioRoot,
                new Vector2(255f, 0f),
                m_RadioNextSprite,
                NextSharedTrack
            );

            EnsureEventSystem();
            ApplySafeArea(true);
            RefreshRadioRootVisibility();
            s_CanvasRoot.SetActive(false);
        }

        private void BuildFuelGauge()
        {
            RectTransform fuelRoot = CreateRect(
                "Fuel - Curved Vertical",
                s_SpeedRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                m_FuelGaugeOffset,
                new Vector2(FUEL_GAUGE_WIDTH, FUEL_GAUGE_HEIGHT)
            );

            Image background = CreateSpriteImage(
                "Fuel Arc Background - ImageGen",
                fuelRoot,
                Vector2.zero,
                new Vector2(FUEL_GAUGE_WIDTH, FUEL_GAUGE_HEIGHT),
                m_FuelGaugeSprite
            );
            background.color = new Color(0.12f, 0.13f, 0.15f, 0.42f);
            background.transform.SetAsFirstSibling();

            s_FuelFillImage = CreateSpriteImage(
                "Fuel Arc Fill - ImageGen Yellow",
                fuelRoot,
                Vector2.zero,
                new Vector2(FUEL_GAUGE_WIDTH, FUEL_GAUGE_HEIGHT),
                m_FuelGaugeSprite
            );
            s_FuelFillImage.type = Image.Type.Filled;
            s_FuelFillImage.fillMethod = Image.FillMethod.Vertical;
            s_FuelFillImage.fillOrigin = 0;
            s_FuelFillImage.fillAmount = 1f;
            s_FuelFillImage.fillClockwise = true;

            Text fullLabel = CreateText(
                "Full",
                fuelRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(29f, 59f),
                new Vector2(24f, 22f),
                15,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                m_HudFont
            );
            fullLabel.text = "F";
            fullLabel.color = new Color(1f, 1f, 1f, 0.78f);

            Text emptyLabel = CreateText(
                "Empty",
                fuelRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(29f, -59f),
                new Vector2(24f, 22f),
                15,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                m_HudFont
            );
            emptyLabel.text = "E";
            emptyLabel.color = new Color(1f, 1f, 1f, 0.62f);
        }

        private void BuildHealthGauge()
        {
            s_HealthRect = CreateRect(
                "Health - Mirrored Curved Vertical",
                s_SafeAreaRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(HEALTH_GAUGE_LAYOUT_WIDTH, HEALTH_GAUGE_LAYOUT_HEIGHT)
            );

            Image background = CreateSpriteImage(
                "Health Arc Background - Mirrored ImageGen",
                s_HealthRect,
                new Vector2(HEALTH_GAUGE_BAR_OFFSET_X, HEALTH_GAUGE_BAR_OFFSET_Y),
                new Vector2(HEALTH_GAUGE_WIDTH, HEALTH_GAUGE_HEIGHT),
                m_FuelGaugeSprite
            );
            background.preserveAspect = false;
            background.color = new Color(0.12f, 0.13f, 0.15f, 0.42f);
            background.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            background.transform.SetAsFirstSibling();

            s_HealthFillImage = CreateSpriteImage(
                "Health Arc Fill - Mirrored",
                s_HealthRect,
                new Vector2(HEALTH_GAUGE_BAR_OFFSET_X, HEALTH_GAUGE_BAR_OFFSET_Y),
                new Vector2(HEALTH_GAUGE_WIDTH, HEALTH_GAUGE_HEIGHT),
                m_FuelGaugeSprite
            );
            s_HealthFillImage.preserveAspect = false;
            s_HealthFillImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            s_HealthFillImage.type = Image.Type.Filled;
            s_HealthFillImage.fillMethod = Image.FillMethod.Vertical;
            s_HealthFillImage.fillOrigin = 0;
            s_HealthFillImage.fillAmount = 1f;
            s_HealthFillImage.fillClockwise = true;
            s_HealthFillImage.color = HEALTH_SKY_BLUE;
            s_HealthFillImage.material = GetHealthGaugeAlphaTintMaterial();

            RectTransform iconRoot = CreateRect(
                "Car Health Icon",
                s_HealthRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(HEALTH_GAUGE_BAR_OFFSET_X, 91f),
                new Vector2(34f, 34f)
            );
            s_HealthIconVertical = CreateSolidImage(
                "Health Icon Vertical",
                iconRoot,
                Vector2.zero,
                new Vector2(9f, 30f),
                HEALTH_SKY_BLUE
            );
            s_HealthIconHorizontal = CreateSolidImage(
                "Health Icon Horizontal",
                iconRoot,
                Vector2.zero,
                new Vector2(30f, 9f),
                HEALTH_SKY_BLUE
            );
        }

        private static Material GetHealthGaugeAlphaTintMaterial()
        {
            if (s_HealthGaugeAlphaTintMaterial != null)
                return s_HealthGaugeAlphaTintMaterial;

            Shader shader = Shader.Find(HEALTH_TINT_SHADER);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"Car health HUD could not find shader '{HEALTH_TINT_SHADER}'."
                );
                return null;
            }

            s_HealthGaugeAlphaTintMaterial = new Material(shader)
            {
                name = "Franklin Car Health Gauge Alpha Tint (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return s_HealthGaugeAlphaTintMaterial;
        }

        private static void ToggleSharedRadio()
        {
            if (s_ActiveDashboard != null) s_ActiveDashboard.ToggleRadio();
        }

        private static void PreviousSharedTrack()
        {
            if (s_ActiveDashboard != null) s_ActiveDashboard.PreviousTrack();
        }

        private static void NextSharedTrack()
        {
            if (s_ActiveDashboard != null) s_ActiveDashboard.NextTrack();
        }

        private static Image CreateSolidImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size
            );
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateSpriteButton(
            string name,
            Transform parent,
            Vector2 position,
            Sprite sprite,
            UnityEngine.Events.UnityAction callback)
        {
            Image image = CreateSpriteImage(name, parent, position, new Vector2(88f, 88f), sprite);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.76f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            return image;
        }

        private static Image CreateSpriteImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite sprite)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size
            );
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            FontStyle style,
            Font font)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                pivot,
                position,
                size
            );
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void ApplySafeArea(bool force)
        {
            if (s_SafeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == m_LastSafeArea && screenSize == m_LastScreenSize) return;

            m_LastSafeArea = safeArea;
            m_LastScreenSize = screenSize;
            Vector2 minimum = safeArea.position;
            Vector2 maximum = safeArea.position + safeArea.size;
            minimum.x /= Screen.width;
            minimum.y /= Screen.height;
            maximum.x /= Screen.width;
            maximum.y /= Screen.height;
            s_SafeAreaRoot.anchorMin = minimum;
            s_SafeAreaRoot.anchorMax = maximum;
            s_SafeAreaRoot.offsetMin = Vector2.zero;
            s_SafeAreaRoot.offsetMax = Vector2.zero;
            m_HasSpeedPosition = false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystem = new GameObject(
                "EventSystem (Car Dashboard)",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            DontDestroyOnLoad(eventSystem);
        }

        private void OnValidate()
        {
            m_UpdateInterval = Mathf.Clamp(m_UpdateInterval, 0.05f, 0.5f);
            m_WorldFollowInterval = Mathf.Clamp(
                m_WorldFollowInterval,
                0.033f,
                0.1f
            );
            m_RadioVolume = Mathf.Clamp01(m_RadioVolume);
            m_SpeedWorldLeftOffset = Mathf.Max(0.5f, m_SpeedWorldLeftOffset);
            m_HealthWorldRightOffset = Mathf.Max(0.5f, m_HealthWorldRightOffset);
            m_SpeedFollowSmooth = Mathf.Clamp(m_SpeedFollowSmooth, 0.03f, 0.3f);
            m_TelemetrySideGap = Mathf.Max(0f, m_TelemetrySideGap);
        }
    }
}
