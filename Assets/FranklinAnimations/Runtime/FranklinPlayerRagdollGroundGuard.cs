using System;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Prevents the Player's GC2 ragdoll from tunnelling through thin ground at
    /// vehicle-ejection speeds. The guard sleeps outside ragdoll and performs one
    /// allocation-free hips sweep per physics tick while active.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinPlayerRagdollGroundGuard : MonoBehaviour
    {
        private const int HitCapacity = 12;
        private const int OverlapCapacity = 12;

        [SerializeField] private Character m_Character;
        [SerializeField] private LayerMask m_GroundLayers = ~0;

        [Header("Mobile Ragdoll Physics")]
        [SerializeField, Min(0.02f)] private float m_HipsProbeRadius = 0.1f;
        [SerializeField, Min(0f)] private float m_GroundClearance = 0.025f;
        [SerializeField, Min(0.05f)] private float m_MaximumCorrection = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_MinimumGroundNormal = 0.45f;
        [SerializeField, Min(1)] private int m_CoreSolverIterations = 12;
        [SerializeField, Min(1)] private int m_CoreSolverVelocityIterations = 4;
        [SerializeField, Min(1)] private int m_LimbSolverIterations = 8;
        [SerializeField, Min(1)] private int m_LimbSolverVelocityIterations = 2;

        private readonly RaycastHit[] m_Hits = new RaycastHit[HitCapacity];
        private readonly Collider[] m_Overlaps = new Collider[OverlapCapacity];

        private Rigidbody[] m_RagdollBodies = Array.Empty<Rigidbody>();
        private Animator m_Animator;
        private Rigidbody m_HipsBody;
        private Collider m_HipsCollider;
        private Vector3 m_PreviousHipsCenter;
        private bool m_IsGuarding;

        private void Reset()
        {
            this.m_Character = this.GetComponent<Character>();
        }

        private void Awake()
        {
            if (this.m_Character == null)
                this.m_Character = this.GetComponent<Character>();
        }

        private void OnEnable()
        {
            if (this.m_Character == null)
                this.m_Character = this.GetComponent<Character>();

            if (this.m_Character == null) return;

            this.m_Character.Ragdoll.EventAfterStartRagdoll += this.OnRagdollStarted;
            this.m_Character.Ragdoll.EventAfterFinishRecover += this.OnRagdollFinished;

            if (this.m_Character.Ragdoll.IsRagdoll) this.OnRagdollStarted();
        }

        private void OnDisable()
        {
            if (this.m_Character != null)
            {
                this.m_Character.Ragdoll.EventAfterStartRagdoll -= this.OnRagdollStarted;
                this.m_Character.Ragdoll.EventAfterFinishRecover -= this.OnRagdollFinished;
            }

            this.StopGuarding();
        }

        private void FixedUpdate()
        {
            if (!this.m_IsGuarding) return;
            if (this.m_Character == null || !this.m_Character.Ragdoll.IsRagdoll)
            {
                this.StopGuarding();
                return;
            }

            if (this.m_HipsBody == null || this.m_HipsCollider == null)
            {
                this.CacheAndConfigureRagdoll();
                if (this.m_HipsBody == null || this.m_HipsCollider == null) return;
            }

            Vector3 currentCenter = this.m_HipsBody.worldCenterOfMass;
            Vector3 correction = this.GetSweptGroundCorrection(currentCenter);

            if (correction.sqrMagnitude <= 0.000001f)
                correction = this.GetOverlapCorrection();

            if (correction.sqrMagnitude > 0.000001f)
            {
                correction = Vector3.ClampMagnitude(
                    correction,
                    this.m_MaximumCorrection
                );

                this.TranslateWholeRagdoll(correction);
                currentCenter += correction;
            }

            this.m_PreviousHipsCenter = currentCenter;
        }

        private void OnRagdollStarted()
        {
            this.CacheAndConfigureRagdoll();
            this.m_IsGuarding = this.m_HipsBody != null && this.m_HipsCollider != null;

            if (this.m_IsGuarding)
                this.m_PreviousHipsCenter = this.m_HipsBody.worldCenterOfMass;
        }

        private void OnRagdollFinished()
        {
            this.StopGuarding();
        }

        private void CacheAndConfigureRagdoll()
        {
            this.m_Animator = this.m_Character != null
                ? this.m_Character.Animim.Animator
                : null;

            if (this.m_Animator == null)
            {
                this.m_RagdollBodies = Array.Empty<Rigidbody>();
                this.m_HipsBody = null;
                this.m_HipsCollider = null;
                return;
            }

            this.m_RagdollBodies = this.m_Animator.GetComponentsInChildren<Rigidbody>(true);

            Transform hips = this.m_Animator.isHuman
                ? this.m_Animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;

            this.m_HipsBody = hips != null ? hips.GetComponent<Rigidbody>() : null;
            this.m_HipsCollider = hips != null ? hips.GetComponent<Collider>() : null;

            for (int i = 0; i < this.m_RagdollBodies.Length; ++i)
            {
                Rigidbody body = this.m_RagdollBodies[i];
                if (body == null) continue;

                bool isCore = this.IsCoreBody(body.transform);
                body.collisionDetectionMode = isCore
                    ? CollisionDetectionMode.ContinuousDynamic
                    : CollisionDetectionMode.ContinuousSpeculative;
                body.solverIterations = isCore
                    ? this.m_CoreSolverIterations
                    : this.m_LimbSolverIterations;
                body.solverVelocityIterations = isCore
                    ? this.m_CoreSolverVelocityIterations
                    : this.m_LimbSolverVelocityIterations;
            }
        }

        private bool IsCoreBody(Transform candidate)
        {
            if (this.m_Animator == null || !this.m_Animator.isHuman) return candidate == this.m_HipsBody?.transform;

            return candidate == this.m_Animator.GetBoneTransform(HumanBodyBones.Hips) ||
                   candidate == this.m_Animator.GetBoneTransform(HumanBodyBones.Spine) ||
                   candidate == this.m_Animator.GetBoneTransform(HumanBodyBones.Chest) ||
                   candidate == this.m_Animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private Vector3 GetSweptGroundCorrection(Vector3 currentCenter)
        {
            Vector3 movement = currentCenter - this.m_PreviousHipsCenter;
            float distance = movement.magnitude;
            if (distance <= 0.0001f) return Vector3.zero;

            int hitCount = Physics.SphereCastNonAlloc(
                this.m_PreviousHipsCenter,
                this.m_HipsProbeRadius,
                movement / distance,
                this.m_Hits,
                distance + this.m_GroundClearance,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hit = this.m_Hits[i];
                if (!this.IsValidGround(hit.collider, hit.normal)) continue;
                if (hit.distance >= nearestDistance) continue;

                nearest = hit;
                nearestDistance = hit.distance;
                found = true;
            }

            if (!found) return Vector3.zero;

            float requiredDistance = this.m_HipsProbeRadius + this.m_GroundClearance;
            float currentDistance = Vector3.Dot(
                currentCenter - nearest.point,
                nearest.normal
            );

            float penetration = requiredDistance - currentDistance;
            return penetration > 0f ? nearest.normal * penetration : Vector3.zero;
        }

        private Vector3 GetOverlapCorrection()
        {
            Bounds bounds = this.m_HipsCollider.bounds;
            float radius = Mathf.Max(
                this.m_HipsProbeRadius,
                Mathf.Min(bounds.extents.x, bounds.extents.z)
            );

            int overlapCount = Physics.OverlapSphereNonAlloc(
                bounds.center,
                radius + this.m_GroundClearance,
                this.m_Overlaps,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            Vector3 bestCorrection = Vector3.zero;
            float bestUpwardDistance = 0f;

            for (int i = 0; i < overlapCount; ++i)
            {
                Collider candidate = this.m_Overlaps[i];
                if (!this.IsValidGround(candidate, Vector3.up)) continue;

                bool overlaps = Physics.ComputePenetration(
                    this.m_HipsCollider,
                    this.m_HipsCollider.transform.position,
                    this.m_HipsCollider.transform.rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out Vector3 direction,
                    out float distance
                );

                if (!overlaps || Vector3.Dot(direction, Vector3.up) < this.m_MinimumGroundNormal)
                    continue;

                float upwardDistance = Vector3.Dot(direction * distance, Vector3.up);
                if (upwardDistance <= bestUpwardDistance) continue;

                bestUpwardDistance = upwardDistance;
                bestCorrection = direction * (distance + this.m_GroundClearance);
            }

            return bestCorrection;
        }

        private bool IsValidGround(Collider candidate, Vector3 normal)
        {
            if (candidate == null || candidate.isTrigger) return false;
            if (Vector3.Dot(normal, Vector3.up) < this.m_MinimumGroundNormal) return false;

            if (this.m_Animator != null && candidate.transform.IsChildOf(this.m_Animator.transform))
                return false;

            Rigidbody attachedBody = candidate.attachedRigidbody;
            return attachedBody == null || attachedBody.isKinematic;
        }

        private void TranslateWholeRagdoll(Vector3 correction)
        {
            for (int i = 0; i < this.m_RagdollBodies.Length; ++i)
            {
                Rigidbody body = this.m_RagdollBodies[i];
                if (body == null || body.isKinematic) continue;
                body.position += correction;
            }
        }

        private void StopGuarding()
        {
            this.m_IsGuarding = false;
            this.m_RagdollBodies = Array.Empty<Rigidbody>();
            this.m_Animator = null;
            this.m_HipsBody = null;
            this.m_HipsCollider = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_HipsProbeRadius = Mathf.Max(0.02f, this.m_HipsProbeRadius);
            this.m_GroundClearance = Mathf.Max(0f, this.m_GroundClearance);
            this.m_MaximumCorrection = Mathf.Max(0.05f, this.m_MaximumCorrection);
            this.m_CoreSolverIterations = Mathf.Max(1, this.m_CoreSolverIterations);
            this.m_CoreSolverVelocityIterations = Mathf.Max(1, this.m_CoreSolverVelocityIterations);
            this.m_LimbSolverIterations = Mathf.Max(1, this.m_LimbSolverIterations);
            this.m_LimbSolverVelocityIterations = Mathf.Max(1, this.m_LimbSolverVelocityIterations);
        }
#endif
    }
}
