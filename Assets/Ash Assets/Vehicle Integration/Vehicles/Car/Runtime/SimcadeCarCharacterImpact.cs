using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Converts a Sim-Cade body contact into a bounded GC2 ragdoll reaction. It
    /// has no Update/physics query and only runs on a real collision.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SimcadeCarImpactAudio))]
    public sealed class SimcadeCarCharacterImpact : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private SimcadeCarImpactAudio m_ImpactAudio;
        [SerializeField] private Rigidbody m_CarBody;
        [SerializeField] private Collider m_CarCollider;
        [SerializeField] private CarEntry m_CarEntry;
        [SerializeField] private SimcadeCarDriver m_Driver;

        [Header("GC2 Ragdoll Response")]
        [Tooltip("Vehicle point speed in m/s. 4.1667 m/s is approximately 15 km/h.")]
        [SerializeField, Min(0f)] private float m_MinimumRagdollSpeed = 4.1667f;
        [Tooltip("Vehicle point speed in m/s that reaches the strongest configured reaction.")]
        [SerializeField, Min(0.1f)] private float m_FullResponseSpeed = 13.89f;
        [SerializeField, Min(0f)] private float m_MinimumPushSpeed = 2.4f;
        [SerializeField, Min(0f)] private float m_MaximumPushSpeed = 5.5f;
        [SerializeField, Min(0f)] private float m_MinimumUpwardSpeed = 0.35f;
        [SerializeField, Min(0f)] private float m_MaximumUpwardSpeed = 0.8f;
        [SerializeField, Min(0.1f)] private float m_MaximumResultSpeed = 6f;
        [SerializeField, Min(0.25f)] private float m_OwnedRagdollDuration = 2.25f;
        [SerializeField, Min(0f)] private float m_PerCharacterCooldown = 0.8f;

        private SimcadeCarImpactAudio m_SubscribedImpactAudio;

        public bool IsConfigured =>
            m_ImpactAudio != null && m_CarBody != null &&
            m_CarCollider != null && m_CarEntry != null && m_Driver != null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (m_ImpactAudio == null) m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();
            if (m_CarBody == null) m_CarBody = GetComponent<Rigidbody>();
            if (m_CarCollider == null) m_CarCollider = GetComponent<Collider>();
            if (m_CarEntry == null) m_CarEntry = GetComponent<CarEntry>();
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
        }

        private void Subscribe()
        {
            if (m_SubscribedImpactAudio == m_ImpactAudio) return;
            Unsubscribe();
            if (m_ImpactAudio == null) return;
            m_ImpactAudio.EventCollisionContact += OnCollisionContact;
            m_SubscribedImpactAudio = m_ImpactAudio;
        }

        private void Unsubscribe()
        {
            if (m_SubscribedImpactAudio == null) return;
            m_SubscribedImpactAudio.EventCollisionContact -= OnCollisionContact;
            m_SubscribedImpactAudio = null;
        }

        private void OnCollisionContact(Collision collision)
        {
            if (collision == null || m_CarBody == null)
            {
                return;
            }

            Collider otherCollider = collision.collider;
            Character character = otherCollider != null
                ? otherCollider.GetComponentInParent<Character>()
                : null;
            if (character == null && otherCollider != null)
            {
                FranklinRagdollCharacterProxy proxy =
                    otherCollider.GetComponentInParent<
                        FranklinRagdollCharacterProxy
                    >();
                character = proxy != null ? proxy.Character : null;
            }
            if (character != null)
            {
                // A body hit is not a metal-body impact: suppress Car sparks,
                // debris, dents, health damage and door damage for this contact.
                m_ImpactAudio?.ConsumeCurrentCollision(collision);
            }
            if (character == null ||
                m_Driver != null && m_Driver.IsDestroyed ||
                m_CarEntry != null &&
                    m_CarEntry.IsCharacterInTransition(character) ||
                character.IsDead ||
                character.Ragdoll.Get<RagdollDefault>() == null ||
                character.IsPlayer && character.Player?.IsControllable != true ||
                (m_CarEntry != null && m_CarEntry.IsCharacterSeated(character)))
            {
                return;
            }

            Vector3 contactPoint = character.transform.position;
            if (collision.contactCount > 0)
                contactPoint = collision.GetContact(0).point;

            Vector3 up = Vector3.up;
            Rigidbody targetBody = otherCollider != null
                ? otherCollider.attachedRigidbody
                : null;
            bool usesRagdollBody = character.Ragdoll.IsRagdoll &&
                targetBody != null && targetBody != m_CarBody;
            Vector3 targetPosition = usesRagdollBody
                ? targetBody.worldCenterOfMass
                : character.transform.position;
            Vector3 carPointVelocity = Vector3.ProjectOnPlane(
                m_CarBody.GetPointVelocity(contactPoint),
                up
            );
            Vector3 characterVelocity = usesRagdollBody
                ? Vector3.ProjectOnPlane(
                    targetBody.GetPointVelocity(contactPoint),
                    up
                )
                : character.Driver != null
                ? Vector3.ProjectOnPlane(character.Driver.WorldMoveDirection, up)
                : Vector3.zero;
            Vector3 relativeVelocity = carPointVelocity - characterVelocity;
            Vector3 awayFromContact = Vector3.ProjectOnPlane(
                targetPosition - contactPoint,
                up
            );
            if (awayFromContact.sqrMagnitude < 0.001f)
            {
                awayFromContact = Vector3.ProjectOnPlane(
                    targetPosition - m_CarBody.worldCenterOfMass,
                    up
                );
            }
            if (awayFromContact.sqrMagnitude < 0.001f)
                awayFromContact = carPointVelocity;
            awayFromContact.Normalize();

            // Both the Car point speed and its closing speed toward the GC2
            // Character must cross the threshold. A Character running into a
            // parked Car, or merely moving alongside it, cannot self-ragdoll.
            float closingSpeed = Mathf.Max(
                0f,
                Vector3.Dot(relativeVelocity, awayFromContact)
            );
            float effectiveSpeed = Mathf.Min(
                carPointVelocity.magnitude,
                closingSpeed
            );
            if (effectiveSpeed < m_MinimumRagdollSpeed) return;

            Vector3 pushDirection = awayFromContact;
            if (pushDirection.sqrMagnitude < 0.001f)
                pushDirection = transform.forward;

            float response = Mathf.InverseLerp(
                m_MinimumRagdollSpeed,
                Mathf.Max(m_MinimumRagdollSpeed + 0.1f, m_FullResponseSpeed),
                effectiveSpeed
            );
            float pushSpeed = Mathf.Lerp(
                m_MinimumPushSpeed,
                m_MaximumPushSpeed,
                response
            );
            float upwardSpeed = Mathf.Lerp(
                m_MinimumUpwardSpeed,
                m_MaximumUpwardSpeed,
                response
            );
            Vector3 ragdollVelocity = Vector3.ClampMagnitude(
                pushDirection * pushSpeed + up * upwardSpeed,
                m_MaximumResultSpeed
            );

            FranklinExplosionRagdollImpulse.ApplyVehicleVelocity(
                character,
                ragdollVelocity,
                m_MaximumResultSpeed,
                m_OwnedRagdollDuration,
                m_PerCharacterCooldown,
                m_CarCollider
            );
        }

        private void OnValidate()
        {
            m_MinimumRagdollSpeed = Mathf.Max(0f, m_MinimumRagdollSpeed);
            m_FullResponseSpeed = Mathf.Max(
                m_MinimumRagdollSpeed + 0.1f,
                m_FullResponseSpeed
            );
            m_MinimumPushSpeed = Mathf.Max(0f, m_MinimumPushSpeed);
            m_MaximumPushSpeed = Mathf.Max(m_MinimumPushSpeed, m_MaximumPushSpeed);
            m_MinimumUpwardSpeed = Mathf.Max(0f, m_MinimumUpwardSpeed);
            m_MaximumUpwardSpeed = Mathf.Max(
                m_MinimumUpwardSpeed,
                m_MaximumUpwardSpeed
            );
            m_MaximumResultSpeed = Mathf.Max(0.1f, m_MaximumResultSpeed);
            m_OwnedRagdollDuration = Mathf.Max(0.25f, m_OwnedRagdollDuration);
            m_PerCharacterCooldown = Mathf.Max(0f, m_PerCharacterCooldown);
        }
    }
}
