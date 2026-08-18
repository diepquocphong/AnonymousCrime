using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Shared per-character GC2 ragdoll impulse lease used by explosions and
    /// vehicle body hits. It only ticks while a temporary ragdoll is active and
    /// caches its bodies/colliders once per Animator model.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-240)]
    public sealed class FranklinExplosionRagdollImpulse : MonoBehaviour
    {
        private Character m_Character;
        private Traits m_Traits;
        private Rigidbody[] m_Bodies = Array.Empty<Rigidbody>();
        private Collider[] m_BodyColliders = Array.Empty<Collider>();
        private int m_AnimatorId = -1;
        private Vector3 m_PendingVelocity;
        private float m_RecoverAt;
        private float m_ForceRecoverAt;
        private float m_NextClearanceCheckAt;
        private float m_NextVehicleImpactAt;
        private bool m_StartPending;
        private bool m_RecoverPending;
        private bool m_ShouldRecover;
        private bool m_OwnsRagdoll;
        private bool m_RecoveryBlocked;
        private bool m_IssuingOwnedRecovery;
        private int m_LeaseVersion;
        private Collider m_RecoveryBlocker;

        public static void Apply(
            Character character,
            Vector3 center,
            float velocity,
            float upwardModifier,
            float duration,
            bool shouldRecover)
        {
            if (character == null || velocity <= 0f ||
                character.Ragdoll.Get<RagdollDefault>() == null)
            {
                return;
            }

            Vector3 away = Vector3.ProjectOnPlane(
                character.transform.position - center,
                Vector3.up
            );
            if (away.sqrMagnitude < 0.001f) away = character.transform.forward;
            away.Normalize();
            Vector3 impulse = (
                away + Vector3.up * Mathf.Clamp(upwardModifier, 0f, 1f)
            ).normalized * Mathf.Clamp(velocity, 0f, 6f);

            GetOrCreate(character).RequestVelocity(
                impulse,
                duration,
                shouldRecover,
                null
            );
        }

        /// <summary>
        /// Adds a coherent vehicle-hit velocity and shares the same recovery
        /// deadline as explosion impulses. The optional blocker briefly delays
        /// recovery while the Character remains physically beneath the vehicle.
        /// </summary>
        public static bool ApplyVehicleVelocity(
            Character character,
            Vector3 velocity,
            float maximumVelocity,
            float duration,
            float cooldown,
            Collider recoveryBlocker)
        {
            if (character == null || character.IsDead ||
                character.Ragdoll.Get<RagdollDefault>() == null)
            {
                return false;
            }

            FranklinExplosionRagdollImpulse reaction = GetOrCreate(character);
            float now = Time.unscaledTime;
            if (reaction.m_RecoverPending ||
                now < reaction.m_NextVehicleImpactAt)
            {
                return false;
            }

            reaction.m_NextVehicleImpactAt = now + Mathf.Max(0f, cooldown);
            reaction.RequestVelocity(
                Vector3.ClampMagnitude(
                    velocity,
                    Mathf.Max(0.1f, maximumVelocity)
                ),
                duration,
                true,
                recoveryBlocker
            );
            return true;
        }

        private static FranklinExplosionRagdollImpulse GetOrCreate(
            Character character)
        {
            FranklinExplosionRagdollImpulse reaction =
                character.GetComponent<FranklinExplosionRagdollImpulse>();
            if (reaction == null)
            {
                reaction = character.gameObject
                    .AddComponent<FranklinExplosionRagdollImpulse>();
            }
            reaction.EnsureLifecycleGuard();
            return reaction;
        }

        private void Awake()
        {
            this.m_Character = this.GetComponent<Character>();
            this.m_Traits = this.GetComponent<Traits>() ??
                            this.GetComponentInChildren<Traits>(true);
            if (this.m_Character != null)
            {
                this.m_Character.Ragdoll.EventBeforeStartRagdoll +=
                    this.OnBeforeStartRagdoll;
                this.m_Character.Ragdoll.EventBeforeStartRecover +=
                    this.OnBeforeStartRecover;
                this.m_Character.Ragdoll.EventAfterFinishRecover +=
                    this.OnRagdollFinished;
            }
            this.enabled = false;
        }

        private void OnDestroy()
        {
            if (this.m_Character != null)
            {
                this.m_Character.Ragdoll.EventBeforeStartRagdoll -=
                    this.OnBeforeStartRagdoll;
                this.m_Character.Ragdoll.EventBeforeStartRecover -=
                    this.OnBeforeStartRecover;
                this.m_Character.Ragdoll.EventAfterFinishRecover -=
                    this.OnRagdollFinished;
            }
        }

        private void Update()
        {
            if (this.m_Character == null) return;
            if (this.m_StartPending || this.m_RecoverPending) return;

            if (!this.m_Character.Ragdoll.IsRagdoll)
            {
                this.ResetLease();
                this.enabled = false;
                return;
            }

            float now = Time.unscaledTime;
            if (now < this.m_RecoverAt) return;

            if (!this.m_ShouldRecover || !this.m_OwnsRagdoll ||
                this.m_Character.IsDead || this.IsHealthDepleted())
            {
                this.ResetLease();
                this.enabled = false;
                return;
            }

            if (now >= this.m_NextClearanceCheckAt)
            {
                this.m_NextClearanceCheckAt = now + 0.1f;
                this.m_RecoveryBlocked = this.IsRecoveryBlocked();
            }
            if (this.m_RecoveryBlocked && now < this.m_ForceRecoverAt) return;

            _ = this.RecoverAsync();
        }

        private void RequestVelocity(
            Vector3 velocity,
            float duration,
            bool shouldRecover,
            Collider recoveryBlocker)
        {
            if (this.m_Character == null || this.m_RecoverPending) return;

            float deadline = Time.unscaledTime +
                             Mathf.Clamp(duration, 0.25f, 4f);
            this.m_PendingVelocity += Vector3.ClampMagnitude(velocity, 6f);
            this.m_RecoverAt = Mathf.Max(this.m_RecoverAt, deadline);
            this.m_ForceRecoverAt = Mathf.Max(
                this.m_ForceRecoverAt,
                deadline + 1.5f
            );
            this.m_ShouldRecover |= shouldRecover;
            if (recoveryBlocker != null) this.m_RecoveryBlocker = recoveryBlocker;
            this.enabled = true;

            if (this.m_Character.Ragdoll.IsRagdoll)
            {
                this.EnsureCharacterProxy();
                this.CacheBodies();
                this.ApplyPendingVelocity();
                return;
            }

            if (!this.m_StartPending)
            {
                int version = ++this.m_LeaseVersion;
                this.m_StartPending = true;
                _ = this.StartAndApplyAsync(version);
            }
        }

        private async Task StartAndApplyAsync(int version)
        {
            if (this.m_Character == null) return;
            try
            {
                bool startsOwnedRagdoll =
                    !this.m_Character.Ragdoll.IsRagdoll;
                this.EnsureCharacterProxy();
                Task startTask = startsOwnedRagdoll
                    ? this.m_Character.Ragdoll.StartRagdoll()
                    : Task.CompletedTask;
                this.m_OwnsRagdoll |= startsOwnedRagdoll;
                this.CacheBodies();
                this.ApplyPendingVelocity();
                await startTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (version == this.m_LeaseVersion)
                    this.m_StartPending = false;
            }
        }

        private Task RecoverAsync()
        {
            return this.BeginOwnedRecoveryAsync(false);
        }

        internal Task RecoverForLifecycleAsync()
        {
            return this.BeginOwnedRecoveryAsync(true);
        }

        private Task BeginOwnedRecoveryAsync(bool immediate)
        {
            if (this.m_Character == null ||
                !this.m_Character.Ragdoll.IsRagdoll)
            {
                return Task.CompletedTask;
            }
            if (this.m_RecoverPending) return Task.CompletedTask;

            int version = ++this.m_LeaseVersion;
            this.ResetLease();
            this.m_RecoverPending = true;
            if (immediate) this.m_NextVehicleImpactAt = 0f;
            this.enabled = false;
            return this.RunOwnedRecoveryAsync(version, immediate);
        }

        private async Task RunOwnedRecoveryAsync(int version, bool immediate)
        {
            try
            {
                Task recoveryTask;
                this.m_IssuingOwnedRecovery = true;
                try
                {
                    recoveryTask = immediate
                        ? this.m_Character.Ragdoll.StopRagdollImmediate()
                        : this.m_Character.Ragdoll.StartRecover();
                }
                finally
                {
                    this.m_IssuingOwnedRecovery = false;
                }
                await recoveryTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (version == this.m_LeaseVersion)
                {
                    this.ResetLease();
                    this.enabled = false;
                }
            }
        }

        private void ApplyPendingVelocity()
        {
            Vector3 velocity = Vector3.ClampMagnitude(
                this.m_PendingVelocity,
                6f
            );
            this.m_PendingVelocity = Vector3.zero;
            for (int index = 0; index < this.m_Bodies.Length; ++index)
            {
                Rigidbody body = this.m_Bodies[index];
                if (body == null || body.isKinematic) continue;
                body.linearVelocity = Vector3.ClampMagnitude(
                    body.linearVelocity + velocity,
                    6f
                );
            }
        }

        private bool IsRecoveryBlocked()
        {
            if (this.m_RecoveryBlocker == null ||
                !this.m_RecoveryBlocker.enabled)
            {
                return false;
            }

            Bounds blockerBounds = this.m_RecoveryBlocker.bounds;
            for (int index = 0; index < this.m_BodyColliders.Length; ++index)
            {
                Collider bodyCollider = this.m_BodyColliders[index];
                if (bodyCollider != null && bodyCollider.enabled &&
                    blockerBounds.Intersects(bodyCollider.bounds))
                {
                    return true;
                }
            }
            return false;
        }

        private void CacheBodies()
        {
            Animator animator = this.m_Character?.Animim?.Animator;
            if (animator == null)
            {
                this.m_Bodies = Array.Empty<Rigidbody>();
                this.m_BodyColliders = Array.Empty<Collider>();
                this.m_AnimatorId = -1;
                return;
            }

            int animatorId = animator.GetInstanceID();
            if (animatorId == this.m_AnimatorId && this.m_Bodies.Length > 0)
                return;

            this.m_AnimatorId = animatorId;
            this.m_Bodies = animator.GetComponentsInChildren<Rigidbody>(true);
            this.m_BodyColliders = new Collider[this.m_Bodies.Length];
            for (int index = 0; index < this.m_Bodies.Length; ++index)
            {
                Rigidbody body = this.m_Bodies[index];
                this.m_BodyColliders[index] = body != null
                    ? body.GetComponent<Collider>()
                    : null;
            }
        }

        private void EnsureCharacterProxy()
        {
            Animator animator = this.m_Character?.Animim?.Animator;
            if (animator == null) return;
            FranklinRagdollCharacterProxy proxy =
                animator.GetComponent<FranklinRagdollCharacterProxy>();
            if (proxy == null)
            {
                proxy = animator.gameObject
                    .AddComponent<FranklinRagdollCharacterProxy>();
            }
            proxy.Initialize(this.m_Character);
        }

        private void EnsureLifecycleGuard()
        {
            if (this.m_Character == null) return;
            FranklinRagdollLifecycleGuard lifecycleGuard =
                this.m_Character.GetComponent<FranklinRagdollLifecycleGuard>();
            if (lifecycleGuard == null)
            {
                lifecycleGuard = this.m_Character.gameObject
                    .AddComponent<FranklinRagdollLifecycleGuard>();
            }
            lifecycleGuard.Initialize(this.m_Character);
        }

        private void OnBeforeStartRagdoll()
        {
            this.EnsureCharacterProxy();
            this.EnsureLifecycleGuard();
        }

        private void OnBeforeStartRecover()
        {
            if (this.m_IssuingOwnedRecovery) return;

            ++this.m_LeaseVersion;
            this.ResetLease();
            // EventAfterFinishRecover is the only point at which a new lease may
            // begin; this blocks vehicle/explosion impulses during external get-up.
            this.m_RecoverPending = true;
            this.enabled = false;
        }

        private void OnRagdollFinished()
        {
            ++this.m_LeaseVersion;
            this.ResetLease();
            this.enabled = false;
        }

        private void ResetLease()
        {
            this.m_PendingVelocity = Vector3.zero;
            this.m_RecoverAt = 0f;
            this.m_ForceRecoverAt = 0f;
            this.m_NextClearanceCheckAt = 0f;
            this.m_StartPending = false;
            this.m_RecoverPending = false;
            this.m_ShouldRecover = false;
            this.m_OwnsRagdoll = false;
            this.m_RecoveryBlocked = false;
            this.m_RecoveryBlocker = null;
        }

        private bool IsHealthDepleted()
        {
            if (this.m_Traits == null) return false;
            try
            {
                RuntimeAttributeData health =
                    this.m_Traits.RuntimeAttributes.Get("hp");
                return health != null && health.Value <= health.MinValue;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Persistent zero-tick back-reference on the Animator model. GC2 unparents
    /// that model while ragdolled, so bone collisions can still resolve the
    /// owning Character and stay out of the Car's metal-impact pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class FranklinRagdollCharacterProxy : MonoBehaviour
    {
        public Character Character { get; private set; }

        public void Initialize(Character character)
        {
            this.Character = character;
        }
    }

    /// <summary>
    /// Zero-tick pool/despawn safety. GC2 temporarily unparents the Animator in
    /// ragdoll mode; recovering synchronously on hierarchy disable prevents that
    /// model and its physics bodies from becoming orphaned scene roots.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class FranklinRagdollLifecycleGuard : MonoBehaviour
    {
        private Character m_Character;
        private bool m_CleanupPending;

        public void Initialize(Character character)
        {
            this.m_Character = character;
        }

        private void Awake()
        {
            if (this.m_Character == null)
                this.m_Character = this.GetComponent<Character>();
        }

        private void OnEnable()
        {
            if (this.m_Character == null ||
                !this.m_Character.Ragdoll.IsRagdoll)
            {
                this.m_CleanupPending = false;
            }
        }

        private void OnDisable()
        {
            _ = this.CleanupRagdollHierarchyAsync();
        }

        private void OnDestroy()
        {
            _ = this.CleanupRagdollHierarchyAsync();
        }

        private async Task CleanupRagdollHierarchyAsync()
        {
            if (this.m_CleanupPending || this.m_Character == null ||
                !this.m_Character.Ragdoll.IsRagdoll)
            {
                return;
            }

            this.m_CleanupPending = true;
            try
            {
                FranklinExplosionRagdollImpulse reaction =
                    this.GetComponent<FranklinExplosionRagdollImpulse>();
                if (reaction != null)
                {
                    await reaction.RecoverForLifecycleAsync();
                }
                else
                {
                    await this.m_Character.Ragdoll.StopRagdollImmediate();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                this.m_CleanupPending = false;
            }
        }
    }
}
