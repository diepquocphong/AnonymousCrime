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
    [RequireComponent(typeof(SimcadeCarDriver), typeof(SimcadeCarHealth))]
    public sealed class SimcadeCarDashboard : MonoBehaviour
    {
        private static readonly Color HEALTH_GREEN = new Color(0.55f, 0.96f, 0.16f, 1f);
        private static readonly Color HEALTH_YELLOW = new Color(1f, 0.68f, 0.05f, 1f);
        private static readonly Color HEALTH_RED = new Color(0.96f, 0.08f, 0.08f, 1f);
        private static readonly Color RADIO_ACTIVE = Color.white;
        private static readonly Color RADIO_INACTIVE = new Color(0.58f, 0.6f, 0.64f, 0.9f);
        private const float HEALTH_FILL_WIDTH = 732f;

        [Header("Telemetry")]
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private SimcadeCarHealth m_Health;
        [SerializeField, Range(0.05f, 0.5f)] private float m_UpdateInterval = 0.1f;

        [Header("Speed Position (Editable)")]
        [Tooltip("Khoảng cách world-space sang trái thân xe.")]
        [SerializeField, Min(0.5f)] private float m_SpeedWorldLeftOffset = 2.35f;
        [Tooltip("Độ cao world-space của target; 0.82 nằm ngang tầm kính sedan.")]
        [SerializeField] private float m_SpeedWorldHeight = 0.82f;
        [Tooltip("Tinh chỉnh cuối theo pixel Canvas sau khi project world target.")]
        [SerializeField] private Vector2 m_SpeedScreenOffset = Vector2.zero;
        [SerializeField, Range(0.03f, 0.3f)] private float m_SpeedFollowSmooth = 0.11f;

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
        [SerializeField] private Sprite m_RadioNextSprite;
        [SerializeField] private Sprite m_HealthFrameSprite;

        private GameObject m_CanvasRoot;
        private RectTransform m_SafeAreaRoot;
        private RectTransform m_SpeedRect;
        private RectTransform m_RadioDiscRect;
        private RectTransform m_HealthFill;
        private Text m_SpeedText;
        private Text m_RadioStatusText;
        private Image m_RadioDiscImage;
        private Image m_RadioPowerImage;
        private Image m_RadioPlayImage;
        private Image m_HealthFillImage;
        private Camera m_HudCamera;
        private Vector2 m_SpeedPosition;
        private Vector2 m_SpeedVelocity;
        private int m_CurrentTrackIndex;
        private int m_LastDisplayedSpeed = int.MinValue;
        private float m_NextTelemetryUpdate;
        private float m_NextSafeAreaUpdate;
        private Rect m_LastSafeArea;
        private Vector2Int m_LastScreenSize;
        private bool m_IsRadioPlaying;
        private bool m_HasSpeedPosition;

        public bool IsConfigured => m_Driver != null && m_Health != null &&
            m_RadioSource != null && m_RadioTuningSource != null &&
            m_RadioTuningClip != null && m_RadioTracks != null &&
            m_RadioTracks.Length >= 3 && m_RadioTracks[0] != null &&
            m_HudFont != null &&
            m_RadioDiscSprite != null && m_RadioPowerSprite != null &&
            m_RadioPreviousSprite != null && m_RadioPlaySprite != null &&
            m_RadioNextSprite != null && m_HealthFrameSprite != null;
        public bool IsVisible => m_CanvasRoot != null && m_CanvasRoot.activeSelf;
        public bool IsRadioPlaying => m_IsRadioPlaying;
        public int CurrentTrackIndex => m_CurrentTrackIndex;
        public int RadioTrackCount => m_RadioTracks?.Length ?? 0;
        public float SpeedWorldLeftOffset => m_SpeedWorldLeftOffset;
        public float SpeedWorldHeight => m_SpeedWorldHeight;
        public Vector2 SpeedScreenOffset => m_SpeedScreenOffset;

        private void Awake()
        {
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
            if (m_Health == null) m_Health = GetComponent<SimcadeCarHealth>();
            ConfigureAudioSources();
            BuildHud();
            SetPresentationActive(false);
        }

        private void OnEnable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged += OnHealthChanged;
        }

        private void Start()
        {
            RefreshHealth();
            RefreshRadioStatus();
        }

        private void OnDisable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged -= OnHealthChanged;
            SetPresentationActive(false);
        }

        private void OnDestroy()
        {
            if (m_CanvasRoot != null) Destroy(m_CanvasRoot);
        }

        private void Update()
        {
            if (!IsVisible) return;

            float now = Time.unscaledTime;
            if (now >= m_NextTelemetryUpdate)
            {
                m_NextTelemetryUpdate = now + m_UpdateInterval;
                RefreshSpeed(false);
            }

            UpdateSpeedWorldFollow();

            if (m_IsRadioPlaying && m_RadioDiscRect != null)
                m_RadioDiscRect.Rotate(0f, 0f, -38f * Time.unscaledDeltaTime);

            if (now >= m_NextSafeAreaUpdate)
            {
                m_NextSafeAreaUpdate = now + 0.5f;
                ApplySafeArea(false);
            }
        }

        public void SetPresentationActive(bool active)
        {
            if (m_CanvasRoot == null) BuildHud();
            if (m_CanvasRoot != null && m_CanvasRoot.activeSelf != active)
                m_CanvasRoot.SetActive(active);

            if (active)
            {
                m_HasSpeedPosition = false;
                m_SpeedVelocity = Vector2.zero;
                m_NextTelemetryUpdate = 0f;
                m_NextSafeAreaUpdate = 0f;
                RefreshSpeed(true);
                RefreshHealth();
                RefreshRadioStatus();
                ApplySafeArea(true);
                UpdateSpeedWorldFollow();
            }
            else if (m_StopRadioWhenLeavingCar)
            {
                SetRadioEnabled(false, false);
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
            if (IsVisible) UpdateSpeedWorldFollow();
        }

        public void Configure(
            SimcadeCarDriver driver,
            SimcadeCarHealth health,
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
            Sprite radioNext,
            Sprite healthFrame)
        {
            m_Driver = driver;
            m_Health = health;
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
            m_RadioNextSprite = radioNext;
            m_HealthFrameSprite = healthFrame;
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

        private void RefreshSpeed(bool force)
        {
            int speed = m_Driver != null ? Mathf.RoundToInt(m_Driver.SpeedKph) : 0;
            if (!force && speed == m_LastDisplayedSpeed) return;
            m_LastDisplayedSpeed = speed;
            if (m_SpeedText != null)
                m_SpeedText.text = $"{speed}<size=29> km/h</size>";
        }

        private void UpdateSpeedWorldFollow()
        {
            if (m_SpeedRect == null || m_SafeAreaRoot == null) return;

            if (m_HudCamera == null || !m_HudCamera.isActiveAndEnabled)
                m_HudCamera = Camera.main;
            if (m_HudCamera == null) return;

            Vector3 worldTarget = transform.position + Vector3.up * m_SpeedWorldHeight -
                m_HudCamera.transform.right * m_SpeedWorldLeftOffset;
            Vector3 screenPoint = m_HudCamera.WorldToScreenPoint(worldTarget);
            if (screenPoint.z <= 0.01f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_SafeAreaRoot,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            localPoint += m_SpeedScreenOffset;

            Rect safeRect = m_SafeAreaRoot.rect;
            Vector2 half = m_SpeedRect.rect.size * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, safeRect.xMin + half.x, safeRect.xMax - half.x);
            localPoint.y = Mathf.Clamp(localPoint.y, safeRect.yMin + half.y, safeRect.yMax - half.y);

            if (!m_HasSpeedPosition)
            {
                m_SpeedPosition = localPoint;
                m_HasSpeedPosition = true;
            }
            else
            {
                m_SpeedPosition = Vector2.SmoothDamp(
                    m_SpeedPosition,
                    localPoint,
                    ref m_SpeedVelocity,
                    m_SpeedFollowSmooth,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }
            m_SpeedRect.anchoredPosition = m_SpeedPosition;
        }

        private void RefreshHealth()
        {
            float current = m_Health != null ? m_Health.CurrentHealth : 0f;
            float maximum = m_Health != null ? m_Health.MaximumHealth : 0f;
            OnHealthChanged(current, maximum);
        }

        private void OnHealthChanged(float current, float maximum)
        {
            float ratio = maximum > 0.001f ? Mathf.Clamp01(current / maximum) : 0f;
            if (m_HealthFill != null)
            {
                if (m_HealthFill.gameObject.activeSelf != (ratio > 0.001f))
                    m_HealthFill.gameObject.SetActive(ratio > 0.001f);
                m_HealthFill.sizeDelta = new Vector2(
                    HEALTH_FILL_WIDTH * ratio,
                    m_HealthFill.sizeDelta.y
                );
            }
            if (m_HealthFillImage != null)
            {
                m_HealthFillImage.color = ratio > 0.5f
                    ? Color.Lerp(HEALTH_YELLOW, HEALTH_GREEN, (ratio - 0.5f) * 2f)
                    : Color.Lerp(HEALTH_RED, HEALTH_YELLOW, ratio * 2f);
            }
        }

        private void RefreshRadioStatus()
        {
            AudioClip clip = GetCurrentTrack();
            string station = m_CurrentTrackIndex < (m_StationNames?.Length ?? 0)
                ? m_StationNames[m_CurrentTrackIndex]
                : clip != null ? clip.name : "No station";
            if (m_RadioStatusText != null)
                m_RadioStatusText.text = m_IsRadioPlaying ? station : "Off";
            if (m_RadioDiscImage != null)
                m_RadioDiscImage.color = m_IsRadioPlaying ? RADIO_ACTIVE : RADIO_INACTIVE;
            if (m_RadioPowerImage != null)
                m_RadioPowerImage.color = m_IsRadioPlaying ? RADIO_ACTIVE : RADIO_INACTIVE;
            if (m_RadioPlayImage != null)
                m_RadioPlayImage.color = m_IsRadioPlaying ? RADIO_ACTIVE : RADIO_INACTIVE;
        }

        private void BuildHud()
        {
            if (m_CanvasRoot != null) return;

            m_CanvasRoot = new GameObject(
                "Sim-Cade Car Dashboard",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            Canvas canvas = m_CanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1210;

            CanvasScaler scaler = m_CanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            m_SafeAreaRoot = CreateRect(
                "Safe Area",
                m_CanvasRoot.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero
            );
            m_SafeAreaRoot.offsetMin = Vector2.zero;
            m_SafeAreaRoot.offsetMax = Vector2.zero;

            m_SpeedText = CreateText(
                "Speed",
                m_SafeAreaRoot,
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
            m_SpeedRect = m_SpeedText.rectTransform;
            Shadow speedShadow = m_SpeedText.gameObject.AddComponent<Shadow>();
            speedShadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
            speedShadow.effectDistance = new Vector2(1f, -1f);

            RectTransform radioRoot = CreateRect(
                "Radio Controls - No Background",
                m_SafeAreaRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 126f),
                new Vector2(820f, 116f)
            );

            m_RadioDiscImage = CreateSpriteImage(
                "Spinning Radio Disc",
                radioRoot,
                new Vector2(-350f, 0f),
                new Vector2(108f, 108f),
                m_RadioDiscSprite
            );
            m_RadioDiscRect = m_RadioDiscImage.rectTransform;

            m_RadioStatusText = CreateText(
                "Station",
                radioRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-282f, 0f),
                new Vector2(210f, 90f),
                31,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                m_HudFont
            );
            Shadow radioTextShadow = m_RadioStatusText.gameObject.AddComponent<Shadow>();
            radioTextShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            radioTextShadow.effectDistance = new Vector2(1f, -1f);

            m_RadioPowerImage = CreateSpriteButton(
                "Power",
                radioRoot,
                new Vector2(-36f, 0f),
                m_RadioPowerSprite,
                ToggleRadio
            );
            CreateSpriteButton(
                "Previous",
                radioRoot,
                new Vector2(66f, 0f),
                m_RadioPreviousSprite,
                PreviousTrack
            );
            m_RadioPlayImage = CreateSpriteButton(
                "Play",
                radioRoot,
                new Vector2(168f, 0f),
                m_RadioPlaySprite,
                ToggleRadio
            );
            CreateSpriteButton(
                "Next",
                radioRoot,
                new Vector2(270f, 0f),
                m_RadioNextSprite,
                NextTrack
            );

            RectTransform healthRoot = CreateRect(
                "Car Health - No Background",
                m_SafeAreaRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 48f),
                new Vector2(760f, 74f)
            );

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            m_HealthFill = fillObject.GetComponent<RectTransform>();
            m_HealthFill.SetParent(healthRoot, false);
            m_HealthFill.anchorMin = new Vector2(0f, 0.5f);
            m_HealthFill.anchorMax = new Vector2(0f, 0.5f);
            m_HealthFill.pivot = new Vector2(0f, 0.5f);
            m_HealthFill.anchoredPosition = new Vector2(14f, 0f);
            m_HealthFill.sizeDelta = new Vector2(HEALTH_FILL_WIDTH, 38f);
            m_HealthFillImage = fillObject.GetComponent<Image>();
            m_HealthFillImage.color = HEALTH_GREEN;
            m_HealthFillImage.raycastTarget = false;

            CreateSpriteImage(
                "Generated Health Frame",
                healthRoot,
                Vector2.zero,
                new Vector2(760f, 74f),
                m_HealthFrameSprite
            );

            EnsureEventSystem();
            ApplySafeArea(true);
            RefreshRadioStatus();
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
            if (m_SafeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
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
            m_SafeAreaRoot.anchorMin = minimum;
            m_SafeAreaRoot.anchorMax = maximum;
            m_SafeAreaRoot.offsetMin = Vector2.zero;
            m_SafeAreaRoot.offsetMax = Vector2.zero;
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
            m_RadioVolume = Mathf.Clamp01(m_RadioVolume);
            m_SpeedWorldLeftOffset = Mathf.Max(0.5f, m_SpeedWorldLeftOffset);
            m_SpeedFollowSmooth = Mathf.Clamp(m_SpeedFollowSmooth, 0.03f, 0.3f);
        }
    }
}
