using System.Reflection;
using System.Threading.Tasks;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Temporarily applies bike-specific Third Person aim settings to the GC2 Main Camera Shot.
    /// It never creates, activates or transitions to another Shot Camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-450)]
    public sealed class FranklinBikeMainShotAim : MonoBehaviour
    {
        public enum PivotMode
        {
            Player,
            Bike,
            Custom
        }

        private const BindingFlags PRIVATE_INSTANCE =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Pivot", PRIVATE_INSTANCE);
        private static readonly FieldInfo SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Shoulder", PRIVATE_INSTANCE);
        private static readonly FieldInfo LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Lift", PRIVATE_INSTANCE);
        private static readonly FieldInfo RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Radius", PRIVATE_INSTANCE);
        private static readonly FieldInfo SENSITIVITY_X_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SensitivityX", PRIVATE_INSTANCE);
        private static readonly FieldInfo SENSITIVITY_Y_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SensitivityY", PRIVATE_INSTANCE);
        private static readonly FieldInfo MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_MaxYaw", PRIVATE_INSTANCE);
        private static readonly FieldInfo SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SmoothTime", PRIVATE_INSTANCE);

        [Header("Main Camera Shot")]
        [SerializeField] private Character m_Player;
        [SerializeField]
        [Tooltip("Optional. When empty, the currently active GC2 Main Camera Shot is adjusted.")]
        private ShotCamera m_MainShot;
        [SerializeField] private PivotMode m_Pivot = PivotMode.Player;
        [SerializeField] private Transform m_CustomPivot;
        [SerializeField]
        [Tooltip("Keeps the same Main Camera Shot but points its TPS orbit behind the bike on enter.")]
        private bool m_SnapBehindBikeOnEnter = true;

        [Header("Bike TPS Aim")]
        [SerializeField] private float m_Shoulder = 0.5f;
        [SerializeField] private float m_Lift = 0.5f;
        [SerializeField, Min(0.01f)] private float m_Radius = 3f;

        [Header("Bike TPS Orbit")]
        [SerializeField] private bool m_OverrideSensitivity = true;
        [SerializeField, Min(0f)] private float m_SensitivityX = 0.2f;
        [SerializeField, Min(0f)] private float m_SensitivityY = 0.2f;
        [SerializeField, Range(1f, 179f)] private float m_MaxPitch = 150f;
        [SerializeField] private bool m_EnableMaxYaw;
        [SerializeField, Range(0f, 179f)] private float m_MaxYaw = 120f;
        [SerializeField, Min(0f)] private float m_SmoothTime = 0.15f;

        [Header("Bike TPS Align")]
        [SerializeField] private bool m_AutoAlign;
        [SerializeField, Min(0f)] private float m_AlignDelay = 3f;
        [SerializeField, Min(0f)] private float m_AlignSmoothTime = 3f;

        private MainCamera m_MainCamera;
        private ShotCamera m_ActiveShot;
        private ShotSystemThirdPerson m_ThirdPerson;
        private BikeEntry m_ActiveBike;
        private Snapshot m_Snapshot;
        private bool m_HasSnapshot;
        private GameObject m_RecoveryPivot;

        public bool IsActive { get; private set; }
        public ShotCamera ActiveShot => this.m_ActiveShot;

        private struct Snapshot
        {
            public object Pivot;
            public object Shoulder;
            public object Lift;
            public object Radius;
            public object SensitivityX;
            public object SensitivityY;
            public object SmoothTime;
            public float MaxPitch;
            public bool MaxYawEnabled;
            public float MaxYaw;
            public bool AutoAlign;
            public float AlignDelay;
            public float AlignSmoothTime;
        }

        private void Awake()
        {
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            this.Deactivate();
        }

        private void OnDestroy()
        {
            this.DestroyRecoveryPivot();
        }

        private void OnValidate()
        {
            this.m_Radius = Mathf.Max(0.01f, this.m_Radius);
            this.m_SensitivityX = Mathf.Max(0f, this.m_SensitivityX);
            this.m_SensitivityY = Mathf.Max(0f, this.m_SensitivityY);
            this.m_MaxPitch = Mathf.Clamp(this.m_MaxPitch, 1f, 179f);
            this.m_MaxYaw = Mathf.Clamp(this.m_MaxYaw, 0f, 179f);
            this.m_SmoothTime = Mathf.Max(0f, this.m_SmoothTime);
            this.m_AlignDelay = Mathf.Max(0f, this.m_AlignDelay);
            this.m_AlignSmoothTime = Mathf.Max(0f, this.m_AlignSmoothTime);

            // Inspector changes made during Play Mode update the active Main Shot immediately.
            if (Application.isPlaying && this.IsActive)
            {
                this.ApplySettings();
            }
        }

        public bool Activate(BikeEntry bike)
        {
            if (bike == null || !this.ResolvePlayer() || !this.ResolveMainCamera())
            {
                return false;
            }

            ShotCamera shot = this.m_MainShot != null
                ? this.m_MainShot
                : this.m_MainCamera.Transition.CurrentShotCamera;
            if (!TryGetThirdPerson(shot, out ShotSystemThirdPerson thirdPerson))
            {
                Debug.LogWarning(
                    "Bike TPS Aim requires the current GC2 Main Camera Shot to use Third Person.",
                    this
                );
                return false;
            }

            if (this.IsActive) this.Deactivate();

            this.m_ActiveBike = bike;
            this.m_ActiveShot = shot;
            this.m_ThirdPerson = thirdPerson;
            this.CaptureSnapshot();
            this.IsActive = true;
            this.ApplySettings();

            if (this.m_SnapBehindBikeOnEnter)
            {
                Vector3 forward = Vector3.ProjectOnPlane(bike.transform.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    this.m_ThirdPerson.SetDirection(forward.normalized);
                }
            }

            return true;
        }

        public void Deactivate()
        {
            if (!this.IsActive && !this.m_HasSnapshot) return;

            this.RestoreSnapshot();
            this.DestroyRecoveryPivot();
            this.IsActive = false;
            this.m_ActiveBike = null;
            this.m_ActiveShot = null;
            this.m_ThirdPerson = null;
            this.m_HasSnapshot = false;
        }

        /// <summary>
        /// Keeps the current Main Camera Shot and moves only its TPS pivot from
        /// the bike/player root to the physical ragdoll body. The proxy remains
        /// attached to the hips during the stand-up clip and is removed when the
        /// normal Player camera settings are restored.
        /// </summary>
        public async Task<bool> LerpToRagdollPlayer(
            Character player,
            float duration)
        {
            if (player == null || !this.IsActive || this.m_ThirdPerson == null)
                return false;

            Animator animator = player.Animim.Animator;
            if (animator == null) return false;

            Transform target = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : animator.transform;
            if (target == null) target = animator.transform;

            this.DestroyRecoveryPivot();
            this.m_RecoveryPivot = new GameObject("Bike Ragdoll Recovery Camera Pivot")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Transform currentPivot = this.m_ThirdPerson.Pivot != null
                ? this.m_ThirdPerson.Pivot.transform
                : player.transform;
            Vector3 startPosition = currentPivot.position;
            Quaternion startRotation = currentPivot.rotation;
            this.m_RecoveryPivot.transform.SetPositionAndRotation(
                startPosition,
                startRotation
            );
            PIVOT_FIELD?.SetValue(
                this.m_ThirdPerson,
                GetGameObjectInstance.Create(this.m_RecoveryPivot)
            );

            float blendDuration = Mathf.Max(0f, duration);
            float elapsed = 0f;
            while (this.m_RecoveryPivot != null && target != null &&
                   elapsed < blendDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = blendDuration > 0f
                    ? Mathf.Clamp01(elapsed / blendDuration)
                    : 1f;
                progress = progress * progress * (3f - 2f * progress);
                this.m_RecoveryPivot.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, target.position, progress),
                    Quaternion.Slerp(startRotation, target.rotation, progress)
                );
                await Task.Yield();
            }

            if (this.m_RecoveryPivot == null || target == null) return false;
            this.m_RecoveryPivot.transform.SetPositionAndRotation(
                target.position,
                target.rotation
            );
            this.m_RecoveryPivot.transform.SetParent(target, true);
            return true;
        }

        private void DestroyRecoveryPivot()
        {
            if (this.m_RecoveryPivot == null) return;
            if (Application.isPlaying) Destroy(this.m_RecoveryPivot);
            else DestroyImmediate(this.m_RecoveryPivot);
            this.m_RecoveryPivot = null;
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            return this.m_Player != null;
        }

        private bool ResolveMainCamera()
        {
            if (this.m_MainCamera == null)
            {
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            }

            return this.m_MainCamera != null;
        }

        private void CaptureSnapshot()
        {
            EnablerAngle180 maxYaw = MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) as
                EnablerAngle180;

            this.m_Snapshot = new Snapshot
            {
                Pivot = PIVOT_FIELD?.GetValue(this.m_ThirdPerson),
                Shoulder = SHOULDER_FIELD?.GetValue(this.m_ThirdPerson),
                Lift = LIFT_FIELD?.GetValue(this.m_ThirdPerson),
                Radius = RADIUS_FIELD?.GetValue(this.m_ThirdPerson),
                SensitivityX = SENSITIVITY_X_FIELD?.GetValue(this.m_ThirdPerson),
                SensitivityY = SENSITIVITY_Y_FIELD?.GetValue(this.m_ThirdPerson),
                SmoothTime = SMOOTH_TIME_FIELD?.GetValue(this.m_ThirdPerson),
                MaxPitch = this.m_ThirdPerson.MaxPitch,
                MaxYawEnabled = maxYaw?.IsEnabled ?? false,
                MaxYaw = maxYaw?.Value ?? 0f,
                AutoAlign = this.m_ThirdPerson.Alignment.AutoAlign,
                AlignDelay = this.m_ThirdPerson.Alignment.Delay,
                AlignSmoothTime = this.m_ThirdPerson.Alignment.SmoothTime
            };
            this.m_HasSnapshot = true;
        }

        private void ApplySettings()
        {
            if (this.m_ThirdPerson == null) return;

            GameObject pivot = this.ResolvePivot();
            if (pivot != null)
            {
                PIVOT_FIELD?.SetValue(
                    this.m_ThirdPerson,
                    GetGameObjectInstance.Create(pivot)
                );
            }

            SetDecimal(SHOULDER_FIELD, this.m_Shoulder);
            SetDecimal(LIFT_FIELD, this.m_Lift);
            SetDecimal(RADIUS_FIELD, this.m_Radius);
            SetDecimal(SMOOTH_TIME_FIELD, this.m_SmoothTime);

            if (this.m_OverrideSensitivity)
            {
                SetDecimal(SENSITIVITY_X_FIELD, this.m_SensitivityX);
                SetDecimal(SENSITIVITY_Y_FIELD, this.m_SensitivityY);
            }

            this.m_ThirdPerson.MaxPitch = this.m_MaxPitch;
            this.m_ThirdPerson.Alignment.AutoAlign = this.m_AutoAlign;
            this.m_ThirdPerson.Alignment.Delay = this.m_AlignDelay;
            this.m_ThirdPerson.Alignment.SmoothTime = this.m_AlignSmoothTime;

            if (MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) is EnablerAngle180 maxYaw)
            {
                maxYaw.IsEnabled = this.m_EnableMaxYaw;
                maxYaw.Value = this.m_MaxYaw;
            }
        }

        private void RestoreSnapshot()
        {
            if (!this.m_HasSnapshot || this.m_ThirdPerson == null) return;

            PIVOT_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Pivot);
            SHOULDER_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Shoulder);
            LIFT_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Lift);
            RADIUS_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.Radius);
            SENSITIVITY_X_FIELD?.SetValue(
                this.m_ThirdPerson,
                this.m_Snapshot.SensitivityX
            );
            SENSITIVITY_Y_FIELD?.SetValue(
                this.m_ThirdPerson,
                this.m_Snapshot.SensitivityY
            );
            SMOOTH_TIME_FIELD?.SetValue(this.m_ThirdPerson, this.m_Snapshot.SmoothTime);

            this.m_ThirdPerson.MaxPitch = this.m_Snapshot.MaxPitch;
            this.m_ThirdPerson.Alignment.AutoAlign = this.m_Snapshot.AutoAlign;
            this.m_ThirdPerson.Alignment.Delay = this.m_Snapshot.AlignDelay;
            this.m_ThirdPerson.Alignment.SmoothTime = this.m_Snapshot.AlignSmoothTime;

            if (MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) is EnablerAngle180 maxYaw)
            {
                maxYaw.IsEnabled = this.m_Snapshot.MaxYawEnabled;
                maxYaw.Value = this.m_Snapshot.MaxYaw;
            }
        }

        private GameObject ResolvePivot()
        {
            return this.m_Pivot switch
            {
                PivotMode.Bike when this.m_ActiveBike != null =>
                    this.m_ActiveBike.gameObject,
                PivotMode.Custom when this.m_CustomPivot != null =>
                    this.m_CustomPivot.gameObject,
                _ => this.m_Player != null ? this.m_Player.gameObject : null
            };
        }

        private void SetDecimal(FieldInfo field, float value)
        {
            field?.SetValue(this.m_ThirdPerson, new PropertyGetDecimal(value));
        }

        private static bool TryGetThirdPerson(
            ShotCamera shot,
            out ShotSystemThirdPerson thirdPerson)
        {
            thirdPerson = null;
            if (shot?.ShotType is not ShotTypeThirdPerson shotType) return false;

            thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            return thirdPerson != null;
        }
    }
}
