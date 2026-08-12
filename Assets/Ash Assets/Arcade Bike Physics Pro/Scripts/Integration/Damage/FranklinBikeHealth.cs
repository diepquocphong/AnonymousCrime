using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven Bike health using the same GC2 Attribute and accepted-impact
    /// pipeline as the Sim-Cade Car. Collision classification, cooldown, audio
    /// and pooled effects remain owned by FranklinBikeImpactAudio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(Traits),
        typeof(FranklinBikeImpactAudio),
        typeof(FranklinArcadeBikeDriver)
    )]
    public sealed class FranklinBikeHealth : MonoBehaviour
    {
        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField] private string m_HealthAttributeId = "health-attribute-id";

        [Header("Impact Damage")]
        [SerializeField] private FranklinBikeImpactAudio m_ImpactAudio;
        [SerializeField] private bool m_ApplyImpactDamage = true;
        [SerializeField, Min(0f)] private float m_LightDamageMin = 0.35f;
        [SerializeField, Min(0f)] private float m_LightDamageMax = 2f;
        [SerializeField, Min(0f)] private float m_HeavyDamageMin = 4f;
        [SerializeField, Min(0f)] private float m_HeavyDamageMax = 12f;
        [SerializeField, Min(0.1f)] private float m_FullDamageSeverity = 14f;

        [Header("Seated Player Impact Damage")]
        [SerializeField] private BikeEntry m_BikeEntry;
        [SerializeField] private FranklinBikeCrashRagdoll m_CrashRagdoll;
        [SerializeField] private string m_PlayerHealthAttributeId = "hp";
        [Tooltip("Only a heavy impact that actually starts rider ragdoll damages the Player.")]
        [SerializeField] private bool m_ApplySeatedPlayerDamage = true;
        [SerializeField, Min(0f)] private float m_PlayerDamageMin = 4f;
        [SerializeField, Min(0f)] private float m_PlayerDamageMax = 18f;

        [Header("Destroyed State")]
        [SerializeField] private FranklinArcadeBikeDriver m_Driver;

        private RuntimeAttributeData m_RuntimeHealth;
        private bool m_IsBound;
        private bool m_WasDestroyed;
        private bool m_IsTerminallyDestroyed;
        private bool m_HasWarnedMissingAttribute;

        public event Action<float, float> EventHealthChanged;
        public event Action EventDestroyed;
        public event Action EventRestored;

        public bool IsConfigured => m_Traits != null && m_ImpactAudio != null &&
            m_Driver != null && !string.IsNullOrWhiteSpace(m_HealthAttributeId);
        public bool HasReducedBikeDamageProfile =>
            Mathf.Abs(m_LightDamageMin - 0.35f) < 0.001f &&
            Mathf.Abs(m_LightDamageMax - 2f) < 0.001f &&
            Mathf.Abs(m_HeavyDamageMin - 4f) < 0.001f &&
            Mathf.Abs(m_HeavyDamageMax - 12f) < 0.001f &&
            Mathf.Abs(m_FullDamageSeverity - 14f) < 0.001f;

        public float CurrentHealth => m_RuntimeHealth != null
            ? (float)m_RuntimeHealth.Value
            : 0f;

        public float MaximumHealth => m_RuntimeHealth != null
            ? (float)m_RuntimeHealth.MaxValue
            : 0f;

        public float HealthRatio => m_RuntimeHealth != null
            ? Mathf.Clamp01((float)m_RuntimeHealth.Ratio)
            : 0f;

        public bool IsDestroyed => m_IsTerminallyDestroyed ||
            (m_RuntimeHealth != null &&
             m_RuntimeHealth.Value <= m_RuntimeHealth.MinValue + double.Epsilon);
        public bool IsTerminallyDestroyed => m_IsTerminallyDestroyed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void Start()
        {
            if (!m_IsBound) Bind();
            PublishHealth();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || m_RuntimeHealth == null || IsDestroyed) return;
            m_RuntimeHealth.Value -= amount;
        }

        public void Repair(float amount)
        {
            if (amount <= 0f || m_RuntimeHealth == null || m_IsTerminallyDestroyed)
                return;
            m_RuntimeHealth.Value += amount;
        }

        public void RepairFull()
        {
            if (m_RuntimeHealth == null || m_IsTerminallyDestroyed) return;
            m_RuntimeHealth.Value = m_RuntimeHealth.MaxValue;
        }

        public void SetNormalizedHealth(float ratio)
        {
            if (m_RuntimeHealth == null || m_IsTerminallyDestroyed) return;
            m_RuntimeHealth.Value = Mathf.Lerp(
                (float)m_RuntimeHealth.MinValue,
                (float)m_RuntimeHealth.MaxValue,
                Mathf.Clamp01(ratio)
            );
        }

        /// <summary>
        /// Makes a post-explosion wreck permanent until the prefab is respawned.
        /// Repair remains available during the 1.35-second warning window only.
        /// </summary>
        public void SetTerminallyDestroyed()
        {
            if (m_IsTerminallyDestroyed) return;
            m_IsTerminallyDestroyed = true;
            m_Driver?.SetDamageLocked(true);
        }

        public void Configure(
            Traits traits,
            FranklinBikeImpactAudio impactAudio,
            FranklinArcadeBikeDriver driver,
            BikeEntry bikeEntry,
            string healthAttributeId)
        {
            m_Traits = traits;
            m_ImpactAudio = impactAudio;
            m_Driver = driver;
            m_BikeEntry = bikeEntry;
            m_HealthAttributeId = string.IsNullOrWhiteSpace(healthAttributeId)
                ? "health-attribute-id"
                : healthAttributeId;
        }

        public void ConfigureReducedBikeDamageProfile()
        {
            m_LightDamageMin = 0.35f;
            m_LightDamageMax = 2f;
            m_HeavyDamageMin = 4f;
            m_HeavyDamageMax = 12f;
            m_FullDamageSeverity = 14f;
        }

        private void ResolveReferences()
        {
            if (m_Traits == null) m_Traits = GetComponent<Traits>();
            if (m_ImpactAudio == null)
                m_ImpactAudio = GetComponent<FranklinBikeImpactAudio>();
            if (m_Driver == null)
                m_Driver = GetComponent<FranklinArcadeBikeDriver>();
            if (m_BikeEntry == null) m_BikeEntry = GetComponent<BikeEntry>();
            if (m_CrashRagdoll == null)
                m_CrashRagdoll = GetComponent<FranklinBikeCrashRagdoll>();
        }

        private void Bind()
        {
            if (m_IsBound) return;
            ResolveReferences();

            try
            {
                m_RuntimeHealth = m_Traits != null
                    ? m_Traits.RuntimeAttributes.Get(m_HealthAttributeId)
                    : null;
            }
            catch (Exception exception)
            {
                if (!m_HasWarnedMissingAttribute)
                {
                    m_HasWarnedMissingAttribute = true;
                    Debug.LogWarning(
                        $"Bike health Attribute '{m_HealthAttributeId}' is unavailable: " +
                        exception.Message,
                        this
                    );
                }
                return;
            }

            if (m_RuntimeHealth == null) return;
            m_Traits.RuntimeAttributes.EventChange += OnAttributeChanged;
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactAccepted += OnImpactAccepted;
            if (m_CrashRagdoll != null)
                m_CrashRagdoll.EventRiderRagdollStarted += OnRiderRagdollStarted;
            m_IsBound = true;
            m_WasDestroyed = IsDestroyed;
            m_Driver?.SetDamageLocked(m_WasDestroyed);
        }

        private void Unbind()
        {
            if (!m_IsBound) return;
            if (m_Traits != null)
                m_Traits.RuntimeAttributes.EventChange -= OnAttributeChanged;
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactAccepted -= OnImpactAccepted;
            if (m_CrashRagdoll != null)
                m_CrashRagdoll.EventRiderRagdollStarted -= OnRiderRagdollStarted;
            m_RuntimeHealth = null;
            m_IsBound = false;
        }

        private void OnImpactAccepted(bool isHeavy, float severity)
        {
            if (!m_ApplyImpactDamage || m_RuntimeHealth == null) return;

            float amount = Mathf.Clamp01(
                severity / Mathf.Max(0.1f, m_FullDamageSeverity)
            );
            float damage = isHeavy
                ? Mathf.Lerp(m_HeavyDamageMin, m_HeavyDamageMax, amount)
                : Mathf.Lerp(m_LightDamageMin, m_LightDamageMax, amount);
            ApplyDamage(damage);
        }

        private void OnRiderRagdollStarted(Character rider, float severity)
        {
            if (!m_ApplySeatedPlayerDamage || rider == null || rider.Player == null ||
                !rider.Ragdoll.IsRagdoll)
            {
                return;
            }

            float amount = Mathf.Clamp01(
                severity / Mathf.Max(0.1f, m_FullDamageSeverity)
            );
            ApplyDamageToPlayer(
                rider,
                Mathf.Lerp(m_PlayerDamageMin, m_PlayerDamageMax, amount)
            );
        }

        private void ApplyDamageToPlayer(Character rider, float damage)
        {
            if (damage <= 0f || rider == null) return;
            Traits playerTraits = rider.GetComponent<Traits>();
            if (playerTraits == null) return;

            try
            {
                RuntimeAttributeData playerHealth = playerTraits.RuntimeAttributes.Get(
                    m_PlayerHealthAttributeId
                );
                if (playerHealth != null) playerHealth.Value -= damage;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Bike impact could not damage Player Traits " +
                    $"'{m_PlayerHealthAttributeId}': {exception.Message}",
                    rider
                );
            }
        }

        private void OnAttributeChanged(IdString attributeId)
        {
            if (!string.Equals(
                    attributeId.String,
                    m_HealthAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PublishHealth();
        }

        private void PublishHealth()
        {
            if (m_RuntimeHealth == null) return;

            bool destroyed = IsDestroyed;
            m_Driver?.SetDamageLocked(destroyed);
            EventHealthChanged?.Invoke(CurrentHealth, MaximumHealth);

            if (destroyed && !m_WasDestroyed)
                EventDestroyed?.Invoke();
            else if (!destroyed && m_WasDestroyed)
                EventRestored?.Invoke();

            m_WasDestroyed = destroyed;
        }

        private void OnValidate()
        {
            ResolveReferences();
            if (string.IsNullOrWhiteSpace(m_HealthAttributeId))
                m_HealthAttributeId = "health-attribute-id";
            m_LightDamageMax = Mathf.Max(m_LightDamageMin, m_LightDamageMax);
            m_HeavyDamageMax = Mathf.Max(m_HeavyDamageMin, m_HeavyDamageMax);
            m_FullDamageSeverity = Mathf.Max(0.1f, m_FullDamageSeverity);
            m_PlayerDamageMax = Mathf.Max(m_PlayerDamageMin, m_PlayerDamageMax);
            if (string.IsNullOrWhiteSpace(m_PlayerHealthAttributeId))
                m_PlayerHealthAttributeId = "hp";
        }
    }
}
