using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Shooter
{
    /// <summary>Fixed two-slot smoke pool. No collision, lights, shadows or runtime growth.</summary>
    [DefaultExecutionOrder(640)]
    internal sealed class FranklinSmokeCloudPool : MonoBehaviour
    {
        private const int SLOT_COUNT = 2;
        private const int MAX_PARTICLES = 64;

        private sealed class Slot
        {
            public GameObject Root;
            public ParticleSystem Particle;
            public float ExpiresAt;
            public uint Serial;
        }

        private static FranklinSmokeCloudPool s_Instance;
        private static uint s_Serial;

        private readonly Slot[] m_Slots = new Slot[SLOT_COUNT];
        private Material m_Material;
        private Texture2D m_Texture;

        public static void Spawn(Vector3 position, float duration, float radius)
        {
            EnsureInstance();
            s_Instance?.SpawnInternal(position, duration, radius);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_Serial = 0;
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;
            GameObject root = new("Franklin Smoke Cloud Pool");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<FranklinSmokeCloudPool>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            this.CreateSharedRenderingAssets();
            for (int i = 0; i < this.m_Slots.Length; ++i)
                this.m_Slots[i] = this.CreateSlot(i);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot slot = this.m_Slots[i];
                if (slot?.Root == null || !slot.Root.activeSelf || now < slot.ExpiresAt)
                    continue;

                slot.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slot.Root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (this.m_Material != null) Destroy(this.m_Material);
            if (this.m_Texture != null) Destroy(this.m_Texture);
            if (s_Instance == this) s_Instance = null;
        }

        private void SpawnInternal(Vector3 position, float duration, float radius)
        {
            Slot slot = this.m_Slots[0];
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot candidate = this.m_Slots[i];
                if (candidate == null) continue;
                if (!candidate.Root.activeSelf)
                {
                    slot = candidate;
                    break;
                }
                if (slot == null || candidate.Serial < slot.Serial) slot = candidate;
            }
            if (slot == null) return;

            slot.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Root.transform.SetPositionAndRotation(
                position + Vector3.up * 0.18f,
                Quaternion.identity
            );
            slot.Root.transform.localScale = Vector3.one * Mathf.Clamp(radius / 4.5f, 0.7f, 2f);
            slot.Root.SetActive(true);
            slot.ExpiresAt = Time.unscaledTime + Mathf.Max(1f, duration);
            slot.Serial = ++s_Serial;
            slot.Particle.Play(true);
        }

        private Slot CreateSlot(int index)
        {
            GameObject root = new($"Smoke Cloud {index + 1}");
            root.transform.SetParent(this.transform, false);
            ParticleSystem particle = root.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 5.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.38f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.60f, 0.62f, 0.65f, 0.92f);
            main.maxParticles = MAX_PARTICLES;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 9f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 20));

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1.3f;
            shape.radiusThickness = 0.55f;

            ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.57f, 0.6f), 0f),
                    new GradientColorKey(new Color(0.74f, 0.76f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.94f, 0.07f),
                    new GradientAlphaKey(0.82f, 0.72f),
                    new GradientAlphaKey(0.64f, 0.88f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.32f, 1.05f),
                    new Keyframe(0.7f, 1.38f),
                    new Keyframe(1f, 1.55f)
                )
            );

            ParticleSystem.VelocityOverLifetimeModule velocity = particle.velocityOverLifetime;
            velocity.enabled = true;
            // Unity requires the X/Y/Z velocity curves to use the same mode. Explicit
            // Two-Constants on all axes removes the warning and produces a quick mobile-
            // friendly outward plume without enabling particle collision or turbulence.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.42f, 0.42f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.32f, 0.78f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.42f, 0.42f);
            velocity.space = ParticleSystemSimulationSpace.World;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = this.m_Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sortingFudge = 2f;

            root.SetActive(false);
            return new Slot { Root = root, Particle = particle };
        }

        private void CreateSharedRenderingAssets()
        {
            this.m_Texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true)
            {
                name = "Franklin Smoke Soft Particle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; ++y)
            {
                for (int x = 0; x < 64; ++x)
                {
                    float nx = (x + 0.5f) / 32f - 1f;
                    float ny = (y + 0.5f) / 32f - 1f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    float detail = 0.88f + 0.12f * Mathf.Sin(x * 0.39f + y * 0.31f);
                    float body = Mathf.Pow(radial, 0.68f);
                    byte alpha = (byte) Mathf.RoundToInt(
                        255f * body * detail
                    );
                    pixels[y * 64 + x] = new Color32(255, 255, 255, alpha);
                }
            }
            this.m_Texture.SetPixels32(pixels);
            this.m_Texture.Apply(false, true);

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit") ??
                            Shader.Find("Unlit/Transparent");
            if (shader == null) return;

            this.m_Material = new Material(shader)
            {
                name = "Franklin Smoke Mobile Runtime",
                renderQueue = (int) RenderQueue.Transparent
            };
            this.m_Material.SetOverrideTag("RenderType", "Transparent");
            if (this.m_Material.HasProperty("_Surface"))
                this.m_Material.SetFloat("_Surface", 1f);
            if (this.m_Material.HasProperty("_Blend"))
                this.m_Material.SetFloat("_Blend", 0f);
            if (this.m_Material.HasProperty("_SrcBlend"))
                this.m_Material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
            if (this.m_Material.HasProperty("_DstBlend"))
                this.m_Material.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            if (this.m_Material.HasProperty("_SrcBlendAlpha"))
                this.m_Material.SetFloat("_SrcBlendAlpha", (float) BlendMode.One);
            if (this.m_Material.HasProperty("_DstBlendAlpha"))
                this.m_Material.SetFloat("_DstBlendAlpha", (float) BlendMode.OneMinusSrcAlpha);
            if (this.m_Material.HasProperty("_ZWrite"))
                this.m_Material.SetFloat("_ZWrite", 0f);
            if (this.m_Material.HasProperty("_Cull"))
                this.m_Material.SetFloat("_Cull", (float) CullMode.Off);
            this.m_Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (this.m_Material.HasProperty("_BaseMap"))
                this.m_Material.SetTexture("_BaseMap", this.m_Texture);
            if (this.m_Material.HasProperty("_MainTex"))
                this.m_Material.SetTexture("_MainTex", this.m_Texture);
            if (this.m_Material.HasProperty("_BaseColor"))
                this.m_Material.SetColor("_BaseColor", Color.white);
            if (this.m_Material.HasProperty("_Color"))
                this.m_Material.SetColor("_Color", Color.white);
        }
    }
}
