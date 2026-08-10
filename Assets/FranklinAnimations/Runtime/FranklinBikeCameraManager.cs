using System.Reflection;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Owns the single GC2 camera shot shared by every arcade bike. This component,
    /// its runtime Shot and its yaw-only pivot all live below the Player hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class FranklinBikeCameraManager : MonoBehaviour
    {
        private static readonly FieldInfo THIRD_PERSON_SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Shoulder",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Lift",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Radius",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Pivot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_MaxYaw",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SmoothTime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        [Header("Shared bike camera")]
        [SerializeField] private Character m_Player;
        [SerializeField] private ShotCamera m_BikeShotTemplate;
        [SerializeField] private Transform m_CameraPivot;
        [SerializeField] private bool m_SnapBehindBikeOnEnter = true;
        [SerializeField, Min(0f)] private float m_EnterBlend = 0.35f;
        [SerializeField, Min(0f)] private float m_ExitBlend = 0.35f;
        [SerializeField] private Easing.Type m_BlendEasing = Easing.Type.Linear;

        [Header("Third Person")]
        [SerializeField] private float m_Shoulder;
        [SerializeField] private float m_Lift = 1f;
        [SerializeField, Min(0.01f)] private float m_Radius = 5f;
        [SerializeField] private bool m_UseTemplateSensitivity = true;
        [SerializeField, Min(0f)] private float m_SensitivityX = 0.5f;
        [SerializeField, Min(0f)] private float m_SensitivityY = 0.5f;
        [SerializeField, Range(1f, 179f)] private float m_MaxPitch = 150f;
        [SerializeField] private bool m_EnableMaxYaw;
        [SerializeField, Range(0f, 179f)] private float m_MaxYaw = 120f;
        [SerializeField, Min(0f)] private float m_OrbitSmoothTime = 0.15f;

        [Header("Auto Align")]
        [SerializeField] private bool m_AutoAlign = true;
        [SerializeField, Min(0f)] private float m_AlignDelay = 1f;
        [SerializeField, Min(0f)] private float m_AlignSmoothTime = 1f;

        private MainCamera m_MainCamera;
        private ShotCamera m_RuntimeBikeShot;
        private ShotCamera m_PreBikeShot;
        private ShotSystemThirdPerson m_ThirdPerson;
        private BikeEntry m_ActiveBike;
        private int m_SnapFramesRemaining;

        public bool IsActive { get; private set; }
        public BikeEntry ActiveBike => this.m_ActiveBike;
        public Transform CameraPivot => this.m_CameraPivot;
        public ShotCamera RuntimeShot => this.m_RuntimeBikeShot;

        private void Awake()
        {
            this.ResolvePlayer();
            this.EnsureCameraPivot();
        }

        private void Update()
        {
            if (!this.IsActive) return;
            this.UpdateCameraPivot();
        }

        private void LateUpdate()
        {
            if (!this.IsActive || this.m_SnapFramesRemaining <= 0) return;
            if (this.m_MainCamera?.Transition.CurrentShotCamera != this.m_RuntimeBikeShot) return;

            this.SnapBehindActiveBike();
            this.m_SnapFramesRemaining--;
        }

        private void OnDisable()
        {
            this.Deactivate();
        }

        private void OnValidate()
        {
            this.m_EnterBlend = Mathf.Max(0f, this.m_EnterBlend);
            this.m_ExitBlend = Mathf.Max(0f, this.m_ExitBlend);
            this.m_Radius = Mathf.Max(0.01f, this.m_Radius);
            this.m_MaxPitch = Mathf.Clamp(this.m_MaxPitch, 1f, 179f);
            this.m_MaxYaw = Mathf.Clamp(this.m_MaxYaw, 0f, 179f);
            this.m_OrbitSmoothTime = Mathf.Max(0f, this.m_OrbitSmoothTime);
            this.m_AlignDelay = Mathf.Max(0f, this.m_AlignDelay);
            this.m_AlignSmoothTime = Mathf.Max(0f, this.m_AlignSmoothTime);
        }

        public bool Activate(BikeEntry bike)
        {
            if (bike == null || !this.ResolvePlayer() || !this.ResolveMainCamera() ||
                !this.EnsureRuntimeShot() || !this.EnsureCameraPivot())
            {
                return false;
            }

            this.m_ActiveBike = bike;
            this.UpdateCameraPivot();
            this.ApplySharedSettings();

            if (!this.IsActive)
            {
                this.m_PreBikeShot = this.m_MainCamera.Transition.CurrentShotCamera;
            }

            this.m_MainCamera.Transition.ChangeToShot(
                this.m_RuntimeBikeShot,
                this.m_EnterBlend,
                this.m_BlendEasing
            );
            this.IsActive = true;

            // GC2 initializes a Third Person shot from the outgoing camera rotation.
            // Reapply the bike heading after activation so entry never starts side-on.
            this.m_SnapFramesRemaining = this.m_SnapBehindBikeOnEnter ? 3 : 0;
            if (this.m_SnapBehindBikeOnEnter) this.SnapBehindActiveBike();
            return true;
        }

        public void Deactivate()
        {
            if (!this.IsActive) return;

            if (this.ResolveMainCamera() && this.m_PreBikeShot != null &&
                this.m_MainCamera.Transition.CurrentShotCamera == this.m_RuntimeBikeShot)
            {
                this.m_MainCamera.Transition.ChangeToShot(
                    this.m_PreBikeShot,
                    this.m_ExitBlend,
                    this.m_BlendEasing
                );
            }

            this.IsActive = false;
            this.m_ActiveBike = null;
            this.m_PreBikeShot = null;
            this.m_SnapFramesRemaining = 0;
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

        private bool EnsureRuntimeShot()
        {
            if (this.m_RuntimeBikeShot != null) return true;
            if (this.m_BikeShotTemplate == null)
            {
                Debug.LogWarning(
                    "Shared bike camera has no GC2 Shot template assigned.",
                    this
                );
                return false;
            }

            this.m_RuntimeBikeShot = Instantiate(this.m_BikeShotTemplate, this.transform);
            this.m_RuntimeBikeShot.name = "Shared Runtime Bike Camera Shot";
            this.m_RuntimeBikeShot.transform.localPosition = Vector3.zero;
            this.m_RuntimeBikeShot.transform.localRotation = Quaternion.identity;
            this.m_RuntimeBikeShot.transform.localScale = Vector3.one;

            // The source shot also serves Player sprinting. Its sprint-only yaw helper
            // must not compete with bike auto alignment on the shared runtime clone.
            FranklinSprintCameraYaw sprintYaw =
                this.m_RuntimeBikeShot.GetComponent<FranklinSprintCameraYaw>();
            if (sprintYaw != null) sprintYaw.enabled = false;

            return this.CacheThirdPersonSystem();
        }

        private bool EnsureCameraPivot()
        {
            if (this.m_CameraPivot != null) return true;

            Transform existing = this.transform.Find("Bike Camera Pivot");
            if (existing != null)
            {
                this.m_CameraPivot = existing;
                return true;
            }

            GameObject pivot = new GameObject("Bike Camera Pivot");
            this.m_CameraPivot = pivot.transform;
            this.m_CameraPivot.SetParent(this.transform, false);
            return true;
        }

        private bool CacheThirdPersonSystem()
        {
            if (this.m_RuntimeBikeShot?.ShotType is not ShotTypeThirdPerson shotType)
            {
                Debug.LogWarning("Shared bike camera Shot must use GC2 Third Person.", this);
                return false;
            }

            this.m_ThirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            return this.m_ThirdPerson != null;
        }

        private void ApplySharedSettings()
        {
            if (this.m_ThirdPerson == null && !this.CacheThirdPersonSystem()) return;

            SetDecimalField(this.m_ThirdPerson, THIRD_PERSON_SHOULDER_FIELD, this.m_Shoulder);
            SetDecimalField(this.m_ThirdPerson, THIRD_PERSON_LIFT_FIELD, this.m_Lift);
            SetDecimalField(this.m_ThirdPerson, THIRD_PERSON_RADIUS_FIELD, this.m_Radius);
            SetDecimalField(
                this.m_ThirdPerson,
                THIRD_PERSON_SMOOTH_TIME_FIELD,
                this.m_OrbitSmoothTime
            );

            THIRD_PERSON_PIVOT_FIELD?.SetValue(
                this.m_ThirdPerson,
                GetGameObjectInstance.Create(this.m_CameraPivot.gameObject)
            );

            if (!this.m_UseTemplateSensitivity)
            {
                this.m_ThirdPerson.Sensitivity = new Vector2(
                    this.m_SensitivityX,
                    this.m_SensitivityY
                );
            }

            this.m_ThirdPerson.MaxPitch = this.m_MaxPitch;
            this.m_ThirdPerson.Alignment.AutoAlign = this.m_AutoAlign;
            this.m_ThirdPerson.Alignment.Delay = this.m_AlignDelay;
            this.m_ThirdPerson.Alignment.SmoothTime = this.m_AlignSmoothTime;

            if (THIRD_PERSON_MAX_YAW_FIELD?.GetValue(this.m_ThirdPerson) is
                EnablerAngle180 maxYaw)
            {
                maxYaw.IsEnabled = this.m_EnableMaxYaw;
                maxYaw.Value = this.m_MaxYaw;
            }
        }

        private void UpdateCameraPivot()
        {
            if (this.m_CameraPivot == null || this.m_Player == null ||
                this.m_ActiveBike == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                this.m_ActiveBike.transform.forward,
                Vector3.up
            );
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(this.m_Player.transform.forward, Vector3.up);
            }

            Quaternion yaw = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : Quaternion.identity;
            this.m_CameraPivot.SetPositionAndRotation(this.m_Player.transform.position, yaw);
        }

        private void SnapBehindActiveBike()
        {
            this.UpdateCameraPivot();
            if (this.m_ThirdPerson == null || this.m_CameraPivot == null) return;
            this.m_ThirdPerson.SetDirection(this.m_CameraPivot.forward);
        }

        private static void SetDecimalField(
            ShotSystemThirdPerson thirdPerson,
            FieldInfo field,
            float value)
        {
            field?.SetValue(thirdPerson, new PropertyGetDecimal(value));
        }
    }
}
