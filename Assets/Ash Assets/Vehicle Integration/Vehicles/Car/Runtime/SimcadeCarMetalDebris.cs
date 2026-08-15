using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Pooled, billboard metal fragments for accepted Sim-Cade Car impacts.
    /// It reuses the impact classifier and never runs a second collision handler.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimcadeCarImpactAudio), typeof(Rigidbody))]
    public sealed class SimcadeCarMetalDebris : MonoBehaviour
    {
        [Header("Mobile Car Metal Debris")]
        [SerializeField] private SimcadeCarImpactAudio m_ImpactAudio;
        [SerializeField] private Rigidbody m_Body;
        [SerializeField] private Material m_Material;
        [Tooltip("Metal debris only emits when the vehicle/collision speed is above this value.")]
        [Min(1f)] [SerializeField] private float m_MinImpactSpeedKph = 60f;
        [Range(6, 14)] [SerializeField] private int m_MinFragmentCount = 9;
        [Range(8, 20)] [SerializeField] private int m_MaxFragmentCount = 16;
        [Min(0.1f)] [SerializeField] private float m_FullBurstSeverity = 16f;
        [SerializeField] private Vector2 m_Lifetime = new Vector2(0.75f, 1.2f);
        [SerializeField] private Vector2 m_Speed = new Vector2(3.2f, 7.2f);
        [Tooltip("Car fragments are intentionally larger than Bike fragments.")]
        [SerializeField] private Vector2 m_Size = new Vector2(0.21675f, 0.39525f);
        [Range(0f, 3f)] [SerializeField] private float m_Gravity = 1.45f;
        [SerializeField, HideInInspector] private int m_ConfigurationVersion;

        private const float MetersPerSecondToKph = 3.6f;
        private const int CurrentConfigurationVersion = 1;
        private const int ParticleCap = 24;

        private ParticleSystem m_Particles;

        public bool IsConfigured =>
            m_ImpactAudio != null && m_Body != null && m_Material != null;
        public bool HasCurrentConfiguration =>
            m_ConfigurationVersion >= CurrentConfigurationVersion;

        public void Configure(
            SimcadeCarImpactAudio impactAudio,
            Rigidbody body,
            Material material)
        {
            m_ImpactAudio = impactAudio;
            m_Body = body;
            m_Material = material;
            m_MinImpactSpeedKph = 60f;
            m_MinFragmentCount = 9;
            m_MaxFragmentCount = 16;
            m_FullBurstSeverity = 16f;
            m_Lifetime = new Vector2(0.75f, 1.2f);
            m_Speed = new Vector2(3.2f, 7.2f);
            m_Size = new Vector2(0.21675f, 0.39525f);
            m_Gravity = 1.45f;
            m_ConfigurationVersion = CurrentConfigurationVersion;

        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactContactAccepted += OnImpactAccepted;
        }

        private void OnDisable()
        {
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactContactAccepted -= OnImpactAccepted;
            if (m_Particles != null)
            {
                m_Particles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        private void ResolveReferences()
        {
            if (m_ImpactAudio == null) m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
        }

        private void OnImpactAccepted(Collision collision, bool isHeavy, float severity)
        {
            if (!IsConfigured || collision == null) return;

            float impactSpeedKph = GetImpactSpeedKph(collision);
            if (impactSpeedKph <= m_MinImpactSpeedKph) return;

            // Most parked Cars never create debris. Allocate this one bounded
            // particle system only after the first qualifying high-speed hit.
            if (m_Particles == null) BuildParticlePool();
            if (m_Particles == null) return;

            EmitFragments(
                collision,
                Mathf.Max(severity, impactSpeedKph / MetersPerSecondToKph)
            );
        }

        private void BuildParticlePool()
        {
            if (m_Particles != null || m_Material == null) return;

            GameObject poolObject = new GameObject("Car Metal Debris Pool");
            poolObject.layer = gameObject.layer;
            poolObject.transform.SetParent(transform, false);
            m_Particles = poolObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = m_Particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = ParticleCap;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(m_Lifetime.x, m_Lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(m_Size.x, m_Size.y);
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = m_Gravity;

            ParticleSystem.EmissionModule emission = m_Particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = m_Particles.shape;
            shape.enabled = false;
            ParticleSystem.CollisionModule particleCollision = m_Particles.collision;
            particleCollision.enabled = false;
            ParticleSystem.TrailModule trails = m_Particles.trails;
            trails.enabled = false;
            ParticleSystem.NoiseModule noise = m_Particles.noise;
            noise.enabled = false;

            ParticleSystem.RotationOverLifetimeModule rotation =
                m_Particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(-10f, 10f);

            ParticleSystem.TextureSheetAnimationModule sheet =
                m_Particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            ParticleSystemRenderer particleRenderer =
                poolObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sharedMaterial = m_Material;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            m_Particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        private void EmitFragments(Collision collision, float severity)
        {
            Vector3 position = transform.position;
            Vector3 normal = transform.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                position = contact.point + contact.normal * 0.045f;
                normal = contact.normal.normalized;
            }

            float amount = Mathf.InverseLerp(6f, m_FullBurstSeverity, severity);
            int count = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    m_MinFragmentCount,
                    m_MaxFragmentCount,
                    amount
                )),
                1,
                ParticleCap
            );
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
                tangent = Vector3.Cross(normal, transform.forward);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            Vector3 inheritedVelocity = m_Body.GetPointVelocity(position) * 0.14f;

            if (!m_Particles.isPlaying) m_Particles.Play(false);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int index = 0; index < count; ++index)
            {
                float speed = Random.Range(m_Speed.x, m_Speed.y) *
                              Mathf.Lerp(0.88f, 1.18f, amount);
                Vector3 direction = (
                    normal * Random.Range(0.42f, 1f) +
                    tangent * Random.Range(-0.82f, 0.82f) +
                    bitangent * Random.Range(-0.7f, 0.7f) +
                    Vector3.up * Random.Range(0.15f, 0.55f)
                ).normalized;

                emit.position = position + Random.insideUnitSphere * 0.065f;
                emit.velocity = direction * speed + inheritedVelocity;
                emit.startLifetime = Random.Range(m_Lifetime.x, m_Lifetime.y);
                emit.startSize = Random.Range(m_Size.x, m_Size.y);
                emit.rotation3D = new Vector3(
                    0f,
                    0f,
                    Random.Range(-Mathf.PI, Mathf.PI)
                );
                emit.startColor = Color.Lerp(
                    new Color(0.58f, 0.61f, 0.65f, 1f),
                    Color.white,
                    Random.value
                );
                m_Particles.Emit(emit, 1);
            }
        }

        private float GetImpactSpeedKph(Collision collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;
            float carSpeed = m_Body != null ? m_Body.linearVelocity.magnitude : 0f;
            return Mathf.Max(relativeSpeed, carSpeed) * MetersPerSecondToKph;
        }

        private void OnValidate()
        {
            m_MinImpactSpeedKph = Mathf.Max(1f, m_MinImpactSpeedKph);
            m_MinFragmentCount = Mathf.Clamp(m_MinFragmentCount, 6, 14);
            m_MaxFragmentCount = Mathf.Clamp(
                m_MaxFragmentCount,
                m_MinFragmentCount,
                20
            );
            m_FullBurstSeverity = Mathf.Max(6.1f, m_FullBurstSeverity);
            m_Lifetime.x = Mathf.Max(0.1f, m_Lifetime.x);
            m_Lifetime.y = Mathf.Max(m_Lifetime.x, m_Lifetime.y);
            m_Speed.x = Mathf.Max(0.1f, m_Speed.x);
            m_Speed.y = Mathf.Max(m_Speed.x, m_Speed.y);
            m_Size.x = Mathf.Max(0.01f, m_Size.x);
            m_Size.y = Mathf.Max(m_Size.x, m_Size.y);
        }
    }
}
