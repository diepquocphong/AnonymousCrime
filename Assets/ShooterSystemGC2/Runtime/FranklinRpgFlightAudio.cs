using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Plays the RPG launch transient and one pooled-projectile flight loop. The component
    /// owns no Update loop and reuses its AudioSource across every GC2 pool activation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class FranklinRpgFlightAudio : MonoBehaviour
    {
        private const float LAUNCH_MIN_DISTANCE = 12f;
        private const float LAUNCH_MAX_DISTANCE = 240f;
        private const float FLIGHT_MIN_DISTANCE = 14f;
        private const float FLIGHT_MAX_DISTANCE = 260f;

        [SerializeField] private AudioClip m_LaunchClip;
        [SerializeField] private AudioClip m_FlightLoop;
        [SerializeField, Range(0f, 1f)] private float m_FlightVolume = 0.72f;

        private AudioSource m_Source;

        public AudioClip LaunchClip => this.m_LaunchClip;
        public AudioClip FlightLoop => this.m_FlightLoop;
        public float FlightVolume => this.m_FlightVolume;

        public void Configure(AudioClip launchClip, AudioClip flightLoop)
        {
            this.m_LaunchClip = launchClip;
            this.m_FlightLoop = flightLoop;
            this.m_FlightVolume = 0.72f;
            this.ResolveAndConfigureSource();
        }

        private void Awake()
        {
            this.ResolveAndConfigureSource();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.ResolveAndConfigureSource();
            FranklinThrowableAudioPool.Play(
                this.transform.position,
                this.m_LaunchClip,
                1f,
                1f,
                LAUNCH_MAX_DISTANCE,
                LAUNCH_MIN_DISTANCE
            );

            if (this.m_Source == null || this.m_FlightLoop == null) return;
            this.m_Source.clip = this.m_FlightLoop;
            this.m_Source.Play();
        }

        private void OnDisable()
        {
            if (this.m_Source == null) return;
            this.m_Source.Stop();
            this.m_Source.clip = null;
        }

        private void ResolveAndConfigureSource()
        {
            if (this.m_Source == null) this.m_Source = this.GetComponent<AudioSource>();
            if (this.m_Source == null) return;

            this.m_Source.playOnAwake = false;
            this.m_Source.loop = true;
            this.m_Source.spatialBlend = 1f;
            this.m_Source.rolloffMode = AudioRolloffMode.Logarithmic;
            this.m_Source.minDistance = FLIGHT_MIN_DISTANCE;
            this.m_Source.maxDistance = FLIGHT_MAX_DISTANCE;
            this.m_Source.dopplerLevel = 0.65f;
            this.m_Source.spread = 30f;
            this.m_Source.volume = this.m_FlightVolume;
            this.m_Source.priority = 96;
            this.m_Source.reverbZoneMix = 0.7f;
        }
    }
}
