using System.Reflection;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Applies a wrap-safe Third Person yaw constraint only while the Player is sprinting.
    /// GC2's built-in clamp mixes 0..360 pivot Euler angles with an unbounded target angle,
    /// which can make SmoothDamp take the long way around when the Player crosses north.
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
        private ShotSystemThirdPerson m_ThirdPerson;
        private EnablerAngle180 m_MaxYaw;
        private bool m_DefaultMaxYawEnabled;
        private bool m_HasCachedMaxYaw;
        private bool m_HasWarnedUnavailable;
        private bool m_HasAppliedSprintState;
        private bool m_LastAppliedSprintState;
        private bool m_IsSprintYawLatched;
        private bool m_IsWrapSafeYawActive;
        private float m_NextBridgeLookupTime;
        private float m_NextYawCacheAttemptTime;

        public bool IsMaxYawEnabled => this.m_IsWrapSafeYawActive ||
                                       this.m_MaxYaw?.IsEnabled == true;

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
            bool isSprintRunning = this.m_PlayerBridge?.IsSprintRunning == true;
            if (!isSprintRunning)
            {
                this.m_IsSprintYawLatched = false;
            }
            else if (!this.m_IsSprintYawLatched &&
                     this.m_PlayerBridge.IsRunCameraAligned)
            {
                // Once enabled, keep the constraint active for the rest of this Sprint. Orbiting
                // can temporarily make the body unaligned and must not toggle Max Yaw every frame.
                this.m_IsSprintYawLatched = true;
            }

            bool shouldEnable = this.m_EnableMaxYawWhileRunning &&
                                this.m_IsSprintYawLatched;
            if (this.m_HasAppliedSprintState &&
                shouldEnable == this.m_LastAppliedSprintState)
            {
                return;
            }

            if (!this.SetSprintYawEnabled(shouldEnable)) return;

            this.m_LastAppliedSprintState = shouldEnable;
            this.m_HasAppliedSprintState = true;
        }

        private void LateUpdate()
        {
            if (!this.m_IsWrapSafeYawActive || !this.TryCacheMaxYaw()) return;

            // Keep GC2's built-in clamp disabled. Its world/local conversion can change the
            // target representation by 360 degrees when the pivot crosses 0, which makes its
            // non-angular SmoothDamp spin the camera before recovering.
            this.m_MaxYaw.IsEnabled = false;

            Transform pivot = this.m_ThirdPerson?.Pivot?.transform;
            if (pivot == null) return;

            float halfRange = this.m_MaxYaw.Value * 0.5f;
            float targetYaw = this.m_ThirdPerson.Yaw;
            float pivotYaw = pivot.eulerAngles.y;
            float localYaw = Mathf.DeltaAngle(pivotYaw, targetYaw);
            float clampedLocalYaw = Mathf.Clamp(localYaw, -halfRange, halfRange);
            float clampedWorldYaw = pivotYaw + clampedLocalYaw;

            // Preserve the target's continuous angle representation and only move by the
            // shortest signed arc. This remains stable across 359 -> 0 and 0 -> 359 crossings.
            this.m_ThirdPerson.Yaw = targetYaw +
                                     Mathf.DeltaAngle(targetYaw, clampedWorldYaw);
        }

        private void OnDisable()
        {
            this.RestoreDefaultMaxYaw();
            this.m_HasAppliedSprintState = false;
            this.m_IsSprintYawLatched = false;
        }

        private bool SetSprintYawEnabled(bool enabled)
        {
            if (!this.TryCacheMaxYaw()) return false;

            this.m_IsWrapSafeYawActive = enabled;
            // During Sprint our LateUpdate clamp replaces GC2's unstable built-in clamp.
            this.m_MaxYaw.IsEnabled = enabled ? false : this.m_DefaultMaxYawEnabled;
            return true;
        }

        private void RestoreDefaultMaxYaw()
        {
            this.m_IsWrapSafeYawActive = false;
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

            this.m_ThirdPerson = null;
            foreach (IShotSystem system in shotType.ShotSystems)
            {
                if (system is ShotSystemThirdPerson candidate)
                {
                    this.m_ThirdPerson = candidate;
                    break;
                }
            }

            this.m_MaxYaw = this.m_ThirdPerson != null
                ? MAX_YAW_FIELD.GetValue(this.m_ThirdPerson) as EnablerAngle180
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
