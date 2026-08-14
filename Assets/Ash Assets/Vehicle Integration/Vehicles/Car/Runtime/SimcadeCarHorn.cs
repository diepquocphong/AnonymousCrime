using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Mobile-friendly hold horn for the Sim-Cade Car. The component sleeps
    /// while silent, owns one preconfigured 3D AudioSource, and performs no
    /// polling, allocation, lookup, or clip creation while the horn is idle.
    /// </summary>
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarHorn : MonoBehaviour
    {
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private AudioSource m_Source;
        [SerializeField] private AudioClip m_Clip;
        [SerializeField, Range(0f, 1f)] private float m_MaxVolume = 0.78f;
        [SerializeField, Min(0.01f)] private float m_FadeInSpeed = 18f;
        [SerializeField, Min(0.01f)] private float m_FadeOutSpeed = 14f;

        private bool m_IsPressed;

        public bool IsPressed => this.m_IsPressed;
        public bool IsConfigured => this.m_Driver != null &&
                                    this.m_Source != null &&
                                    this.m_Clip != null;
        public AudioSource Source => this.m_Source;
        public AudioClip Clip => this.m_Clip;

        private void Awake()
        {
            this.ResolveReferences();
            this.ConfigureSource();
            this.enabled = false;
        }

        private void Update()
        {
            if (!this.IsConfigured)
            {
                this.StopImmediately();
                this.enabled = false;
                return;
            }

            if (!this.m_Driver.IsVehicleEnabled || this.m_Driver.IsDestroyed)
                this.m_IsPressed = false;

            float target = this.m_IsPressed ? this.m_MaxVolume : 0f;
            float speed = this.m_IsPressed
                ? this.m_FadeInSpeed
                : this.m_FadeOutSpeed;
            this.m_Source.volume = Mathf.MoveTowards(
                this.m_Source.volume,
                target,
                speed * Time.unscaledDeltaTime
            );

            if (!this.m_IsPressed && this.m_Source.volume <= 0.001f)
            {
                this.StopImmediately();
                this.enabled = false;
            }
        }

        private void OnDisable()
        {
            this.m_IsPressed = false;
            this.StopImmediately();
        }

        public void SetPressed(bool pressed)
        {
            if (!this.ResolveReferences()) return;
            if (pressed &&
                (!this.m_Driver.IsVehicleEnabled || this.m_Driver.IsDestroyed))
            {
                return;
            }

            this.m_IsPressed = pressed;
            if (!pressed && !this.m_Source.isPlaying) return;

            this.enabled = true;
            if (pressed && !this.m_Source.isPlaying)
            {
                this.ConfigureSource();
                this.m_Source.volume = 0f;
                this.m_Source.Play();
            }
        }

        public void StopImmediately()
        {
            if (this.m_Source == null) return;
            this.m_Source.Stop();
            this.m_Source.volume = 0f;
        }

        private bool ResolveReferences()
        {
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponent<SimcadeCarDriver>();
            return this.IsConfigured;
        }

        private void ConfigureSource()
        {
            if (this.m_Source == null || this.m_Clip == null) return;

            this.m_Source.clip = this.m_Clip;
            this.m_Source.playOnAwake = false;
            this.m_Source.loop = true;
            this.m_Source.spatialBlend = 1f;
            this.m_Source.dopplerLevel = 0.2f;
            this.m_Source.minDistance = 3f;
            this.m_Source.maxDistance = 55f;
            this.m_Source.rolloffMode = AudioRolloffMode.Logarithmic;
            this.m_Source.priority = 96;
        }

#if UNITY_EDITOR
        public void Configure(
            SimcadeCarDriver driver,
            AudioSource source,
            AudioClip clip)
        {
            this.m_Driver = driver;
            this.m_Source = source;
            this.m_Clip = clip;
            this.ConfigureSource();
        }

        private void OnValidate()
        {
            this.m_MaxVolume = Mathf.Clamp01(this.m_MaxVolume);
            this.m_FadeInSpeed = Mathf.Max(0.01f, this.m_FadeInSpeed);
            this.m_FadeOutSpeed = Mathf.Max(0.01f, this.m_FadeOutSpeed);
            this.ConfigureSource();
        }
#endif
    }
}
