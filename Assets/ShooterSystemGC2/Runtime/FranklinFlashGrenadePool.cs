using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Mobile-budget flash grenade presentation: two pooled world particles and one reused
    /// screen overlay. No lights, post-processing volumes, collision particles or raycasts.
    /// </summary>
    [DefaultExecutionOrder(640)]
    internal sealed class FranklinFlashGrenadePool : MonoBehaviour
    {
        private const int SLOT_COUNT = 2;
        private const int MAX_PARTICLES = 12;

        private sealed class Slot
        {
            public GameObject Root;
            public ParticleSystem Particle;
            public float ExpiresAt;
            public uint Serial;
        }

        private static FranklinFlashGrenadePool s_Instance;
        private static uint s_Serial;

        private readonly Slot[] m_Slots = new Slot[SLOT_COUNT];
        private Material m_Material;
        private Texture2D m_Texture;
        private Image m_ScreenFlash;
        private float m_ScreenFlashStartedAt;
        private float m_ScreenFlashDuration;
        private float m_ScreenFlashStrength;

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
            GameObject root = new("Franklin Flash Grenade Pool");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<FranklinFlashGrenadePool>();
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
            this.enabled = false;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool anyActive = false;
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot slot = this.m_Slots[i];
                if (slot?.Root == null || !slot.Root.activeSelf) continue;
                if (now >= slot.ExpiresAt)
                {
                    slot.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    slot.Root.SetActive(false);
                    continue;
                }

                anyActive = true;
            }

            if (this.m_ScreenFlash != null && this.m_ScreenFlash.gameObject.activeSelf)
            {
                float elapsed = now - this.m_ScreenFlashStartedAt;
                float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, this.m_ScreenFlashDuration));
                float alpha = this.m_ScreenFlashStrength * (1f - ratio) * (1f - ratio);
                this.m_ScreenFlash.color = new Color(1f, 1f, 1f, alpha);
                if (ratio >= 1f)
                {
                    this.m_ScreenFlash.gameObject.SetActive(false);
                    this.m_ScreenFlashStrength = 0f;
                }
                else anyActive = true;
            }

            if (!anyActive) this.enabled = false;
        }

        private void OnDestroy()
        {
            if (this.m_Material != null) Destroy(this.m_Material);
            if (this.m_Texture != null) Destroy(this.m_Texture);
            if (s_Instance == this) s_Instance = null;
        }

        private void SpawnInternal(Vector3 position, float duration, float radius)
        {
            Slot slot = this.GetAvailableSlot();
            if (slot == null) return;

            slot.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Root.transform.SetPositionAndRotation(position + Vector3.up * 0.08f, Quaternion.identity);
            slot.Root.transform.localScale = Vector3.one * Mathf.Clamp(radius / 12f, 0.75f, 1.5f);
            slot.Root.SetActive(true);
            slot.ExpiresAt = Time.unscaledTime + 0.45f;
            slot.Serial = ++s_Serial;
            slot.Particle.Play(true);

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 toFlash = position - camera.transform.position;
                float distance = toFlash.magnitude;
                if (distance <= Mathf.Max(1f, radius))
                {
                    float facing = distance > 0.01f
                        ? Mathf.Clamp01((Vector3.Dot(camera.transform.forward, toFlash / distance) + 0.2f) / 1.2f)
                        : 1f;
                    float distanceStrength = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, radius));
                    float strength = distanceStrength * Mathf.Lerp(0.28f, 0.88f, facing);
                    this.ShowScreenFlash(Mathf.Max(0.25f, duration), strength);
                }
            }

            this.enabled = true;
        }

        private Slot GetAvailableSlot()
        {
            Slot selected = this.m_Slots[0];
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot candidate = this.m_Slots[i];
                if (candidate == null) continue;
                if (!candidate.Root.activeSelf) return candidate;
                if (selected == null || candidate.Serial < selected.Serial) selected = candidate;
            }

            return selected;
        }

        private void ShowScreenFlash(float duration, float strength)
        {
            if (strength <= 0.01f) return;
            this.EnsureScreenOverlay();
            if (this.m_ScreenFlash == null) return;

            this.m_ScreenFlashStartedAt = Time.unscaledTime;
            this.m_ScreenFlashDuration = Mathf.Clamp(duration, 0.25f, 1.5f);
            this.m_ScreenFlashStrength = Mathf.Clamp01(Mathf.Max(
                this.m_ScreenFlashStrength,
                strength
            ));
            this.m_ScreenFlash.color = new Color(1f, 1f, 1f, this.m_ScreenFlashStrength);
            this.m_ScreenFlash.gameObject.SetActive(true);
        }

        private void EnsureScreenOverlay()
        {
            if (this.m_ScreenFlash != null) return;
            GameObject canvasObject = new("Flash Screen Overlay", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(this.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19980;

            GameObject imageObject = new("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            this.m_ScreenFlash = imageObject.GetComponent<Image>();
            this.m_ScreenFlash.raycastTarget = false;
            this.m_ScreenFlash.color = Color.clear;
            imageObject.SetActive(false);
        }

        private Slot CreateSlot(int index)
        {
            GameObject root = new($"Flash Burst {index + 1}");
            root.transform.SetParent(this.transform, false);
            ParticleSystem particle = root.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startColor = new Color(1f, 0.98f, 0.82f, 1f);
            main.maxParticles = MAX_PARTICLES;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, MAX_PARTICLES));

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 0.92f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.35f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = gradient;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = this.m_Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            root.SetActive(false);
            return new Slot { Root = root, Particle = particle };
        }

        private void CreateSharedRenderingAssets()
        {
            const int size = 32;
            this.m_Texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Franklin Flash Soft Particle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float nx = (x + 0.5f) / (size * 0.5f) - 1f;
                    float ny = (y + 0.5f) / (size * 0.5f) - 1f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 0.45f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte) Mathf.RoundToInt(alpha * 255f));
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
                name = "Franklin Flash Mobile Runtime",
                renderQueue = (int) RenderQueue.Transparent
            };
            this.m_Material.SetOverrideTag("RenderType", "Transparent");
            if (this.m_Material.HasProperty("_Surface")) this.m_Material.SetFloat("_Surface", 1f);
            if (this.m_Material.HasProperty("_SrcBlend")) this.m_Material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
            if (this.m_Material.HasProperty("_DstBlend")) this.m_Material.SetFloat("_DstBlend", (float) BlendMode.One);
            if (this.m_Material.HasProperty("_ZWrite")) this.m_Material.SetFloat("_ZWrite", 0f);
            this.m_Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (this.m_Material.HasProperty("_BaseMap")) this.m_Material.SetTexture("_BaseMap", this.m_Texture);
            if (this.m_Material.HasProperty("_MainTex")) this.m_Material.SetTexture("_MainTex", this.m_Texture);
            if (this.m_Material.HasProperty("_BaseColor")) this.m_Material.SetColor("_BaseColor", Color.white);
            if (this.m_Material.HasProperty("_Color")) this.m_Material.SetColor("_Color", Color.white);
        }
    }
}
