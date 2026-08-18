using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Event-only collision sparks and small metal chips based on the Car/Bike VFX.
    /// Pools are created once on the first accepted impact and then reused.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DroneHealth), typeof(Rigidbody))]
    public sealed class DroneImpactEffects : MonoBehaviour
    {
        [SerializeField] private DroneHealth m_Health;
        [SerializeField] private Rigidbody m_Body;

        [Header("Pooled Collision Sparks")]
        [SerializeField] private GameObject m_ImpactEffectPrefab;
        [SerializeField, Range(1, 3)] private int m_EffectPoolSize = 2;
        [SerializeField, Range(1, 8)] private int m_LightSparkCount = 3;
        [SerializeField, Range(2, 12)] private int m_HeavySparkCount = 7;
        [SerializeField, Range(1, 4)] private int m_HeavyFlashCount = 2;
        [SerializeField, Min(0.1f)] private float m_HeavyImpactSpeed = 9f;
        [SerializeField, Range(0.05f, 1f)] private float m_EffectScale = 0.28f;
        [SerializeField, Min(0.25f)] private float m_EffectSleepTime = 1.6f;

        [Header("Mobile Metal Chipping")]
        [SerializeField] private Material m_MetalDebrisMaterial;
        [SerializeField, Min(0.1f)] private float m_MinDebrisImpactSpeed = 7f;
        [SerializeField, Range(1, 6)] private int m_MinFragmentCount = 3;
        [SerializeField, Range(2, 10)] private int m_MaxFragmentCount = 6;
        [SerializeField, Min(0.1f)] private float m_FullBurstSpeed = 16f;
        [SerializeField] private Vector2 m_FragmentLifetime = new Vector2(0.45f, 0.75f);
        [SerializeField] private Vector2 m_FragmentSpeed = new Vector2(1.2f, 3.4f);
        [SerializeField] private Vector2 m_FragmentSize = new Vector2(0.035f, 0.07f);
        [SerializeField, Range(0f, 3f)] private float m_FragmentGravity = 1.1f;
        [SerializeField, Min(0f)] private float m_DebrisCooldown = 0.18f;

        private const int MobileSparkParticleCap = 8;
        private const int MetalFragmentParticleCap = 8;

        private Transform[] m_EffectRoots;
        private ParticleSystem[][] m_EffectSystems;
        private DroneImpactEffectSleeper[] m_EffectSleepers;
        private int m_NextEffectIndex;
        private ParticleSystem m_MetalDebrisParticles;
        private float m_NextDebrisTime;

        private void Awake()
        {
            this.ResolveReferences();
        }

        private void OnEnable()
        {
            this.ResolveReferences();
            if (this.m_Health == null) return;

            this.m_Health.EventImpactAccepted += this.OnImpactAccepted;
            this.m_Health.EventDestroyed += this.OnDestroyed;
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
                this.m_Health.EventImpactAccepted -= this.OnImpactAccepted;
                this.m_Health.EventDestroyed -= this.OnDestroyed;
            }
            this.StopAllEffects();
            this.m_NextDebrisTime = 0f;
        }

        private void ResolveReferences()
        {
            if (this.m_Health == null) this.m_Health = this.GetComponent<DroneHealth>();
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
        }

        private void OnImpactAccepted(
            Vector3 impactPoint,
            Vector3 impactNormal,
            float impactSpeed)
        {
            if (this.m_Health == null || this.m_Health.IsDestroyed) return;

            Vector3 normal = impactNormal.sqrMagnitude > 0.0001f
                ? impactNormal.normalized
                : this.transform.up;
            this.PlaySparkBurst(impactPoint, normal, impactSpeed);
            this.TryEmitMetalFragments(impactPoint, normal, impactSpeed);
        }

        private void BuildSparkPool()
        {
            if (this.m_ImpactEffectPrefab == null || this.m_EffectRoots != null) return;

            int poolSize = Application.isMobilePlatform
                ? Mathf.Min(2, this.m_EffectPoolSize)
                : Mathf.Clamp(this.m_EffectPoolSize, 1, 3);
            this.m_EffectRoots = new Transform[poolSize];
            this.m_EffectSystems = new ParticleSystem[poolSize][];
            this.m_EffectSleepers = new DroneImpactEffectSleeper[poolSize];

            for (int index = 0; index < poolSize; ++index)
            {
                GameObject instance = Instantiate(this.m_ImpactEffectPrefab, this.transform);
                instance.name = $"Drone Impact FX Pool {index + 1}";
                Transform effectTransform = instance.transform;
                effectTransform.localPosition = Vector3.zero;
                effectTransform.localRotation = Quaternion.identity;

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int systemIndex = 0; systemIndex < systems.Length; ++systemIndex)
                {
                    ParticleSystem particles = systems[systemIndex];
                    ParticleSystem.MainModule main = particles.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.cullingMode = ParticleSystemCullingMode.Automatic;
                    if (Application.isMobilePlatform)
                    {
                        main.maxParticles = Mathf.Min(
                            main.maxParticles,
                            MobileSparkParticleCap
                        );
                    }

                    ParticleSystem.EmissionModule emission = particles.emission;
                    emission.enabled = false;

                    ParticleSystemRenderer effectRenderer =
                        particles.GetComponent<ParticleSystemRenderer>();
                    if (effectRenderer != null)
                    {
                        effectRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        effectRenderer.receiveShadows = false;
                        effectRenderer.lightProbeUsage = LightProbeUsage.Off;
                        effectRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        effectRenderer.motionVectorGenerationMode =
                            MotionVectorGenerationMode.ForceNoMotion;
                    }
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                DroneImpactEffectSleeper sleeper =
                    instance.AddComponent<DroneImpactEffectSleeper>();
                sleeper.Initialize(systems);
                this.m_EffectRoots[index] = effectTransform;
                this.m_EffectSystems[index] = systems;
                this.m_EffectSleepers[index] = sleeper;
                instance.SetActive(false);
            }
        }

        private void PlaySparkBurst(
            Vector3 impactPoint,
            Vector3 impactNormal,
            float impactSpeed)
        {
            if (this.m_EffectRoots == null) this.BuildSparkPool();
            if (this.m_EffectRoots == null || this.m_EffectRoots.Length == 0) return;

            int index = this.m_NextEffectIndex;
            this.m_NextEffectIndex = (this.m_NextEffectIndex + 1) % this.m_EffectRoots.Length;
            Transform effectRoot = this.m_EffectRoots[index];
            ParticleSystem[] systems = this.m_EffectSystems[index];
            if (effectRoot == null || systems == null || systems.Length == 0) return;

            bool isHeavy = impactSpeed >= this.m_HeavyImpactSpeed;
            if (!effectRoot.gameObject.activeSelf) effectRoot.gameObject.SetActive(true);

            Vector3 orientationUp = Mathf.Abs(Vector3.Dot(impactNormal, this.transform.up)) > 0.98f
                ? this.transform.forward
                : this.transform.up;
            effectRoot.SetPositionAndRotation(
                impactPoint + impactNormal * 0.015f,
                Quaternion.LookRotation(impactNormal, orientationUp)
            );
            effectRoot.localScale = Vector3.one * this.m_EffectScale *
                (isHeavy ? 1f : 0.72f);

            for (int systemIndex = 0; systemIndex < systems.Length; ++systemIndex)
            {
                ParticleSystem particles = systems[systemIndex];
                if (particles == null) continue;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                bool isSparkTrail = particles.gameObject.name == "Sparks";
                int count = isSparkTrail
                    ? (isHeavy ? this.m_HeavySparkCount : this.m_LightSparkCount)
                    : (isHeavy ? this.m_HeavyFlashCount : 1);
                particles.Emit(count);
            }

            this.m_EffectSleepers[index]?.Arm(this.m_EffectSleepTime);
        }

        private void BuildMetalFragmentPool()
        {
            if (this.m_MetalDebrisParticles != null || this.m_MetalDebrisMaterial == null)
            {
                return;
            }

            GameObject poolObject = new GameObject("Drone Metal Chipping Pool");
            poolObject.layer = this.gameObject.layer;
            poolObject.transform.SetParent(this.transform, false);
            this.m_MetalDebrisParticles = poolObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = this.m_MetalDebrisParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Disable;
            main.maxParticles = MetalFragmentParticleCap;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                this.m_FragmentLifetime.x,
                this.m_FragmentLifetime.y
            );
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                this.m_FragmentSize.x,
                this.m_FragmentSize.y
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = this.m_FragmentGravity;

            ParticleSystem.EmissionModule emission = this.m_MetalDebrisParticles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = this.m_MetalDebrisParticles.shape;
            shape.enabled = false;
            ParticleSystem.CollisionModule particleCollision =
                this.m_MetalDebrisParticles.collision;
            particleCollision.enabled = false;
            ParticleSystem.TrailModule trails = this.m_MetalDebrisParticles.trails;
            trails.enabled = false;
            ParticleSystem.NoiseModule noise = this.m_MetalDebrisParticles.noise;
            noise.enabled = false;

            ParticleSystem.RotationOverLifetimeModule rotation =
                this.m_MetalDebrisParticles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(-11f, 11f);

            ParticleSystem.TextureSheetAnimationModule sheet =
                this.m_MetalDebrisParticles.textureSheetAnimation;
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
            particleRenderer.sharedMaterial = this.m_MetalDebrisMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            this.m_MetalDebrisParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        private void TryEmitMetalFragments(
            Vector3 impactPoint,
            Vector3 impactNormal,
            float impactSpeed)
        {
            if (impactSpeed < this.m_MinDebrisImpactSpeed ||
                Time.unscaledTime < this.m_NextDebrisTime ||
                this.m_MetalDebrisMaterial == null)
            {
                return;
            }
            if (this.m_MetalDebrisParticles == null) this.BuildMetalFragmentPool();
            if (this.m_MetalDebrisParticles == null) return;

            GameObject debrisObject = this.m_MetalDebrisParticles.gameObject;
            if (!debrisObject.activeSelf) debrisObject.SetActive(true);

            float amount = Mathf.InverseLerp(
                this.m_MinDebrisImpactSpeed,
                Mathf.Max(this.m_MinDebrisImpactSpeed + 0.1f, this.m_FullBurstSpeed),
                impactSpeed
            );
            int count = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    this.m_MinFragmentCount,
                    this.m_MaxFragmentCount,
                    amount
                )),
                1,
                MetalFragmentParticleCap
            );

            Vector3 tangent = Vector3.Cross(impactNormal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(impactNormal, this.transform.forward);
            }
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(impactNormal, tangent).normalized;
            Vector3 inheritedVelocity = this.m_Body != null
                ? this.m_Body.GetPointVelocity(impactPoint) * 0.12f
                : Vector3.zero;

            if (!this.m_MetalDebrisParticles.isPlaying)
            {
                this.m_MetalDebrisParticles.Play(false);
            }
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int index = 0; index < count; ++index)
            {
                float speed = Random.Range(
                    this.m_FragmentSpeed.x,
                    this.m_FragmentSpeed.y
                ) * Mathf.Lerp(0.85f, 1.12f, amount);
                Vector3 direction = (
                    impactNormal * Random.Range(0.45f, 1f) +
                    tangent * Random.Range(-0.72f, 0.72f) +
                    bitangent * Random.Range(-0.62f, 0.62f) +
                    Vector3.up * Random.Range(0.1f, 0.4f)
                ).normalized;

                emit.position = impactPoint + Random.insideUnitSphere * 0.025f;
                emit.velocity = direction * speed + inheritedVelocity;
                emit.startLifetime = Random.Range(
                    this.m_FragmentLifetime.x,
                    this.m_FragmentLifetime.y
                );
                emit.startSize = Random.Range(
                    this.m_FragmentSize.x,
                    this.m_FragmentSize.y
                );
                emit.rotation3D = new Vector3(
                    0f,
                    0f,
                    Random.Range(-Mathf.PI, Mathf.PI)
                );
                emit.startColor = Color.Lerp(
                    new Color(0.58f, 0.62f, 0.66f, 1f),
                    Color.white,
                    Random.value
                );
                this.m_MetalDebrisParticles.Emit(emit, 1);
            }
            this.m_MetalDebrisParticles.Stop(
                false,
                ParticleSystemStopBehavior.StopEmitting
            );
            this.m_NextDebrisTime = Time.unscaledTime + this.m_DebrisCooldown;
        }

        private void OnDestroyed()
        {
            this.StopAllEffects();
        }

        private void StopAllEffects()
        {
            if (this.m_EffectSleepers != null)
            {
                for (int index = 0; index < this.m_EffectSleepers.Length; ++index)
                {
                    this.m_EffectSleepers[index]?.SleepNow();
                }
            }
            if (this.m_MetalDebrisParticles != null)
            {
                this.m_MetalDebrisParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        private void OnValidate()
        {
            this.m_EffectPoolSize = Mathf.Clamp(this.m_EffectPoolSize, 1, 3);
            this.m_LightSparkCount = Mathf.Clamp(this.m_LightSparkCount, 1, 8);
            this.m_HeavySparkCount = Mathf.Clamp(
                this.m_HeavySparkCount,
                this.m_LightSparkCount,
                12
            );
            this.m_HeavyImpactSpeed = Mathf.Max(0.1f, this.m_HeavyImpactSpeed);
            this.m_EffectScale = Mathf.Clamp(this.m_EffectScale, 0.05f, 1f);
            this.m_EffectSleepTime = Mathf.Max(0.25f, this.m_EffectSleepTime);
            this.m_MinDebrisImpactSpeed = Mathf.Max(0.1f, this.m_MinDebrisImpactSpeed);
            this.m_MaxFragmentCount = Mathf.Clamp(
                this.m_MaxFragmentCount,
                this.m_MinFragmentCount,
                10
            );
            this.m_FullBurstSpeed = Mathf.Max(
                this.m_MinDebrisImpactSpeed + 0.1f,
                this.m_FullBurstSpeed
            );
            this.m_FragmentLifetime.x = Mathf.Max(0.1f, this.m_FragmentLifetime.x);
            this.m_FragmentLifetime.y = Mathf.Max(
                this.m_FragmentLifetime.x,
                this.m_FragmentLifetime.y
            );
            this.m_FragmentSpeed.x = Mathf.Max(0.1f, this.m_FragmentSpeed.x);
            this.m_FragmentSpeed.y = Mathf.Max(
                this.m_FragmentSpeed.x,
                this.m_FragmentSpeed.y
            );
            this.m_FragmentSize.x = Mathf.Max(0.005f, this.m_FragmentSize.x);
            this.m_FragmentSize.y = Mathf.Max(
                this.m_FragmentSize.x,
                this.m_FragmentSize.y
            );
            this.m_DebrisCooldown = Mathf.Max(0f, this.m_DebrisCooldown);
        }
    }

    /// <summary>
    /// Sleeps only while one pooled burst is alive, so parked drones add no Update work.
    /// </summary>
    internal sealed class DroneImpactEffectSleeper : MonoBehaviour
    {
        private ParticleSystem[] m_Systems;
        private float m_SleepAt;

        public void Initialize(ParticleSystem[] systems)
        {
            this.m_Systems = systems;
            this.enabled = false;
        }

        public void Arm(float duration)
        {
            this.m_SleepAt = Time.unscaledTime + duration;
            this.enabled = true;
        }

        public void SleepNow()
        {
            this.enabled = false;
            if (this.m_Systems != null)
            {
                for (int index = 0; index < this.m_Systems.Length; ++index)
                {
                    ParticleSystem particles = this.m_Systems[index];
                    if (particles != null)
                    {
                        particles.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmittingAndClear
                        );
                    }
                }
            }
            if (this.gameObject.activeSelf) this.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= this.m_SleepAt) this.SleepNow();
        }
    }
}
