using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Draws bounded vehicle/environment bullet marks and separate metal/surface explosion
    /// marks in four instanced batches that share one mesh/shader.
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
        private const string EXPLOSION_SURFACE_MATERIAL_RESOURCE =
            "FranklinShooter/Materials/RPG Explosion Scorch URP";
        private const string EXPLOSION_SURFACE_TEXTURE_RESOURCE =
            "FranklinShooter/Textures/rpg-explosion-scorch";
        private const string EXPLOSION_VEHICLE_MATERIAL_RESOURCE =
            "FranklinShooter/Materials/RPG Explosion Vehicle Scorch URP";
        private const string EXPLOSION_VEHICLE_TEXTURE_RESOURCE =
            "FranklinShooter/Textures/rpg-explosion-vehicle-scorch";
        private const string SHADER_NAME = "Franklin Game/Vehicle Bullet Decal Mobile";

        // A fixed circular buffer avoids Instantiate/Destroy and bounds mobile cost.
        private const int MAX_VEHICLE_MARKS = 32;
        private const int MAX_SURFACE_MARKS = 64;
        private const int MOBILE_MAX_VEHICLE_MARKS = 16;
        private const int MOBILE_MAX_SURFACE_MARKS = 32;
        private const int MAX_EXPLOSION_VEHICLE_MARKS = 8;
        private const int MAX_EXPLOSION_SURFACE_MARKS = 8;
        private const int MOBILE_MAX_EXPLOSION_VEHICLE_MARKS = 4;
        private const int MOBILE_MAX_EXPLOSION_SURFACE_MARKS = 4;
        private const int MAX_VEHICLE_MARKS_PER_FRAME = 4;
        private const int MAX_SURFACE_MARKS_PER_FRAME = 6;
        private const int MAX_EXPLOSION_MARKS_PER_FRAME = 2;
        private const int DEFAULT_LAYER = 0;
        private const float VEHICLE_MARK_LIFETIME = 30f;
        private const float SURFACE_MARK_LIFETIME = 40f;
        private const float MOBILE_VEHICLE_MARK_LIFETIME = 18f;
        private const float MOBILE_SURFACE_MARK_LIFETIME = 24f;
        private const float EXPLOSION_MARK_LIFETIME = 48f;
        private const float MOBILE_EXPLOSION_MARK_LIFETIME = 30f;
        private const float MAX_DRAW_DISTANCE = 32f;
        private const float MOBILE_MAX_DRAW_DISTANCE = 24f;
        private const float SURFACE_OFFSET = 0.003f;
        private const float NORMAL_RAY_OFFSET = 0.08f;
        private const float NORMAL_RAY_DISTANCE = 0.18f;
        private const float CONTACT_SNAP_OFFSET = 1.25f;
        private const float CONTACT_SNAP_DISTANCE = 2.5f;
        private const float SUPPORT_RAY_OFFSET = 0.08f;
        private const float SUPPORT_RAY_DISTANCE = 0.16f;
        private const float MIN_SUPPORTED_SURFACE_NORMAL_Y = -0.7f;
        private const float CAMERA_LOOKUP_INTERVAL = 0.5f;

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
        private readonly Mark[] m_ExplosionVehicleMarks =
            new Mark[MAX_EXPLOSION_VEHICLE_MARKS];
        private readonly Matrix4x4[] m_ExplosionVehicleMatrices =
            new Matrix4x4[MAX_EXPLOSION_VEHICLE_MARKS];
        private readonly Mark[] m_ExplosionSurfaceMarks =
            new Mark[MAX_EXPLOSION_SURFACE_MARKS];
        private readonly Matrix4x4[] m_ExplosionSurfaceMatrices =
            new Matrix4x4[MAX_EXPLOSION_SURFACE_MARKS];

        private int m_NextVehicleMark;
        private int m_NextSurfaceMark;
        private int m_NextExplosionVehicleMark;
        private int m_NextExplosionSurfaceMark;
        private int m_BudgetFrame = -1;
        private int m_VehicleMarksThisFrame;
        private int m_SurfaceMarksThisFrame;
        private int m_ExplosionMarksThisFrame;
        private Mesh m_Quad;
        private Material m_VehicleMaterial;
        private Material m_SurfaceMaterial;
        private Material m_ExplosionVehicleMaterial;
        private Material m_ExplosionSurfaceMaterial;
        private bool m_OwnsVehicleMaterial;
        private bool m_OwnsSurfaceMaterial;
        private bool m_OwnsExplosionVehicleMaterial;
        private bool m_OwnsExplosionSurfaceMaterial;
        private Camera m_Camera;
        private float m_NextCameraLookup;

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
            AddHit(hitObject, hitPoint, incomingDirection, Vector3.zero, size);
        }

        public static void AddHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            Vector3 hitNormal,
            float size)
        {
            if (hitObject == null || size <= 0f) return;

            Collider collider = hitObject.GetComponent<Collider>();
            if (collider == null) collider = hitObject.GetComponentInParent<Collider>();
            Transform anchor = collider != null ? collider.transform : hitObject.transform;
            Vector3 direction = incomingDirection.sqrMagnitude > 0.000001f
                ? incomingDirection.normalized
                : Vector3.forward;
            hitPoint = ResolveSurfaceContact(
                collider,
                hitPoint,
                direction,
                hitNormal,
                out Vector3 normal
            );

            FranklinVehicleBulletDecalSystem system = GetOrCreate();
            if (system == null || !system.TryConsumeMarkBudget(true)) return;
            system.AddMark(
                system.m_VehicleMarks,
                ref system.m_NextVehicleMark,
                Application.isMobilePlatform
                    ? MOBILE_MAX_VEHICLE_MARKS
                    : MAX_VEHICLE_MARKS,
                anchor,
                hitPoint,
                normal,
                size,
                Application.isMobilePlatform
                    ? MOBILE_VEHICLE_MARK_LIFETIME
                    : VEHICLE_MARK_LIFETIME,
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
            AddSurfaceHit(hitObject, hitPoint, incomingDirection, Vector3.zero, size);
        }

        public static void AddSurfaceHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            Vector3 hitNormal,
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
            hitPoint = ResolveSurfaceContact(
                collider,
                hitPoint,
                direction,
                hitNormal,
                out Vector3 normal
            );
            // Ground and slopes are supported. Skip only downward-facing ceilings because
            // they are easy to hit through thin geometry and rarely visible on mobile.
            if (Vector3.Dot(normal, Vector3.up) < MIN_SUPPORTED_SURFACE_NORMAL_Y) return;

            FranklinVehicleBulletDecalSystem system = GetOrCreate();
            if (system == null || !system.TryConsumeMarkBudget(false)) return;
            system.AddMark(
                system.m_SurfaceMarks,
                ref system.m_NextSurfaceMark,
                Application.isMobilePlatform
                    ? MOBILE_MAX_SURFACE_MARKS
                    : MAX_SURFACE_MARKS,
                collider.transform,
                hitPoint,
                normal,
                size,
                Application.isMobilePlatform
                    ? MOBILE_SURFACE_MARK_LIFETIME
                    : SURFACE_MARK_LIFETIME,
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

        /// <summary>
        /// Adds one large RPG scorch mark. Dynamic vehicle marks follow their collider;
        /// static wall and ground marks remain attached to the hit surface.
        /// </summary>
        public static void AddExplosionHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            bool followsVehicle,
            float size)
        {
            AddExplosionHit(
                hitObject,
                hitPoint,
                incomingDirection,
                Vector3.zero,
                followsVehicle,
                size
            );
        }

        public static void AddExplosionHit(
            GameObject hitObject,
            Vector3 hitPoint,
            Vector3 incomingDirection,
            Vector3 hitNormal,
            bool followsVehicle,
            float size)
        {
            if (hitObject == null || size <= 0f) return;

            Collider collider = hitObject.GetComponent<Collider>();
            if (collider == null) collider = hitObject.GetComponentInParent<Collider>();
            if (collider == null || collider.isTrigger) return;

            Rigidbody body = collider.attachedRigidbody;
            if (!followsVehicle && body != null && !body.isKinematic) return;

            Vector3 direction = incomingDirection.sqrMagnitude > 0.000001f
                ? incomingDirection.normalized
                : Vector3.forward;
            hitPoint = ResolveSurfaceContact(
                collider,
                hitPoint,
                direction,
                hitNormal,
                out Vector3 normal
            );
            if (!followsVehicle &&
                Vector3.Dot(normal, Vector3.up) < MIN_SUPPORTED_SURFACE_NORMAL_Y)
            {
                return;
            }

            float minimumSize = followsVehicle ? 0.9f : 1.35f;
            size = FitExplosionMarkToSurface(
                collider,
                hitPoint,
                normal,
                size,
                minimumSize
            );
            if (size <= 0f) return;

            FranklinVehicleBulletDecalSystem system = GetOrCreate();
            if (system == null || !system.TryConsumeExplosionMarkBudget()) return;
            Mark[] marks = followsVehicle
                ? system.m_ExplosionVehicleMarks
                : system.m_ExplosionSurfaceMarks;
            ref int nextMark = ref (followsVehicle
                ? ref system.m_NextExplosionVehicleMark
                : ref system.m_NextExplosionSurfaceMark);
            int capacity = followsVehicle
                ? Application.isMobilePlatform
                    ? MOBILE_MAX_EXPLOSION_VEHICLE_MARKS
                    : MAX_EXPLOSION_VEHICLE_MARKS
                : Application.isMobilePlatform
                    ? MOBILE_MAX_EXPLOSION_SURFACE_MARKS
                    : MAX_EXPLOSION_SURFACE_MARKS;
            system.AddMark(
                marks,
                ref nextMark,
                capacity,
                collider.transform,
                hitPoint,
                normal,
                size,
                Application.isMobilePlatform
                    ? MOBILE_EXPLOSION_MARK_LIFETIME
                    : EXPLOSION_MARK_LIFETIME,
                minimumSize,
                followsVehicle ? 1.75f : 2.6f,
                false
            );
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

        private static Vector3 ResolveSurfaceContact(
            Collider collider,
            Vector3 point,
            Vector3 incomingDirection,
            Vector3 reportedNormal,
            out Vector3 surfaceNormal)
        {
            surfaceNormal = reportedNormal.sqrMagnitude > 0.000001f
                ? OrientNormal(reportedNormal, incomingDirection)
                : -incomingDirection;
            if (collider == null) return point;

            // Rigidbody ContinuousSpeculative contacts can be slightly separated from the
            // rendered surface. Reproject against the exact collider instead of trusting the
            // projectile root/contact approximation.
            Ray normalRay = new(
                point + surfaceNormal * CONTACT_SNAP_OFFSET,
                -surfaceNormal
            );
            if (collider.Raycast(
                    normalRay,
                    out RaycastHit normalHit,
                    CONTACT_SNAP_DISTANCE))
            {
                surfaceNormal = OrientNormal(normalHit.normal, incomingDirection);
                return normalHit.point;
            }

            Ray incomingRay = new(
                point - incomingDirection * CONTACT_SNAP_OFFSET,
                incomingDirection
            );
            if (collider.Raycast(
                    incomingRay,
                    out RaycastHit incomingHit,
                    CONTACT_SNAP_DISTANCE))
            {
                surfaceNormal = OrientNormal(incomingHit.normal, incomingDirection);
                return incomingHit.point;
            }

            Ray shortRay = new(
                point - incomingDirection * NORMAL_RAY_OFFSET,
                incomingDirection
            );
            if (collider.Raycast(shortRay, out RaycastHit shortHit, NORMAL_RAY_DISTANCE))
            {
                surfaceNormal = OrientNormal(shortHit.normal, incomingDirection);
                return shortHit.point;
            }

            Vector3 closestPoint = collider.ClosestPoint(point);
            Vector3 closestNormal = point - closestPoint;
            if (closestNormal.sqrMagnitude > 0.000001f &&
                closestNormal.sqrMagnitude <=
                CONTACT_SNAP_DISTANCE * CONTACT_SNAP_DISTANCE)
            {
                surfaceNormal = OrientNormal(closestNormal, incomingDirection);
                return closestPoint;
            }

            return point;
        }

        private static Vector3 OrientNormal(Vector3 normal, Vector3 incomingDirection)
        {
            Vector3 result = normal.sqrMagnitude > 0.000001f
                ? normal.normalized
                : -incomingDirection;
            return Vector3.Dot(result, incomingDirection) > 0f ? -result : result;
        }

        private static float FitExplosionMarkToSurface(
            Collider collider,
            Vector3 center,
            Vector3 normal,
            float requestedSize,
            float minimumSize)
        {
            if (collider == null) return 0f;

            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.96f
                ? Vector3.right
                : Vector3.up;
            Vector3 tangent = Vector3.Cross(reference, normal).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            float candidate = Mathf.Max(requestedSize, minimumSize);

            for (int attempt = 0; attempt < 4; ++attempt)
            {
                if (HasExplosionSurfaceSupport(
                        collider,
                        center,
                        normal,
                        tangent,
                        bitangent,
                        candidate))
                {
                    return candidate;
                }

                candidate *= 0.75f;
                if (candidate < minimumSize) break;
            }

            // A large planar quad on a narrow edge looks detached. Omitting that one mark is
            // less distracting and cheaper than drawing pixels over empty space.
            return 0f;
        }

        private static bool HasExplosionSurfaceSupport(
            Collider collider,
            Vector3 center,
            Vector3 normal,
            Vector3 tangent,
            Vector3 bitangent,
            float size)
        {
            float radius = size * 0.42f;
            for (int i = 0; i < 8; ++i)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 radial = tangent * Mathf.Cos(angle) +
                                 bitangent * Mathf.Sin(angle);
                Vector3 sample = center + radial * radius;
                Ray supportRay = new(
                    sample + normal * SUPPORT_RAY_OFFSET,
                    -normal
                );
                if (!collider.Raycast(
                        supportRay,
                        out RaycastHit supportHit,
                        SUPPORT_RAY_DISTANCE))
                {
                    return false;
                }

                if (Vector3.Dot(supportHit.normal.normalized, normal) < 0.65f)
                    return false;
            }

            return true;
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
            this.m_ExplosionSurfaceMaterial = LoadMaterial(
                EXPLOSION_SURFACE_MATERIAL_RESOURCE,
                EXPLOSION_SURFACE_TEXTURE_RESOURCE,
                "RPG Explosion Surface Scorch Runtime",
                out this.m_OwnsExplosionSurfaceMaterial
            );
            this.m_ExplosionVehicleMaterial = LoadMaterial(
                EXPLOSION_VEHICLE_MATERIAL_RESOURCE,
                EXPLOSION_VEHICLE_TEXTURE_RESOURCE,
                "RPG Explosion Vehicle Scorch Runtime",
                out this.m_OwnsExplosionVehicleMaterial
            );

            // No marks exist yet. Stay out of Unity's LateUpdate list until the first hit.
            this.enabled = false;
        }

        private void LateUpdate()
        {
            if (this.m_Quad == null)
            {
                this.enabled = false;
                return;
            }

            float now = Time.unscaledTime;
            if (this.m_Camera != null &&
                (!this.m_Camera.isActiveAndEnabled ||
                 !this.m_Camera.CompareTag("MainCamera")))
            {
                this.m_Camera = null;
                this.m_NextCameraLookup = 0f;
            }
            if (this.m_Camera == null && now >= this.m_NextCameraLookup)
            {
                this.m_Camera = Camera.main;
                this.m_NextCameraLookup = now + CAMERA_LOOKUP_INTERVAL;
            }
            Vector3 cameraPosition = this.m_Camera != null
                ? this.m_Camera.transform.position
                : Vector3.zero;
            bool useDistanceCulling = this.m_Camera != null;
            float drawDistance = Application.isMobilePlatform
                ? MOBILE_MAX_DRAW_DISTANCE
                : MAX_DRAW_DISTANCE;
            float maxDistanceSquared = drawDistance * drawDistance;
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
            bool hasExplosionVehicleMarks = this.RenderMarks(
                this.m_ExplosionVehicleMarks,
                this.m_ExplosionVehicleMatrices,
                this.m_ExplosionVehicleMaterial,
                now,
                cameraPosition,
                useDistanceCulling,
                maxDistanceSquared
            );
            bool hasExplosionSurfaceMarks = this.RenderMarks(
                this.m_ExplosionSurfaceMarks,
                this.m_ExplosionSurfaceMatrices,
                this.m_ExplosionSurfaceMaterial,
                now,
                cameraPosition,
                useDistanceCulling,
                maxDistanceSquared
            );

            if (!hasVehicleMarks && !hasSurfaceMarks &&
                !hasExplosionVehicleMarks && !hasExplosionSurfaceMarks)
                this.enabled = false;
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

            if (count == 0 || this.m_Camera == null) return hasActiveMarks;

            RenderParams renderParams = new(material)
            {
                // Without an explicit camera Graphics.RenderMeshInstanced submits this
                // transparent batch to every camera (including minimap/reflection cameras).
                // Bullet marks are gameplay-camera feedback and do not need those extra passes.
                camera = this.m_Camera,
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
            int capacity,
            Transform anchor,
            Vector3 worldPosition,
            Vector3 worldNormal,
            float size,
            float lifetime,
            float minimumSize,
            float maximumSize,
            bool varyScale = true)
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
            float scaleVariation = varyScale
                ? Mathf.Lerp(
                    0.88f,
                    1.12f,
                    Mathf.Repeat(nextMark * 0.6180339f, 1f)
                )
                : 1f;

            int boundedCapacity = Mathf.Clamp(capacity, 1, marks.Length);
            nextMark %= boundedCapacity;
            marks[nextMark] = new Mark
            {
                Active = true,
                Anchor = anchor,
                LocalPosition = anchor.InverseTransformPoint(
                    worldPosition + normal * SURFACE_OFFSET
                ),
                LocalRotation = Quaternion.Inverse(anchor.rotation) * worldRotation,
                Size = Mathf.Clamp(size * scaleVariation, minimumSize, maximumSize),
                // Decals are a visual budget, so expire them in real time even if a weapon
                // wheel, pause or slow-motion flow stops gameplay time.
                ExpireAt = Time.unscaledTime + lifetime
            };
            nextMark = (nextMark + 1) % boundedCapacity;
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
                this.m_ExplosionMarksThisFrame = 0;
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

        private bool TryConsumeExplosionMarkBudget()
        {
            int frame = Time.frameCount;
            if (this.m_BudgetFrame != frame)
            {
                this.m_BudgetFrame = frame;
                this.m_VehicleMarksThisFrame = 0;
                this.m_SurfaceMarksThisFrame = 0;
                this.m_ExplosionMarksThisFrame = 0;
            }

            if (this.m_ExplosionMarksThisFrame >= MAX_EXPLOSION_MARKS_PER_FRAME)
                return false;
            this.m_ExplosionMarksThisFrame += 1;
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
            if (this.m_OwnsExplosionVehicleMaterial &&
                this.m_ExplosionVehicleMaterial != null)
            {
                Destroy(this.m_ExplosionVehicleMaterial);
            }
            if (this.m_OwnsExplosionSurfaceMaterial &&
                this.m_ExplosionSurfaceMaterial != null)
            {
                Destroy(this.m_ExplosionSurfaceMaterial);
            }
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
