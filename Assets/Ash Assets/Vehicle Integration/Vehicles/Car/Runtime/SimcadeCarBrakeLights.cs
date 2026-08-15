using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Car-specific rear lamp controller modelled after the Arcade Bike brake /
    /// reverse flare. It reads the already sampled Sim-Cade input and Rigidbody
    /// velocity, so it adds no second input stack and no collision polling.
    /// </summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarBrakeLights : MonoBehaviour
    {
        [Header("Car References")]
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private VehicleLights m_VehicleLights;
        [SerializeField] private Rigidbody m_Rigidbody;
        [SerializeField] private LensFlareComponentSRP m_LeftBrakeFlare;
        [SerializeField] private LensFlareComponentSRP m_RightBrakeFlare;

        [Header("Headlight Beam")]
        [SerializeField, Min(0f)] private float m_HeadlightIntensity = 45f;
        [SerializeField, Min(0.1f)] private float m_HeadlightRange = 42f;
        [SerializeField, Range(1f, 179f)] private float m_HeadlightSpotAngle = 68f;
        [SerializeField, Range(0f, 179f)] private float m_HeadlightInnerSpotAngle = 48f;

        [Header("Rear Lamp")]
        [SerializeField, Min(0f)] private float m_RunningLightMultiplier = 0.32f;
        [SerializeField, Min(0f)] private float m_BrakeLightMultiplier = 1.8f;
        [SerializeField, Min(0f)] private float m_BrakeFlareIntensity = 1.1f;
        [SerializeField, Min(0f)] private float m_ReverseVelocityThreshold = 0.15f;
        [SerializeField, Min(0.01f)] private float m_FadeInSpeed = 18f;
        [SerializeField, Min(0.01f)] private float m_FadeOutSpeed = 6f;

        private float m_CurrentBlend;
        private float m_LastAppliedBlend = -1f;
        private bool m_LastHeadlightState;

        public bool IsIlluminated => this.m_CurrentBlend > 0.01f;
        public float CurrentBlend => this.m_CurrentBlend;
        public float HeadlightIntensity => this.m_HeadlightIntensity;
        public float HeadlightRange => this.m_HeadlightRange;
        public float HeadlightSpotAngle => this.m_HeadlightSpotAngle;
        public VehicleLights VehicleLights => this.m_VehicleLights;
        public LensFlareComponentSRP LeftBrakeFlare => this.m_LeftBrakeFlare;
        public LensFlareComponentSRP RightBrakeFlare => this.m_RightBrakeFlare;
        public bool IsConfigured => this.m_Driver != null &&
            this.m_VehicleLights != null &&
            this.m_VehicleLights.HasRearLights &&
            this.m_Rigidbody != null &&
            this.m_LeftBrakeFlare != null &&
            this.m_RightBrakeFlare != null &&
            this.m_LeftBrakeFlare.lensFlareData != null &&
            this.m_RightBrakeFlare.lensFlareData != null;

        private void Awake()
        {
            this.ResolveReferences();
            this.ApplyHeadlightProfile();
            this.ApplyVisual(true);
        }

        private void OnEnable()
        {
            this.m_CurrentBlend = 0f;
            this.m_LastAppliedBlend = -1f;
            this.ApplyHeadlightProfile();
            this.ApplyVisual(true);
        }

        private void OnDisable()
        {
            this.m_CurrentBlend = 0f;
            this.m_LastAppliedBlend = -1f;
            this.SetBrakeFlareIntensity(0f);
            this.m_VehicleLights?.ClearRearLightOverride();
        }

        private void Update()
        {
            if (!this.ResolveReferences()) return;

            float target = this.ShouldIlluminate() ? 1f : 0f;
            float speed = target > this.m_CurrentBlend
                ? this.m_FadeInSpeed
                : this.m_FadeOutSpeed;
            this.m_CurrentBlend = Mathf.MoveTowards(
                this.m_CurrentBlend,
                target,
                speed * Time.deltaTime
            );
            this.ApplyVisual(false);
        }

        private void LateUpdate()
        {
            // The Sim-Cade visual body can roll independently from the Rigidbody
            // root while steering. The flare anchors therefore cannot remain at
            // a one-time root-local position: they would appear to slide over
            // the lamp meshes. Follow the exact renderer centres immediately
            // before rendering. Two TransformPoint calls are cheaper than adding
            // another per-light behaviour and are safe for the mobile target.
            if (this.m_CurrentBlend <= 0.001f || this.m_VehicleLights == null)
                return;

            SyncFlareAnchor(
                this.m_LeftBrakeFlare,
                this.m_VehicleLights.backLight1
            );
            SyncFlareAnchor(
                this.m_RightBrakeFlare,
                this.m_VehicleLights.backLight2
            );
        }

        public void Configure(
            SimcadeCarDriver driver,
            VehicleLights vehicleLights,
            Rigidbody rigidbody,
            LensFlareComponentSRP leftBrakeFlare,
            LensFlareComponentSRP rightBrakeFlare)
        {
            this.m_Driver = driver;
            this.m_VehicleLights = vehicleLights;
            this.m_Rigidbody = rigidbody;
            this.m_LeftBrakeFlare = leftBrakeFlare;
            this.m_RightBrakeFlare = rightBrakeFlare;
            this.SyncFlareAnchors();
            this.ApplyHeadlightProfile();
        }

        /// <summary>
        /// Removes both frame callbacks while this Car is parked. VehicleLights
        /// retains its normal head/tail emission; only brake/reverse presentation
        /// sleeps until driving resumes.
        /// </summary>
        public void SetRuntimeActive(bool active)
        {
            if (this.enabled == active) return;
            this.enabled = active;
        }

        private void SyncFlareAnchors()
        {
            if (this.m_VehicleLights == null) return;

            SyncFlareAnchor(
                this.m_LeftBrakeFlare,
                this.m_VehicleLights.backLight1
            );
            SyncFlareAnchor(
                this.m_RightBrakeFlare,
                this.m_VehicleLights.backLight2
            );
        }

        private static void SyncFlareAnchor(
            LensFlareComponentSRP flare,
            Renderer lamp)
        {
            if (flare == null || lamp == null) return;

            Vector3 center = lamp.transform.TransformPoint(
                lamp.localBounds.center
            );
            Transform anchor = flare.transform;
            if ((anchor.position - center).sqrMagnitude > 0.00000001f)
                anchor.position = center;
        }

        private bool ResolveReferences()
        {
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponent<SimcadeCarDriver>();
            if (this.m_VehicleLights == null)
                this.m_VehicleLights = this.GetComponent<VehicleLights>();
            if (this.m_Rigidbody == null)
                this.m_Rigidbody = this.GetComponent<Rigidbody>();

            return this.IsConfigured;
        }

        private bool ShouldIlluminate()
        {
            if (!this.m_Driver.IsVehicleEnabled || this.m_Driver.IsDestroyed)
                return false;
            if (this.m_Driver.IsHandbrakeRequested ||
                this.m_Driver.IsStoppingForExit)
            {
                return true;
            }

            Transform directionSource = this.m_Driver.VehicleBody;
            Vector3 forward = directionSource != null
                ? directionSource.forward
                : this.transform.forward;
            float signedSpeed = Vector3.Dot(this.m_Rigidbody.linearVelocity, forward);

            // The Sim-Cade negative axis first brakes a forward-moving car and
            // then becomes reverse. Both states need a visible rear warning.
            return this.m_Driver.SignedAccelerationInput < -0.01f ||
                   signedSpeed < -this.m_ReverseVelocityThreshold;
        }

        private void ApplyHeadlightProfile()
        {
            if (this.m_VehicleLights == null) return;

            this.m_VehicleLights.spotLightIntensity = this.m_HeadlightIntensity;
            this.ConfigureSpotlight(this.m_VehicleLights.spotLight1);
            this.ConfigureSpotlight(this.m_VehicleLights.spotLight2);
        }

        private void ConfigureSpotlight(Light spotlight)
        {
            if (spotlight == null) return;

            spotlight.range = this.m_HeadlightRange;
            spotlight.spotAngle = this.m_HeadlightSpotAngle;
            spotlight.innerSpotAngle = Mathf.Min(
                this.m_HeadlightInnerSpotAngle,
                this.m_HeadlightSpotAngle
            );
            spotlight.shadows = LightShadows.None;
        }

        private void ApplyVisual(bool force)
        {
            if (this.m_VehicleLights == null) return;

            bool headlightsOn = this.m_VehicleLights.AreLightsOn;
            if (!force &&
                Mathf.Abs(this.m_CurrentBlend - this.m_LastAppliedBlend) < 0.002f &&
                headlightsOn == this.m_LastHeadlightState)
            {
                return;
            }

            Color source = this.m_VehicleLights.backLightOnColor;
            Color running = headlightsOn
                ? source * this.m_RunningLightMultiplier
                : Color.black;
            Color braking = source * this.m_BrakeLightMultiplier;
            Color output = Color.Lerp(running, braking, this.m_CurrentBlend);
            this.m_VehicleLights.SetRearLightOverride(true, output);
            this.SetBrakeFlareIntensity(
                this.m_BrakeFlareIntensity * this.m_CurrentBlend
            );
            this.m_LastAppliedBlend = this.m_CurrentBlend;
            this.m_LastHeadlightState = headlightsOn;
        }

        private void SetBrakeFlareIntensity(float intensity)
        {
            intensity = Mathf.Max(0f, intensity);
            if (this.m_LeftBrakeFlare != null)
                this.m_LeftBrakeFlare.intensity = intensity;
            if (this.m_RightBrakeFlare != null)
                this.m_RightBrakeFlare.intensity = intensity;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_HeadlightIntensity = Mathf.Max(0f, this.m_HeadlightIntensity);
            this.m_HeadlightRange = Mathf.Max(0.1f, this.m_HeadlightRange);
            this.m_HeadlightSpotAngle = Mathf.Clamp(
                this.m_HeadlightSpotAngle,
                1f,
                179f
            );
            this.m_HeadlightInnerSpotAngle = Mathf.Clamp(
                this.m_HeadlightInnerSpotAngle,
                0f,
                this.m_HeadlightSpotAngle
            );
            this.m_RunningLightMultiplier = Mathf.Max(0f, this.m_RunningLightMultiplier);
            this.m_BrakeLightMultiplier = Mathf.Max(0f, this.m_BrakeLightMultiplier);
            this.m_BrakeFlareIntensity = Mathf.Max(0f, this.m_BrakeFlareIntensity);
            this.m_ReverseVelocityThreshold = Mathf.Max(
                0f,
                this.m_ReverseVelocityThreshold
            );
            this.m_FadeInSpeed = Mathf.Max(0.01f, this.m_FadeInSpeed);
            this.m_FadeOutSpeed = Mathf.Max(0.01f, this.m_FadeOutSpeed);
            this.ApplyHeadlightProfile();
        }
#endif
    }
}
