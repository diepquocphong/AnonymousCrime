using System;
using System.Collections;
using System.Collections.Generic;
using FranklinGame.Animations;
using FranklinGame.UI;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.PhoneSystem
{
    /// <summary>
    /// Owns the standalone in-game phone Canvas and listens to the existing
    /// minimap phone button without changing that button's layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinPhoneSystem : MonoBehaviour
    {
        private const string RESOURCE_PATH = "FranklinPhoneSystem";
        private const float TRANSITION_DURATION = 0.22f;
        private const float CLOSED_EDGE_PADDING = 30f;

        [SerializeField] private GameObject m_InterfaceRoot;
        [SerializeField] private RectTransform m_SafeAreaRoot;
        [SerializeField] private RectTransform m_PhonePanel;
        [SerializeField] private GameObject m_HomeScreen;
        [SerializeField] private GameObject[] m_AppScreens = Array.Empty<GameObject>();
        [SerializeField] private Button[] m_AppButtons = Array.Empty<Button>();
        [SerializeField] private Button m_HomeButton;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_BottomCloseButton;
        [SerializeField] private Text m_TimeText;
        [SerializeField] private Text m_DateText;
        [SerializeField] private Text m_HeaderTitleText;
        [SerializeField] private Text m_HeaderSubtitleText;
        [Header("Dial Pad")]
        [SerializeField] private Text m_DialNumberText;
        [SerializeField] private Text m_DialStatusText;
        [SerializeField] private Button[] m_DialKeyButtons = Array.Empty<Button>();
        [SerializeField] private Text[] m_DialKeyLabels = Array.Empty<Text>();
        [SerializeField] private Button m_DialDeleteButton;
        [SerializeField] private Button m_DialCallButton;
        [SerializeField] private Button m_DialModeButton;
        [SerializeField] private Text m_DialModeButtonText;
        [SerializeField] private GameObject m_DialKeypadPanel;
        [SerializeField] private GameObject m_CallHistoryPanel;
        [SerializeField] private Button m_DialKeypadTabButton;
        [SerializeField] private Button m_CallHistoryTabButton;
        [SerializeField] private Text m_CallHistoryText;
        [SerializeField] private Button m_ClearCallHistoryButton;
        [Header("T9 Text Entry")]
        [SerializeField] private Text m_TextEntryText;
        [SerializeField] private Text m_TextStatusText;
        [SerializeField] private Button[] m_TextKeyButtons = Array.Empty<Button>();
        [SerializeField] private Button m_TextDeleteButton;
        [SerializeField] private Button m_TextSendButton;
        [Header("Mission Messages")]
        [SerializeField] private GameObject m_MessageListPanel;
        [SerializeField] private GameObject m_MessageDetailPanel;
        [SerializeField] private Button[] m_MessageButtons = Array.Empty<Button>();
        [SerializeField] private Text m_MessageSenderText;
        [SerializeField] private Text m_MessageBodyText;
        [SerializeField] private Text m_MessageResultText;
        [SerializeField] private Button m_MessageAcceptButton;
        [SerializeField] private Button m_MessageCancelButton;
        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float m_SoundVolume = 0.7f;
        [SerializeField] private AudioClip m_PhoneOpenSound;
        [SerializeField] private AudioClip m_PhoneCloseSound;
        [SerializeField] private AudioClip m_AppOpenSound;
        [SerializeField] private AudioClip m_AppBackSound;
        [SerializeField] private AudioClip m_MessageOpenSound;
        [SerializeField] private AudioClip[] m_KeySounds = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip m_ModeSwitchSound;
        [SerializeField] private AudioClip m_DeleteSound;
        [SerializeField] private AudioClip m_CallSound;
        [SerializeField] private AudioClip m_SuccessSound;
        [SerializeField] private AudioClip m_ErrorSound;
        [SerializeField] private AudioClip m_HistoryClearSound;
        [SerializeField] private AudioClip m_MissionCancelSound;
        [Tooltip("Runtime reads the open position directly from Phone Device RectTransform. This value is retained for prefab compatibility.")]
        [SerializeField, HideInInspector]
        private Vector2 m_OpenPosition = new(-433f, -48f);
        [Tooltip("Runtime calculates the closed X position outside the right screen edge. This value is retained for prefab compatibility.")]
        [SerializeField, HideInInspector]
        private Vector2 m_ClosedPosition = new(250f, -48f);

        private static FranklinPhoneSystem s_Instance;
        private FranklinMobileHud m_Hud;
        private AudioSource m_AudioSource;
        private FranklinPhoneHandPresentation m_HandPresentation;
        private FranklinPhoneSelfieCamera m_SelfieCamera;
        private FranklinAnimationBridge m_PlayerAnimationBridge;
        private GameObject m_IdleSuppressedPlayer;
        private Coroutine m_Transition;
        private Rect m_LastSafeArea;
        private Vector2Int m_LastScreenSize;
        private float m_NextClockRefresh;
        private float m_NextHudSearch;
        private bool m_IsOpen;
        private string m_DialNumber = string.Empty;
        private bool m_DialTextMode;
        private int m_LastDialT9Key = -1;
        private int m_LastDialT9Character;
        private float m_LastDialT9PressTime = -10f;
        private string m_TextEntry = string.Empty;
        private int m_LastT9Key = -1;
        private int m_LastT9Character;
        private float m_LastT9PressTime = -10f;
        private int m_SelectedMessage = -1;
        private int m_CurrentApp = -1;

        private const int MAX_DIAL_LENGTH = 18;
        private const int MAX_CALL_HISTORY = 8;
        private const int MAX_TEXT_LENGTH = 72;
        private const float T9_MULTITAP_WINDOW = 0.85f;
        private const string CALL_HISTORY_PREFS_KEY =
            "FranklinGame.PhoneSystem.CallHistory.v1";

        private static readonly string[] DIAL_KEYS =
        {
            "1", "2", "3", "4", "5", "6",
            "7", "8", "9", "*", "0", "#"
        };

        private static readonly string[] DIAL_NUMBER_LABELS =
        {
            "1", "2", "3", "4", "5", "6",
            "7", "8", "9", "*", "0", "#"
        };

        private static readonly string[] DIAL_TEXT_LABELS =
        {
            ".,?", "ABC", "DEF", "GHI", "JKL", "MNO",
            "PQRS", "TUV", "WXYZ", "+", "SPACE", "#"
        };

        private static readonly string[] DIAL_TEXT_CHARACTERS =
        {
            ".,?", "ABC", "DEF", "GHI", "JKL", "MNO",
            "PQRS", "TUV", "WXYZ", "+", " ", "#"
        };

        private static readonly string[] T9_CHARACTERS =
        {
            "1.,?", "ABC2", "DEF3", "GHI4", "JKL5", "MNO6",
            "PQRS7", "TUV8", "WXYZ9", "*", " ", "#"
        };

        private static readonly string[] MESSAGE_SENDERS =
        {
            "LAMAR", "LESTER", "DOWNTOWN CAB"
        };

        private static readonly string[] MESSAGE_BODIES =
        {
            "Meet me at Strawberry. We need a fast car for the pickup.",
            "Go to the marked warehouse and photograph the delivery van.",
            "A VIP pickup is waiting downtown. Reach the marker before time runs out."
        };

        private static readonly string[] APP_TITLES =
        {
            "ĐIỆN THOẠI",
            "TIN NHẮN",
            "GHI CHÚ",
            "BẢN ĐỒ",
            "CAMERA",
            "ẢNH"
        };

        [Serializable]
        private sealed class CallHistoryEntry
        {
            public string value;
            public string timestamp;
            public bool textMode;
        }

        [Serializable]
        private sealed class CallHistoryData
        {
            public List<CallHistoryEntry> entries = new();
        }

        private CallHistoryData m_CallHistory = new();

        public bool IsConfigured => m_InterfaceRoot != null &&
            m_SafeAreaRoot != null && m_PhonePanel != null &&
            m_HomeScreen != null && m_AppScreens != null &&
            m_AppScreens.Length == 6 && m_AppButtons != null &&
            m_AppButtons.Length == 6 && m_CloseButton != null &&
            m_TimeText != null &&
            m_DateText != null && m_HeaderTitleText != null &&
            m_HeaderSubtitleText != null &&
            m_DialNumberText != null && m_DialStatusText != null &&
            m_DialKeyButtons != null && m_DialKeyButtons.Length == 12 &&
            m_DialKeyLabels != null && m_DialKeyLabels.Length == 12 &&
            m_DialDeleteButton != null && m_DialCallButton != null &&
            m_DialModeButton != null && m_DialModeButtonText != null &&
            m_DialKeypadPanel != null && m_CallHistoryPanel != null &&
            m_DialKeypadTabButton != null && m_CallHistoryTabButton != null &&
            m_CallHistoryText != null && m_ClearCallHistoryButton != null &&
            m_TextEntryText != null && m_TextStatusText != null &&
            m_TextKeyButtons != null && m_TextKeyButtons.Length == 12 &&
            m_TextDeleteButton != null && m_TextSendButton != null &&
            m_MessageListPanel != null && m_MessageDetailPanel != null &&
            m_MessageButtons != null && m_MessageButtons.Length == 3 &&
            m_MessageSenderText != null && m_MessageBodyText != null &&
            m_MessageResultText != null && m_MessageAcceptButton != null &&
            m_MessageCancelButton != null &&
            m_PhoneOpenSound != null && m_PhoneCloseSound != null &&
            m_AppOpenSound != null && m_AppBackSound != null &&
            m_MessageOpenSound != null && m_KeySounds != null &&
            m_KeySounds.Length >= 4 && m_ModeSwitchSound != null &&
            m_DeleteSound != null && m_CallSound != null &&
            m_SuccessSound != null && m_ErrorSound != null &&
            m_HistoryClearSound != null && m_MissionCancelSound != null &&
            this.GetComponent<FranklinPhoneSelfieCamera>() is
                { IsConfigured: true } &&
            this.GetComponent<FranklinPhoneHandPresentation>() is
                { IsConfigured: true };

        public bool IsOpen => m_IsOpen;
        public static FranklinPhoneSystem Instance => s_Instance;
        public event Action<int> EventMissionAccepted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<FranklinPhoneSystem>() != null) return;

            FranklinPhoneSystem prefab =
                Resources.Load<FranklinPhoneSystem>(RESOURCE_PATH);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "Franklin Phone System prefab is missing from PhoneSystem/Resources."
                );
                return;
            }

            Instantiate(prefab);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(this.gameObject);
            this.m_HandPresentation =
                this.GetComponent<FranklinPhoneHandPresentation>();
            this.m_SelfieCamera = this.GetComponent<FranklinPhoneSelfieCamera>();
            this.CapturePhoneAnimationPositions();
            this.m_SelfieCamera?.Initialize(this, this.m_HandPresentation);
            if (this.m_SelfieCamera != null)
                this.m_SelfieCamera.EventPhotoCaptured += this.OnPhotoCaptured;
            this.HideLegacyBottomNavigation();
            this.ConfigureAudioSource();
            this.LoadCallHistory();
            this.RegisterUiCallbacks();
            this.RefreshDialMode();
            this.RefreshDialDisplay();
            this.RefreshCallHistoryDisplay();
            this.RefreshTextDisplay();
            this.ApplySafeArea(true);
            this.RefreshClock(true);
            this.ShowHome();
            if (this.m_InterfaceRoot != null) this.m_InterfaceRoot.SetActive(false);
            this.TryBindHud(true);
        }

        private void OnDestroy()
        {
            if (this.m_SelfieCamera != null)
            {
                this.m_SelfieCamera.EventPhotoCaptured -= this.OnPhotoCaptured;
                this.m_SelfieCamera.SetSelfieActive(false);
            }
            this.UnbindHud();
            FranklinMobileHud.ReleaseControlsSuppression(this);
            FranklinMobileHud.ReleaseFastMovementSuppression(this);
            this.SetWeaponHudVisible(true);
            this.SetPlayerIdleSuppressed(false);
            this.m_HandPresentation?.SetPhoneOpen(false, true);
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            this.TryBindHud(false);
            this.RefreshClock(false);
            this.ApplySafeArea(false);
            if (this.m_IsOpen)
            {
                this.SetWeaponHudVisible(false);
                this.SetPlayerIdleSuppressed(true);
            }
        }

        public void TogglePhone()
        {
            this.SetOpen(!this.m_IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (this.m_IsOpen == open &&
                (this.m_InterfaceRoot == null ||
                 this.m_InterfaceRoot.activeSelf == open))
            {
                return;
            }

            this.m_IsOpen = open;
            if (this.m_Transition != null)
            {
                this.StopCoroutine(this.m_Transition);
                this.m_Transition = null;
            }

            if (open)
            {
                this.PlaySound(this.m_PhoneOpenSound);
                this.m_HandPresentation?.SetPhoneOpen(true);
                this.ShowHome();
                this.ApplySafeArea(true);
                this.RefreshClock(true);
                this.SetWeaponHudVisible(false);
                this.SetPlayerIdleSuppressed(true);
                if (this.m_InterfaceRoot != null)
                    this.m_InterfaceRoot.SetActive(true);
                if (this.m_PhonePanel != null)
                    this.m_PhonePanel.anchoredPosition = this.m_ClosedPosition;
                FranklinMobileHud.AcquireFastMovementSuppression(this);
                this.m_Transition = this.StartCoroutine(
                    this.AnimatePhone(this.m_OpenPosition, false)
                );
            }
            else
            {
                this.PlaySound(this.m_PhoneCloseSound);
                this.m_SelfieCamera?.SetSelfieActive(false);
                this.m_HandPresentation?.SetPhoneOpen(false);
                FranklinMobileHud.ReleaseFastMovementSuppression(this);
                this.SetWeaponHudVisible(true);
                this.SetPlayerIdleSuppressed(false);
                this.m_Transition = this.StartCoroutine(
                    this.AnimatePhone(this.m_ClosedPosition, true)
                );
            }
        }

        private void SetWeaponHudVisible(bool visible)
        {
            FranklinPlayerStatusHud.Instance?.SetWeaponHudVisible(visible);
        }

        private void SetPlayerIdleSuppressed(bool suppressed)
        {
            if (!suppressed)
            {
                this.m_PlayerAnimationBridge?.SetIdleVariationsSuppressed(false);
                this.m_PlayerAnimationBridge?.SetFastLocomotionSuppressed(false);
                this.m_PlayerAnimationBridge = null;
                this.m_IdleSuppressedPlayer = null;
                return;
            }

            GameObject player = ShortcutPlayer.Instance;
            if (this.m_IdleSuppressedPlayer != player)
            {
                this.m_PlayerAnimationBridge?.SetIdleVariationsSuppressed(false);
                this.m_PlayerAnimationBridge?.SetFastLocomotionSuppressed(false);
                this.m_IdleSuppressedPlayer = player;
                this.m_PlayerAnimationBridge = player != null
                    ? player.GetComponentInChildren<FranklinAnimationBridge>(true)
                    : null;
            }

            this.m_PlayerAnimationBridge?.SetIdleVariationsSuppressed(true);
            this.m_PlayerAnimationBridge?.SetFastLocomotionSuppressed(true);
        }

        public void OpenApp(int index)
        {
            if (index < 0 || index >= this.m_AppScreens.Length) return;

            this.m_CurrentApp = index;
            this.PlaySound(this.m_AppOpenSound);
            this.m_HomeScreen.SetActive(false);
            for (int i = 0; i < this.m_AppScreens.Length; ++i)
                if (this.m_AppScreens[i] != null)
                    this.m_AppScreens[i].SetActive(i == index);

            this.m_HeaderTitleText.text = APP_TITLES[index];
            this.m_HeaderSubtitleText.text = index switch
            {
                0 => "DIAL NUMBER",
                1 => "INBOX • 3 MESSAGES",
                2 => "T9 TEXT INPUT",
                3 => "LOS SANTOS NAVIGATION",
                4 => "LIVE VIEW",
                _ => this.m_SelfieCamera != null
                    ? $"{this.m_SelfieCamera.StoredPhotoCount} SAVED MOMENTS"
                    : "RECENT MOMENTS"
            };
            if (index == 0) this.RefreshCallHistoryDisplay();
            if (index == 0) this.SetDialTab(true, false);
            if (index == 1) this.ShowMessageList(false);
            this.m_SelfieCamera?.SetSelfieActive(index == 4);
            if (index == 5) this.m_SelfieCamera?.RefreshGallery();
            else this.m_SelfieCamera?.ClosePhotoViewer();
            this.HideLegacyBottomNavigation();
        }

        public void ShowHome()
        {
            this.m_CurrentApp = -1;
            this.m_SelfieCamera?.SetSelfieActive(false);
            this.m_SelfieCamera?.ClosePhotoViewer();
            if (this.m_HomeScreen != null) this.m_HomeScreen.SetActive(true);
            if (this.m_AppScreens != null)
            {
                foreach (GameObject screen in this.m_AppScreens)
                    if (screen != null) screen.SetActive(false);
            }
            if (this.m_HeaderTitleText != null)
                this.m_HeaderTitleText.text = "HOME";
            if (this.m_HeaderSubtitleText != null)
                this.m_HeaderSubtitleText.text = "LS MOBILE • ONLINE";
            this.HideLegacyBottomNavigation();
        }

        private void HideLegacyBottomNavigation()
        {
            // Older prefabs may still contain the former HOME/APPS buttons.
            // Keep their serialized references compatible, but never display them.
            if (this.m_HomeButton != null)
                this.m_HomeButton.gameObject.SetActive(false);
            if (this.m_BottomCloseButton != null)
                this.m_BottomCloseButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Closes only the active app. The same button closes the phone only
        /// when the player is already on the phone Home screen.
        /// </summary>
        public void HandleCloseButton()
        {
            if (this.m_HomeScreen != null && !this.m_HomeScreen.activeSelf)
            {
                this.CloseCurrentApp();
                return;
            }

            this.SetOpen(false);
        }

        private void CloseCurrentApp()
        {
            this.PlaySound(this.m_AppBackSound);
            this.ShowHome();
        }

        private void OnPhotoCaptured(Texture2D texture, string savedPath)
        {
            this.PlaySound(this.m_SuccessSound);
            if (this.m_CurrentApp == 4 && this.m_HeaderSubtitleText != null)
            {
                this.m_HeaderSubtitleText.text =
                    $"SAVED TO PHOTOS • {this.m_SelfieCamera.StoredPhotoCount}";
            }
        }

        public void RefreshPhotosHeader()
        {
            if (this.m_CurrentApp != 5 || this.m_HeaderSubtitleText == null) return;
            this.m_HeaderSubtitleText.text = this.m_SelfieCamera != null
                ? $"{this.m_SelfieCamera.StoredPhotoCount} SAVED MOMENTS"
                : "RECENT MOMENTS";
        }

        public void Configure(
            GameObject interfaceRoot,
            RectTransform safeAreaRoot,
            RectTransform phonePanel,
            GameObject homeScreen,
            GameObject[] appScreens,
            Button[] appButtons,
            Button homeButton,
            Button closeButton,
            Button bottomCloseButton,
            Text timeText,
            Text dateText,
            Text headerTitleText,
            Text headerSubtitleText,
            Text dialNumberText,
            Text dialStatusText,
            Button[] dialKeyButtons,
            Text[] dialKeyLabels,
            Button dialDeleteButton,
            Button dialCallButton,
            Button dialModeButton,
            Text dialModeButtonText,
            GameObject dialKeypadPanel,
            GameObject callHistoryPanel,
            Button dialKeypadTabButton,
            Button callHistoryTabButton,
            Text callHistoryText,
            Button clearCallHistoryButton,
            Text textEntryText,
            Text textStatusText,
            Button[] textKeyButtons,
            Button textDeleteButton,
            Button textSendButton,
            GameObject messageListPanel,
            GameObject messageDetailPanel,
            Button[] messageButtons,
            Text messageSenderText,
            Text messageBodyText,
            Text messageResultText,
            Button messageAcceptButton,
            Button messageCancelButton)
        {
            this.m_InterfaceRoot = interfaceRoot;
            this.m_SafeAreaRoot = safeAreaRoot;
            this.m_PhonePanel = phonePanel;
            this.m_HomeScreen = homeScreen;
            this.m_AppScreens = appScreens ?? Array.Empty<GameObject>();
            this.m_AppButtons = appButtons ?? Array.Empty<Button>();
            this.m_HomeButton = homeButton;
            this.m_CloseButton = closeButton;
            this.m_BottomCloseButton = bottomCloseButton;
            this.m_TimeText = timeText;
            this.m_DateText = dateText;
            this.m_HeaderTitleText = headerTitleText;
            this.m_HeaderSubtitleText = headerSubtitleText;
            this.m_DialNumberText = dialNumberText;
            this.m_DialStatusText = dialStatusText;
            this.m_DialKeyButtons = dialKeyButtons ?? Array.Empty<Button>();
            this.m_DialKeyLabels = dialKeyLabels ?? Array.Empty<Text>();
            this.m_DialDeleteButton = dialDeleteButton;
            this.m_DialCallButton = dialCallButton;
            this.m_DialModeButton = dialModeButton;
            this.m_DialModeButtonText = dialModeButtonText;
            this.m_DialKeypadPanel = dialKeypadPanel;
            this.m_CallHistoryPanel = callHistoryPanel;
            this.m_DialKeypadTabButton = dialKeypadTabButton;
            this.m_CallHistoryTabButton = callHistoryTabButton;
            this.m_CallHistoryText = callHistoryText;
            this.m_ClearCallHistoryButton = clearCallHistoryButton;
            this.m_TextEntryText = textEntryText;
            this.m_TextStatusText = textStatusText;
            this.m_TextKeyButtons = textKeyButtons ?? Array.Empty<Button>();
            this.m_TextDeleteButton = textDeleteButton;
            this.m_TextSendButton = textSendButton;
            this.m_MessageListPanel = messageListPanel;
            this.m_MessageDetailPanel = messageDetailPanel;
            this.m_MessageButtons = messageButtons ?? Array.Empty<Button>();
            this.m_MessageSenderText = messageSenderText;
            this.m_MessageBodyText = messageBodyText;
            this.m_MessageResultText = messageResultText;
            this.m_MessageAcceptButton = messageAcceptButton;
            this.m_MessageCancelButton = messageCancelButton;
        }

        public void ConfigureAudio(
            AudioClip phoneOpenSound,
            AudioClip phoneCloseSound,
            AudioClip appOpenSound,
            AudioClip appBackSound,
            AudioClip messageOpenSound,
            AudioClip[] keySounds,
            AudioClip modeSwitchSound,
            AudioClip deleteSound,
            AudioClip callSound,
            AudioClip successSound,
            AudioClip errorSound,
            AudioClip historyClearSound,
            AudioClip missionCancelSound)
        {
            this.m_PhoneOpenSound = phoneOpenSound;
            this.m_PhoneCloseSound = phoneCloseSound;
            this.m_AppOpenSound = appOpenSound;
            this.m_AppBackSound = appBackSound;
            this.m_MessageOpenSound = messageOpenSound;
            this.m_KeySounds = keySounds ?? Array.Empty<AudioClip>();
            this.m_ModeSwitchSound = modeSwitchSound;
            this.m_DeleteSound = deleteSound;
            this.m_CallSound = callSound;
            this.m_SuccessSound = successSound;
            this.m_ErrorSound = errorSound;
            this.m_HistoryClearSound = historyClearSound;
            this.m_MissionCancelSound = missionCancelSound;
        }

        private void RegisterUiCallbacks()
        {
            // Register this first so every current or future Phone UI button
            // drives one physical right-thumb press before its own action runs.
            if (this.m_InterfaceRoot != null)
            {
                foreach (Button button in
                         this.m_InterfaceRoot.GetComponentsInChildren<Button>(true))
                {
                    if (button != null)
                        button.onClick.AddListener(this.NotifyPhysicalTap);
                }
            }

            if (this.m_AppButtons != null)
            {
                for (int i = 0; i < this.m_AppButtons.Length; ++i)
                {
                    int appIndex = i;
                    if (this.m_AppButtons[i] != null)
                        this.m_AppButtons[i].onClick.AddListener(
                            () => this.OpenApp(appIndex)
                        );
                }
            }
            this.m_HomeButton?.onClick.AddListener(this.CloseCurrentApp);
            this.m_CloseButton?.onClick.AddListener(this.HandleCloseButton);
            this.m_BottomCloseButton?.onClick.AddListener(this.CloseCurrentApp);

            for (int i = 0; i < this.m_DialKeyButtons.Length; ++i)
            {
                int keyIndex = i;
                this.m_DialKeyButtons[i]?.onClick.AddListener(
                    () => this.PressDialKey(keyIndex)
                );
            }
            this.m_DialDeleteButton?.onClick.AddListener(this.DeleteDialCharacter);
            this.m_DialCallButton?.onClick.AddListener(this.CallDialedNumber);
            this.m_DialModeButton?.onClick.AddListener(this.ToggleDialMode);
            this.m_DialKeypadTabButton?.onClick.AddListener(this.ShowDialKeypadTab);
            this.m_CallHistoryTabButton?.onClick.AddListener(this.ShowCallHistoryTab);
            this.m_ClearCallHistoryButton?.onClick.AddListener(this.ClearCallHistory);

            for (int i = 0; i < this.m_TextKeyButtons.Length; ++i)
            {
                int keyIndex = i;
                this.m_TextKeyButtons[i]?.onClick.AddListener(
                    () => this.PressTextKey(keyIndex)
                );
            }
            this.m_TextDeleteButton?.onClick.AddListener(this.DeleteTextCharacter);
            this.m_TextSendButton?.onClick.AddListener(this.SendTextMessage);

            for (int i = 0; i < this.m_MessageButtons.Length; ++i)
            {
                int messageIndex = i;
                this.m_MessageButtons[i]?.onClick.AddListener(
                    () => this.OpenMessage(messageIndex)
                );
            }
            this.m_MessageAcceptButton?.onClick.AddListener(this.AcceptMission);
            this.m_MessageCancelButton?.onClick.AddListener(this.ShowMessageList);
        }

        public void OpenMessage(int messageIndex)
        {
            if (messageIndex < 0 || messageIndex >= MESSAGE_SENDERS.Length) return;

            this.PlaySound(this.m_MessageOpenSound);
            this.m_SelectedMessage = messageIndex;
            if (this.m_MessageListPanel != null)
                this.m_MessageListPanel.SetActive(false);
            if (this.m_MessageDetailPanel != null)
                this.m_MessageDetailPanel.SetActive(true);
            if (this.m_MessageSenderText != null)
                this.m_MessageSenderText.text = MESSAGE_SENDERS[messageIndex];
            if (this.m_MessageBodyText != null)
                this.m_MessageBodyText.text = MESSAGE_BODIES[messageIndex];
            if (this.m_MessageResultText != null)
                this.m_MessageResultText.text = "MISSION REQUEST";
            if (this.m_HeaderTitleText != null)
                this.m_HeaderTitleText.text = "NHIỆM VỤ";
            if (this.m_HeaderSubtitleText != null)
                this.m_HeaderSubtitleText.text = "FROM • " + MESSAGE_SENDERS[messageIndex];
        }

        public void ShowMessageList()
        {
            this.ShowMessageList(true);
        }

        private void ShowMessageList(bool playCancelSound)
        {
            bool wasReadingMessage = this.m_MessageDetailPanel != null &&
                this.m_MessageDetailPanel.activeSelf;
            if (playCancelSound && wasReadingMessage)
                this.PlaySound(this.m_MissionCancelSound);
            this.m_SelectedMessage = -1;
            if (this.m_MessageListPanel != null)
                this.m_MessageListPanel.SetActive(true);
            if (this.m_MessageDetailPanel != null)
                this.m_MessageDetailPanel.SetActive(false);
            if (this.m_HeaderTitleText != null &&
                this.m_AppScreens != null && this.m_AppScreens.Length > 1 &&
                this.m_AppScreens[1] != null && this.m_AppScreens[1].activeSelf)
            {
                this.m_HeaderTitleText.text = "TIN NHẮN";
                this.m_HeaderSubtitleText.text = "INBOX • 3 MESSAGES";
            }
        }

        public void AcceptMission()
        {
            if (this.m_SelectedMessage < 0 || this.m_MessageResultText == null) return;
            this.PlaySound(this.m_SuccessSound);
            this.m_MessageResultText.text = "MISSION ACCEPTED • CHECK MAP";
            this.EventMissionAccepted?.Invoke(this.m_SelectedMessage);
        }

        public void PressDialKey(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= DIAL_KEYS.Length) return;

            this.PlayKeySound(keyIndex);
            if (this.m_DialTextMode)
                this.PressDialTextKey(keyIndex);
            else if (this.m_DialNumber.Length < MAX_DIAL_LENGTH)
                this.m_DialNumber += DIAL_KEYS[keyIndex];

            if (this.m_DialStatusText != null)
                this.m_DialStatusText.text = "READY TO CALL";
            this.RefreshDialDisplay();
        }

        public void DeleteDialCharacter()
        {
            this.PlaySound(this.m_DeleteSound);
            if (this.m_DialNumber.Length > 0)
                this.m_DialNumber = this.m_DialNumber[..^1];
            this.ResetDialT9Cycle();
            if (this.m_DialStatusText != null)
                this.m_DialStatusText.text = this.m_DialTextMode
                    ? "ENTER A NAME"
                    : "ENTER A NUMBER";
            this.RefreshDialDisplay();
        }

        public void CallDialedNumber()
        {
            if (this.m_DialStatusText == null) return;
            if (this.m_DialNumber.Length == 0)
            {
                this.PlaySound(this.m_ErrorSound);
                this.m_DialStatusText.text = this.m_DialTextMode
                    ? "ENTER A NAME"
                    : "ENTER A NUMBER";
                return;
            }

            this.PlaySound(this.m_CallSound);
            this.m_DialStatusText.text = "CALLING • " + this.m_DialNumber;
            this.AddCallHistory(this.m_DialNumber, this.m_DialTextMode);
        }

        public void ToggleDialMode()
        {
            this.PlaySound(this.m_ModeSwitchSound);
            this.m_DialTextMode = !this.m_DialTextMode;
            this.m_DialNumber = string.Empty;
            this.ResetDialT9Cycle();
            if (this.m_DialStatusText != null)
            {
                this.m_DialStatusText.text = this.m_DialTextMode
                    ? "ENTER A CONTACT NAME"
                    : "ENTER A NUMBER";
            }
            this.RefreshDialMode();
            this.RefreshDialDisplay();
        }

        public void ShowDialKeypadTab()
        {
            this.SetDialTab(true, true);
        }

        public void ShowCallHistoryTab()
        {
            this.SetDialTab(false, true);
        }

        private void SetDialTab(bool showKeypad, bool playSwitchSound)
        {
            GameObject targetPanel = showKeypad
                ? this.m_DialKeypadPanel
                : this.m_CallHistoryPanel;
            bool changedTab = targetPanel != null && !targetPanel.activeSelf;
            if (playSwitchSound && changedTab)
                this.PlaySound(this.m_ModeSwitchSound);

            if (this.m_DialKeypadPanel != null)
                this.m_DialKeypadPanel.SetActive(showKeypad);
            if (this.m_CallHistoryPanel != null)
                this.m_CallHistoryPanel.SetActive(!showKeypad);
            if (!showKeypad) this.RefreshCallHistoryDisplay();
            this.SetDialTabVisuals(showKeypad);
            if (this.m_HeaderSubtitleText != null)
            {
                this.m_HeaderSubtitleText.text = showKeypad
                    ? "DIAL NUMBER"
                    : "SAVED ON DEVICE";
            }
        }

        public void ClearCallHistory()
        {
            this.PlaySound(this.m_HistoryClearSound);
            this.m_CallHistory = new CallHistoryData();
            PlayerPrefs.DeleteKey(CALL_HISTORY_PREFS_KEY);
            PlayerPrefs.Save();
            this.RefreshCallHistoryDisplay();
        }

        private void SetDialTabVisuals(bool keypadSelected)
        {
            this.SetTabVisual(this.m_DialKeypadTabButton, keypadSelected);
            this.SetTabVisual(this.m_CallHistoryTabButton, !keypadSelected);
        }

        private void SetTabVisual(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null) return;
            button.targetGraphic.color = selected
                ? new Color(0.13f, 0.46f, 0.48f, 1f)
                : new Color(0.15f, 0.19f, 0.2f, 1f);
        }

        private void PressDialTextKey(int keyIndex)
        {
            string characters = DIAL_TEXT_CHARACTERS[keyIndex];
            float now = Time.unscaledTime;
            bool cycleLastCharacter = keyIndex == this.m_LastDialT9Key &&
                now - this.m_LastDialT9PressTime <= T9_MULTITAP_WINDOW &&
                this.m_DialNumber.Length > 0 && characters.Length > 1;

            if (cycleLastCharacter)
            {
                this.m_LastDialT9Character =
                    (this.m_LastDialT9Character + 1) % characters.Length;
                this.m_DialNumber = this.m_DialNumber[..^1] +
                    characters[this.m_LastDialT9Character];
            }
            else if (this.m_DialNumber.Length < MAX_DIAL_LENGTH)
            {
                this.m_LastDialT9Character = 0;
                this.m_DialNumber += characters[0];
            }

            this.m_LastDialT9Key = keyIndex;
            this.m_LastDialT9PressTime = now;
        }

        private void RefreshDialMode()
        {
            string[] labels = this.m_DialTextMode
                ? DIAL_TEXT_LABELS
                : DIAL_NUMBER_LABELS;
            for (int i = 0; i < this.m_DialKeyLabels.Length && i < labels.Length; ++i)
                if (this.m_DialKeyLabels[i] != null)
                    this.m_DialKeyLabels[i].text = labels[i];

            if (this.m_DialModeButtonText != null)
                this.m_DialModeButtonText.text = this.m_DialTextMode ? "123" : "ABC";
        }

        private void AddCallHistory(string value, bool textMode)
        {
            this.m_CallHistory ??= new CallHistoryData();
            this.m_CallHistory.entries ??= new List<CallHistoryEntry>();
            this.m_CallHistory.entries.Insert(0, new CallHistoryEntry
            {
                value = value,
                timestamp = DateTime.Now.ToString("dd/MM HH:mm"),
                textMode = textMode
            });
            if (this.m_CallHistory.entries.Count > MAX_CALL_HISTORY)
            {
                this.m_CallHistory.entries.RemoveRange(
                    MAX_CALL_HISTORY,
                    this.m_CallHistory.entries.Count - MAX_CALL_HISTORY
                );
            }

            PlayerPrefs.SetString(
                CALL_HISTORY_PREFS_KEY,
                JsonUtility.ToJson(this.m_CallHistory)
            );
            PlayerPrefs.Save();
            this.RefreshCallHistoryDisplay();
        }

        private void LoadCallHistory()
        {
            string json = PlayerPrefs.GetString(CALL_HISTORY_PREFS_KEY, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                this.m_CallHistory = new CallHistoryData();
                return;
            }

            try
            {
                this.m_CallHistory = JsonUtility.FromJson<CallHistoryData>(json) ??
                    new CallHistoryData();
                this.m_CallHistory.entries ??= new List<CallHistoryEntry>();
            }
            catch (ArgumentException)
            {
                this.m_CallHistory = new CallHistoryData();
            }
        }

        private void RefreshCallHistoryDisplay()
        {
            if (this.m_CallHistoryText == null) return;
            if (this.m_CallHistory?.entries == null ||
                this.m_CallHistory.entries.Count == 0)
            {
                this.m_CallHistoryText.text = "RECENT • NO CALLS";
                return;
            }

            int count = Mathf.Min(MAX_CALL_HISTORY, this.m_CallHistory.entries.Count);
            string history = string.Empty;
            for (int i = 0; i < count; ++i)
            {
                CallHistoryEntry entry = this.m_CallHistory.entries[i];
                if (i > 0) history += "\n";
                history += (i + 1) + ".  " + entry.value + "\n     " +
                    entry.timestamp + (entry.textMode ? "  •  ABC" : "  •  123");
            }
            this.m_CallHistoryText.text = history;
        }

        private void ResetDialT9Cycle()
        {
            this.m_LastDialT9Key = -1;
            this.m_LastDialT9Character = 0;
            this.m_LastDialT9PressTime = -10f;
        }

        public void PressTextKey(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= T9_CHARACTERS.Length) return;

            this.PlayKeySound(keyIndex);
            string characters = T9_CHARACTERS[keyIndex];
            float now = Time.unscaledTime;
            bool cycleLastCharacter = keyIndex == this.m_LastT9Key &&
                now - this.m_LastT9PressTime <= T9_MULTITAP_WINDOW &&
                this.m_TextEntry.Length > 0 && characters.Length > 1;

            if (cycleLastCharacter)
            {
                this.m_LastT9Character =
                    (this.m_LastT9Character + 1) % characters.Length;
                this.m_TextEntry = this.m_TextEntry[..^1] +
                    characters[this.m_LastT9Character];
            }
            else if (this.m_TextEntry.Length < MAX_TEXT_LENGTH)
            {
                this.m_LastT9Character = 0;
                this.m_TextEntry += characters[0];
            }

            this.m_LastT9Key = keyIndex;
            this.m_LastT9PressTime = now;
            if (this.m_TextStatusText != null)
                this.m_TextStatusText.text = "MULTI-TAP T9 • " +
                    this.m_TextEntry.Length + "/" + MAX_TEXT_LENGTH;
            this.RefreshTextDisplay();
        }

        public void DeleteTextCharacter()
        {
            this.PlaySound(this.m_DeleteSound);
            if (this.m_TextEntry.Length > 0)
                this.m_TextEntry = this.m_TextEntry[..^1];
            this.ResetT9Cycle();
            if (this.m_TextStatusText != null)
                this.m_TextStatusText.text = "TYPE A MESSAGE";
            this.RefreshTextDisplay();
        }

        public void SendTextMessage()
        {
            if (this.m_TextStatusText == null) return;
            if (string.IsNullOrWhiteSpace(this.m_TextEntry))
            {
                this.PlaySound(this.m_ErrorSound);
                this.m_TextStatusText.text = "TYPE A MESSAGE";
                return;
            }

            this.PlaySound(this.m_SuccessSound);
            this.m_TextStatusText.text =
                "NOTE SAVED • " + DateTime.Now.ToString("HH:mm");
            this.m_TextEntry = string.Empty;
            this.ResetT9Cycle();
            this.RefreshTextDisplay();
        }

        private void RefreshDialDisplay()
        {
            if (this.m_DialNumberText != null)
            {
                this.m_DialNumberText.text = this.m_DialNumber.Length == 0
                    ? (this.m_DialTextMode ? "ENTER NAME" : "ENTER NUMBER")
                    : this.m_DialNumber;
            }
        }

        private void RefreshTextDisplay()
        {
            if (this.m_TextEntryText != null)
            {
                this.m_TextEntryText.text = this.m_TextEntry.Length == 0
                    ? "ENTER MESSAGE..."
                    : this.m_TextEntry;
            }
        }

        private void ResetT9Cycle()
        {
            this.m_LastT9Key = -1;
            this.m_LastT9Character = 0;
            this.m_LastT9PressTime = -10f;
        }

        private void ConfigureAudioSource()
        {
            this.m_AudioSource = this.GetComponent<AudioSource>();
            if (this.m_AudioSource == null)
                this.m_AudioSource = this.gameObject.AddComponent<AudioSource>();

            this.m_AudioSource.playOnAwake = false;
            this.m_AudioSource.loop = false;
            this.m_AudioSource.spatialBlend = 0f;
            this.m_AudioSource.ignoreListenerPause = true;
        }

        private void PlayKeySound(int keyIndex)
        {
            if (this.m_KeySounds == null || this.m_KeySounds.Length == 0) return;
            int soundIndex = Mathf.Abs(keyIndex) % this.m_KeySounds.Length;
            this.PlaySound(this.m_KeySounds[soundIndex]);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || this.m_SoundVolume <= 0f) return;
            if (this.m_AudioSource == null) this.ConfigureAudioSource();
            this.m_AudioSource.PlayOneShot(clip, this.m_SoundVolume);
        }

        private void NotifyPhysicalTap()
        {
            if (this.m_HandPresentation == null)
            {
                this.m_HandPresentation =
                    this.GetComponent<FranklinPhoneHandPresentation>();
            }
            this.m_HandPresentation?.NotifyTap();
        }

        private void CapturePhoneAnimationPositions()
        {
            if (this.m_PhonePanel == null) return;

            // The prefab's RectTransform is the source of truth. This lets a UI
            // designer move Phone Device in Prefab Mode without a second hidden
            // Open Position value snapping it back when Play Mode starts.
            this.m_OpenPosition = this.m_PhonePanel.anchoredPosition;

            float halfWidth = this.m_PhonePanel.rect.width * 0.5f;
            if (halfWidth <= 0.01f)
                halfWidth = this.m_PhonePanel.sizeDelta.x * 0.5f;
            this.m_ClosedPosition = new Vector2(
                Mathf.Max(halfWidth, 0f) + CLOSED_EDGE_PADDING,
                this.m_OpenPosition.y
            );
        }

        private IEnumerator AnimatePhone(Vector2 target, bool disableAfter)
        {
            if (this.m_PhonePanel == null)
            {
                if (disableAfter && this.m_InterfaceRoot != null)
                    this.m_InterfaceRoot.SetActive(false);
                yield break;
            }

            Vector2 start = this.m_PhonePanel.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < TRANSITION_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / TRANSITION_DURATION);
                t = 1f - Mathf.Pow(1f - t, 3f);
                this.m_PhonePanel.anchoredPosition = Vector2.LerpUnclamped(
                    start,
                    target,
                    t
                );
                yield return null;
            }

            this.m_PhonePanel.anchoredPosition = target;
            if (disableAfter && this.m_InterfaceRoot != null)
                this.m_InterfaceRoot.SetActive(false);
            this.m_Transition = null;
        }

        private void TryBindHud(bool immediate)
        {
            if (this.m_Hud != null) return;
            if (!immediate && Time.unscaledTime < this.m_NextHudSearch) return;
            this.m_NextHudSearch = Time.unscaledTime + 0.5f;

            this.m_Hud = FindFirstObjectByType<FranklinMobileHud>();
            if (this.m_Hud != null)
                this.m_Hud.EventPhoneRequested += this.OnPhoneRequested;
        }

        private void UnbindHud()
        {
            if (this.m_Hud != null)
                this.m_Hud.EventPhoneRequested -= this.OnPhoneRequested;
            this.m_Hud = null;
        }

        private void OnPhoneRequested()
        {
            this.TogglePhone();
        }

        private void RefreshClock(bool force)
        {
            if (!force && Time.unscaledTime < this.m_NextClockRefresh) return;
            this.m_NextClockRefresh = Time.unscaledTime + 1f;
            DateTime now = DateTime.Now;
            if (this.m_TimeText != null) this.m_TimeText.text = now.ToString("HH:mm");
            if (this.m_DateText != null)
                this.m_DateText.text = now.ToString("ddd • dd MMM").ToUpperInvariant();
        }

        private void ApplySafeArea(bool force)
        {
            if (this.m_SafeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new(Screen.width, Screen.height);
            if (!force && safeArea == this.m_LastSafeArea &&
                screenSize == this.m_LastScreenSize)
            {
                return;
            }

            this.m_LastSafeArea = safeArea;
            this.m_LastScreenSize = screenSize;
            this.m_SafeAreaRoot.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height
            );
            this.m_SafeAreaRoot.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height
            );
            this.m_SafeAreaRoot.offsetMin = Vector2.zero;
            this.m_SafeAreaRoot.offsetMax = Vector2.zero;
        }
    }
}
