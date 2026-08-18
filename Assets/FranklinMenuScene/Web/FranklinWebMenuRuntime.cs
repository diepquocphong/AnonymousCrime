using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using FranklinGame.Settings;
using GameCreator.Runtime.Common;
using RobotAstro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FranklinGame.Menu.Web
{
    /// <summary>
    /// Small runtime coordinator for the first web-menu screen. It keeps Unity
    /// authoritative for save state and scene changes while all presentation and
    /// pointer/touch input stays in the local web bundle.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinWebMenuBridge))]
    [RequireComponent(typeof(FranklinWebMenuHost))]
    public sealed class FranklinWebMenuRuntime : MonoBehaviour
    {
        private const string GameplayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string AudioEnabledPreferenceKey = "Franklin.Menu.Audio.Enabled.v1";
        private const string AudioMasterPreferenceKey = "Franklin.Menu.Audio.Master.v2";
        private const string AudioMusicPreferenceKey = "Franklin.Menu.Audio.Music.v2";
        private const string AudioSfxPreferenceKey = "Franklin.Menu.Audio.Sfx.v2";
        private const string LanguagePreferenceKey = "Franklin.Menu.Language.v1";
        private const int TransitionSoundLeadMilliseconds = 220;
        private const int QuitSoundLeadMilliseconds = 90;
        private const float GraphicsSaveDebounceSeconds = 0.4f;
        private const float MenuPreferenceSaveDebounceSeconds = 0.4f;

        [Serializable]
        private sealed class AudioTogglePayload
        {
            public bool enabled;
        }

        [Serializable]
        private sealed class SettingTokenPayload
        {
            public string value;
        }

        [Serializable]
        private sealed class GuestCreatePayload
        {
            public string displayName;
            public string avatar;
            public string requestId;
        }

        [Serializable]
        private sealed class GuestLoginPayload
        {
            public string id;
            public string requestId;
        }

        [SerializeField] private FranklinWebMenuBridge m_Bridge;
        [SerializeField] private FranklinWebMenuHost m_Host;

        private bool m_IsBusy;
        private bool m_GraphicsSavePending;
        private float m_GraphicsSaveDeadline;
        private bool m_MenuPreferenceSavePending;
        private float m_MenuPreferenceSaveDeadline;

        private void Awake()
        {
            if (this.m_Bridge == null)
            {
                this.m_Bridge = this.GetComponent<FranklinWebMenuBridge>();
            }
            if (this.m_Host == null)
            {
                this.m_Host = this.GetComponent<FranklinWebMenuHost>();
            }

            this.m_Bridge.ActionReceived += this.OnActionReceived;
            this.m_Bridge.SetSyncState("offline");
            this.m_Bridge.SetProfileState("guest");
            this.m_Bridge.SetAudioState(
                PlayerPrefs.GetInt(AudioEnabledPreferenceKey, 1) != 0,
                Mathf.Clamp(PlayerPrefs.GetInt(AudioMasterPreferenceKey, 100), 0, 100),
                Mathf.Clamp(PlayerPrefs.GetInt(AudioMusicPreferenceKey, 70), 0, 100),
                Mathf.Clamp(PlayerPrefs.GetInt(AudioSfxPreferenceKey, 100), 0, 100)
            );
            string language;
            if (!TryParseLanguage(
                PlayerPrefs.GetString(LanguagePreferenceKey, "vi"),
                out language
            )) language = "vi";
            this.m_Bridge.SetLanguage(language);
        }

        private void Start()
        {
            this.RefreshSaveState();
        }

        private void Update()
        {
            if (this.m_GraphicsSavePending &&
                Time.unscaledTime >= this.m_GraphicsSaveDeadline)
            {
                this.FlushGraphicsSettings();
            }
            if (this.m_MenuPreferenceSavePending &&
                Time.unscaledTime >= this.m_MenuPreferenceSaveDeadline)
            {
                this.FlushMenuPreferences();
            }
            if (!this.m_IsBusy && Input.GetKeyDown(KeyCode.Escape))
            {
                this.m_Host.RequestBack();
            }
        }

        private void OnDestroy()
        {
            this.FlushGraphicsSettings();
            this.FlushMenuPreferences();
            if (this.m_Bridge != null)
            {
                this.m_Bridge.ActionReceived -= this.OnActionReceived;
            }
        }

        private void OnDisable()
        {
            this.FlushGraphicsSettings();
            this.FlushMenuPreferences();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                this.FlushGraphicsSettings();
                this.FlushMenuPreferences();
            }
        }

        private void OnApplicationQuit()
        {
            this.FlushGraphicsSettings();
            this.FlushMenuPreferences();
        }

        private void OnActionReceived(string action, string payload)
        {
            switch (action)
            {
                case "ready":
                    this.RefreshSaveState();
                    break;
                case "continue":
                    this.ContinueGame();
                    break;
                case "new-game":
                case "mission-open":
                    this.ApplyMenuPerformancePolicy();
                    this.m_Bridge.RefreshState();
                    break;
                case "mission-select":
                    this.ApplyMissionSelection(payload);
                    break;
                case "mission-play":
                    this.StartNewGame(payload);
                    break;
                case "settings":
                    this.ApplyMenuPerformancePolicy();
                    this.m_Bridge.RefreshState();
                    break;
                case "settings-quality":
                case "settings-aa":
                case "settings-dof":
                case "settings-fbs":
                case "settings-ssao":
                    this.ApplyGraphicsSetting(action, payload);
                    break;
                case "settings-audio-enabled":
                case "settings-audio-master":
                case "settings-audio-music":
                case "settings-audio-sfx":
                    this.ApplyAudioSetting(action, payload);
                    break;
                case "settings-language":
                    this.ApplyLanguagePreference(payload);
                    break;
                case "settings-complete":
                    this.FlushGraphicsSettings();
                    this.FlushMenuPreferences();
                    this.ApplyMenuPerformancePolicy();
                    this.m_Bridge.RefreshState();
                    break;
                case "profile":
                    this.m_Bridge.SetGuestResponse("idle");
                    break;
                case "guest-create":
                    this.ApplyGuestProfile(payload);
                    break;
                case "guest-login":
                    this.LoginGuestProfile(payload);
                    break;
                case "sync":
                    this.m_Host.ShowLocalizedToast(
                        "runtime.syncOffline",
                        "Đồng bộ đang offline; dữ liệu hiện lưu trên thiết bị.",
                        "warning"
                    );
                    break;
                case "audio-toggle":
                    this.ApplyAudioPreference(payload);
                    break;
                case "quit":
                    this.QuitApplication();
                    break;
            }
        }

        private void RefreshSaveState()
        {
            bool hasSave = false;
            try
            {
                SaveLoadManager manager = SaveLoadManager.Instance;
                hasSave = manager != null && manager.HasSave();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Franklin web menu could not inspect save data: " + exception.Message,
                    this
                );
            }
            this.m_Bridge.SetHasSave(hasSave);
        }

        private async void ContinueGame()
        {
            if (this.m_IsBusy) return;
            SaveLoadManager manager = SaveLoadManager.Instance;
            if (manager == null || !manager.HasSave())
            {
                this.RefreshSaveState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.continue.noSave",
                    "Chưa có dữ liệu lưu để tiếp tục.",
                    "error"
                );
                return;
            }
            if (manager.IsSaving || manager.IsLoading || manager.IsDeleting)
            {
                this.m_Host.ShowLocalizedToast(
                    "runtime.saveBusy",
                    "Hệ thống lưu đang bận, vui lòng thử lại.",
                    "error"
                );
                return;
            }

            this.SetBusyLocalized(
                true,
                "runtime.busy.loadingProgress",
                "ĐANG TẢI TIẾN TRÌNH..."
            );
            this.m_Host.PlaySound("transition");
            FranklinMissionProgress.ClearActiveMissionHandoff();
            try
            {
                await this.WaitForAudioLead(TransitionSoundLeadMilliseconds, true);
                if (this == null) return;
                this.PrepareGameplayGraphics();
                await manager.LoadLatest();

                if (this != null && SceneManager.GetActiveScene().name == "FranklinMenuScene")
                {
                    throw new InvalidOperationException(
                        "Dữ liệu lưu không mở được scene gameplay."
                    );
                }
                FranklinGuestProfileStore.MarkPlayed();
            }
            catch (Exception exception)
            {
                if (this == null)
                {
                    Debug.LogError(
                        "Franklin continue failed after the menu scene closed: " + exception
                    );
                    return;
                }
                Debug.LogError("Franklin continue failed: " + exception, this);
                this.ApplyMenuPerformancePolicy();
                this.SetBusy(false);
                this.m_Host.ShowLocalizedToast(
                    "runtime.continue.failed",
                    "Không thể tải dữ liệu. Vui lòng kiểm tra save.",
                    "error"
                );
            }
        }

        private void ApplyMissionSelection(string payload)
        {
            string missionId = ParseSettingToken(payload);
            int missionIndex;
            if (!FranklinMissionCatalog.TryGetIndex(missionId, out missionIndex))
            {
                this.m_Bridge.RefreshState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.mission.invalid",
                    "Màn chơi không hợp lệ.",
                    "error"
                );
                return;
            }
            if (!FranklinMissionProgress.SelectMission(missionIndex))
            {
                this.m_Bridge.RefreshState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.mission.completePrevious",
                    "Hoàn thành màn trước để mở khóa màn này.",
                    "warning"
                );
                return;
            }
            this.m_Bridge.RefreshState();
        }

        private async void StartNewGame(string payload)
        {
            if (this.m_IsBusy) return;
            string missionId = ParseSettingToken(payload);
            int missionIndex;
            if (!FranklinMissionCatalog.TryGetIndex(missionId, out missionIndex))
            {
                this.SetBusy(false);
                this.m_Bridge.RefreshState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.mission.invalid",
                    "Màn chơi không hợp lệ.",
                    "error"
                );
                return;
            }
            if (!FranklinMissionProgress.BeginMission(missionIndex))
            {
                this.SetBusy(false);
                this.m_Bridge.RefreshState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.mission.locked",
                    "Màn chơi đã chọn chưa được mở khóa.",
                    "error"
                );
                return;
            }

            int gameplayBuildIndex = SceneUtility.GetBuildIndexByScenePath(
                GameplayScenePath
            );
            if (gameplayBuildIndex < 0)
            {
                FranklinMissionProgress.ClearActiveMissionHandoff(missionIndex);
                this.SetBusy(false);
                this.m_Host.ShowLocalizedToast(
                    "runtime.gameplay.sceneMissing",
                    "Scene GamePlay chưa có trong Build Settings.",
                    "error"
                );
                return;
            }

            this.SetBusyLocalized(
                true,
                "runtime.busy.startingCity",
                "ĐANG KHỞI TẠO THÀNH PHỐ..."
            );
            this.m_Host.PlaySound("transition");
            try
            {
                await this.WaitForAudioLead(TransitionSoundLeadMilliseconds, true);
                if (this == null) return;
                this.PrepareGameplayGraphics();

                SaveLoadManager manager = SaveLoadManager.Instance;
                if (manager != null)
                {
                    if (manager.IsSaving || manager.IsLoading || manager.IsDeleting)
                    {
                        throw new InvalidOperationException("Hệ thống lưu đang bận.");
                    }
                    await manager.Restart(gameplayBuildIndex);
                }
                else
                {
                    AsyncOperation operation = SceneManager.LoadSceneAsync(
                        gameplayBuildIndex,
                        UnityEngine.SceneManagement.LoadSceneMode.Single
                    );
                    if (operation == null)
                    {
                        throw new InvalidOperationException("Không thể mở scene GamePlay.");
                    }
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }
                }
                FranklinGuestProfileStore.MarkPlayed();
            }
            catch (Exception exception)
            {
                if (this == null)
                {
                    Debug.LogError(
                        "Franklin new game failed after the menu scene closed: " + exception
                    );
                    return;
                }
                FranklinMissionProgress.ClearActiveMissionHandoff(missionIndex);
                Debug.LogError("Franklin new game failed: " + exception, this);
                this.ApplyMenuPerformancePolicy();
                this.SetBusy(false);
                this.m_Host.ShowLocalizedToast(
                    "runtime.gameplay.failed",
                    "Không thể mở GamePlay. Vui lòng kiểm tra scene.",
                    "error"
                );
            }
        }

        private void SetBusy(bool busy, string message = "")
        {
            this.m_IsBusy = busy;
            this.m_Host.SetBusy(busy, message);
        }

        private void SetBusyLocalized(bool busy, string key, string fallback)
        {
            this.m_IsBusy = busy;
            this.m_Host.SetBusyLocalized(busy, key, fallback);
        }

        private void ApplyGuestProfile(string payload)
        {
            GuestCreatePayload request;
            try
            {
                request = JsonUtility.FromJson<GuestCreatePayload>(payload);
            }
            catch (ArgumentException)
            {
                request = null;
            }

            string errorKey = string.Empty;
            if (request == null || !IsValidGuestRequestId(request.requestId) ||
                !FranklinGuestProfileStore.TryCreateOrUpdate(
                request.displayName,
                request.avatar,
                out errorKey
            ))
            {
                if (string.IsNullOrWhiteSpace(errorKey))
                {
                    errorKey = "guest.response.invalid";
                }
                this.m_Bridge.SetGuestResponse(
                    "error",
                    errorKey,
                    request != null ? request.requestId : string.Empty
                );
                this.m_Host.ShowLocalizedToast(
                    errorKey,
                    "Dữ liệu hồ sơ khách không hợp lệ.",
                    "error"
                );
                return;
            }

            const string successKey = "guest.response.saved";
            this.m_Bridge.SetGuestResponse(
                "created",
                successKey,
                request.requestId
            );
            this.m_Host.ShowLocalizedToast(
                successKey,
                "Hồ sơ khách đã được lưu trên thiết bị.",
                "success"
            );
        }

        private void LoginGuestProfile(string payload)
        {
            GuestLoginPayload request;
            try
            {
                request = JsonUtility.FromJson<GuestLoginPayload>(payload);
            }
            catch (ArgumentException)
            {
                request = null;
            }

            string errorKey = string.Empty;
            if (request == null || !IsValidGuestRequestId(request.requestId) ||
                !FranklinGuestProfileStore.TryLogin(
                request.id,
                out errorKey
            ))
            {
                if (string.IsNullOrWhiteSpace(errorKey))
                {
                    errorKey = "guest.response.notFound";
                }
                this.m_Bridge.SetGuestResponse(
                    "error",
                    errorKey,
                    request != null ? request.requestId : string.Empty
                );
                this.m_Host.ShowLocalizedToast(
                    errorKey,
                    "Không tìm thấy hồ sơ khách trên thiết bị.",
                    "error"
                );
                return;
            }

            const string successKey = "guest.response.opened";
            this.m_Bridge.SetGuestResponse(
                "logged-in",
                successKey,
                request.requestId
            );
            this.m_Host.ShowLocalizedToast(
                successKey,
                "Đã mở hồ sơ khách trên thiết bị.",
                "success"
            );
        }

        private void ApplyGraphicsSetting(string action, string payload)
        {
            string token = ParseSettingToken(payload);
            bool valid = false;

            switch (action)
            {
                case "settings-quality":
                    int quality;
                    valid = TryParseQuality(token, out quality);
                    if (valid)
                    {
                        FranklinMobileGraphicsSettings.SetQualityLevel(quality, false);
                    }
                    break;
                case "settings-aa":
                    int antiAliasing;
                    valid = TryParseAntiAliasing(token, out antiAliasing);
                    if (valid)
                    {
                        FranklinMobileGraphicsSettings.SetAntiAliasingMode(
                            antiAliasing,
                            false
                        );
                    }
                    break;
                case "settings-dof":
                    int depthOfField;
                    valid = TryParseDepthOfField(token, out depthOfField);
                    if (valid)
                    {
                        FranklinMobileGraphicsSettings.SetDepthOfFieldMode(
                            depthOfField,
                            false
                        );
                    }
                    break;
                case "settings-fbs":
                    bool fbsEnabled;
                    valid = TryParseToggle(token, out fbsEnabled);
                    if (valid)
                    {
                        FranklinMobileGraphicsSettings.SetFbsEnabled(fbsEnabled, false);
                    }
                    break;
                case "settings-ssao":
                    bool ssaoEnabled;
                    valid = TryParseToggle(token, out ssaoEnabled);
                    if (valid)
                    {
                        FranklinMobileGraphicsSettings.SetSsaoEnabled(ssaoEnabled, false);
                    }
                    break;
            }

            if (valid) this.ScheduleGraphicsSave();
            this.ApplyMenuPerformancePolicy();
            this.m_Bridge.RefreshState();
            if (!valid)
            {
                this.m_Host.ShowLocalizedToast(
                    "runtime.settings.invalid",
                    "Giá trị cài đặt không hợp lệ.",
                    "error"
                );
            }
        }

        private static bool IsValidGuestRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64)
            {
                return false;
            }
            for (int index = 0; index < requestId.Length; index++)
            {
                char character = requestId[index];
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_')
                {
                    return false;
                }
            }
            return true;
        }

        private static string ParseSettingToken(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
            SettingTokenPayload preference;
            try
            {
                preference = JsonUtility.FromJson<SettingTokenPayload>(payload);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            return preference == null || string.IsNullOrWhiteSpace(preference.value)
                ? string.Empty
                : preference.value.Trim().ToLowerInvariant();
        }

        private static bool TryParseQuality(string token, out int value)
        {
            switch (token)
            {
                case "low":
                    value = FranklinMobileGraphicsSettings.QualityLow;
                    return true;
                case "balanced":
                    value = FranklinMobileGraphicsSettings.QualityBalanced;
                    return true;
                case "high":
                    value = FranklinMobileGraphicsSettings.QualityHigh;
                    return true;
                case "auto":
                    value = FranklinMobileGraphicsSettings.QualityAuto;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryParseAntiAliasing(string token, out int value)
        {
            switch (token)
            {
                case "off":
                    value = FranklinMobileGraphicsSettings.AntiAliasingOff;
                    return true;
                case "fxaa":
                    value = FranklinMobileGraphicsSettings.AntiAliasingFxaa;
                    return true;
                case "smaa":
                    value = FranklinMobileGraphicsSettings.AntiAliasingSmaa;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryParseDepthOfField(string token, out int value)
        {
            switch (token)
            {
                case "off":
                    value = FranklinMobileGraphicsSettings.DepthOfFieldOff;
                    return true;
                case "near":
                    value = FranklinMobileGraphicsSettings.DepthOfFieldNear;
                    return true;
                case "far":
                    value = FranklinMobileGraphicsSettings.DepthOfFieldFar;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryParseToggle(string token, out bool value)
        {
            if (token == "on")
            {
                value = true;
                return true;
            }
            if (token == "off")
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private void ApplyMenuPerformancePolicy()
        {
            DynamicResolutionScaler.SetMenuSuspended(true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
        }

        private void ScheduleGraphicsSave()
        {
            this.m_GraphicsSavePending = true;
            this.m_GraphicsSaveDeadline =
                Time.unscaledTime + GraphicsSaveDebounceSeconds;
        }

        private void FlushGraphicsSettings()
        {
            if (!this.m_GraphicsSavePending) return;
            this.m_GraphicsSavePending = false;
            FranklinMobileGraphicsSettings.PersistCurrentSettings();
        }

        private void PrepareGameplayGraphics()
        {
            this.FlushGraphicsSettings();
            this.FlushMenuPreferences();
            DynamicResolutionScaler.SetMenuSuspended(false);
            FranklinMobileGraphicsSettings.ReapplyCurrentSettings();
        }

        private void ApplyAudioPreference(string payload)
        {
            AudioTogglePayload preference;
            try
            {
                preference = JsonUtility.FromJson<AudioTogglePayload>(payload);
            }
            catch (ArgumentException)
            {
                preference = null;
            }

            if (preference == null)
            {
                this.PublishAudioState();
                return;
            }

            PlayerPrefs.SetInt(AudioEnabledPreferenceKey, preference.enabled ? 1 : 0);
            this.ScheduleMenuPreferenceSave();
            this.PublishAudioState();
        }

        private void ApplyAudioSetting(string action, string payload)
        {
            string token = ParseSettingToken(payload);
            bool valid = false;

            if (action == "settings-audio-enabled")
            {
                bool enabled;
                valid = TryParseToggle(token, out enabled);
                if (valid)
                {
                    PlayerPrefs.SetInt(AudioEnabledPreferenceKey, enabled ? 1 : 0);
                }
            }
            else
            {
                int percentage;
                valid = TryParsePercentage(token, out percentage);
                if (valid)
                {
                    if (action == "settings-audio-master")
                    {
                        PlayerPrefs.SetInt(AudioMasterPreferenceKey, percentage);
                    }
                    else if (action == "settings-audio-music")
                    {
                        PlayerPrefs.SetInt(AudioMusicPreferenceKey, percentage);
                    }
                    else if (action == "settings-audio-sfx")
                    {
                        PlayerPrefs.SetInt(AudioSfxPreferenceKey, percentage);
                    }
                    else
                    {
                        valid = false;
                    }
                }
            }

            if (!valid)
            {
                this.PublishAudioState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.audio.invalid",
                    "Giá trị âm thanh không hợp lệ.",
                    "error"
                );
                return;
            }

            this.ScheduleMenuPreferenceSave();
            this.PublishAudioState();
        }

        private void ApplyLanguagePreference(string payload)
        {
            string language;
            if (!TryParseLanguage(ParseSettingToken(payload), out language))
            {
                this.m_Bridge.RefreshState();
                this.m_Host.ShowLocalizedToast(
                    "runtime.language.invalid",
                    "Ngôn ngữ đã chọn không hợp lệ.",
                    "error"
                );
                return;
            }

            PlayerPrefs.SetString(LanguagePreferenceKey, language);
            this.ScheduleMenuPreferenceSave();
            this.m_Bridge.SetLanguage(language);
        }

        private void PublishAudioState()
        {
            this.m_Bridge.SetAudioState(
                PlayerPrefs.GetInt(AudioEnabledPreferenceKey, 1) != 0,
                Mathf.Clamp(PlayerPrefs.GetInt(AudioMasterPreferenceKey, 100), 0, 100),
                Mathf.Clamp(PlayerPrefs.GetInt(AudioMusicPreferenceKey, 70), 0, 100),
                Mathf.Clamp(PlayerPrefs.GetInt(AudioSfxPreferenceKey, 100), 0, 100)
            );
        }

        private static bool TryParsePercentage(string token, out int value)
        {
            return int.TryParse(
                token,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value
            ) && value >= 0 && value <= 100;
        }

        private static bool TryParseLanguage(string token, out string language)
        {
            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vi": language = "vi"; return true;
                case "en":
                case "en-us":
                case "en-gb":
                case "en-ca":
                case "en-au":
                case "en-nz":
                case "en-sg": language = "en"; return true;
                case "zh-hans":
                case "zh-cn": language = "zh-Hans"; return true;
                case "zh-hant":
                case "zh-tw":
                case "zh-hk":
                case "zh-mo": language = "zh-Hant"; return true;
                case "de":
                case "de-de":
                case "de-at":
                case "de-ch": language = "de"; return true;
                case "fr":
                case "fr-fr":
                case "fr-ca":
                case "fr-be":
                case "fr-ch": language = "fr"; return true;
                case "ja":
                case "ja-jp": language = "ja"; return true;
                case "ko":
                case "ko-kr": language = "ko"; return true;
                case "nl":
                case "nl-nl":
                case "nl-be": language = "nl"; return true;
                case "ru":
                case "ru-ru": language = "ru"; return true;
                case "pt":
                case "pt-pt":
                case "pt-br": language = "pt-BR"; return true;
                case "es":
                case "es-es":
                case "es-mx":
                case "es-419": language = "es-419"; return true;
                default: language = "vi"; return false;
            }
        }

        private void ScheduleMenuPreferenceSave()
        {
            this.m_MenuPreferenceSavePending = true;
            this.m_MenuPreferenceSaveDeadline =
                Time.unscaledTime + MenuPreferenceSaveDebounceSeconds;
        }

        private void FlushMenuPreferences()
        {
            if (!this.m_MenuPreferenceSavePending) return;
            this.m_MenuPreferenceSavePending = false;
            PlayerPrefs.Save();
        }

        private async void QuitApplication()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return;
#else
            this.FlushMenuPreferences();
            await this.WaitForAudioLead(QuitSoundLeadMilliseconds, false);
            if (this == null) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
#endif
        }

        private async Task WaitForAudioLead(int milliseconds, bool yieldWhenMuted)
        {
            bool cueCanBeHeard =
                PlayerPrefs.GetInt(AudioEnabledPreferenceKey, 1) != 0 &&
                PlayerPrefs.GetInt(AudioMasterPreferenceKey, 100) > 0 &&
                PlayerPrefs.GetInt(AudioSfxPreferenceKey, 100) > 0;
            if (cueCanBeHeard)
            {
                await Task.Delay(milliseconds);
            }
            else if (yieldWhenMuted)
            {
                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// One offline guest identity for the current device. GameCreator saves and
    /// mission progress are currently device-global, so updating the profile
    /// intentionally preserves the existing identity instead of creating a
    /// second, misleading account slot.
    /// </summary>
    internal static class FranklinGuestProfileStore
    {
        private const int CurrentProfileVersion = 1;
        private const string IdKey = "Franklin.Menu.GuestId";
        private const string ProfileVersionKey =
            "Franklin.Menu.Guest.ProfileVersion.v1";
        private const string DisplayNameKey =
            "Franklin.Menu.Guest.DisplayName.v1";
        private const string AvatarKey = "Franklin.Menu.Guest.Avatar.v1";
        private const string CreatedUtcKey =
            "Franklin.Menu.Guest.CreatedUtc.v1";
        private const string LastPlayedUtcKey =
            "Franklin.Menu.Guest.LastPlayedUtc.v1";
        private const string ActiveKey = "Franklin.Menu.Guest.Active.v1";

        internal sealed class Snapshot
        {
            public bool Exists;
            public bool Active;
            public string Id;
            public string Label;
            public string DisplayName;
            public string Avatar;
            public string CreatedUtc;
            public string LastPlayedUtc;
        }

        internal static Snapshot Read()
        {
            string id = ReadOrRepairId();
            bool exists = !string.IsNullOrWhiteSpace(id);
            string displayName = PlayerPrefs.GetString(
                DisplayNameKey,
                string.Empty
            );
            string normalizedName;
            if (!TryNormalizeDisplayName(displayName, out normalizedName))
            {
                // Let the web layer show its locale-specific guest.player fallback.
                normalizedName = string.Empty;
            }

            string avatar = PlayerPrefs.GetString(AvatarKey, "avatar-1");
            if (!IsSupportedAvatar(avatar)) avatar = "avatar-1";

            return new Snapshot
            {
                Exists = exists,
                Active = exists && PlayerPrefs.GetInt(ActiveKey, 1) != 0,
                Id = exists ? id : string.Empty,
                Label = exists ? BuildDisplayLabel(id) : "GUEST-LOCAL",
                DisplayName = normalizedName,
                Avatar = avatar,
                CreatedUtc = PlayerPrefs.GetString(CreatedUtcKey, string.Empty),
                LastPlayedUtc = PlayerPrefs.GetString(
                    LastPlayedUtcKey,
                    string.Empty
                )
            };
        }

        internal static bool TryCreateOrUpdate(
            string displayName,
            string avatar,
            out string error
        )
        {
            string normalizedName;
            if (!TryNormalizeDisplayName(displayName, out normalizedName))
            {
                error = "guest.error.profileInvalid";
                return false;
            }
            if (!IsSupportedAvatar(avatar))
            {
                error = "guest.error.avatar";
                return false;
            }

            string id = ReadOrRepairId();
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N").ToUpperInvariant();
                PlayerPrefs.SetString(IdKey, id);
            }

            string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            PlayerPrefs.SetInt(ProfileVersionKey, CurrentProfileVersion);
            PlayerPrefs.SetString(DisplayNameKey, normalizedName);
            PlayerPrefs.SetString(AvatarKey, avatar);
            if (string.IsNullOrWhiteSpace(PlayerPrefs.GetString(CreatedUtcKey, string.Empty)))
            {
                PlayerPrefs.SetString(CreatedUtcKey, now);
            }
            PlayerPrefs.SetInt(ActiveKey, 1);
            PlayerPrefs.Save();
            error = string.Empty;
            return true;
        }

        internal static bool TryLogin(string id, out string error)
        {
            Snapshot snapshot = Read();
            if (!snapshot.Exists || string.IsNullOrWhiteSpace(id) ||
                !string.Equals(snapshot.Id, id.Trim(), StringComparison.Ordinal))
            {
                error = "guest.response.notFound";
                return false;
            }

            PlayerPrefs.SetInt(ActiveKey, 1);
            PlayerPrefs.Save();
            error = string.Empty;
            return true;
        }

        internal static void MarkPlayed()
        {
            Snapshot snapshot = Read();
            if (!snapshot.Exists) return;
            PlayerPrefs.SetString(
                LastPlayedUtcKey,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            );
            PlayerPrefs.SetInt(ActiveKey, 1);
            PlayerPrefs.Save();
        }

        private static bool IsSupportedAvatar(string avatar)
        {
            return avatar == "avatar-1" ||
                avatar == "avatar-2" ||
                avatar == "avatar-3";
        }

        private static string ReadOrRepairId()
        {
            string id = PlayerPrefs.GetString(IdKey, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            if (IsSupportedStoredId(id)) return id;

            // Older builds used the first eight hexadecimal GUID characters;
            // current builds store all 32. Repair only malformed values. Save
            // and mission data remain untouched because they are device-global.
            string repaired = Guid.NewGuid().ToString("N").ToUpperInvariant();
            PlayerPrefs.SetString(IdKey, repaired);
            PlayerPrefs.SetInt(ProfileVersionKey, CurrentProfileVersion);
            PlayerPrefs.Save();
            return repaired;
        }

        private static bool IsSupportedStoredId(string id)
        {
            if (id.Length != 8 && id.Length != 32) return false;
            for (int index = 0; index < id.Length; index++)
            {
                char character = id[index];
                bool hexadecimal =
                    character >= '0' && character <= '9' ||
                    character >= 'A' && character <= 'F' ||
                    character >= 'a' && character <= 'f';
                if (!hexadecimal) return false;
            }
            return true;
        }

        private static bool TryNormalizeDisplayName(
            string value,
            out string normalized
        )
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string source;
            try
            {
                source = value.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return false;
            }

            StringBuilder builder = new StringBuilder(source.Length);
            bool previousWasSpace = true;
            bool hasLetterOrDigit = false;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (char.IsControl(character) ||
                    character == '\u200B' ||
                    character == '\u200C' ||
                    character == '\u200D' ||
                    character == '\uFEFF' ||
                    char.IsSurrogate(character))
                {
                    return false;
                }
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasSpace)
                    {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }
                    continue;
                }

                UnicodeCategory category = char.GetUnicodeCategory(character);
                bool letterOrDigit = char.IsLetterOrDigit(character);
                bool combiningMark =
                    category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.SpacingCombiningMark;
                bool allowed = letterOrDigit ||
                    combiningMark ||
                    character == '_' ||
                    character == '-';
                if (!allowed) return false;

                builder.Append(character);
                if (letterOrDigit) hasLetterOrDigit = true;
                previousWasSpace = false;
            }

            normalized = builder.ToString().Trim();
            return normalized.Length >= 2 &&
                normalized.Length <= 20 &&
                hasLetterOrDigit;
        }

        private static string BuildDisplayLabel(string id)
        {
            StringBuilder compact = new StringBuilder(8);
            for (int index = 0; index < id.Length && compact.Length < 8; index++)
            {
                char character = id[index];
                if (char.IsLetterOrDigit(character))
                {
                    compact.Append(char.ToUpperInvariant(character));
                }
            }
            if (compact.Length == 0) compact.Append("LOCAL");
            return "GUEST-" + compact;
        }
    }
}
