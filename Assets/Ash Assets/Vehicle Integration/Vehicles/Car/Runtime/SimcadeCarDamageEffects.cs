using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven, mobile-safe damage presentation. Loop effects are prebuilt on
    /// the prefab and the explosion burst can fire only once per destroyed state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimcadeCarHealth))]
    public sealed class SimcadeCarDamageEffects : MonoBehaviour
    {
        [SerializeField] private SimcadeCarHealth m_Health;
        [SerializeField, Range(0.05f, 0.8f)] private float m_SmokeHealthThreshold = 0.32f;
        [SerializeField, Range(0.01f, 0.5f)] private float m_CriticalFireThreshold = 0.14f;
        [SerializeField, Min(0.1f)] private float m_PreExplosionWarningDuration = 1.35f;

        [Header("Critical Fire Health Drain")]
        [SerializeField, Min(1f)] private float m_CriticalBurnDuration = 7f;
        [SerializeField, Range(0.1f, 0.5f)] private float m_CriticalBurnTickInterval = 0.25f;

        [Header("Loop Effects")]
        [SerializeField] private GameObject m_WeakHealthSmoke;
        [SerializeField] private GameObject m_CriticalWarningFire;
        [SerializeField] private GameObject m_DestroyedFire;
        [SerializeField] private SimcadeCarParticleWind m_ParticleWind;
        [SerializeField, Min(10f)] private float m_LoopEffectsCullDistance = 55f;
        [SerializeField, Range(0.5f, 2f)]
        private float m_LoopEffectsLodInterval = 1f;
        [Tooltip("Destroyed Fire stops after this lifetime; the charred wreck remains visible.")]
        [SerializeField, Min(5f)] private float m_DestroyedFireLifetime = 25f;

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
        [SerializeField] private SimcadeCarDestruction m_Destruction;

        [Header("Explosion Camera Timing")]
        [Tooltip("Minimum time the Car camera keeps showing the explosion burst.")]
        [SerializeField, Min(0f)] private float m_MinimumExplosionCameraHold = 0.9f;
        [Tooltip("Caps long particle/audio tails before the camera returns to Player.")]
        [SerializeField, Min(0.1f)] private float m_MaximumExplosionCameraHold = 2.5f;

        private bool m_HasExploded;
        private Coroutine m_PendingExplosion;
        private Coroutine m_CriticalHealthDrain;
        private Coroutine m_LoopEffectsLodRoutine;
        private Coroutine m_DestroyedFireLifetimeRoutine;
        private ParticleSystem[] m_WeakSmokeParticles =
            System.Array.Empty<ParticleSystem>();
        private ParticleSystem[] m_CriticalFireParticles =
            System.Array.Empty<ParticleSystem>();
        private ParticleSystem[] m_DestroyedFireParticles =
            System.Array.Empty<ParticleSystem>();
        private bool m_LoopEffectsCulled;
        private bool m_DestroyedFireExpired;
        private static Camera s_LoopEffectsCamera;

        public bool IsConfigured => m_Health != null && m_WeakHealthSmoke != null &&
            m_CriticalWarningFire != null && m_DestroyedFire != null &&
            m_ParticleWind != null && m_ParticleWind.IsConfigured &&
            m_SmokeAudioSource != null && m_SmokeLoopClip != null &&
            m_FireAudioSource != null && m_FireLoopClip != null &&
            m_ExplosionParticles != null &&
            m_ExplosionParticles.Length >= 3 && m_ExplosionAudioSource != null &&
            m_ExplosionClip != null && m_Destruction != null &&
            m_Destruction.IsConfigured;
        public bool HasExploded => m_HasExploded;
        public float SmokeHealthThreshold => m_SmokeHealthThreshold;
        public float CriticalFireThreshold => m_CriticalFireThreshold;
        public float PreExplosionWarningDuration => m_PreExplosionWarningDuration;
        public float CriticalBurnDuration => m_CriticalBurnDuration;
        public float CriticalBurnTickInterval => m_CriticalBurnTickInterval;
        public float SmokeLoopVolume => m_SmokeLoopVolume;
        public float CriticalFireLoopVolume => m_CriticalFireLoopVolume;
        public float DestroyedFireLoopVolume => m_DestroyedFireLoopVolume;
        public float DestroyedFireLifetime => m_DestroyedFireLifetime;
        public float MinimumExplosionCameraHold => m_MinimumExplosionCameraHold;
        public float MaximumExplosionCameraHold => m_MaximumExplosionCameraHold;

        private void Awake()
        {
            if (m_Health == null) m_Health = GetComponent<SimcadeCarHealth>();
            CacheLoopParticles();
        }

        private void OnEnable()
        {
            if (m_Health == null) m_Health = GetComponent<SimcadeCarHealth>();
            if (m_Health != null)
            {
                m_Health.EventHealthChanged += OnHealthChanged;
                ApplyHealthState(m_Health.CurrentHealth, m_Health.MaximumHealth);
            }
        }

        private void Start()
        {
            ApplyHealthState(
                m_Health != null ? m_Health.CurrentHealth : 0f,
                m_Health != null ? m_Health.MaximumHealth : 0f
            );
        }

        private void OnDisable()
        {
            if (m_Health != null)
                m_Health.EventHealthChanged -= OnHealthChanged;
            CancelPendingExplosion();
            StopCriticalHealthDrain();
            StopDestroyedFireLifetime();
            if (m_HasExploded || (m_Destruction != null && m_Destruction.IsDestroyed))
                m_DestroyedFireExpired = true;
            SetLoopEffectActive(m_DestroyedFire, false);
            StopLoopEffectsLod();
            m_LoopEffectsCulled = true;
            ApplyLoopParticleCulling();
            m_ParticleWind?.SetWindActive(false);
            StopLoopAudio(m_SmokeAudioSource);
            StopLoopAudio(m_FireAudioSource);
        }

        public void Configure(
            SimcadeCarHealth health,
            GameObject weakHealthSmoke,
            GameObject criticalWarningFire,
            GameObject destroyedFire,
            SimcadeCarParticleWind particleWind,
            AudioSource smokeAudioSource,
            AudioClip smokeLoopClip,
            AudioSource fireAudioSource,
            AudioClip fireLoopClip,
            float smokeLoopVolume,
            float criticalFireLoopVolume,
            float destroyedFireLoopVolume,
            ParticleSystem[] explosionParticles,
            AudioSource explosionAudioSource,
            AudioClip explosionClip,
            float smokeHealthThreshold,
            float criticalFireThreshold,
            float preExplosionWarningDuration,
            SimcadeCarDestruction destruction)
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
            m_SmokeLoopVolume = Mathf.Clamp01(smokeLoopVolume);
            m_CriticalFireLoopVolume = Mathf.Clamp01(criticalFireLoopVolume);
            m_DestroyedFireLoopVolume = Mathf.Clamp01(destroyedFireLoopVolume);
            m_ExplosionParticles = explosionParticles ?? System.Array.Empty<ParticleSystem>();
            m_ExplosionAudioSource = explosionAudioSource;
            m_ExplosionClip = explosionClip;
            m_SmokeHealthThreshold = Mathf.Clamp(smokeHealthThreshold, 0.05f, 0.8f);
            m_CriticalFireThreshold = Mathf.Clamp(
                criticalFireThreshold,
                0.01f,
                m_SmokeHealthThreshold
            );
            m_PreExplosionWarningDuration = Mathf.Max(
                0.1f,
                preExplosionWarningDuration
            );
            m_CriticalBurnDuration = 7f;
            m_CriticalBurnTickInterval = 0.25f;
            m_MinimumExplosionCameraHold = 0.9f;
            m_MaximumExplosionCameraHold = 2.5f;
            m_DestroyedFireLifetime = 25f;
            m_Destruction = destruction;
            CacheLoopParticles();
        }

        public void RefreshFromHealth()
        {
            if (m_Health == null) m_Health = GetComponent<SimcadeCarHealth>();
            if (m_Health != null)
                ApplyHealthState(m_Health.CurrentHealth, m_Health.MaximumHealth);
        }

        [ContextMenu("Damage FX/Preview Weak Smoke")]
        public void PreviewWeakHealthSmoke()
        {
            if (!CanPreviewInPlayMode()) return;
            SetLoopEffectActive(m_WeakHealthSmoke, true);
            SetLoopEffectActive(m_CriticalWarningFire, false);
            SetLoopEffectActive(m_DestroyedFire, false);
            RefreshWindActivity();
        }

        [ContextMenu("Damage FX/Preview Critical Fire Warning")]
        public void PreviewCriticalFireWarning()
        {
            if (!CanPreviewInPlayMode()) return;
            SetLoopEffectActive(m_WeakHealthSmoke, true);
            SetLoopEffectActive(m_CriticalWarningFire, true);
            SetLoopEffectActive(m_DestroyedFire, false);
            RefreshWindActivity();
        }

        [ContextMenu("Damage FX/Preview Explosion + Fire")]
        public void PreviewExplosionAndFire()
        {
            if (!CanPreviewInPlayMode()) return;
            SetLoopEffectActive(m_WeakHealthSmoke, false);
            SetLoopEffectActive(m_CriticalWarningFire, false);
            SetLoopEffectActive(m_DestroyedFire, true);
            RefreshWindActivity();
            m_HasExploded = false;
            TriggerExplosion(false);
        }

        [ContextMenu("Damage FX/Stop Preview")]
        public void StopPreview()
        {
            if (!CanPreviewInPlayMode()) return;
            SetLoopEffectActive(m_WeakHealthSmoke, false);
            SetLoopEffectActive(m_CriticalWarningFire, false);
            SetLoopEffectActive(m_DestroyedFire, false);
            CancelPendingExplosion();
            StopDestroyedFireLifetime();
            m_DestroyedFireExpired = false;
            RefreshWindActivity();
            for (int i = 0; i < m_ExplosionParticles.Length; ++i)
            {
                if (m_ExplosionParticles[i] == null) continue;
                m_ExplosionParticles[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
            if (m_ExplosionAudioSource != null) m_ExplosionAudioSource.Stop();
            StopLoopAudio(m_SmokeAudioSource);
            StopLoopAudio(m_FireAudioSource);
        }

        private bool CanPreviewInPlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning(
                "Damage FX preview chỉ chạy trong Play Mode để không ghi trạng thái preview vào prefab.",
                this
            );
            return false;
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
                SetLoopEffectActive(m_DestroyedFire, !m_DestroyedFireExpired);
                RefreshWindActivity();
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
                ScheduleExplosionAfterWarning();
            }
            else
            {
                CancelPendingExplosion();
                if (ratio <= m_CriticalFireThreshold)
                    EnsureCriticalHealthDrain();
                // Terminal destruction is deliberately permanent. A repaired
                // wreck may stop emitting loop VFX, but it must never explode a
                // second time or become driveable again without respawning it.
                if (m_Destruction == null || !m_Destruction.IsDestroyed)
                {
                    m_HasExploded = false;
                    m_DestroyedFireExpired = false;
                    StopDestroyedFireLifetime();
                }
            }
            RefreshWindActivity();
        }

        private void EnsureCriticalHealthDrain()
        {
            if (m_CriticalHealthDrain != null || m_Health == null) return;
            m_CriticalHealthDrain = StartCoroutine(DrainCriticalHealth());
        }

        private IEnumerator DrainCriticalHealth()
        {
            float tickInterval = Mathf.Clamp(m_CriticalBurnTickInterval, 0.1f, 0.5f);
            WaitForSeconds wait = new WaitForSeconds(tickInterval);

            while (isActiveAndEnabled && m_Health != null)
            {
                yield return wait;

                if (!isActiveAndEnabled || m_Health == null ||
                    (m_Destruction != null && m_Destruction.IsDestroyed))
                {
                    break;
                }

                float maximum = m_Health.MaximumHealth;
                if (maximum <= 0.001f) break;

                float ratio = m_Health.CurrentHealth / maximum;
                if (ratio <= 0.001f || ratio > m_CriticalFireThreshold) break;

                // Drain a fixed percentage of maximum health. Entering the fire
                // threshold at 14% therefore reaches zero in about 7 seconds,
                // independent of the Car's configured maximum health.
                float damagePerSecond = maximum * m_CriticalFireThreshold /
                    Mathf.Max(1f, m_CriticalBurnDuration);
                m_Health.ApplyDamage(damagePerSecond * tickInterval);
            }

            m_CriticalHealthDrain = null;
        }

        private void StopCriticalHealthDrain()
        {
            if (m_CriticalHealthDrain == null) return;
            StopCoroutine(m_CriticalHealthDrain);
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
            TriggerExplosion(true);
        }

        private void CancelPendingExplosion()
        {
            if (m_PendingExplosion == null) return;
            StopCoroutine(m_PendingExplosion);
            m_PendingExplosion = null;
        }

        /// <summary>
        /// Immediately enters the terminal Car explosion pipeline. External
        /// explosions can skip the Car's local burst to avoid duplicate VFX/audio.
        /// </summary>
        public void TriggerImmediateExplosion(bool playOneShotEffects = true)
        {
            CancelPendingExplosion();
            StopCriticalHealthDrain();
            TriggerExplosion(true, playOneShotEffects);
        }

        private void TriggerExplosion(
            bool applyDestruction,
            bool playOneShotEffects = true)
        {
            if (m_HasExploded) return;
            m_HasExploded = true;
            m_DestroyedFireExpired = false;
            SetLoopEffectActive(m_WeakHealthSmoke, false);
            SetLoopEffectActive(m_CriticalWarningFire, false);
            SetLoopEffectActive(m_DestroyedFire, true);
            StartDestroyedFireLifetime();
            RefreshWindActivity();
            if (playOneShotEffects && m_ExplosionAudioSource != null &&
                m_ExplosionClip != null)
            {
                m_ExplosionAudioSource.Stop();
                m_ExplosionAudioSource.PlayOneShot(m_ExplosionClip, 1f);
            }

            if (playOneShotEffects)
            {
                for (int i = 0; i < m_ExplosionParticles.Length; ++i)
                {
                    ParticleSystem particles = m_ExplosionParticles[i];
                    if (particles == null) continue;
                    particles.Clear(true);
                    particles.Play(true);
                }
            }

            if (applyDestruction)
            {
                m_Destruction?.TriggerDestruction(
                    CalculateExplosionCameraHoldDuration()
                );
            }
        }

        private void StartDestroyedFireLifetime()
        {
            StopDestroyedFireLifetime();
            if (!isActiveAndEnabled || m_DestroyedFire == null) return;
            m_DestroyedFireLifetimeRoutine = StartCoroutine(
                ExpireDestroyedFireAfterLifetime()
            );
        }

        private IEnumerator ExpireDestroyedFireAfterLifetime()
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Max(5f, m_DestroyedFireLifetime)
            );
            m_DestroyedFireLifetimeRoutine = null;
            m_DestroyedFireExpired = true;
            SetLoopEffectActive(m_DestroyedFire, false);
            RefreshWindActivity();
        }

        private void StopDestroyedFireLifetime()
        {
            if (m_DestroyedFireLifetimeRoutine == null) return;
            StopCoroutine(m_DestroyedFireLifetimeRoutine);
            m_DestroyedFireLifetimeRoutine = null;
        }

        private float CalculateExplosionCameraHoldDuration()
        {
            float duration = 0f;
            for (int i = 0; i < m_ExplosionParticles.Length; ++i)
            {
                ParticleSystem particles = m_ExplosionParticles[i];
                if (particles == null) continue;
                ParticleSystem.MainModule main = particles.main;
                duration = Mathf.Max(duration, main.duration);
            }
            // The 7-second recording contains a long acoustic tail. Camera hold
            // follows the visual burst (Explosion11 is 1.5s), using audio length
            // only as a fallback when no particle system is assigned.
            if (duration <= 0f && m_ExplosionClip != null)
                duration = m_ExplosionClip.length;
            return Mathf.Clamp(
                duration,
                m_MinimumExplosionCameraHold,
                Mathf.Max(
                    m_MinimumExplosionCameraHold,
                    m_MaximumExplosionCameraHold
                )
            );
        }

        private void RefreshWindActivity()
        {
            bool active = HasActiveLoopEffects();
            if (active)
            {
                RefreshLoopEffectsCullState();
                EnsureLoopEffectsLod();
            }
            else
            {
                StopLoopEffectsLod();
                m_LoopEffectsCulled = false;
            }

            ApplyLoopParticleCulling();
            m_ParticleWind?.SetWindActive(active && !m_LoopEffectsCulled);
            RefreshLoopAudio();
        }

        private void RefreshLoopAudio()
        {
            bool smokeActive = m_WeakHealthSmoke != null &&
                m_WeakHealthSmoke.activeSelf;
            bool criticalFireActive = m_CriticalWarningFire != null &&
                m_CriticalWarningFire.activeSelf;
            bool destroyedFireActive = m_DestroyedFire != null &&
                m_DestroyedFire.activeSelf;

            SetLoopAudioState(
                m_SmokeAudioSource,
                m_SmokeLoopClip,
                smokeActive && !m_LoopEffectsCulled,
                m_SmokeLoopVolume
            );
            SetLoopAudioState(
                m_FireAudioSource,
                m_FireLoopClip,
                (criticalFireActive || destroyedFireActive) &&
                !m_LoopEffectsCulled,
                destroyedFireActive
                    ? m_DestroyedFireLoopVolume
                    : m_CriticalFireLoopVolume
            );
        }

        private bool HasActiveLoopEffects()
        {
            return (m_WeakHealthSmoke != null && m_WeakHealthSmoke.activeSelf) ||
                   (m_CriticalWarningFire != null &&
                    m_CriticalWarningFire.activeSelf) ||
                   (m_DestroyedFire != null && m_DestroyedFire.activeSelf);
        }

        private void EnsureLoopEffectsLod()
        {
            if (m_LoopEffectsLodRoutine != null || !isActiveAndEnabled) return;
            m_LoopEffectsLodRoutine = StartCoroutine(UpdateLoopEffectsLod());
        }

        private void StopLoopEffectsLod()
        {
            if (m_LoopEffectsLodRoutine == null) return;
            StopCoroutine(m_LoopEffectsLodRoutine);
            m_LoopEffectsLodRoutine = null;
        }

        private IEnumerator UpdateLoopEffectsLod()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
                Mathf.Clamp(m_LoopEffectsLodInterval, 0.5f, 2f)
            );
            while (isActiveAndEnabled && HasActiveLoopEffects())
            {
                yield return wait;
                if (RefreshLoopEffectsCullState())
                {
                    ApplyLoopParticleCulling();
                    m_ParticleWind?.SetWindActive(!m_LoopEffectsCulled);
                    RefreshLoopAudio();
                }
            }
            m_LoopEffectsLodRoutine = null;
        }

        private bool RefreshLoopEffectsCullState()
        {
            if (s_LoopEffectsCamera == null ||
                !s_LoopEffectsCamera.isActiveAndEnabled)
            {
                s_LoopEffectsCamera = Camera.main;
            }

            bool culled = false;
            if (s_LoopEffectsCamera != null)
            {
                float maximumDistance = Mathf.Max(10f, m_LoopEffectsCullDistance);
                culled = (s_LoopEffectsCamera.transform.position - transform.position)
                    .sqrMagnitude > maximumDistance * maximumDistance;
            }

            if (m_LoopEffectsCulled == culled) return false;
            m_LoopEffectsCulled = culled;
            return true;
        }

        private void ApplyLoopParticleCulling()
        {
            SetParticleArrayPaused(
                m_WeakSmokeParticles,
                m_WeakHealthSmoke != null && m_WeakHealthSmoke.activeSelf &&
                m_LoopEffectsCulled
            );
            SetParticleArrayPaused(
                m_CriticalFireParticles,
                m_CriticalWarningFire != null &&
                m_CriticalWarningFire.activeSelf && m_LoopEffectsCulled
            );
            SetParticleArrayPaused(
                m_DestroyedFireParticles,
                m_DestroyedFire != null && m_DestroyedFire.activeSelf &&
                m_LoopEffectsCulled
            );
        }

        private static void SetParticleArrayPaused(
            ParticleSystem[] particles,
            bool paused)
        {
            if (particles == null) return;
            for (int i = 0; i < particles.Length; ++i)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                if (paused)
                {
                    if (particle.isPlaying) particle.Pause(true);
                }
                else if (particle.isPaused)
                {
                    particle.Play(true);
                }
            }
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

        private void CacheLoopParticles()
        {
            m_WeakSmokeParticles = GetParticles(m_WeakHealthSmoke);
            m_CriticalFireParticles = GetParticles(m_CriticalWarningFire);
            m_DestroyedFireParticles = GetParticles(m_DestroyedFire);
        }

        private static ParticleSystem[] GetParticles(GameObject effect)
        {
            return effect != null
                ? effect.GetComponentsInChildren<ParticleSystem>(true)
                : System.Array.Empty<ParticleSystem>();
        }

        private ParticleSystem[] GetCachedLoopParticles(GameObject effect)
        {
            if (effect == m_WeakHealthSmoke) return m_WeakSmokeParticles;
            if (effect == m_CriticalWarningFire) return m_CriticalFireParticles;
            if (effect == m_DestroyedFire) return m_DestroyedFireParticles;
            return System.Array.Empty<ParticleSystem>();
        }

        private void SetLoopEffectActive(GameObject effect, bool active)
        {
            if (effect == null || effect.activeSelf == active) return;
            ParticleSystem[] particles = GetCachedLoopParticles(effect);
            if (!active)
            {
                for (int i = 0; i < particles.Length; ++i)
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            effect.SetActive(active);
            if (!active) return;

            for (int i = 0; i < particles.Length; ++i)
                particles[i].Play(true);
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
            m_CriticalBurnDuration = Mathf.Max(1f, m_CriticalBurnDuration);
            m_CriticalBurnTickInterval = Mathf.Clamp(
                m_CriticalBurnTickInterval,
                0.1f,
                0.5f
            );
            m_SmokeLoopVolume = Mathf.Clamp01(m_SmokeLoopVolume);
            m_CriticalFireLoopVolume = Mathf.Clamp01(m_CriticalFireLoopVolume);
            m_DestroyedFireLoopVolume = Mathf.Clamp01(m_DestroyedFireLoopVolume);
            m_LoopEffectsCullDistance = Mathf.Max(10f, m_LoopEffectsCullDistance);
            m_LoopEffectsLodInterval = Mathf.Clamp(
                m_LoopEffectsLodInterval,
                0.5f,
                2f
            );
            m_DestroyedFireLifetime = Mathf.Max(5f, m_DestroyedFireLifetime);
            m_MinimumExplosionCameraHold = Mathf.Max(
                0f,
                m_MinimumExplosionCameraHold
            );
            m_MaximumExplosionCameraHold = Mathf.Max(
                m_MinimumExplosionCameraHold,
                m_MaximumExplosionCameraHold
            );
        }
    }
}
