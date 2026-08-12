using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven Bike damage presentation: smoke, critical fire and a single
    /// terminal explosion. All effect objects are prebuilt by the editor setup.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinBikeHealth))]
    public sealed class FranklinBikeDamageEffects : MonoBehaviour
    {
        [SerializeField] private FranklinBikeHealth m_Health;
        [SerializeField, Range(0.05f, 0.8f)] private float m_SmokeHealthThreshold = 0.32f;
        [SerializeField, Range(0.01f, 0.5f)] private float m_CriticalFireThreshold = 0.14f;
        [SerializeField, Min(0.1f)] private float m_PreExplosionWarningDuration = 1.35f;

        [Header("Critical Fire Health Drain")]
        [Tooltip("Fraction of maximum Bike HP removed each second while critical fire is active. 0.015 drains the final 14% in about 9.3 seconds.")]
        [SerializeField, Range(0.005f, 0.2f)]
        private float m_CriticalHealthDrainPerSecond = 0.015f;
        [Tooltip("Low-frequency damage tick used instead of a per-frame health update.")]
        [SerializeField, Range(0.1f, 1f)] private float m_CriticalHealthDrainTick = 0.25f;

        [Header("Loop Effects")]
        [SerializeField] private GameObject m_WeakHealthSmoke;
        [SerializeField] private GameObject m_CriticalWarningFire;
        [SerializeField] private GameObject m_DestroyedFire;
        [SerializeField] private FranklinBikeParticleWind m_ParticleWind;

        [Header("Loop Audio")]
        [SerializeField] private AudioSource m_SmokeAudioSource;
        [SerializeField] private AudioClip m_SmokeLoopClip;
        [SerializeField] private AudioSource m_FireAudioSource;
        [SerializeField] private AudioClip m_FireLoopClip;
        [SerializeField, Range(0f, 1f)] private float m_SmokeLoopVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] private float m_CriticalFireLoopVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_DestroyedFireLoopVolume = 0.8f;

        [Header("One-shot Explosion")]
        [SerializeField] private ParticleSystem[] m_ExplosionParticles =
            System.Array.Empty<ParticleSystem>();
        [SerializeField] private AudioSource m_ExplosionAudioSource;
        [SerializeField] private AudioClip m_ExplosionClip;
        [SerializeField] private FranklinBikeDestruction m_Destruction;

        private bool m_HasExploded;
        private Coroutine m_PendingExplosion;
        private Coroutine m_CriticalHealthDrain;

        public bool IsConfigured => m_Health != null && m_WeakHealthSmoke != null &&
            m_CriticalWarningFire != null && m_DestroyedFire != null &&
            m_ParticleWind != null && m_ParticleWind.IsConfigured &&
            m_SmokeAudioSource != null && m_SmokeLoopClip != null &&
            m_FireAudioSource != null && m_FireLoopClip != null &&
            m_ExplosionParticles != null && m_ExplosionParticles.Length >= 3 &&
            m_ExplosionAudioSource != null && m_ExplosionClip != null &&
            m_Destruction != null && m_Destruction.IsConfigured;
        public bool HasExploded => m_HasExploded;
        public float SmokeHealthThreshold => m_SmokeHealthThreshold;
        public float CriticalFireThreshold => m_CriticalFireThreshold;
        public float CriticalHealthDrainPerSecond =>
            m_CriticalHealthDrainPerSecond;
        public bool HasCurrentConfiguration =>
            Mathf.Abs(m_CriticalHealthDrainPerSecond - 0.015f) < 0.0001f &&
            Mathf.Abs(m_CriticalHealthDrainTick - 0.25f) < 0.001f;

        public void Configure(
            FranklinBikeHealth health,
            GameObject weakHealthSmoke,
            GameObject criticalWarningFire,
            GameObject destroyedFire,
            FranklinBikeParticleWind particleWind,
            AudioSource smokeAudioSource,
            AudioClip smokeLoopClip,
            AudioSource fireAudioSource,
            AudioClip fireLoopClip,
            ParticleSystem[] explosionParticles,
            AudioSource explosionAudioSource,
            AudioClip explosionClip,
            FranklinBikeDestruction destruction)
        {
            m_Health = health;
            m_WeakHealthSmoke = weakHealthSmoke;
            m_CriticalWarningFire = criticalWarningFire;
            m_DestroyedFire = destroyedFire;
            m_ParticleWind = particleWind;
            m_SmokeAudioSource = smokeAudioSource;
            m_SmokeLoopClip = smokeLoopClip;
            m_FireAudioSource = fireAudioSource;
            m_FireLoopClip = fireLoopClip;
            m_ExplosionParticles = explosionParticles ??
                System.Array.Empty<ParticleSystem>();
            m_ExplosionAudioSource = explosionAudioSource;
            m_ExplosionClip = explosionClip;
            m_Destruction = destruction;
            m_SmokeHealthThreshold = 0.32f;
            m_CriticalFireThreshold = 0.14f;
            m_PreExplosionWarningDuration = 1.35f;
            m_CriticalHealthDrainPerSecond = 0.015f;
            m_CriticalHealthDrainTick = 0.25f;
            m_SmokeLoopVolume = 0.18f;
            m_CriticalFireLoopVolume = 0.5f;
            m_DestroyedFireLoopVolume = 0.8f;
        }

        private void Awake()
        {
            if (m_Health == null) m_Health = GetComponent<FranklinBikeHealth>();
        }

        private void OnEnable()
        {
            if (m_Health == null) m_Health = GetComponent<FranklinBikeHealth>();
            if (m_Health != null)
            {
                m_Health.EventHealthChanged += OnHealthChanged;
                ApplyHealthState(m_Health.CurrentHealth, m_Health.MaximumHealth);
            }
        }

        private void Start()
        {
            if (m_Health != null)
                ApplyHealthState(m_Health.CurrentHealth, m_Health.MaximumHealth);
        }

        private void OnDisable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged -= OnHealthChanged;
            CancelPendingExplosion();
            StopCriticalHealthDrain();
            m_ParticleWind?.SetWindActive(false);
            StopLoopAudio(m_SmokeAudioSource);
            StopLoopAudio(m_FireAudioSource);
        }

        private void OnHealthChanged(float current, float maximum)
        {
            ApplyHealthState(current, maximum);
        }

        private void ApplyHealthState(float current, float maximum)
        {
            if (maximum <= 0.001f) return;
            float ratio = Mathf.Clamp01(current / maximum);
            bool destroyed = ratio <= 0.001f;
            bool terminalWreck = m_Destruction != null && m_Destruction.IsDestroyed;

            if (terminalWreck)
            {
                CancelPendingExplosion();
                StopCriticalHealthDrain();
                SetLoopEffectActive(m_WeakHealthSmoke, false);
                SetLoopEffectActive(m_CriticalWarningFire, false);
                SetLoopEffectActive(m_DestroyedFire, true);
                RefreshLoopState();
                return;
            }

            SetLoopEffectActive(m_WeakHealthSmoke, ratio <= m_SmokeHealthThreshold);
            SetLoopEffectActive(
                m_CriticalWarningFire,
                ratio <= m_CriticalFireThreshold
            );
            SetLoopEffectActive(m_DestroyedFire, false);

            if (destroyed)
            {
                StopCriticalHealthDrain();
                ScheduleExplosionAfterWarning();
            }
            else
            {
                CancelPendingExplosion();
                m_HasExploded = false;
                if (ratio <= m_CriticalFireThreshold)
                    StartCriticalHealthDrain();
                else
                    StopCriticalHealthDrain();
            }
            RefreshLoopState();
        }

        private void StartCriticalHealthDrain()
        {
            if (m_CriticalHealthDrain != null || !isActiveAndEnabled) return;
            m_CriticalHealthDrain = StartCoroutine(DrainCriticalHealth());
        }

        private void StopCriticalHealthDrain()
        {
            if (m_CriticalHealthDrain == null) return;
            StopCoroutine(m_CriticalHealthDrain);
            m_CriticalHealthDrain = null;
        }

        private IEnumerator DrainCriticalHealth()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
                Mathf.Clamp(m_CriticalHealthDrainTick, 0.1f, 1f)
            );
            while (m_Health != null && !m_Health.IsDestroyed &&
                   m_Health.MaximumHealth > 0.001f &&
                   m_Health.HealthRatio <= m_CriticalFireThreshold)
            {
                yield return wait;
                if (m_Health == null || m_Health.IsDestroyed ||
                    m_Health.HealthRatio > m_CriticalFireThreshold)
                {
                    break;
                }

                float damage = m_Health.MaximumHealth *
                    m_CriticalHealthDrainPerSecond *
                    Mathf.Clamp(m_CriticalHealthDrainTick, 0.1f, 1f);
                m_Health.ApplyDamage(damage);
            }
            m_CriticalHealthDrain = null;
        }

        private void ScheduleExplosionAfterWarning()
        {
            if (m_HasExploded || m_PendingExplosion != null) return;
            m_PendingExplosion = StartCoroutine(ExplosionAfterWarning());
        }

        private IEnumerator ExplosionAfterWarning()
        {
            yield return new WaitForSecondsRealtime(m_PreExplosionWarningDuration);
            m_PendingExplosion = null;
            if (m_Health == null || m_Health.MaximumHealth <= 0.001f) yield break;
            if (m_Health.CurrentHealth / m_Health.MaximumHealth > 0.001f) yield break;
            TriggerExplosion();
        }

        private void TriggerExplosion()
        {
            if (m_HasExploded) return;
            m_HasExploded = true;
            SetLoopEffectActive(m_WeakHealthSmoke, false);
            SetLoopEffectActive(m_CriticalWarningFire, false);
            SetLoopEffectActive(m_DestroyedFire, true);
            RefreshLoopState();

            if (m_ExplosionAudioSource != null && m_ExplosionClip != null)
            {
                m_ExplosionAudioSource.Stop();
                m_ExplosionAudioSource.PlayOneShot(m_ExplosionClip, 1f);
            }
            for (int i = 0; i < m_ExplosionParticles.Length; ++i)
            {
                ParticleSystem particles = m_ExplosionParticles[i];
                if (particles == null) continue;
                particles.Clear(true);
                particles.Play(true);
            }
            m_Destruction?.TriggerDestruction();
        }

        private void CancelPendingExplosion()
        {
            if (m_PendingExplosion == null) return;
            StopCoroutine(m_PendingExplosion);
            m_PendingExplosion = null;
        }

        private void RefreshLoopState()
        {
            bool smoke = m_WeakHealthSmoke != null && m_WeakHealthSmoke.activeSelf;
            bool warningFire = m_CriticalWarningFire != null &&
                m_CriticalWarningFire.activeSelf;
            bool destroyedFire = m_DestroyedFire != null && m_DestroyedFire.activeSelf;
            m_ParticleWind?.SetWindActive(smoke || warningFire || destroyedFire);
            SetLoopAudioState(
                m_SmokeAudioSource,
                m_SmokeLoopClip,
                smoke,
                m_SmokeLoopVolume
            );
            SetLoopAudioState(
                m_FireAudioSource,
                m_FireLoopClip,
                warningFire || destroyedFire,
                destroyedFire ? m_DestroyedFireLoopVolume : m_CriticalFireLoopVolume
            );
        }

        private static void SetLoopAudioState(
            AudioSource source,
            AudioClip clip,
            bool active,
            float volume)
        {
            if (source == null) return;
            if (!active || clip == null)
            {
                StopLoopAudio(source);
                return;
            }
            source.clip = clip;
            source.loop = true;
            source.volume = Mathf.Clamp01(volume);
            if (!source.isPlaying) source.Play();
        }

        private static void StopLoopAudio(AudioSource source)
        {
            if (source != null && source.isPlaying) source.Stop();
        }

        private static void SetLoopEffectActive(GameObject effect, bool active)
        {
            if (effect == null || effect.activeSelf == active) return;
            if (!active)
            {
                ParticleSystem[] systems =
                    effect.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; ++i)
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            effect.SetActive(active);
            if (!active) return;
            ParticleSystem[] activeSystems =
                effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < activeSystems.Length; ++i)
                activeSystems[i].Play(true);
        }

        private void OnValidate()
        {
            m_SmokeHealthThreshold = Mathf.Clamp(m_SmokeHealthThreshold, 0.05f, 0.8f);
            m_CriticalFireThreshold = Mathf.Clamp(
                m_CriticalFireThreshold,
                0.01f,
                m_SmokeHealthThreshold
            );
            m_PreExplosionWarningDuration = Mathf.Max(0.1f, m_PreExplosionWarningDuration);
            m_CriticalHealthDrainPerSecond = Mathf.Clamp(
                m_CriticalHealthDrainPerSecond,
                0.005f,
                0.2f
            );
            m_CriticalHealthDrainTick = Mathf.Clamp(
                m_CriticalHealthDrainTick,
                0.1f,
                1f
            );
        }
    }
}
