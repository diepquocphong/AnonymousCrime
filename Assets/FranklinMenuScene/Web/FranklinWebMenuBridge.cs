using System;
using System.Threading;
using FranklinGame.Settings;
using RobotAstro;
using UnityEngine;
using UnityEngine.Events;

namespace FranklinGame.Menu.Web
{
    /// <summary>
    /// Stable message contract between the local web menu and a native WebView
    /// host. The component deliberately does not depend on a specific WebView
    /// package, so Android/iOS hosting can be selected without rewriting the UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinWebMenuBridge : MonoBehaviour
    {
        private const int CurrentMessageVersion = 1;
        private const int MaximumMessageLength = 2048;

        [Serializable]
        private sealed class MenuMessage
        {
            public int version;
            public string action;
            public string payload;
            public long timestamp;
        }

        [Serializable]
        private sealed class MenuState
        {
            public string version;
            public bool hasSave;
            public string sync;
            public string profile;
            public GuestState guest;
            public bool showQuit;
            public bool audioEnabled;
            public AudioState audio;
            public string language;
            public GraphicsState graphics;
            public MissionState missions;
        }

        [Serializable]
        private sealed class AudioState
        {
            public bool enabled;
            public int master;
            public int music;
            public int sfx;
        }

        [Serializable]
        private sealed class GraphicsState
        {
            public string quality;
            public string antiAliasing;
            public string depthOfField;
            public bool fbsEnabled;
            public bool ssaoEnabled;
            public string autoProfile;
            public bool ssaoAvailable;
            public bool dofAvailable;
            public bool canChangeFbs;
            public bool canChangeSsao;
            public bool canChangeAntiAliasing;
            public bool canChangeDepthOfField;
        }

        [Serializable]
        private sealed class GuestState
        {
            public bool exists;
            public bool active;
            public string id;
            public string label;
            public string displayName;
            public string avatar;
            public string createdUtc;
            public string lastPlayedUtc;
            public bool progressOnDevice;
            public string response;
            public string responseId;
            public string message;
        }

        [Serializable]
        private sealed class MissionState
        {
            public string activeId;
            public string selectedId;
            public int completedCount;
            public int totalCount;
            public MissionItemState[] items;
        }

        [Serializable]
        private sealed class MissionItemState
        {
            public string id;
            public int number;
            public string title;
            public string sender;
            public string description;
            public string thumbnail;
            public bool unlocked;
            public bool completed;
        }

        [Header("State supplied to the web menu")]
        [SerializeField] private bool m_HasSave;
        [SerializeField] private string m_SyncState = "offline";
        [SerializeField] private string m_ProfileState = "guest";
        [SerializeField] private bool m_ShowQuit = true;
        [SerializeField] private bool m_AudioEnabled = true;
        [SerializeField, Range(0, 100)] private int m_AudioMaster = 100;
        [SerializeField, Range(0, 100)] private int m_AudioMusic = 70;
        [SerializeField, Range(0, 100)] private int m_AudioSfx = 100;
        [SerializeField] private string m_Language = "vi";

        [Header("Validated web actions")]
        [SerializeField] private UnityEvent m_OnReady;
        [SerializeField] private UnityEvent m_OnContinue;
        [SerializeField] private UnityEvent m_OnNewGame;
        [SerializeField] private UnityEvent m_OnSettings;
        [SerializeField] private UnityEvent m_OnQuit;
        [SerializeField] private UnityEvent m_OnProfile;
        [SerializeField] private UnityEvent m_OnSync;
        [SerializeField] private UnityEvent m_OnAudioToggle;

        private SynchronizationContext m_UnityContext;
        private int m_UnityThreadId;
        private string m_LastActionFingerprint;
        private float m_LastActionTime = float.NegativeInfinity;
        private string m_GuestResponse = "idle";
        private string m_GuestResponseId = string.Empty;
        private string m_GuestResponseMessage = string.Empty;

        /// <summary>
        /// The WebView host subscribes and evaluates:
        /// window.FranklinMenu.setState(json).
        /// </summary>
        public event Action<string> StateJsonChanged;

        /// <summary>
        /// Runtime menu logic subscribes to validated actions. Keeping this event
        /// beside the serialized UnityEvents makes the bridge useful both from a
        /// hand-authored scene and from the minimal generated web bootstrap.
        /// </summary>
        public event Action<string, string> ActionReceived;

        private void Awake()
        {
            this.gameObject.name = "FranklinWebMenuBridge";
            this.m_UnityContext = SynchronizationContext.Current;
            this.m_UnityThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// A WebView adapter calls this method with the JSON emitted by menu.js.
        /// Only the fixed allow-list below can invoke Unity events.
        /// </summary>
        public void OnWebMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumMessageLength)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId != this.m_UnityThreadId)
            {
                if (this.m_UnityContext == null) return;
                string postedJson = json;
                this.m_UnityContext.Post(_ =>
                {
                    if (this != null) this.ProcessWebMessage(postedJson);
                }, null);
                return;
            }

