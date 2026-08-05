using System.Reflection;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Enables the existing Third Person Max Yaw constraint only while the Player is sprinting.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShotCamera))]
    [DefaultExecutionOrder(200)]
    public sealed class FranklinSprintCameraYaw : MonoBehaviour
    {
        private const string MAX_YAW_FIELD_NAME = "m_MaxYaw";
        private const float REFERENCE_RETRY_SECONDS = 1f;

        private static readonly FieldInfo MAX_YAW_FIELD = typeof(ShotSystemThirdPerson).GetField(
            MAX_YAW_FIELD_NAME,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        [SerializeField] private FranklinAnimationBridge m_PlayerBridge;
        [SerializeField] private bool m_EnableMaxYawWhileRunning = true;

        private ShotCamera m_ShotCamera;
        private TEnablerValueCommon m_MaxYaw;
        private bool m_DefaultMaxYawEnabled;
        private bool m_HasCachedMaxYaw;
        private bool m_HasWarnedUnavailable;
        private bool m_HasAppliedSprintState;
        private bool m_LastAppliedSprintState;
        private float m_NextBridgeLookupTime;
        private float m_NextYawCacheAttemptTime;

        public bool IsMaxYawEnabled => this.m_MaxYaw?.IsEnabled ?? false;

        private void Awake()
        {
            this.m_ShotCamera = this.GetComponent<ShotCamera>();
        }

        private void OnEnable()
        {
            this.m_HasAppliedSprintState = false;
            this.TryResolvePlayerBridge();
            this.TryCacheMaxYaw();
        }

        private void Update()
        {
            this.TryResolvePlayerBridge();
            bool shouldEnable = this.m_EnableMaxYawWhileRunning &&
                                this.m_PlayerBridge != null &&
                                this.m_PlayerBridge.IsSprintRunning;
            if (this.m_HasAppliedSprintState &&
                shouldEnable == this.m_LastAppliedSprintState)
            {
                return;
            }

            if (!this.SetMaxYawEnabled(shouldEnable)) return;

            this.m_LastAppliedSprintState = shouldEnable;
            this.m_HasAppliedSprintState = true;
        }

        private void OnDisable()
        {
            this.RestoreDefaultMaxYaw();
            this.m_HasAppliedSprintState = false;
        }

        private bool SetMaxYawEnabled(bool enabled)
        {
            if (!this.TryCacheMaxYaw()) return false;
            this.m_MaxYaw.IsEnabled = enabled ? true : this.m_DefaultMaxYawEnabled;
            return true;
        }

        private void RestoreDefaultMaxYaw()
        {
            if (this.m_HasCachedMaxYaw && this.m_MaxYaw != null)
            {
                this.m_MaxYaw.IsEnabled = this.m_DefaultMaxYawEnabled;
            }
        }

        private bool TryResolvePlayerBridge()
        {
            if (this.m_PlayerBridge != null) return true;
            if (UnityEngine.Time.unscaledTime < this.m_NextBridgeLookupTime) return false;

            this.m_PlayerBridge = Object.FindFirstObjectByType<FranklinAnimationBridge>();
            if (this.m_PlayerBridge == null)
            {
                this.m_NextBridgeLookupTime =
                    UnityEngine.Time.unscaledTime + REFERENCE_RETRY_SECONDS;
            }

            return this.m_PlayerBridge != null;
        }

        private bool TryCacheMaxYaw()
        {
            if (this.m_HasCachedMaxYaw) return true;
            if (UnityEngine.Time.unscaledTime < this.m_NextYawCacheAttemptTime) return false;
            if (this.m_ShotCamera == null) this.m_ShotCamera = this.GetComponent<ShotCamera>();

            ShotTypeThirdPerson shotType = this.m_ShotCamera?.ShotType as ShotTypeThirdPerson;
            if (shotType == null || MAX_YAW_FIELD == null)
            {
                this.m_NextYawCacheAttemptTime =
                    UnityEngine.Time.unscaledTime + REFERENCE_RETRY_SECONDS;
                this.WarnUnavailable();
                return false;
            }

            ShotSystemThirdPerson thirdPerson = null;
            foreach (IShotSystem system in shotType.ShotSystems)
            {
                if (system is ShotSystemThirdPerson candidate)
                {
                    thirdPerson = candidate;
                    break;
                }
            }

            this.m_MaxYaw = thirdPerson != null
                ? MAX_YAW_FIELD.GetValue(thirdPerson) as TEnablerValueCommon
                : null;
            if (this.m_MaxYaw == null)
            {
                this.m_NextYawCacheAttemptTime =
                    UnityEngine.Time.unscaledTime + REFERENCE_RETRY_SECONDS;
                this.WarnUnavailable();
                return false;
            }

            this.m_DefaultMaxYawEnabled = this.m_MaxYaw.IsEnabled;
            this.m_HasCachedMaxYaw = true;
            return true;
        }

        private void WarnUnavailable()
        {
            if (this.m_HasWarnedUnavailable) return;
            this.m_HasWarnedUnavailable = true;
            Debug.LogWarning(
                "Camera Shot does not expose a Third Person Max Yaw setting. " +
                "Sprint camera yaw control is disabled.",
                this
            );
        }
    }
}
