using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Draws short-lived vehicle and environment bullet marks in two instanced batches that
    /// share one mesh/shader. Ground and wall marks use the same material and draw batch.
    /// Marks are stored relative to their collider, and the system is visual-only: it never
    /// reads or writes vehicle velocity.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FranklinVehicleBulletDecalSystem : MonoBehaviour
    {
        private const string MATERIAL_RESOURCE =
            "FranklinShooter/Materials/Vehicle Bullet Hole URP";
        private const string TEXTURE_RESOURCE =
            "FranklinShooter/Textures/vehicle-bullet-hole";
        private const string SURFACE_MATERIAL_RESOURCE =
            "FranklinShooter/Materials/Wall Bullet Hole URP";
        private const string SURFACE_TEXTURE_RESOURCE =
            "FranklinShooter/Textures/wall-bullet-hole";
        private const string SHADER_NAME = "Franklin Game/Vehicle Bullet Decal Mobile";

        // A fixed circular buffer avoids Instantiate/Destroy and bounds mobile cost.
        private const int MAX_VEHICLE_MARKS = 32;
        private const int MAX_SURFACE_MARKS = 64;
        private const int MAX_VEHICLE_MARKS_PER_FRAME = 4;
        private const int MAX_SURFACE_MARKS_PER_FRAME = 6;
        private const int DEFAULT_LAYER = 0;
        private const float VEHICLE_MARK_LIFETIME = 30f;
        private const float SURFACE_MARK_LIFETIME = 40f;
        private const float MAX_DRAW_DISTANCE = 32f;
        private const float SURFACE_OFFSET = 0.003f;
        private const float NORMAL_RAY_OFFSET = 0.08f;
        private const float NORMAL_RAY_DISTANCE = 0.18f;
        private const float MIN_SUPPORTED_SURFACE_NORMAL_Y = -0.7f;

        private struct Mark
        {
            public bool Active;
            public Transform Anchor;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public float Size;
            public float ExpireAt;
        }

        private static FranklinVehicleBulletDecalSystem s_Instance;

        private readonly Mark[] m_VehicleMarks = new Mark[MAX_VEHICLE_MARKS];
        private readonly Matrix4x4[] m_VehicleMatrices =
            new Matrix4x4[MAX_VEHICLE_MARKS];
        private readonly Mark[] m_SurfaceMarks = new Mark[MAX_SURFACE_MARKS];
        private readonly Matrix4x4[] m_SurfaceMatrices =
            new Matrix4x4[MAX_SURFACE_MARKS];

        private int m_NextVehicleMark;
        private int m_NextSurfaceMark;
        private int m_BudgetFrame = -1;
        private int m_VehicleMarksThisFrame;
        private int m_SurfaceMarksThisFrame;
        private Mesh m_Quad;
        private Material m_VehicleMaterial;
        private Material m_SurfaceMaterial;
        private bool m_OwnsVehicleMaterial;
        private bool m_OwnsSurfaceMaterial;
        private Camera m_Camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
        }

        /// <summary>Adds a mark to a vehicle hit without applying any physical impulse.</summary>
        public static void AddHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            float size)
        {
            if (hitObject == null || size <= 0f) return;

            Collider collider = hitObject.GetComponent<Collider>() ??
                                hitObject.GetComponentInParent<Collider>();
            Transform anchor = collider != null ? collider.transform : hitObject.transform;
            Vector3 direction = incomingDirection.sqrMagnitude > 0.000001f
                ? incomingDirection.normalized
                : Vector3.forward;
            Vector3 normal = ResolveSurfaceNormal(collider, hitPoint, direction);

            FranklinVehicleBulletDecalSystem system = GetOrCreate();
            if (system == null || !system.TryConsumeMarkBudget(true)) return;
            system.AddMark(
                system.m_VehicleMarks,
                ref system.m_NextVehicleMark,
                anchor,
                hitPoint,
                normal,
                size,
                VEHICLE_MARK_LIFETIME,
                0.055f,
                0.17f
            );
        }

        /// <summary>Adds a mark to a static/kinematic ground or wall surface.</summary>
        public static void AddSurfaceHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            float size)
        {
            if (hitObject == null || size <= 0f) return;

            Collider collider = hitObject.GetComponent<Collider>() ??
                                hitObject.GetComponentInParent<Collider>();
            if (collider == null || collider.isTrigger) return;

            Rigidbody rigidbody = collider.attachedRigidbody;
            if (rigidbody != null && !rigidbody.isKinematic) return;

            Vector3 direction = incomingDirection.sqrMagnitude > 0.000001f
                ? incomingDirection.normalized
                : Vector3.forward;
            Vector3 normal = ResolveSurfaceNormal(collider, hitPoint, direction);
            // Ground and slopes are supported. Skip only downward-facing ceilings because
            // they are easy to hit through thin geometry and rarely visible on mobile.
            if (Vector3.Dot(normal, Vector3.up) < MIN_SUPPORTED_SURFACE_NORMAL_Y) return;

            FranklinVehicleBulletDecalSystem system = GetOrCreate();
            if (system == null || !system.TryConsumeMarkBudget(false)) return;
            system.AddMark(
                system.m_SurfaceMarks,
                ref system.m_NextSurfaceMark,
                collider.transform,
                hitPoint,
                normal,
                size,
                SURFACE_MARK_LIFETIME,
                0.07f,
                0.19f
            );
        }

        /// <summary>Compatibility alias for callers created before ground marks were added.</summary>
        public static void AddWallHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            float size)
        {
            AddSurfaceHit(hitObject, hitPoint, incomingDirection, size);
        }

        private static FranklinVehicleBulletDecalSystem GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;

            s_Instance = FindAnyObjectByType<FranklinVehicleBulletDecalSystem>();
            if (s_Instance != null) return s_Instance;

            GameObject gameObject = new("[Franklin Bullet Decals]");
            s_Instance = gameObject.AddComponent<FranklinVehicleBulletDecalSystem>();
            return s_Instance;
        }

        private static Vector3 ResolveSurfaceNormal(
            Collider collider,
            Vector3 point,
            Vector3 incomingDirection)
        {
            if (collider != null)
            {
                Ray ray = new(
                    point - incomingDirection * NORMAL_RAY_OFFSET,
                    incomingDirection
                );
                if (collider.Raycast(ray, out RaycastHit hit, NORMAL_RAY_DISTANCE) &&
                    hit.normal.sqrMagnitude > 0.000001f)
                {
                    return hit.normal.normalized;
                }
            }

            return -incomingDirection;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            this.m_Quad = CreateQuad();
            this.m_VehicleMaterial = LoadMaterial(
                MATERIAL_RESOURCE,
                TEXTURE_RESOURCE,
                "Vehicle Bullet Hole Runtime",
                out this.m_OwnsVehicleMaterial
            );
            this.m_SurfaceMaterial = LoadMaterial(
                SURFACE_MATERIAL_RESOURCE,
                SURFACE_TEXTURE_RESOURCE,
                "Environment Bullet Hole Runtime",
                out this.m_OwnsSurfaceMaterial
            );

            // No marks exist yet. Stay out of Unity's LateUpdate list until the first hit.
            this.enabled = false;
        }

        private void LateUpdate()
        {
            if (this.m_Quad == null) return;

            if (this.m_Camera == null) this.m_Camera = Camera.main;
            Vector3 cameraPosition = this.m_Camera != null
                ? this.m_Camera.transform.position
                : Vector3.zero;
            bool useDistanceCulling = this.m_Camera != null;
            float maxDistanceSquared = MAX_DRAW_DISTANCE * MAX_DRAW_DISTANCE;
            float now = Time.time;
            bool hasVehicleMarks = this.RenderMarks(
                this.m_VehicleMarks,
                this.m_VehicleMatrices,
                this.m_VehicleMaterial,
                now,
                cameraPosition,
                useDistanceCulling,
                maxDistanceSquared
            );
            bool hasSurfaceMarks = this.RenderMarks(
                this.m_SurfaceMarks,
                this.m_SurfaceMatrices,
                this.m_SurfaceMaterial,
                now,
                cameraPosition,
                useDistanceCulling,
                maxDistanceSquared
            );

            if (!hasVehicleMarks && !hasSurfaceMarks) this.enabled = false;
        }

        private bool RenderMarks(
            Mark[] marks,
            Matrix4x4[] matrices,
            Material material,
            float now,
            Vector3 cameraPosition,
            bool useDistanceCulling,
            float maxDistanceSquared)
        {
            if (material == null) return false;

            int count = 0;
            bool hasActiveMarks = false;

            for (int i = 0; i < marks.Length; ++i)
            {
                Mark mark = marks[i];
                if (!mark.Active) continue;
                if (mark.Anchor == null || now >= mark.ExpireAt)
                {
                    mark.Active = false;
                    marks[i] = mark;
                    continue;
                }

                hasActiveMarks = true;
                Vector3 position = mark.Anchor.TransformPoint(mark.LocalPosition);
                if (useDistanceCulling &&
                    (position - cameraPosition).sqrMagnitude > maxDistanceSquared)
                {
                    continue;
                }

                Quaternion rotation = mark.Anchor.rotation * mark.LocalRotation;
                matrices[count++] = Matrix4x4.TRS(
                    position,
                    rotation,
                    Vector3.one * mark.Size
                );
            }

            if (count == 0) return hasActiveMarks;

            RenderParams renderParams = new(material)
            {
                layer = DEFAULT_LAYER,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMeshInstanced(
                renderParams,
                this.m_Quad,
                0,
                matrices,
                count
            );
            return hasActiveMarks;
        }

        private void AddMark(
            Mark[] marks,
            ref int nextMark,
            Transform anchor,
            Vector3 worldPosition,
            Vector3 worldNormal,
            float size,
            float lifetime,
            float minimumSize,
            float maximumSize)
        {
            if (anchor == null) return;

            Vector3 normal = worldNormal.sqrMagnitude > 0.000001f
                ? worldNormal.normalized
                : Vector3.back;
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.96f
                ? Vector3.right
                : Vector3.up;
            Quaternion worldRotation = Quaternion.LookRotation(normal, up) *
                                       Quaternion.AngleAxis(
                                           Mathf.Repeat(nextMark * 137.5f, 360f),
                                           Vector3.forward
                                       );
            float scaleVariation = Mathf.Lerp(
                0.88f,
                1.12f,
                Mathf.Repeat(nextMark * 0.6180339f, 1f)
            );

            marks[nextMark] = new Mark
            {
                Active = true,
                Anchor = anchor,
                LocalPosition = anchor.InverseTransformPoint(
                    worldPosition + normal * SURFACE_OFFSET
                ),
                LocalRotation = Quaternion.Inverse(anchor.rotation) * worldRotation,
                Size = Mathf.Clamp(size * scaleVariation, minimumSize, maximumSize),
                ExpireAt = Time.time + lifetime
            };
            nextMark = (nextMark + 1) % marks.Length;
            this.enabled = true;
        }

        private bool TryConsumeMarkBudget(bool vehicle)
        {
            int frame = Time.frameCount;
            if (this.m_BudgetFrame != frame)
            {
                this.m_BudgetFrame = frame;
                this.m_VehicleMarksThisFrame = 0;
                this.m_SurfaceMarksThisFrame = 0;
            }

            if (vehicle)
            {
                if (this.m_VehicleMarksThisFrame >= MAX_VEHICLE_MARKS_PER_FRAME)
                    return false;
                this.m_VehicleMarksThisFrame += 1;
                return true;
            }

            if (this.m_SurfaceMarksThisFrame >= MAX_SURFACE_MARKS_PER_FRAME)
                return false;
            this.m_SurfaceMarksThisFrame += 1;
            return true;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
            if (this.m_Quad != null) Destroy(this.m_Quad);
            if (this.m_OwnsVehicleMaterial && this.m_VehicleMaterial != null)
                Destroy(this.m_VehicleMaterial);
            if (this.m_OwnsSurfaceMaterial && this.m_SurfaceMaterial != null)
                Destroy(this.m_SurfaceMaterial);
        }

        private static Material LoadMaterial(
            string materialResource,
            string textureResource,
            string runtimeName,
            out bool ownsMaterial)
        {
            ownsMaterial = false;
            Material material = Resources.Load<Material>(materialResource);
            if (material != null) return material;

            // The installer normally creates shared materials. This fallback keeps newly
            // imported assets usable before the delayed editor repair has completed.
            Shader shader = Shader.Find(SHADER_NAME);
            Texture2D texture = Resources.Load<Texture2D>(textureResource);
            if (shader == null || texture == null) return null;

            material = new Material(shader)
            {
                name = runtimeName,
                enableInstancing = true
            };
            material.SetTexture("_BaseMap", texture);
            ownsMaterial = true;
            return material;
        }

        private static Mesh CreateQuad()
        {
            Mesh mesh = new() { name = "Franklin Vehicle Bullet Mark Quad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            mesh.SetNormals(new[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.5f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
