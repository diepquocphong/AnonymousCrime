using System;
using System.Collections;
using FranklinGame.Animations;
using FranklinGame.UI;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FranklinGame.Combat
{
    /// <summary>
    /// Runs the complete Player death presentation when the GC2 hp Attribute reaches zero.
    /// Ragdoll starts immediately, followed by an original GTA-inspired WASTED treatment.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinDeathSequence : MonoBehaviour
    {
        private const int AUDIO_SAMPLE_RATE = 44100;
        private const float MIN_TIME_SCALE = 0.01f;

        private static FranklinDeathSequence s_ActiveSequence;

        [Header("References")]
        [SerializeField] private Character m_Character;
        [SerializeField] private FranklinBloodLossController m_BloodLoss;
        [SerializeField] private Font m_WastedFont;

        [Header("Death timing")]
        [SerializeField, Min(0f)] private float m_RagdollLeadIn = 0.6f;
        [SerializeField, Min(0.05f)] private float m_OverlayFadeDuration = 0.22f;
        [SerializeField, Range(0.05f, 1f)] private float m_DeathTimeScale = 0.18f;
        [SerializeField, Min(0.05f)] private float m_SlowMotionBlend = 0.3f;

        [Header("Death camera")]
        [SerializeField, Min(0.1f)] private float m_CameraPullDuration = 2.4f;
        [SerializeField, Min(0f)] private float m_CameraPullBack = 3.75f;
        [SerializeField, Min(0f)] private float m_CameraLift = 1.15f;
        [SerializeField, Min(0f)] private float m_FieldOfViewIncrease = 7f;

        [Header("Presentation")]
        [SerializeField] private Color m_WastedColor = new Color32(202, 48, 40, 255);
        [SerializeField, Range(0f, 1f)] private float m_StingVolume = 0.82f;

        [Header("Respawn choices")]
        [SerializeField, Min(0f)] private float m_RespawnPromptDelay = 5f;
        [SerializeField] private string m_HospitalMarkerName = "MarkerHopital";
        [SerializeField] private MonoBehaviour m_RewardedAdProvider;
        [SerializeField] private bool m_SimulateRewardedAdsInEditor = true;

        [Header("Respawn transition")]
        [SerializeField, Min(0.01f)] private float m_FadeToBlackDuration = 0.12f;
        [SerializeField, Min(0f)] private float m_BlackScreenHoldDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float m_FadeFromBlackDuration = 0.9f;

        private CanvasGroup m_OverlayGroup;
        private CanvasGroup m_WastedBandGroup;
        private RectTransform m_WastedTextTransform;
        private Image m_ScreenFlash;
        private GameObject m_OverlayObject;
        private GameObject m_PostFxObject;
        private Volume m_PostFxVolume;
        private VolumeProfile m_RuntimeProfile;
        private AudioSource m_AudioSource;
        private AudioClip m_OriginalSting;
        private FranklinDeathRespawnPanel m_RespawnPanel;
        private Marker m_HospitalMarker;
        private GameObject m_RespawnTransitionObject;
        private CanvasGroup m_RespawnTransitionGroup;

        private Camera m_DeathCamera;
        private UniversalAdditionalCameraData m_CameraData;
        private Transform m_LookTarget;
        private Vector3 m_CameraStartPosition;
        private Quaternion m_CameraStartRotation;
        private Vector3 m_CameraPullDirection;
        private float m_CameraStartFieldOfView;
        private bool m_CameraPostProcessingWasEnabled;

        private bool m_DeathSequenceActive;
        private bool m_WastedRevealed;
        private bool m_RagdollStartInProgress;
        private bool m_RespawnPromptShown;
        private bool m_RespawnInProgress;
        private bool m_RewardedVideoPending;
        private bool m_RespawnTransitionActive;
        private bool m_RespawnTransitionFadingOut;
        private int m_DeathVersion;
        private int m_RespawnTransitionVersion;
        private float m_DeathStartedAt;
        private float m_WastedRevealedAt;
        private float m_RespawnPromptShownAt;
        private float m_PreviousTimeScale = 1f;
        private float m_PreviousFixedDeltaTime = 0.02f;
        private Vector3 m_DeathFeetPosition;
        private Quaternion m_DeathFacing;

        public bool IsDeathSequenceActive => this.m_DeathSequenceActive;
        public bool IsWastedVisible => this.m_WastedRevealed &&
                                       this.m_OverlayGroup != null &&
                                       this.m_OverlayGroup.alpha > 0.9f;
        public bool IsRespawnPromptVisible => this.m_RespawnPanel != null &&
                                              this.m_RespawnPanel.IsVisible;
        public bool IsRespawning => this.m_RespawnInProgress;

        private void Reset()
        {
            this.m_Character = this.GetComponent<Character>();
            this.m_BloodLoss = this.GetComponentInChildren<
                FranklinBloodLossController
            >(true);
        }

        private void Awake()
        {
            if (this.m_Character == null) this.m_Character = this.GetComponent<Character>();
            if (this.m_BloodLoss == null)
            {
                this.m_BloodLoss = this.GetComponentInChildren<
                    FranklinBloodLossController
                >(true);
            }
        }

        private void OnEnable()
        {
            if (this.m_BloodLoss != null)
            {
                this.m_BloodLoss.EventSeverityChanged += this.OnSeverityChanged;
            }

            if (this.m_Character != null)
            {
                this.m_Character.Ragdoll.EventBeforeStartRagdoll +=
                    this.OnBeforeStartRagdoll;
                this.m_Character.Ragdoll.EventAfterStartRagdoll +=
                    this.OnAfterStartRagdoll;
                this.m_Character.Ragdoll.EventAfterFinishRecover +=
                    this.OnAfterFinishRecover;
            }
        }

        private void Start()
        {
            if (this.m_BloodLoss != null &&
                this.m_BloodLoss.Severity == FranklinBloodLossController.BleedSeverity.Dead)
            {
                this.BeginDeathSequence();
            }
        }

        private void OnDisable()
        {
            if (this.m_BloodLoss != null)
            {
                this.m_BloodLoss.EventSeverityChanged -= this.OnSeverityChanged;
            }

            if (this.m_Character != null)
            {
                this.m_Character.Ragdoll.EventBeforeStartRagdoll -=
                    this.OnBeforeStartRagdoll;
                this.m_Character.Ragdoll.EventAfterStartRagdoll -=
                    this.OnAfterStartRagdoll;
                this.m_Character.Ragdoll.EventAfterFinishRecover -=
                    this.OnAfterFinishRecover;
            }

            if (this.m_DeathSequenceActive) this.StopDeathSequence(false);
        }

        private void OnDestroy()
        {
            this.DestroyPresentationObjects();
            this.CancelRespawnTransition();
        }

        private void Update()
        {
            if (!this.m_DeathSequenceActive) return;

            float elapsed = Time.unscaledTime - this.m_DeathStartedAt;
            if (!this.m_WastedRevealed && elapsed >= this.m_RagdollLeadIn)
            {
                this.RevealWasted();
            }

            if (!this.m_WastedRevealed) return;

            float revealElapsed = Time.unscaledTime - this.m_WastedRevealedAt;
            float overlayT = Mathf.Clamp01(revealElapsed / this.m_OverlayFadeDuration);
            float easedOverlay = SmoothStep(overlayT);

            if (this.m_OverlayGroup != null) this.m_OverlayGroup.alpha = easedOverlay;
            if (this.m_PostFxVolume != null) this.m_PostFxVolume.weight = easedOverlay;

            if (this.m_WastedTextTransform != null)
            {
                float punch = 1f + (1f - easedOverlay) * 0.16f;
                this.m_WastedTextTransform.localScale = Vector3.one * punch;
            }

            if (this.m_ScreenFlash != null)
            {
                float flash = Mathf.Clamp01(1f - revealElapsed / 0.18f) * 0.25f;
                this.m_ScreenFlash.color = new Color(1f, 1f, 1f, flash);
            }

            if (!this.m_RespawnPromptShown &&
                revealElapsed >= this.m_RespawnPromptDelay)
            {
                this.ShowRespawnPrompt();
            }

            if (this.m_RespawnPromptShown && this.m_WastedBandGroup != null)
            {
                float promptT = SmoothStep(Mathf.Clamp01(
                    (Time.unscaledTime - this.m_RespawnPromptShownAt) / 0.25f
                ));
                this.m_WastedBandGroup.alpha = 1f - promptT;
            }

            if (this.m_RespawnInProgress)
            {
                Time.timeScale = this.m_PreviousTimeScale;
                Time.fixedDeltaTime = this.m_PreviousFixedDeltaTime;
            }
            else
            {
                float slowT = SmoothStep(Mathf.Clamp01(
                    revealElapsed / this.m_SlowMotionBlend
                ));
                float targetScale = Mathf.Min(
                    this.m_PreviousTimeScale,
                    this.m_DeathTimeScale
                );
                Time.timeScale = Mathf.Lerp(this.m_PreviousTimeScale, targetScale, slowT);
                this.UpdateFixedDeltaTime();
            }
        }

        private void LateUpdate()
        {
            if (!this.m_DeathSequenceActive || !this.m_WastedRevealed ||
                this.m_DeathCamera == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - this.m_WastedRevealedAt;
            float cameraT = SmoothStep(Mathf.Clamp01(elapsed / this.m_CameraPullDuration));
            Vector3 targetPosition = this.m_CameraStartPosition +
                this.m_CameraPullDirection * this.m_CameraPullBack +
                Vector3.up * this.m_CameraLift;
            Vector3 position = Vector3.Lerp(
                this.m_CameraStartPosition,
                targetPosition,
                cameraT
            );

            Vector3 lookPoint = this.GetLookPoint();
            Vector3 lookDirection = lookPoint - position;
            Quaternion targetRotation = lookDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : this.m_CameraStartRotation;

            this.m_DeathCamera.transform.SetPositionAndRotation(
                position,
                Quaternion.Slerp(this.m_CameraStartRotation, targetRotation, cameraT)
            );
            this.m_DeathCamera.fieldOfView = Mathf.Lerp(
                this.m_CameraStartFieldOfView,
                Mathf.Min(100f, this.m_CameraStartFieldOfView + this.m_FieldOfViewIncrease),
                cameraT
            );
        }

        private void OnSeverityChanged(FranklinBloodLossController.BleedSeverity severity)
        {
            if (severity == FranklinBloodLossController.BleedSeverity.Dead)
            {
                this.BeginDeathSequence();
            }
            else if (this.m_DeathSequenceActive)
            {
                this.StopDeathSequence(true);
            }
        }

        private void BeginDeathSequence()
        {
            if (this.m_DeathSequenceActive) return;
            if (s_ActiveSequence != null && s_ActiveSequence != this) return;

            this.CancelRespawnTransition();
            s_ActiveSequence = this;
            this.m_DeathSequenceActive = true;
            ++this.m_DeathVersion;
            this.m_WastedRevealed = false;
            this.m_RespawnPromptShown = false;
            this.m_RespawnInProgress = false;
            this.m_RewardedVideoPending = false;
            this.m_DeathStartedAt = Time.unscaledTime;
            this.m_PreviousTimeScale = Time.timeScale;
            this.m_PreviousFixedDeltaTime = Time.fixedDeltaTime;
            this.m_DeathFeetPosition = this.m_Character.Feet;
            Vector3 facing = Vector3.ProjectOnPlane(
                this.m_Character.transform.forward,
                Vector3.up
            );
            this.m_DeathFacing = facing.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up)
                : this.m_Character.transform.rotation;

            this.m_Character.IsDead = true;
            if (this.m_Character.Player != null)
            {
                this.m_Character.Player.IsControllable = false;
            }

            FranklinMobileHud.SetControlsSuppressed(true);
            this.BuildPresentation();
            this.RequestRagdollOnce();
        }

        private async void RequestRagdollOnce()
        {
            if (!this.m_DeathSequenceActive || this.m_Character == null) return;
            if (this.m_Character.Ragdoll.Get<RagdollDefault>() == null) return;
            if (this.m_Character.Ragdoll.IsRagdoll || this.m_RagdollStartInProgress) return;

            this.m_RagdollStartInProgress = true;
            int deathVersion = this.m_DeathVersion;
            try
            {
                bool releasedBikePose = this.ReleaseBikeRiderPoseForDeath();
                if (releasedBikePose)
                {
                    // BikeEntry removes its state and IK immediately. Let GC2's
                    // playable graph evaluate once before RagdollDefault disables
                    // the Animator and freezes the current bone transforms.
                    await System.Threading.Tasks.Task.Yield();
                    if (!this.m_DeathSequenceActive ||
                        deathVersion != this.m_DeathVersion ||
                        this.m_Character == null)
                    {
                        return;
                    }

                    Animator animator = this.m_Character.Animim?.Animator;
                    if (animator != null && animator.isActiveAndEnabled)
                    {
                        animator.Update(0f);
                    }
                }

                await this.m_Character.Ragdoll.StartRagdoll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                this.m_RagdollStartInProgress = false;
            }
        }

        private bool ReleaseBikeRiderPoseForDeath()
        {
            FranklinVehicleInteractionManager vehicleManager =
                this.m_Character.GetComponentInChildren<
                    FranklinVehicleInteractionManager
                >(true);
            if (vehicleManager != null && vehicleManager.ReleaseActiveBikeForDeath())
            {
                this.m_Character.GetComponentInChildren<FranklinAnimationBridge>(true)
                    ?.RestoreModelRootBaseline();
                return true;
            }

            // Fallback for scenes where the shared manager was not installed or
            // lost its active reference. Death is rare, so a one-time scene scan
            // is preferable to leaving the rider parented to a Bike seat.
            BikeEntry[] bikeEntries = FindObjectsByType<BikeEntry>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            foreach (BikeEntry bikeEntry in bikeEntries)
            {
                if (bikeEntry == null ||
                    bikeEntry.SeatedCharacter != this.m_Character ||
                    !bikeEntry.ReleaseForCrash(this.m_Character))
                {
                    continue;
                }

                this.m_Character.GetComponentInChildren<FranklinAnimationBridge>(true)
                    ?.RestoreModelRootBaseline();
                return true;
            }

            return false;
        }

        private void RevealWasted()
        {
            if (this.m_WastedRevealed) return;

            this.m_WastedRevealed = true;
            this.m_WastedRevealedAt = Time.unscaledTime;
            this.CaptureDeathCamera();

            if (this.m_AudioSource != null && this.m_OriginalSting != null)
            {
                this.m_AudioSource.PlayOneShot(this.m_OriginalSting, this.m_StingVolume);
            }
        }

        private void StopDeathSequence(bool revived)
        {
            bool deferControlRestore = revived && this.m_RespawnTransitionActive;
            bool wasRespawning = this.m_RespawnInProgress;
            this.m_DeathSequenceActive = false;
            this.m_WastedRevealed = false;
            this.m_RagdollStartInProgress = false;
            this.m_RespawnPromptShown = false;
            this.m_RespawnInProgress = false;
            this.m_RewardedVideoPending = false;

            Time.timeScale = this.m_PreviousTimeScale;
            Time.fixedDeltaTime = this.m_PreviousFixedDeltaTime;

            if (this.m_DeathCamera != null)
            {
                this.m_DeathCamera.fieldOfView = this.m_CameraStartFieldOfView;
            }
            if (this.m_CameraData != null)
            {
                this.m_CameraData.renderPostProcessing =
                    this.m_CameraPostProcessingWasEnabled;
            }

            if (revived && this.m_Character != null)
            {
                this.m_Character.IsDead = false;
                if (this.m_Character.Player != null)
                {
                    this.m_Character.Player.IsControllable = !deferControlRestore;
                }
            }

            FranklinMobileHud.SetControlsSuppressed(deferControlRestore);

            if (s_ActiveSequence == this) s_ActiveSequence = null;
            this.DestroyPresentationObjects();
            if (!deferControlRestore) this.CancelRespawnTransition();
            else if (!wasRespawning) this.FadeCurrentRespawnTransitionOut();
        }

        private void OnBeforeStartRagdoll()
        {
            this.m_RagdollStartInProgress = true;
        }

        private void OnAfterStartRagdoll()
        {
            this.m_RagdollStartInProgress = false;
        }

        private void OnAfterFinishRecover()
        {
            if (this.m_DeathSequenceActive && !this.m_RespawnInProgress)
            {
                this.RequestRagdollOnce();
            }
        }

        private void ShowRespawnPrompt()
        {
            if (this.m_RespawnPromptShown || !this.m_DeathSequenceActive) return;

            this.m_RespawnPromptShown = true;
            this.m_RespawnPromptShownAt = Time.unscaledTime;
            this.m_HospitalMarker = this.FindHospitalMarker();
            this.m_RespawnPanel?.Show(this.m_HospitalMarker != null);
        }

        private void OnReviveHereRequested()
        {
            if (!this.m_DeathSequenceActive || this.m_RespawnInProgress ||
                this.m_RewardedVideoPending)
            {
                return;
            }

            IFranklinRewardedAdProvider provider = this.ResolveRewardedAdProvider();
            if (provider != null)
            {
                if (!provider.IsRewardedVideoReady)
                {
                    this.m_RespawnPanel?.SetError(
                        "REWARDED VIDEO ĐANG TẢI — VUI LÒNG THỬ LẠI"
                    );
                    return;
                }

                this.m_RewardedVideoPending = true;
                this.m_RespawnPanel?.SetBusy(true, "ĐANG MỞ REWARDED VIDEO...");
                this.StartRespawnTransitionToBlack();
                try
                {
                    int requestVersion = this.m_DeathVersion;
                    provider.ShowRewardedVideo(
                        reward => this.OnRewardedVideoCompleted(requestVersion, reward)
                    );
                }
                catch (Exception exception)
                {
                    this.m_RewardedVideoPending = false;
                    this.m_RespawnPanel?.SetError("KHÔNG THỂ PHÁT REWARDED VIDEO");
                    this.FadeCurrentRespawnTransitionOut();
                    Debug.LogException(exception, this);
                }
                return;
            }

            #if UNITY_EDITOR
            if (this.m_SimulateRewardedAdsInEditor)
            {
                this.m_RewardedVideoPending = true;
                this.StartRespawnTransitionToBlack();
                this.StartCoroutine(this.SimulateRewardedVideo(this.m_DeathVersion));
                return;
            }
            #endif

            this.m_RespawnPanel?.SetError(
                "REWARDED ADS CHƯA ĐƯỢC CẤU HÌNH — KHÔNG CẤP HỒI SINH"
            );
        }

        private void OnHospitalRequested()
        {
            if (!this.m_DeathSequenceActive || this.m_RespawnInProgress ||
                this.m_RewardedVideoPending)
            {
                return;
            }

            this.m_HospitalMarker = this.FindHospitalMarker();
            if (this.m_HospitalMarker == null)
            {
                this.m_RespawnPanel?.SetError("KHÔNG TÌM THẤY MARKER BỆNH VIỆN");
                return;
            }

            this.RespawnPlayer(true);
        }

        private IEnumerator SimulateRewardedVideo(int requestVersion)
        {
            this.m_RespawnPanel?.SetBusy(
                true,
                "EDITOR: ĐANG PHÁT REWARDED VIDEO THỬ NGHIỆM..."
            );
            yield return new WaitForSecondsRealtime(1.25f);
            this.OnRewardedVideoCompleted(requestVersion, true);
        }

        private void OnRewardedVideoCompleted(int requestVersion, bool rewardEarned)
        {
            if (requestVersion != this.m_DeathVersion) return;
            if (!this.m_RewardedVideoPending) return;
            this.m_RewardedVideoPending = false;
            if (!this.m_DeathSequenceActive) return;

            if (!rewardEarned)
            {
                this.m_RespawnPanel?.SetError(
                    "VIDEO CHƯA HOÀN TẤT — CHƯA THỂ HỒI SINH TẠI ĐÂY"
                );
                this.FadeCurrentRespawnTransitionOut();
                return;
            }

            this.RespawnPlayer(false);
        }

        private void RespawnPlayer(bool atHospital)
        {
            if (this.m_RespawnInProgress || !this.m_DeathSequenceActive) return;

            Marker hospital = atHospital ? this.FindHospitalMarker() : null;
            if (atHospital && hospital == null)
            {
                this.m_RespawnPanel?.SetError("KHÔNG TÌM THẤY MARKER BỆNH VIỆN");
                return;
            }

            this.m_RespawnInProgress = true;
            this.m_RespawnPanel?.SetBusy(
                true,
                atHospital ? "ĐANG CHUYỂN TỚI BỆNH VIỆN..." : "ĐANG HỒI SINH..."
            );
            Time.timeScale = this.m_PreviousTimeScale > MIN_TIME_SCALE
                ? this.m_PreviousTimeScale
                : 1f;
            Time.fixedDeltaTime = this.m_PreviousFixedDeltaTime;

            int transitionVersion = this.m_RespawnTransitionActive
                ? this.m_RespawnTransitionVersion
                : this.StartRespawnTransitionToBlack();
            this.StartCoroutine(this.WaitForBlackThenRespawn(
                atHospital,
                hospital,
                transitionVersion
            ));
        }

        private IEnumerator WaitForBlackThenRespawn(
            bool atHospital,
            Marker hospital,
            int transitionVersion)
        {
            while (this.IsRespawnTransitionCurrent(transitionVersion) &&
                   !this.m_RespawnTransitionFadingOut &&
                   this.m_RespawnTransitionGroup.alpha < 0.999f)
            {
                yield return null;
            }

            if (!this.IsRespawnTransitionCurrent(transitionVersion) ||
                this.m_RespawnTransitionFadingOut)
            {
                yield break;
            }

            if (this.m_BlackScreenHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(this.m_BlackScreenHoldDuration);
            }

            if (!this.IsRespawnTransitionCurrent(transitionVersion)) yield break;
            this.CompleteRespawnUnderBlack(atHospital, hospital, transitionVersion);
        }

        private int StartRespawnTransitionToBlack()
        {
            int version = this.BeginRespawnTransition();
            this.StartCoroutine(this.FadeRespawnTransitionIn(version));
            return version;
        }

        private IEnumerator FadeRespawnTransitionIn(int transitionVersion)
        {
            float startedAt = Time.unscaledTime;
            while (this.IsRespawnTransitionCurrent(transitionVersion) &&
                   !this.m_RespawnTransitionFadingOut)
            {
                float t = Mathf.Clamp01(
                    (Time.unscaledTime - startedAt) / this.m_FadeToBlackDuration
                );
                this.m_RespawnTransitionGroup.alpha = SmoothStep(t);
                if (t >= 1f) break;
                yield return null;
            }

            if (!this.IsRespawnTransitionCurrent(transitionVersion) ||
                this.m_RespawnTransitionFadingOut)
            {
                yield break;
            }
            this.m_RespawnTransitionGroup.alpha = 1f;
        }

        private async void CompleteRespawnUnderBlack(
            bool atHospital,
            Marker hospital,
            int transitionVersion)
        {
            if (!this.IsRespawnTransitionCurrent(transitionVersion)) return;

            try
            {
                if (this.m_Character.Ragdoll.IsRagdoll)
                {
                    await this.m_Character.Ragdoll.StartRecover();
                }
                if (!this.m_DeathSequenceActive)
                {
                    this.StartCoroutine(
                        this.FadeRespawnTransitionOut(transitionVersion)
                    );
                    return;
                }

                this.m_Character.GetComponentInChildren<FranklinAnimationBridge>(true)
                    ?.RestoreModelRootBaseline();

                Vector3 destination = atHospital
                    ? hospital.GetPosition(this.m_Character.gameObject)
                    : this.m_DeathFeetPosition;
                Quaternion rotation = atHospital
                    ? hospital.GetRotation(this.m_Character.gameObject)
                    : this.m_DeathFacing;

                if (this.m_Character.Driver != null)
                {
                    this.m_Character.Driver.SetPosition(destination, true);
                    this.m_Character.Driver.SetRotation(rotation);
                }
                else
                {
                    this.m_Character.transform.SetPositionAndRotation(
                        destination,
                        rotation
                    );
                    Physics.SyncTransforms();
                }

                if (this.m_BloodLoss == null || !this.m_BloodLoss.RestoreFullHealth())
                {
                    throw new InvalidOperationException(
                        "Player hp Attribute could not be restored"
                    );
                }

                this.StartCoroutine(
                    this.FadeRespawnTransitionOut(transitionVersion)
                );
            }
            catch (Exception exception)
            {
                if (!this.m_DeathSequenceActive)
                {
                    this.StartCoroutine(
                        this.FadeRespawnTransitionOut(transitionVersion)
                    );
                    return;
                }

                this.m_RespawnInProgress = false;
                Time.timeScale = Mathf.Min(
                    this.m_PreviousTimeScale,
                    this.m_DeathTimeScale
                );
                this.UpdateFixedDeltaTime();
                this.m_RespawnPanel?.SetError("HỒI SINH THẤT BẠI — VUI LÒNG THỬ LẠI");
                this.RequestRagdollOnce();
                this.StartCoroutine(
                    this.FadeRespawnTransitionOut(transitionVersion)
                );
                Debug.LogException(exception, this);
            }
        }

        private int BeginRespawnTransition()
        {
            this.CancelRespawnTransition();
            this.m_RespawnTransitionActive = true;
            this.m_RespawnTransitionFadingOut = false;
            int version = ++this.m_RespawnTransitionVersion;

            this.m_RespawnTransitionObject = new GameObject(
                "Franklin Respawn Screen Fade",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup)
            );

            Canvas canvas = this.m_RespawnTransitionObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            CanvasScaler scaler = this.m_RespawnTransitionObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            this.m_RespawnTransitionGroup = this.m_RespawnTransitionObject.GetComponent<
                CanvasGroup
            >();
            this.m_RespawnTransitionGroup.alpha = 0f;
            this.m_RespawnTransitionGroup.blocksRaycasts = true;
            this.m_RespawnTransitionGroup.interactable = true;

            RectTransform root = this.m_RespawnTransitionObject.GetComponent<RectTransform>();
            Stretch(root);
            Image black = CreateImage("Full Screen Black", root, Color.black);
            black.raycastTarget = true;
            Stretch(black.rectTransform);
            return version;
        }

        private IEnumerator FadeRespawnTransitionOut(int transitionVersion)
        {
            if (!this.IsRespawnTransitionCurrent(transitionVersion)) yield break;

            this.m_RespawnTransitionFadingOut = true;
            float startAlpha = this.m_RespawnTransitionGroup.alpha;
            float startedAt = Time.unscaledTime;
            while (this.IsRespawnTransitionCurrent(transitionVersion))
            {
                float t = Mathf.Clamp01(
                    (Time.unscaledTime - startedAt) / this.m_FadeFromBlackDuration
                );
                this.m_RespawnTransitionGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    0f,
                    SmoothStep(t)
                );
                if (t >= 1f) break;
                yield return null;
            }

            if (!this.IsRespawnTransitionCurrent(transitionVersion)) yield break;
            this.m_RespawnTransitionGroup.alpha = 0f;
            this.m_RespawnTransitionActive = false;
            this.m_RespawnTransitionFadingOut = false;
            DestroyRuntimeObject(this.m_RespawnTransitionObject);
            this.m_RespawnTransitionObject = null;
            this.m_RespawnTransitionGroup = null;

            if (!this.m_DeathSequenceActive && this.m_Character != null)
            {
                this.m_Character.IsDead = false;
                if (this.m_Character.Player != null)
                {
                    this.m_Character.Player.IsControllable = true;
                }
                FranklinMobileHud.SetControlsSuppressed(false);
            }
        }

        private bool IsRespawnTransitionCurrent(int version)
        {
            return this.m_RespawnTransitionActive &&
                   version == this.m_RespawnTransitionVersion &&
                   this.m_RespawnTransitionGroup != null;
        }

        private void FadeCurrentRespawnTransitionOut()
        {
            if (!this.m_RespawnTransitionActive ||
                this.m_RespawnTransitionFadingOut)
            {
                return;
            }
            this.StartCoroutine(
                this.FadeRespawnTransitionOut(this.m_RespawnTransitionVersion)
            );
        }

        private void CancelRespawnTransition()
        {
            ++this.m_RespawnTransitionVersion;
            this.m_RespawnTransitionActive = false;
            this.m_RespawnTransitionFadingOut = false;
            DestroyRuntimeObject(this.m_RespawnTransitionObject);
            this.m_RespawnTransitionObject = null;
            this.m_RespawnTransitionGroup = null;
        }

        private IFranklinRewardedAdProvider ResolveRewardedAdProvider()
        {
            if (this.m_RewardedAdProvider is IFranklinRewardedAdProvider assigned)
            {
                return assigned;
            }
            if (FranklinRewardedAds.Provider != null)
            {
                return FranklinRewardedAds.Provider;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            for (int i = 0; i < behaviours.Length; ++i)
            {
                if (behaviours[i] is not IFranklinRewardedAdProvider provider) continue;
                FranklinRewardedAds.Provider = provider;
                return provider;
            }
            return null;
        }

        private Marker FindHospitalMarker()
        {
            if (this.m_HospitalMarker != null &&
                string.Equals(
                    this.m_HospitalMarker.name,
                    this.m_HospitalMarkerName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return this.m_HospitalMarker;
            }

            Marker[] markers = FindObjectsByType<Marker>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            for (int i = 0; i < markers.Length; ++i)
            {
                if (!string.Equals(
                        markers[i].name,
                        this.m_HospitalMarkerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                this.m_HospitalMarker = markers[i];
                return this.m_HospitalMarker;
            }
            return null;
        }

        private void BuildPresentation()
        {
            if (this.m_OverlayObject != null) return;

            this.m_OverlayObject = new GameObject(
                "Franklin Wasted Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup)
            );
            Canvas canvas = this.m_OverlayObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = this.m_OverlayObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            this.m_OverlayGroup = this.m_OverlayObject.GetComponent<CanvasGroup>();
            this.m_OverlayGroup.alpha = 0f;
            this.m_OverlayGroup.blocksRaycasts = true;
            this.m_OverlayGroup.interactable = true;

            RectTransform overlayRect = this.m_OverlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            this.m_ScreenFlash = CreateImage(
                "Monochrome Flash",
                overlayRect,
                new Color(1f, 1f, 1f, 0f)
            );
            Stretch(this.m_ScreenFlash.rectTransform);

            Image bar = CreateImage(
                "Wasted Band",
                overlayRect,
                new Color(0.025f, 0.025f, 0.025f, 0.62f)
            );
            this.m_WastedBandGroup = bar.gameObject.AddComponent<CanvasGroup>();
            this.m_WastedBandGroup.alpha = 1f;
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0.5f);
            barRect.anchorMax = new Vector2(1f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 154f);

            GameObject textObject = new GameObject(
                "WASTED",
                typeof(RectTransform),
                typeof(Text),
                typeof(Outline)
            );
            textObject.transform.SetParent(barRect, false);
            Text text = textObject.GetComponent<Text>();
            text.text = "WASTED";
            text.font = this.m_WastedFont != null
                ? this.m_WastedFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 104;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = this.m_WastedColor;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 54;
            text.resizeTextMaxSize = 104;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;

            this.m_WastedTextTransform = textObject.GetComponent<RectTransform>();
            Stretch(this.m_WastedTextTransform);
            this.m_WastedTextTransform.offsetMin = new Vector2(240f, 8f);
            this.m_WastedTextTransform.offsetMax = new Vector2(-240f, -8f);

            this.m_RespawnPanel = FranklinDeathRespawnPanel.Create(
                overlayRect,
                text.font,
                this.m_WastedColor
            );
            this.m_RespawnPanel.EventReviveHere += this.OnReviveHereRequested;
            this.m_RespawnPanel.EventHospital += this.OnHospitalRequested;

            this.m_AudioSource = this.m_OverlayObject.AddComponent<AudioSource>();
            this.m_AudioSource.playOnAwake = false;
            this.m_AudioSource.loop = false;
            this.m_AudioSource.spatialBlend = 0f;
            this.m_AudioSource.ignoreListenerPause = true;
            this.m_OriginalSting = CreateOriginalWastedSting();

            this.BuildPostProcessing();
        }

        private void BuildPostProcessing()
        {
            this.m_PostFxObject = new GameObject("Franklin Wasted Post FX");
            this.m_PostFxVolume = this.m_PostFxObject.AddComponent<Volume>();
            this.m_PostFxVolume.isGlobal = true;
            this.m_PostFxVolume.priority = 1000f;
            this.m_PostFxVolume.weight = 0f;

            this.m_RuntimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            this.m_RuntimeProfile.name = "Franklin Wasted Runtime Profile";
            this.m_PostFxVolume.sharedProfile = this.m_RuntimeProfile;

            ColorAdjustments color = this.m_RuntimeProfile.Add<ColorAdjustments>(true);
            color.saturation.Override(-100f);
            color.contrast.Override(18f);
            color.postExposure.Override(-0.22f);

            Vignette vignette = this.m_RuntimeProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.7f);

            FilmGrain grain = this.m_RuntimeProfile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.24f);
            grain.response.Override(0.72f);
        }

        private void CaptureDeathCamera()
        {
            this.m_DeathCamera = Camera.main;
            if (this.m_DeathCamera == null) return;

            this.m_CameraStartPosition = this.m_DeathCamera.transform.position;
            this.m_CameraStartRotation = this.m_DeathCamera.transform.rotation;
            this.m_CameraStartFieldOfView = this.m_DeathCamera.fieldOfView;

            Animator animator = this.m_Character?.Animim?.Animator;
            this.m_LookTarget = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : this.m_Character?.transform;

            Vector3 away = this.m_CameraStartPosition - this.GetLookPoint();
            if (away.sqrMagnitude < 0.01f) away = -this.m_DeathCamera.transform.forward;
            this.m_CameraPullDirection = away.normalized;

            this.m_CameraData = this.m_DeathCamera.GetComponent<
                UniversalAdditionalCameraData
            >();
            if (this.m_CameraData != null)
            {
                this.m_CameraPostProcessingWasEnabled =
                    this.m_CameraData.renderPostProcessing;
                this.m_CameraData.renderPostProcessing = true;
            }
        }

        private Vector3 GetLookPoint()
        {
            if (this.m_LookTarget != null) return this.m_LookTarget.position;
            return this.m_Character != null
                ? this.m_Character.transform.position + Vector3.up * 0.75f
                : Vector3.zero;
        }

        private void UpdateFixedDeltaTime()
        {
            float ratio = this.m_PreviousTimeScale > MIN_TIME_SCALE
                ? Time.timeScale / this.m_PreviousTimeScale
                : 1f;
            Time.fixedDeltaTime = Mathf.Max(0.001f, this.m_PreviousFixedDeltaTime * ratio);
        }

        private void DestroyPresentationObjects()
        {
            if (this.m_CameraData != null)
            {
                this.m_CameraData.renderPostProcessing =
                    this.m_CameraPostProcessingWasEnabled;
            }

            DestroyRuntimeObject(this.m_OverlayObject);
            DestroyRuntimeObject(this.m_PostFxObject);
            DestroyRuntimeObject(this.m_RuntimeProfile);
            DestroyRuntimeObject(this.m_OriginalSting);

            this.m_OverlayObject = null;
            this.m_OverlayGroup = null;
            this.m_WastedBandGroup = null;
            this.m_WastedTextTransform = null;
            this.m_ScreenFlash = null;
            this.m_AudioSource = null;
            this.m_RespawnPanel = null;
            this.m_HospitalMarker = null;
            this.m_PostFxObject = null;
            this.m_PostFxVolume = null;
            this.m_RuntimeProfile = null;
            this.m_OriginalSting = null;
            this.m_DeathCamera = null;
            this.m_CameraData = null;
            this.m_LookTarget = null;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        /// <summary>
        /// Synthesizes a deterministic, original metallic hit and descending low drone.
        /// No audio from GTA or another third-party asset is copied or embedded.
        /// </summary>
        private static AudioClip CreateOriginalWastedSting()
        {
            const float duration = 2.75f;
            int sampleCount = Mathf.CeilToInt(AUDIO_SAMPLE_RATE * duration);
            float[] samples = new float[sampleCount];
            uint noiseState = 0x9E3779B9u;

            for (int i = 0; i < sampleCount; ++i)
            {
                float time = i / (float)AUDIO_SAMPLE_RATE;
                float metalEnvelope = Mathf.Exp(-time * 8.5f);
                float metal = (
                    Mathf.Sin(2f * Mathf.PI * 191f * time) * 0.32f +
                    Mathf.Sin(2f * Mathf.PI * 307f * time) * 0.24f +
                    Mathf.Sin(2f * Mathf.PI * 479f * time) * 0.18f +
                    Mathf.Sin(2f * Mathf.PI * 733f * time) * 0.11f
                ) * metalEnvelope;

                noiseState ^= noiseState << 13;
                noiseState ^= noiseState >> 17;
                noiseState ^= noiseState << 5;
                float noise = ((noiseState & 0xFFFFu) / 32767.5f - 1f) *
                              Mathf.Exp(-time * 32f) * 0.24f;

                float boomPhase = 2f * Mathf.PI * (57f * time - 5.2f * time * time);
                float boom = Mathf.Sin(boomPhase) * Mathf.Exp(-time * 2.6f) * 0.52f;

                float droneTime = Mathf.Max(0f, time - 0.12f);
                float droneEnvelope = (1f - Mathf.Exp(-droneTime * 7f)) *
                                      Mathf.Exp(-droneTime * 0.92f);
                float sweep = 1f - Mathf.Clamp01(droneTime / duration) * 0.24f;
                float drone = (
                    Mathf.Sin(2f * Mathf.PI * 110f * sweep * droneTime) * 0.22f +
                    Mathf.Sin(2f * Mathf.PI * 164.8f * sweep * droneTime) * 0.14f +
                    Mathf.Sin(2f * Mathf.PI * 220f * sweep * droneTime) * 0.09f
                ) * droneEnvelope;

                float finalFade = Mathf.Clamp01((duration - time) / 0.42f);
                float value = (metal + noise + boom + drone) * finalFade;
                samples[i] = value / (1f + Mathf.Abs(value));
            }

            AudioClip clip = AudioClip.Create(
                "Franklin Wasted Sting (Original)",
                sampleCount,
                1,
                AUDIO_SAMPLE_RATE,
                false
            );
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
