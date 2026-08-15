using ArcadeBP_Pro;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Drives the bike's red rear point light and SRP lens flare from the actual
    /// Arcade Bike Physics brake/reverse input and backward velocity.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FranklinBikeBrakeReverseFlare : MonoBehaviour
    {
        [Header("Bike References")]
        [SerializeField] private FranklinArcadeBikeDriver m_Driver;
        [SerializeField] private ArcadeBikeControllerPro m_Controller;
        [SerializeField] private Rigidbody m_Rigidbody;
        [SerializeField] private Light m_RedPointLight;
        [SerializeField] private LensFlareComponentSRP m_RedFlare;

        [Header("Brake / Reverse Flare")]
        [SerializeField, Min(0f)] private float m_PointLightIntensity = 4.5f;
        [SerializeField, Min(0f)] private float m_FlareIntensity = 1.4f;
        [SerializeField, Min(0f)] private float m_ReverseVelocityThreshold = 0.15f;
        [SerializeField, Min(0.01f)] private float m_FadeInSpeed = 18f;
        [SerializeField, Min(0.01f)] private float m_FadeOutSpeed = 6f;

        private float m_CurrentBlend;

        public bool IsIlluminated => this.m_CurrentBlend > 0.01f;
        public float CurrentBlend => this.m_CurrentBlend;
        public Light RedPointLight => this.m_RedPointLight;
        public LensFlareComponentSRP RedFlare => this.m_RedFlare;

        private void Awake()
        {
            this.ResolveReferences();
            this.ApplyVisual(0f);
            if (this.m_Driver == null || !this.m_Driver.IsVehicleEnabled)
                this.enabled = false;
        }

        private void OnEnable()
        {
            this.m_CurrentBlend = 0f;
            this.ApplyVisual(0f);
        }

        private void OnDisable()
        {
            this.m_CurrentBlend = 0f;
            this.ApplyVisual(0f);
        }

        private void Update()
        {
            if (!this.ResolveReferences())
            {
                this.m_CurrentBlend = 0f;
                this.ApplyVisual(0f);
                this.enabled = false;
                return;
            }

            float target = this.ShouldIlluminate() ? 1f : 0f;
            float speed = target > this.m_CurrentBlend
                ? this.m_FadeInSpeed
                : this.m_FadeOutSpeed;
            this.m_CurrentBlend = Mathf.MoveTowards(
                this.m_CurrentBlend,
                target,
                speed * Time.unscaledDeltaTime
            );
            this.ApplyVisual(this.m_CurrentBlend);
        }

        /// <summary>
        /// The owning driver wakes this component only for the selected Bike.
        /// Parked Bikes therefore contribute no Update callback, Light or SRP flare.
        /// </summary>
        public void SetRuntimeActive(bool active)
        {
            if (active)
            {
                if (!this.enabled) this.enabled = true;
                return;
            }

            this.m_CurrentBlend = 0f;
            this.ApplyVisual(0f);
            if (this.enabled) this.enabled = false;
        }

        public void Configure(
            FranklinArcadeBikeDriver driver,
            ArcadeBikeControllerPro controller,
            Rigidbody rigidbody,
            Light redPointLight,
            LensFlareComponentSRP redFlare)
        {
            this.m_Driver = driver;
            this.m_Controller = controller;
            this.m_Rigidbody = rigidbody;
            this.m_RedPointLight = redPointLight;
            this.m_RedFlare = redFlare;
        }

        private bool ResolveReferences()
        {
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponentInParent<FranklinArcadeBikeDriver>();
            if (this.m_Controller == null)
                this.m_Controller = this.GetComponentInParent<ArcadeBikeControllerPro>();
            if (this.m_Rigidbody == null)
                this.m_Rigidbody = this.GetComponentInParent<Rigidbody>();
            if (this.m_RedPointLight == null)
                this.m_RedPointLight = this.GetComponent<Light>();
            if (this.m_RedFlare == null)
                this.m_RedFlare = this.GetComponent<LensFlareComponentSRP>();

            return this.m_Driver != null &&
                   this.m_Controller != null &&
                   this.m_Rigidbody != null &&
                   this.m_RedPointLight != null &&
                   this.m_RedFlare != null;
        }

        private bool ShouldIlluminate()
        {
            if (!this.m_Driver.IsVehicleEnabled) return false;

            ArcadeBikeControllerPro.BikeInput input = this.m_Controller.bikeInput;
            if (input == null) return false;

            Transform directionSource = this.m_Controller.bikeReferences?.Rotator;
            Vector3 forward = directionSource != null
                ? directionSource.forward
                : this.m_Controller.transform.forward;
            float forwardSpeed = Vector3.Dot(this.m_Rigidbody.linearVelocity, forward);

            bool reverseRequested = input.Reverse > 0.01f;
            bool handbrakeRequested = input.HandBrake > 0.01f;
            bool movingBackward = forwardSpeed < -this.m_ReverseVelocityThreshold;
            bool brakingBackward = movingBackward && input.Accelerate > 0.01f;

            return reverseRequested || handbrakeRequested ||
                   movingBackward || brakingBackward;
        }

        private void ApplyVisual(float blend)
        {
            blend = Mathf.Clamp01(blend);
            bool visible = blend > 0.0001f;
            if (this.m_RedPointLight != null)
            {
                float intensity = this.m_PointLightIntensity * blend;
                if (!Mathf.Approximately(this.m_RedPointLight.intensity, intensity))
                    this.m_RedPointLight.intensity = intensity;
                if (this.m_RedPointLight.enabled != visible)
                    this.m_RedPointLight.enabled = visible;
            }

            if (this.m_RedFlare != null)
            {
                float intensity = this.m_FlareIntensity * blend;
                if (!Mathf.Approximately(this.m_RedFlare.intensity, intensity))
                    this.m_RedFlare.intensity = intensity;
                if (this.m_RedFlare.enabled != visible)
                    this.m_RedFlare.enabled = visible;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_PointLightIntensity = Mathf.Max(0f, this.m_PointLightIntensity);
            this.m_FlareIntensity = Mathf.Max(0f, this.m_FlareIntensity);
            this.m_ReverseVelocityThreshold = Mathf.Max(
                0f,
                this.m_ReverseVelocityThreshold
            );
            this.m_FadeInSpeed = Mathf.Max(0.01f, this.m_FadeInSpeed);
            this.m_FadeOutSpeed = Mathf.Max(0.01f, this.m_FadeOutSpeed);
        }
#endif
    }
}
