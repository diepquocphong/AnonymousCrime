using System;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-only, allocation-free impact audio for the exact Sim-Cade Car.
    /// Contact speed decides whether a collision is audible; impulse per vehicle
    /// mass distinguishes a real heavy crash from touching a lightweight prop.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SimcadeCarImpactAudio : MonoBehaviour
    {
        public event Action<bool, float> EventImpactAccepted;
        public event Action<Collision, bool, float> EventImpactContactAccepted;

        [Header("Audio")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip m_LightImpactClip;
        [SerializeField] private AudioClip m_HeavyImpactClip;

        [Header("Spark And Debris Effect")]
        [SerializeField] private GameObject m_ImpactEffectPrefab;
        [Range(2, 4)] [SerializeField] private int m_EffectPoolSize = 3;
        [Range(1, 12)] [SerializeField] private int m_LightSparkCount = 5;
        [Range(4, 24)] [SerializeField] private int m_HeavySparkCount = 14;
        [Range(1, 8)] [SerializeField] private int m_HeavyFlashCount = 4;

        [Header("Impact Classification")]
        [Tooltip("Minimum normal contact speed before a light impact is audible.")]
        [Min(0f)] [SerializeField] private float m_MinContactSpeed = 1.1f;
        [Tooltip("Impulse divided by car mass at which the heavy crash clip is used.")]
        [Min(0.1f)] [SerializeField] private float m_HeavyImpactSpeed = 6f;
        [Tooltip("Severity used to reach the maximum heavy-impact volume.")]
        [Min(0.2f)] [SerializeField] private float m_MaxImpactSpeed = 16f;
        [Min(0f)] [SerializeField] private float m_ImpactCooldown = 0.16f;

        [Header("Light Impact")]
        [Range(0f, 1f)] [SerializeField] private float m_LightMinVolume = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float m_LightMaxVolume = 0.9f;
        [Range(0.1f, 3f)] [SerializeField] private float m_LightPitch = 1.02f;

        [Header("Heavy Impact")]
        [Range(0f, 1f)] [SerializeField] private float m_HeavyMinVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float m_HeavyMaxVolume = 1f;
        [Range(0.1f, 3f)] [SerializeField] private float m_HeavyPitch = 0.9f;

        private Rigidbody m_Body;
        private float m_NextImpactTime;
        private float m_LastImpactSeverity;
        private Transform[] m_EffectRoots;
        private ParticleSystem[][] m_EffectSystems;
        private int m_NextEffectIndex;

        public bool IsConfigured =>
            m_AudioSource != null &&
            m_LightImpactClip != null &&
            m_HeavyImpactClip != null &&
            m_ImpactEffectPrefab != null;

        public void Configure(
            AudioSource audioSource,
            AudioClip lightImpactClip,
            AudioClip heavyImpactClip,
            GameObject impactEffectPrefab)
        {
            m_AudioSource = audioSource;
            m_LightImpactClip = lightImpactClip;
            m_HeavyImpactClip = heavyImpactClip;
            m_ImpactEffectPrefab = impactEffectPrefab;
            m_LightMinVolume = 0.45f;
            m_LightMaxVolume = 0.9f;
            m_HeavyMinVolume = 0.9f;
            m_HeavyMaxVolume = 1f;
        }

        protected void ConfigureImpactProfile(
            float minContactSpeed,
            float heavyImpactSpeed,
            float maxImpactSpeed,
            float impactCooldown,
            float lightMinVolume,
            float lightMaxVolume,
            float lightPitch,
            float heavyMinVolume,
            float heavyMaxVolume,
            float heavyPitch,
            int lightSparkCount,
            int heavySparkCount,
            int heavyFlashCount)
        {
            m_MinContactSpeed = minContactSpeed;
            m_HeavyImpactSpeed = heavyImpactSpeed;
            m_MaxImpactSpeed = maxImpactSpeed;
            m_ImpactCooldown = impactCooldown;
            m_LightMinVolume = lightMinVolume;
            m_LightMaxVolume = lightMaxVolume;
            m_LightPitch = lightPitch;
            m_HeavyMinVolume = heavyMinVolume;
            m_HeavyMaxVolume = heavyMaxVolume;
            m_HeavyPitch = heavyPitch;
            m_LightSparkCount = lightSparkCount;
            m_HeavySparkCount = heavySparkCount;
            m_HeavyFlashCount = heavyFlashCount;
        }

        protected virtual void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            BuildEffectPool();
        }

        protected virtual void OnDisable()
        {
            if (m_AudioSource != null) m_AudioSource.Stop();
            StopEffectPool();
            m_NextImpactTime = 0f;
            m_LastImpactSeverity = 0f;
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (!IsConfigured || collision == null || IsIgnoredImpact(collision)) return;

            float contactSpeed = GetNormalContactSpeed(collision);
            float impulseSpeed = collision.impulse.magnitude /
                Mathf.Max(1f, m_Body != null ? m_Body.mass : 1f);
            if (contactSpeed < m_MinContactSpeed && impulseSpeed < m_MinContactSpeed) return;

            // A fast hit on a very light prop remains a light bump. Static or
            // massive obstacles create a large impulse and select the crash clip.
            float severity = Mathf.Max(impulseSpeed, contactSpeed * 0.35f);
            bool isHeavy = impulseSpeed >= m_HeavyImpactSpeed;
            float now = Time.unscaledTime;

            // Continuous multi-collider contact must not create an audio burst.
            // A distinctly stronger follow-up impact may replace the previous one.
            if (now < m_NextImpactTime && severity < m_LastImpactSeverity * 1.35f) return;

            if (collision.contactCount > 0)
            {
                m_AudioSource.transform.position = collision.GetContact(0).point;
            }

            AudioClip clip;
            float volume;
            float pitch;
            if (isHeavy)
            {
                float amount = Mathf.InverseLerp(
                    m_HeavyImpactSpeed,
                    Mathf.Max(m_HeavyImpactSpeed + 0.1f, m_MaxImpactSpeed),
                    severity
                );
                clip = m_HeavyImpactClip;
                volume = Mathf.Lerp(m_HeavyMinVolume, m_HeavyMaxVolume, amount);
                pitch = Mathf.Lerp(m_HeavyPitch + 0.05f, m_HeavyPitch - 0.06f, amount);
            }
            else
            {
                float amount = Mathf.InverseLerp(
                    m_MinContactSpeed,
                    m_HeavyImpactSpeed,
                    severity
                );
                clip = m_LightImpactClip;
                volume = Mathf.Lerp(m_LightMinVolume, m_LightMaxVolume, amount);
                pitch = Mathf.Lerp(m_LightPitch + 0.04f, m_LightPitch - 0.06f, amount);
            }

            // One dedicated voice is enough for a car body. Replacing its current
            // clip prevents stacked collision voices and is cheaper on mobile.
            m_AudioSource.Stop();
            m_AudioSource.clip = clip;
            m_AudioSource.volume = volume;
            m_AudioSource.pitch = pitch;
            m_AudioSource.Play();
            PlayImpactEffect(collision, isHeavy, severity);
            EventImpactAccepted?.Invoke(isHeavy, severity);
            EventImpactContactAccepted?.Invoke(collision, isHeavy, severity);
            OnImpactAccepted(collision, isHeavy, severity);

            m_LastImpactSeverity = severity;
            m_NextImpactTime = now + m_ImpactCooldown;
        }

        /// <summary>
        /// Shared by Car and Bike so lightweight props such as ejected shell
        /// casings never reach audio, VFX, health, deformation or ragdoll events.
        /// Their own colliders and collision audio continue to work normally.
        /// </summary>
        protected static bool IsIgnoredImpact(Collision collision)
        {
            Collider otherCollider = collision != null ? collision.collider : null;
            return otherCollider != null &&
                otherCollider.GetComponentInParent<VehicleImpactIgnored>() != null;
        }

        /// <summary>
        /// Extension point invoked once for an accepted, cooldown-filtered impact.
        /// Subclasses can react to the same classification without running a second
        /// collision calculation or producing duplicate effects.
        /// </summary>
        protected virtual void OnImpactAccepted(
            Collision collision,
            bool isHeavy,
            float severity)
        { }

        private static float GetNormalContactSpeed(Collision collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (collision.contactCount <= 0) return relativeSpeed;

            Vector3 normal = collision.GetContact(0).normal;
            return Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
        }

        private void BuildEffectPool()
        {
            if (m_ImpactEffectPrefab == null || m_EffectRoots != null) return;

            int poolSize = Mathf.Clamp(m_EffectPoolSize, 2, 4);
            m_EffectRoots = new Transform[poolSize];
            m_EffectSystems = new ParticleSystem[poolSize][];

            for (int index = 0; index < poolSize; index++)
            {
                GameObject instance = Instantiate(m_ImpactEffectPrefab, transform);
                instance.name = $"Impact FX Pool {index + 1}";
                Transform effectTransform = instance.transform;
                effectTransform.localPosition = Vector3.zero;
                effectTransform.localRotation = Quaternion.identity;

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    ParticleSystem system = systems[systemIndex];
                    ParticleSystem.MainModule main = system.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = false;
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                m_EffectRoots[index] = effectTransform;
                m_EffectSystems[index] = systems;
            }
        }

        private void StopEffectPool()
        {
            if (m_EffectSystems == null) return;

            for (int index = 0; index < m_EffectSystems.Length; index++)
            {
                ParticleSystem[] systems = m_EffectSystems[index];
                if (systems == null) continue;
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    if (systems[systemIndex] != null)
                    {
                        systems[systemIndex].Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmittingAndClear
                        );
                    }
                }
            }
        }

        private void PlayImpactEffect(Collision collision, bool isHeavy, float severity)
        {
            if (m_EffectRoots == null || m_EffectRoots.Length == 0) return;

            int index = m_NextEffectIndex;
            m_NextEffectIndex = (m_NextEffectIndex + 1) % m_EffectRoots.Length;
            Transform effectRoot = m_EffectRoots[index];
            ParticleSystem[] systems = m_EffectSystems[index];
            if (effectRoot == null || systems == null || systems.Length == 0) return;

            Vector3 position = transform.position;
            Vector3 normal = transform.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                position = contact.point + contact.normal * 0.025f;
                normal = contact.normal;
            }

            Vector3 orientationUp = Mathf.Abs(Vector3.Dot(normal, transform.up)) > 0.98f
                ? transform.forward
                : transform.up;
            effectRoot.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(normal, orientationUp)
            );
            float scale = isHeavy
                ? Mathf.Lerp(1f, 1.25f, Mathf.InverseLerp(m_HeavyImpactSpeed, m_MaxImpactSpeed, severity))
                : 0.72f;
            effectRoot.localScale = Vector3.one * scale;

            for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
            {
                ParticleSystem system = systems[systemIndex];
                if (system == null) continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                bool isSparkTrail = system.gameObject.name == "Sparks";
                int count = isSparkTrail
                    ? (isHeavy ? m_HeavySparkCount : m_LightSparkCount)
                    : (isHeavy ? m_HeavyFlashCount : 1);
                system.Emit(count);
            }
        }

        protected virtual void OnValidate()
        {
            m_MinContactSpeed = Mathf.Max(0f, m_MinContactSpeed);
            m_HeavyImpactSpeed = Mathf.Max(m_MinContactSpeed + 0.1f, m_HeavyImpactSpeed);
            m_MaxImpactSpeed = Mathf.Max(m_HeavyImpactSpeed + 0.1f, m_MaxImpactSpeed);
            m_ImpactCooldown = Mathf.Max(0f, m_ImpactCooldown);
            m_EffectPoolSize = Mathf.Clamp(m_EffectPoolSize, 2, 4);
            m_LightMaxVolume = Mathf.Max(m_LightMinVolume, m_LightMaxVolume);
            m_HeavyMaxVolume = Mathf.Max(m_HeavyMinVolume, m_HeavyMaxVolume);
        }
    }
}
