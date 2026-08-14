using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Hardens GC2's generated bone rigidbodies against high-speed tunnelling and
    /// restores the pelvis above a static supporting surface only after the
    /// physics solver has demonstrably crossed that surface.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    [DefaultExecutionOrder(-250)]
    public sealed class FranklinRagdollGroundGuard : MonoBehaviour
    {
        [SerializeField] private LayerMask m_GroundLayers = ~0;
        [SerializeField, Min(1f)] private float m_GroundProbeHeight = 3f;
        [SerializeField, Min(1f)] private float m_GroundProbeDistance = 12f;
        [SerializeField, Min(0.01f)] private float m_MaximumPelvisPenetration = 0.08f;
        [SerializeField, Min(0f)] private float m_SurfaceClearance = 0.025f;
        [SerializeField, Min(1)] private int m_SolverIterations = 12;
        [SerializeField, Min(1)] private int m_SolverVelocityIterations = 4;

        private readonly RaycastHit[] m_GroundHits = new RaycastHit[16];
        private Character m_Character;
        private Animator m_Animator;
        private Rigidbody[] m_Bodies;
        private Rigidbody m_HipsBody;
        private Collider m_HipsCollider;
        private Collider m_CachedGroundCollider;
        private Vector3 m_CachedGroundPoint;
        private Vector3 m_CachedGroundNormal = Vector3.up;
        private bool m_WasRagdoll;

        public void Initialize(Character character)
        {
            this.m_Character = character;
        }

        private void Awake()
        {
            // Player.prefab owns the newer mobile guard. Never let both guards
            // change solver settings and translate the same ragdoll in one tick.
            if (this.TryGetComponent(out FranklinPlayerRagdollGroundGuard _))
            {
                this.enabled = false;
                Destroy(this);
                return;
            }

            if (this.m_Character == null) this.m_Character = this.GetComponent<Character>();
        }

        private void FixedUpdate()
        {
            bool isRagdoll = this.m_Character != null &&
                             this.m_Character.Ragdoll.IsRagdoll;
            if (!isRagdoll)
            {
                this.m_WasRagdoll = false;
                this.m_CachedGroundCollider = null;
                return;
            }

            if (!this.m_WasRagdoll)
            {
                this.CacheRagdollBodies();
                this.HardenRagdollBodies();
                this.m_WasRagdoll = true;
            }
            else
            {
                this.HardenRagdollBodies();
            }

            this.PreventPelvisGroundTunnelling();
        }

        private void CacheRagdollBodies()
        {
            this.m_Animator = this.m_Character?.Animim?.Animator;
            if (this.m_Animator == null)
            {
                this.m_Bodies = System.Array.Empty<Rigidbody>();
                this.m_HipsBody = null;
                this.m_HipsCollider = null;
                return;
            }

            this.m_Bodies = this.m_Animator.GetComponentsInChildren<Rigidbody>(true);
            Transform hips = this.m_Animator.isHuman
                ? this.m_Animator.GetBoneTransform(HumanBodyBones.Hips)
                : this.m_Animator.transform;
            this.m_HipsBody = hips != null ? hips.GetComponent<Rigidbody>() : null;
            this.m_HipsCollider = hips != null ? hips.GetComponent<Collider>() : null;
        }

        private void HardenRagdollBodies()
        {
            if (this.m_Bodies == null) return;
            foreach (Rigidbody body in this.m_Bodies)
            {
                if (body == null || body.isKinematic) continue;
                body.detectCollisions = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.maxDepenetrationVelocity = Mathf.Max(
                    body.maxDepenetrationVelocity,
                    12f
                );
                body.solverIterations = Mathf.Max(
                    body.solverIterations,
                    this.m_SolverIterations
                );
                body.solverVelocityIterations = Mathf.Max(
                    body.solverVelocityIterations,
                    this.m_SolverVelocityIterations
                );
            }
        }

        private void PreventPelvisGroundTunnelling()
        {
            if (this.m_HipsBody == null || this.m_HipsBody.isKinematic ||
                this.m_HipsCollider == null || !this.m_HipsCollider.enabled)
            {
                return;
            }

            Vector3 reference = this.m_HipsCollider.bounds.center;
            if (this.TryFindSupportingGround(reference, out RaycastHit groundHit))
            {
                this.m_CachedGroundCollider = groundHit.collider;
                this.m_CachedGroundPoint = groundHit.point;
                this.m_CachedGroundNormal = groundHit.normal.normalized;
            }
            else if (!this.IsCachedGroundStillBelow(reference))
            {
                return;
            }

            Vector3 normal = this.m_CachedGroundNormal.sqrMagnitude > 0.001f
                ? this.m_CachedGroundNormal.normalized
                : Vector3.up;
            Bounds hipsBounds = this.m_HipsCollider.bounds;
            float pelvisBottom = Vector3.Dot(hipsBounds.center, normal) -
                                 Vector3.Dot(
                                     hipsBounds.extents,
                                     new Vector3(
                                         Mathf.Abs(normal.x),
                                         Mathf.Abs(normal.y),
                                         Mathf.Abs(normal.z)
                                     )
                                 );
            float groundPlane = Vector3.Dot(this.m_CachedGroundPoint, normal);
            float penetration = groundPlane + this.m_SurfaceClearance - pelvisBottom;
            if (penetration <= this.m_MaximumPelvisPenetration) return;

            Vector3 correction = normal * penetration;
            foreach (Rigidbody body in this.m_Bodies)
            {
                if (body == null || body.isKinematic) continue;
                body.position += correction;
                float inwardSpeed = Vector3.Dot(body.linearVelocity, normal);
                if (inwardSpeed < 0f)
                    body.linearVelocity -= normal * inwardSpeed;
            }
        }

        private bool TryFindSupportingGround(
            Vector3 reference,
            out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin = reference + Vector3.up * this.m_GroundProbeHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                this.m_GroundHits,
                this.m_GroundProbeHeight + this.m_GroundProbeDistance,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );
            float closest = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = this.m_GroundHits[index];
                Collider collider = candidate.collider;
                if (collider == null || this.IsOwnRagdollCollider(collider)) continue;
                Rigidbody support = collider.attachedRigidbody;
                if (support != null && !support.isKinematic) continue;
                if (Vector3.Dot(candidate.normal, Vector3.up) < 0.35f) continue;
                if (candidate.distance >= closest) continue;
                closest = candidate.distance;
                groundHit = candidate;
                found = true;
            }
            return found;
        }

        private bool IsCachedGroundStillBelow(Vector3 reference)
        {
            if (this.m_CachedGroundCollider == null ||
                !this.m_CachedGroundCollider.enabled)
            {
                return false;
            }
            Vector3 normal = this.m_CachedGroundNormal.sqrMagnitude > 0.001f
                ? this.m_CachedGroundNormal.normalized
                : Vector3.up;
            float signedDistance = Vector3.Dot(
                reference - this.m_CachedGroundPoint,
                normal
            );
            Vector3 projected = reference - normal * signedDistance;
            return Vector3.Distance(
                this.m_CachedGroundCollider.ClosestPoint(projected),
                projected
            ) <= 0.75f;
        }

        private bool IsOwnRagdollCollider(Collider collider)
        {
            return collider.transform == this.transform ||
                   collider.transform.IsChildOf(this.transform) ||
                   this.m_Animator != null &&
                   collider.transform.IsChildOf(this.m_Animator.transform);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_GroundProbeHeight = Mathf.Max(1f, this.m_GroundProbeHeight);
            this.m_GroundProbeDistance = Mathf.Max(1f, this.m_GroundProbeDistance);
            this.m_MaximumPelvisPenetration = Mathf.Max(
                0.01f,
                this.m_MaximumPelvisPenetration
            );
            this.m_SurfaceClearance = Mathf.Max(0f, this.m_SurfaceClearance);
            this.m_SolverIterations = Mathf.Max(1, this.m_SolverIterations);
            this.m_SolverVelocityIterations = Mathf.Max(
                1,
                this.m_SolverVelocityIterations
            );
        }
#endif
    }
}
