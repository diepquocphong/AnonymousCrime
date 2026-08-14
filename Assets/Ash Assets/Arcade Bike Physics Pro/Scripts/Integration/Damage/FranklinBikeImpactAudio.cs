using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Bike-tuned light/heavy collision audio with the same pooled spark, flash and debris
    /// effect used by the Sim-Cade car.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FranklinBikeImpactAudio : SimcadeCarImpactAudio
    {
        [Header("Mobile Metal Debris")]
        [SerializeField] private bool m_EnableMetalDebris = true;
        [SerializeField] private Material m_MetalDebrisMaterial;
        [Tooltip("Minimum bike/collision speed that guarantees a debris burst, in km/h.")]
        [Min(1f)] [SerializeField] private float m_MinDebrisImpactSpeedKph = 50f;
        [Range(4, 10)] [SerializeField] private int m_MinMetalDebris = 7;
        [Range(6, 16)] [SerializeField] private int m_MaxMetalDebris = 12;
        [Min(0.1f)] [SerializeField] private float m_DebrisFullBurstSeverity = 12f;
        [SerializeField] private Vector2 m_DebrisLifetime = new Vector2(0.65f, 1.05f);
        [SerializeField] private Vector2 m_DebrisSpeed = new Vector2(2.8f, 6.2f);
        [SerializeField] private Vector2 m_DebrisSize = new Vector2(0.22f, 0.4f);
        [Range(0f, 3f)] [SerializeField] private float m_DebrisGravity = 1.35f;
        [Min(0f)] [SerializeField] private float m_DebrisCooldown = 0.16f;
        [SerializeField, HideInInspector] private int m_DebrisConfigurationVersion;

        private const float MetersPerSecondToKph = 3.6f;
        private const int CurrentDebrisConfigurationVersion = 1;
        private const int DebrisParticleCap = 20;
        private ParticleSystem m_MetalDebrisParticles;
        private Rigidbody m_DebrisBody;
        private float m_CurrentCollisionSpeedKph;
        private float m_NextDebrisTime;
        private bool m_EmittedDebrisForCurrentCollision;

        /// <summary>
        /// Raised only for a cooldown-filtered impact classified as heavy by the
        /// bike profile. Severity is impulse per bike mass/contact speed.
        /// </summary>
        public event Action<Collision, float> EventHeavyImpact;

        public bool HasMetalDebrisConfiguration =>
            m_EnableMetalDebris && m_MetalDebrisMaterial != null;
        public bool HasCurrentMetalDebrisConfiguration =>
            m_DebrisConfigurationVersion >= CurrentDebrisConfigurationVersion;

        public void ConfigureMetalDebris(Material material)
        {
            m_MetalDebrisMaterial = material;
            m_EnableMetalDebris = material != null;
            m_MinDebrisImpactSpeedKph = 50f;
            m_MinMetalDebris = 7;
            m_MaxMetalDebris = 12;
            m_DebrisFullBurstSeverity = 12f;
            m_DebrisLifetime = new Vector2(0.65f, 1.05f);
            m_DebrisSpeed = new Vector2(2.8f, 6.2f);
            m_DebrisSize = new Vector2(0.22f, 0.4f);
            m_DebrisGravity = 1.35f;
            m_DebrisCooldown = 0.16f;
            m_DebrisConfigurationVersion = CurrentDebrisConfigurationVersion;
            if (!Application.isPlaying) return;

            if (m_MetalDebrisParticles == null)
            {
                BuildMetalDebrisSystem();
            }
            else
            {
                ParticleSystemRenderer particleRenderer =
                    m_MetalDebrisParticles.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null)
                {
                    particleRenderer.sharedMaterial = m_MetalDebrisMaterial;
                }
            }
        }

        public void ConfigureForBike(
            AudioSource audioSource,
            AudioClip lightImpactClip,
            AudioClip heavyImpactClip,
            GameObject impactEffectPrefab)
        {
            this.Configure(
                audioSource,
                lightImpactClip,
                heavyImpactClip,
                impactEffectPrefab
            );

            // The bike body is lighter and more exposed than a car shell. Use lower
            // normalized thresholds, a slightly brighter light hit and retain a deep
            // heavy hit without allowing multi-collider audio bursts.
            this.ConfigureImpactProfile(
                minContactSpeed: 0.85f,
                heavyImpactSpeed: 4.5f,
                maxImpactSpeed: 12f,
                impactCooldown: 0.14f,
                lightMinVolume: 0.35f,
                lightMaxVolume: 0.82f,
                lightPitch: 1.1f,
                heavyMinVolume: 0.82f,
                heavyMaxVolume: 1f,
                heavyPitch: 0.92f,
                lightSparkCount: 4,
                heavySparkCount: 12,
                heavyFlashCount: 3
            );
        }

        protected override void Awake()
        {
            base.Awake();
            m_DebrisBody = GetComponent<Rigidbody>();
            BuildMetalDebrisSystem();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_NextDebrisTime = 0f;
            if (m_MetalDebrisParticles != null)
            {
                m_MetalDebrisParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        protected override void OnCollisionEnter(Collision collision)
        {
            m_CurrentCollisionSpeedKph = GetDebrisImpactSpeedKph(collision);
            m_EmittedDebrisForCurrentCollision = false;

            base.OnCollisionEnter(collision);

            // The base class may reject a shallow/high-speed contact because its
            // normal impulse is small. A real 50 km/h contact must still shed metal.
            if (!m_EmittedDebrisForCurrentCollision &&
                m_CurrentCollisionSpeedKph >= m_MinDebrisImpactSpeedKph)
            {
                TryEmitMetalDebris(
                    collision,
                    m_CurrentCollisionSpeedKph / MetersPerSecondToKph
                );
            }
        }

        protected override void OnImpactAccepted(
            Collision collision,
            bool isHeavy,
            float severity)
        {
            base.OnImpactAccepted(collision, isHeavy, severity);

            if (isHeavy || m_CurrentCollisionSpeedKph >= m_MinDebrisImpactSpeedKph)
            {
                m_EmittedDebrisForCurrentCollision = TryEmitMetalDebris(
                    collision,
                    Mathf.Max(
                        severity,
                        m_CurrentCollisionSpeedKph / MetersPerSecondToKph
                    )
                );
            }

            if (!isHeavy) return;
            this.EventHeavyImpact?.Invoke(collision, severity);
        }

        private void BuildMetalDebrisSystem()
        {
            if (!m_EnableMetalDebris || m_MetalDebrisMaterial == null ||
                m_MetalDebrisParticles != null)
            {
                return;
            }

            GameObject debrisObject = new GameObject("Metal Debris Pool");
            debrisObject.layer = gameObject.layer;
            debrisObject.transform.SetParent(transform, false);
            m_MetalDebrisParticles = debrisObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = m_MetalDebrisParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = DebrisParticleCap;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                m_DebrisLifetime.x,
                m_DebrisLifetime.y
            );
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                m_DebrisSize.x,
                m_DebrisSize.y
            );
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = m_DebrisGravity;

            ParticleSystem.EmissionModule emission = m_MetalDebrisParticles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = m_MetalDebrisParticles.shape;
            shape.enabled = false;

            // Individual shard colliders and sub-emitters are deliberately disabled:
            // short world-space lifetimes create the impact read while keeping mobile
            // physics and draw-call cost fixed.
            ParticleSystem.CollisionModule collision = m_MetalDebrisParticles.collision;
            collision.enabled = false;
            ParticleSystem.TrailModule trails = m_MetalDebrisParticles.trails;
            trails.enabled = false;
            ParticleSystem.NoiseModule noise = m_MetalDebrisParticles.noise;
            noise.enabled = false;

            ParticleSystem.RotationOverLifetimeModule rotation =
                m_MetalDebrisParticles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(-11f, 11f);

            ParticleSystem.TextureSheetAnimationModule sheet =
                m_MetalDebrisParticles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            ParticleSystemRenderer particleRenderer =
                debrisObject.GetComponent<ParticleSystemRenderer>();
            // Billboard guarantees the generated shard atlas remains readable from
            // every camera angle and is cheaper than individual 3D shard meshes.
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sharedMaterial = m_MetalDebrisMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            m_MetalDebrisParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        private bool TryEmitMetalDebris(Collision collision, float severity)
        {
            if (Time.unscaledTime < m_NextDebrisTime) return false;
            if (!EmitMetalDebris(collision, severity)) return false;

            m_NextDebrisTime = Time.unscaledTime + m_DebrisCooldown;
            return true;
        }

        private bool EmitMetalDebris(Collision collision, float severity)
        {
            if (!m_EnableMetalDebris || m_MetalDebrisParticles == null || collision == null)
            {
                return false;
            }

            Vector3 position = transform.position;
            Vector3 normal = transform.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                position = contact.point + contact.normal * 0.035f;
                normal = contact.normal.normalized;
            }

            float amount = Mathf.InverseLerp(4.5f, m_DebrisFullBurstSeverity, severity);
            int count = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(m_MinMetalDebris, m_MaxMetalDebris, amount)),
                1,
                DebrisParticleCap
            );
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(normal, transform.forward);
            }
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            Vector3 inheritedVelocity = m_DebrisBody != null
                ? m_DebrisBody.GetPointVelocity(position) * 0.18f
                : Vector3.zero;

            if (!m_MetalDebrisParticles.isPlaying)
                m_MetalDebrisParticles.Play(false);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int index = 0; index < count; index++)
            {
                float speed = UnityEngine.Random.Range(m_DebrisSpeed.x, m_DebrisSpeed.y) *
                    Mathf.Lerp(0.85f, 1.15f, amount);
                Vector3 direction = (
                    normal * UnityEngine.Random.Range(0.45f, 1f) +
                    tangent * UnityEngine.Random.Range(-0.78f, 0.78f) +
                    bitangent * UnityEngine.Random.Range(-0.62f, 0.62f) +
                    Vector3.up * UnityEngine.Random.Range(0.12f, 0.48f)
                ).normalized;

                emit.position = position + UnityEngine.Random.insideUnitSphere * 0.045f;
                emit.velocity = direction * speed + inheritedVelocity;
                emit.startLifetime = UnityEngine.Random.Range(
                    m_DebrisLifetime.x,
                    m_DebrisLifetime.y
                );
                emit.startSize = UnityEngine.Random.Range(m_DebrisSize.x, m_DebrisSize.y);
                emit.rotation3D = new Vector3(
                    0f,
                    0f,
                    UnityEngine.Random.Range(-Mathf.PI, Mathf.PI)
                );
                emit.startColor = Color.Lerp(
                    new Color(0.62f, 0.65f, 0.68f, 1f),
                    Color.white,
                    UnityEngine.Random.value
                );
                m_MetalDebrisParticles.Emit(emit, 1);
            }
            return true;
        }

        private float GetDebrisImpactSpeedKph(Collision collision)
        {
            float relativeSpeed = collision != null
                ? collision.relativeVelocity.magnitude
                : 0f;
            float bikeSpeed = m_DebrisBody != null
                ? m_DebrisBody.linearVelocity.magnitude
                : 0f;
            return Mathf.Max(relativeSpeed, bikeSpeed) * MetersPerSecondToKph;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_MinDebrisImpactSpeedKph = Mathf.Max(1f, m_MinDebrisImpactSpeedKph);
            m_MinMetalDebris = Mathf.Clamp(m_MinMetalDebris, 4, 10);
            m_MaxMetalDebris = Mathf.Clamp(m_MaxMetalDebris, m_MinMetalDebris, 16);
            m_DebrisFullBurstSeverity = Mathf.Max(4.6f, m_DebrisFullBurstSeverity);
            m_DebrisLifetime.x = Mathf.Max(0.1f, m_DebrisLifetime.x);
            m_DebrisLifetime.y = Mathf.Max(m_DebrisLifetime.x, m_DebrisLifetime.y);
            m_DebrisSpeed.x = Mathf.Max(0.1f, m_DebrisSpeed.x);
            m_DebrisSpeed.y = Mathf.Max(m_DebrisSpeed.x, m_DebrisSpeed.y);
            m_DebrisSize.x = Mathf.Max(0.01f, m_DebrisSize.x);
            m_DebrisSize.y = Mathf.Max(m_DebrisSize.x, m_DebrisSize.y);
            m_DebrisCooldown = Mathf.Max(0f, m_DebrisCooldown);
        }
    }
}
