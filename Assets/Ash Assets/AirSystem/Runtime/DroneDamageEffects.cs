using System.Collections;
using UnityEngine;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// One-shot, prewarmed explosion presentation based on the Car/Bike effect pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DroneHealth), typeof(DroneFlightController))]
    public sealed class DroneDamageEffects : MonoBehaviour
    {
        [SerializeField] private DroneHealth m_Health;
        [SerializeField] private DroneFlightController m_FlightController;
        [SerializeField] private Rigidbody m_Body;
        [SerializeField] private Collider m_Collider;

        [Header("One-shot Explosion")]
        [SerializeField] private GameObject m_ExplosionPrefab;
        [SerializeField] private AudioSource m_ExplosionAudioSource;
        [SerializeField] private AudioClip m_ExplosionClip;
        [SerializeField, Range(0.05f, 1f)] private float m_ExplosionScale = 0.22f;
        [SerializeField, Range(4, 32)] private int m_MaxParticlesPerSystem = 16;
        [SerializeField, Min(0.5f)] private float m_EffectDuration = 4f;

        private Renderer[] m_DroneRenderers = System.Array.Empty<Renderer>();
        private ParticleSystem[] m_ExplosionParticles =
            System.Array.Empty<ParticleSystem>();
        private GameObject m_ExplosionInstance;
        private Coroutine m_EffectTimeout;
        private bool m_HasExploded;

        public bool HasExploded => this.m_HasExploded;

        private void Awake()
        {
            this.ResolveReferences();
            this.m_DroneRenderers = this.GetComponentsInChildren<Renderer>(true);
            this.PrewarmExplosion();
        }

        private void OnEnable()
        {
            this.ResolveReferences();
            if (this.m_Health != null)
            {
                this.m_Health.EventDestroyed += this.OnDestroyed;
            }
        }

        private void Start()
        {
            if (this.m_Health != null && this.m_Health.IsDestroyed)
            {
                this.OnDestroyed();
            }
        }

        private void OnDisable()
        {
            if (this.m_Health != null)
            {
                this.m_Health.EventDestroyed -= this.OnDestroyed;
            }
            if (this.m_EffectTimeout != null)
            {
                this.StopCoroutine(this.m_EffectTimeout);
                this.m_EffectTimeout = null;
            }
            if (this.m_ExplosionInstance != null)
            {
                this.m_ExplosionInstance.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (this.m_Health == null) this.m_Health = this.GetComponent<DroneHealth>();
            if (this.m_FlightController == null)
            {
                this.m_FlightController = this.GetComponent<DroneFlightController>();
            }
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
            if (this.m_Collider == null) this.m_Collider = this.GetComponent<Collider>();
        }

        private void PrewarmExplosion()
        {
            if (this.m_ExplosionPrefab == null || this.m_ExplosionInstance != null) return;

            // Some legacy particle prefabs use mainObjectFileID 100100000 even
            // though their runtime root is a GameObject/Component. Instantiating
            // through UnityEngine.Object avoids the generic cast exception and
            // still accepts a component-root clone defensively.
            UnityEngine.Object explosionClone = UnityEngine.Object.Instantiate(
                (UnityEngine.Object)this.m_ExplosionPrefab,
                this.transform,
                false
            );
            this.m_ExplosionInstance = explosionClone as GameObject;
            if (this.m_ExplosionInstance == null && explosionClone is Component component)
            {
                this.m_ExplosionInstance = component.gameObject;
            }
            if (this.m_ExplosionInstance == null)
            {
                Debug.LogError(
                    "Drone explosion reference did not instantiate a GameObject.",
                    this
                );
                if (explosionClone != null) Destroy(explosionClone);
                return;
            }
            this.m_ExplosionInstance.name = "Drone Explosion (Pooled)";
            Transform effectTransform = this.m_ExplosionInstance.transform;
            effectTransform.localPosition = Vector3.zero;
            effectTransform.localRotation = Quaternion.identity;
            effectTransform.localScale = Vector3.one * this.m_ExplosionScale;

            this.m_ExplosionParticles = this.m_ExplosionInstance
                .GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < this.m_ExplosionParticles.Length; ++i)
            {
                ParticleSystem particles = this.m_ExplosionParticles[i];
                if (particles == null) continue;
                ParticleSystem.MainModule main = particles.main;
                main.maxParticles = Mathf.Min(
                    main.maxParticles,
                    this.m_MaxParticlesPerSystem
                );
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            this.m_ExplosionInstance.SetActive(false);
        }

        private void OnDestroyed()
        {
            if (this.m_HasExploded) return;
            this.m_HasExploded = true;
            this.m_Health?.SetTerminallyDestroyed();

            if (this.m_FlightController != null)
            {
                this.m_FlightController.enabled = false;
            }
            if (this.m_Collider != null) this.m_Collider.enabled = false;
            if (this.m_Body != null)
            {
                if (!this.m_Body.isKinematic)
                {
                    this.m_Body.linearVelocity = Vector3.zero;
                    this.m_Body.angularVelocity = Vector3.zero;
                }
                this.m_Body.isKinematic = true;
            }

            for (int i = 0; i < this.m_DroneRenderers.Length; ++i)
            {
                Renderer droneRenderer = this.m_DroneRenderers[i];
                if (droneRenderer != null) droneRenderer.enabled = false;
            }

            if (this.m_ExplosionAudioSource != null && this.m_ExplosionClip != null)
            {
                this.m_ExplosionAudioSource.Stop();
                this.m_ExplosionAudioSource.loop = false;
                this.m_ExplosionAudioSource.pitch = 1f;
                this.m_ExplosionAudioSource.volume = 1f;
                this.m_ExplosionAudioSource.PlayOneShot(this.m_ExplosionClip, 1f);
            }

            this.PlayExplosion();
        }

        private void PlayExplosion()
        {
            this.PrewarmExplosion();
            if (this.m_ExplosionInstance == null) return;

            this.m_ExplosionInstance.SetActive(true);
            for (int i = 0; i < this.m_ExplosionParticles.Length; ++i)
            {
                ParticleSystem particles = this.m_ExplosionParticles[i];
                if (particles == null) continue;
                particles.Clear(true);
                particles.Play(true);
            }

            if (this.m_EffectTimeout != null) this.StopCoroutine(this.m_EffectTimeout);
            this.m_EffectTimeout = this.StartCoroutine(this.DisableEffectAfterDelay());
        }

        private IEnumerator DisableEffectAfterDelay()
        {
            yield return new WaitForSecondsRealtime(this.m_EffectDuration);
            this.m_EffectTimeout = null;
            if (this.m_ExplosionInstance != null)
            {
                this.m_ExplosionInstance.SetActive(false);
            }
        }

        private void OnValidate()
        {
            this.m_ExplosionScale = Mathf.Clamp(this.m_ExplosionScale, 0.05f, 1f);
            this.m_MaxParticlesPerSystem = Mathf.Clamp(
                this.m_MaxParticlesPerSystem,
                4,
                32
            );
            this.m_EffectDuration = Mathf.Max(0.5f, this.m_EffectDuration);
        }
    }
}