            this.ProcessWebMessage(json);
        }

        private void ProcessWebMessage(string json)
        {
            MenuMessage message;
            try
            {
                message = JsonUtility.FromJson<MenuMessage>(json);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (message == null || message.version != CurrentMessageVersion ||
                string.IsNullOrWhiteSpace(message.action))
            {
                return;
            }

            if (message.action != "ready")
            {
                float now = Time.unscaledTime;
                string fingerprint = message.action + "\n" +
                    (message.payload ?? string.Empty);
                if (fingerprint == this.m_LastActionFingerprint &&
                    now - this.m_LastActionTime < 0.35f)
                {
                    return;
                }
                this.m_LastActionFingerprint = fingerprint;
                this.m_LastActionTime = now;
            }

            switch (message.action)
            {
                case "ready":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnReady?.Invoke();
                    this.PublishState();
                    break;
                case "continue":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnContinue?.Invoke();
                    break;
                case "new-game":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnNewGame?.Invoke();
                    break;
                case "mission-open":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    break;
                case "settings":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnSettings?.Invoke();
                    break;
                case "settings-quality":
                case "settings-aa":
                case "settings-dof":
                case "settings-fbs":
                case "settings-ssao":
                case "settings-audio-enabled":
                case "settings-audio-master":
                case "settings-audio-music":
                case "settings-audio-sfx":
                case "settings-language":
                case "settings-complete":
                case "mission-select":
                case "mission-play":
                case "guest-create":
                case "guest-login":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    break;
                case "quit":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnQuit?.Invoke();
                    break;
                case "profile":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnProfile?.Invoke();
                    break;
                case "sync":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnSync?.Invoke();
                    break;
                case "audio-toggle":
                    this.ActionReceived?.Invoke(message.action, message.payload);
                    this.m_OnAudioToggle?.Invoke();
                    break;
                default:
                    break;
            }
        }

        public string BuildStateJson()
        {
            MenuState state = new MenuState
            {
                version = Application.version,
                hasSave = this.m_HasSave,
                sync = string.IsNullOrWhiteSpace(this.m_SyncState)
                    ? "offline"
                    : this.m_SyncState,
                profile = string.IsNullOrWhiteSpace(this.m_ProfileState)
                    ? "guest"
                    : this.m_ProfileState,
                guest = BuildGuestState(),
                audioEnabled = this.m_AudioEnabled,
                audio = new AudioState
                {
                    enabled = this.m_AudioEnabled,
                    master = Mathf.Clamp(this.m_AudioMaster, 0, 100),
                    music = Mathf.Clamp(this.m_AudioMusic, 0, 100),
                    sfx = Mathf.Clamp(this.m_AudioSfx, 0, 100)
                },
                language = string.IsNullOrWhiteSpace(this.m_Language)
                    ? "vi"
                    : this.m_Language,
                graphics = BuildGraphicsState(),
                missions = BuildMissionState(),
#if UNITY_IOS && !UNITY_EDITOR
                showQuit = false
#else
                showQuit = this.m_ShowQuit
#endif
            };
            return JsonUtility.ToJson(state);
        }

        private static GraphicsState BuildGraphicsState()
        {
            int quality = FranklinMobileGraphicsSettings.QualityLevel;
            bool isAuto = quality == FranklinMobileGraphicsSettings.QualityAuto;
            bool isHigh = quality == FranklinMobileGraphicsSettings.QualityHigh;

            return new GraphicsState
            {
                quality = QualityToken(quality),
                antiAliasing = AntiAliasingToken(
                    FranklinMobileGraphicsSettings.AntiAliasingMode
                ),
                depthOfField = DepthOfFieldToken(
                    FranklinMobileGraphicsSettings.DepthOfFieldMode
                ),
                fbsEnabled = FranklinMobileGraphicsSettings.FbsEnabled,
                ssaoEnabled = FranklinMobileGraphicsSettings.SsaoEnabled,
                autoProfile = DynamicResolutionScaler.RecommendedAutoProfileName,
                // This build ships SSAO in both configured renderer assets. The
                // gameplay settings owner is intentionally absent from MenuScene,
                // so HasSsaoFeature cannot be used as the capability source here.
                ssaoAvailable = true,
                dofAvailable = true,
                canChangeFbs = isAuto,
                canChangeSsao = isAuto,
                canChangeAntiAliasing = isAuto,
                canChangeDepthOfField = isAuto || isHigh
            };
        }

        private GuestState BuildGuestState()
        {
            FranklinGuestProfileStore.Snapshot snapshot =
                FranklinGuestProfileStore.Read();
            return new GuestState
            {
                exists = snapshot.Exists,
                active = snapshot.Active,
                id = snapshot.Id,
                label = snapshot.Label,
                displayName = snapshot.DisplayName,
                avatar = snapshot.Avatar,
                createdUtc = snapshot.CreatedUtc,
                lastPlayedUtc = snapshot.LastPlayedUtc,
                progressOnDevice = true,
                response = string.IsNullOrWhiteSpace(this.m_GuestResponse)
                    ? "idle"
                    : this.m_GuestResponse,
                responseId = this.m_GuestResponseId ?? string.Empty,
                message = this.m_GuestResponseMessage ?? string.Empty
            };
        }

        private static MissionState BuildMissionState()
        {
            int missionCount = FranklinMissionCatalog.MissionCount;
            int completedCount = Mathf.Clamp(
                FranklinMissionProgress.HighestCompletedIndex + 1,
                0,
                missionCount
            );
            int selectedIndex = FranklinMissionProgress.SelectedMissionIndex;
            if (!FranklinMissionProgress.IsUnlocked(selectedIndex))
            {
                selectedIndex = Mathf.Clamp(completedCount, 0, missionCount - 1);
            }
            int activeIndex = FranklinMissionProgress.ActiveMissionIndex;
            string activeId = activeIndex >= 0 &&
                FranklinMissionProgress.IsUnlocked(activeIndex)
                    ? FranklinMissionCatalog.Get(activeIndex).Id
                    : string.Empty;

            MissionItemState[] items = new MissionItemState[missionCount];
            for (int index = 0; index < missionCount; index++)
            {
                FranklinMissionDefinition definition = FranklinMissionCatalog.Get(index);
                items[index] = new MissionItemState
                {
                    id = definition.Id,
                    number = index + 1,
                    title = definition.Title,
                    sender = definition.Sender,
                    description = definition.Description,
                    thumbnail = "assets/missions/mission-" +
                        (index + 1).ToString("00") + "-" + definition.Id + ".webp",
                    unlocked = FranklinMissionProgress.IsUnlocked(index),
                    completed = FranklinMissionProgress.IsCompleted(index)
                };
            }

            return new MissionState
            {
                activeId = activeId,
                selectedId = FranklinMissionCatalog.Get(selectedIndex).Id,
                completedCount = completedCount,
                totalCount = missionCount,
                items = items
            };
        }

        private static string QualityToken(int value)
        {
            switch (value)
            {
                case FranklinMobileGraphicsSettings.QualityLow: return "low";
                case FranklinMobileGraphicsSettings.QualityBalanced: return "balanced";
                case FranklinMobileGraphicsSettings.QualityHigh: return "high";
                default: return "auto";
            }
        }

        private static string AntiAliasingToken(int value)
        {
            switch (value)
            {
                case FranklinMobileGraphicsSettings.AntiAliasingOff: return "off";
                case FranklinMobileGraphicsSettings.AntiAliasingSmaa: return "smaa";
                default: return "fxaa";
            }
        }

        private static string DepthOfFieldToken(int value)
        {
            switch (value)
            {
                case FranklinMobileGraphicsSettings.DepthOfFieldNear: return "near";
                case FranklinMobileGraphicsSettings.DepthOfFieldFar: return "far";
                default: return "off";
            }
        }

        public void SetHasSave(bool hasSave)
        {
            this.m_HasSave = hasSave;
            this.PublishState();
        }

        public void SetSyncState(string syncState)
        {
            this.m_SyncState = syncState;
            this.PublishState();
        }

        public void SetProfileState(string profileState)
        {
            this.m_ProfileState = profileState;
            this.PublishState();
        }

        public void SetShowQuit(bool showQuit)
        {
            this.m_ShowQuit = showQuit;
            this.PublishState();
        }

        public void SetAudioEnabled(bool audioEnabled)
        {
            this.m_AudioEnabled = audioEnabled;
            this.PublishState();
        }

        public void SetAudioState(bool enabled, int master, int music, int sfx)
        {
            this.m_AudioEnabled = enabled;
            this.m_AudioMaster = Mathf.Clamp(master, 0, 100);
            this.m_AudioMusic = Mathf.Clamp(music, 0, 100);
            this.m_AudioSfx = Mathf.Clamp(sfx, 0, 100);
            this.PublishState();
        }

        public void SetLanguage(string language)
        {
            this.m_Language = string.IsNullOrWhiteSpace(language)
                ? "vi"
                : language.Trim();
            this.PublishState();
        }

        public void SetGuestResponse(
            string response,
            string message = "",
            string responseId = ""
        )
        {
            this.m_GuestResponse = string.IsNullOrWhiteSpace(response)
                ? "idle"
                : response.Trim().ToLowerInvariant();
            this.m_GuestResponseMessage = message ?? string.Empty;
            this.m_GuestResponseId = responseId ?? string.Empty;
            this.PublishState();
        }

        public void RefreshState()
        {
            this.PublishState();
        }

        private void PublishState()
        {
            this.StateJsonChanged?.Invoke(this.BuildStateJson());
        }
    }
}
