using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven bridge between the cooldown-filtered Sim-Cade impact event
    /// and the Car's existing Game Creator 2 health Attribute.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Traits), typeof(SimcadeCarImpactAudio))]
    public sealed class SimcadeCarHealth : MonoBehaviour
    {
        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField] private string m_HealthAttributeId = "health-attribute-id";

        [Header("Impact Damage")]
        [SerializeField] private SimcadeCarImpactAudio m_ImpactAudio;
        [SerializeField] private bool m_ApplyImpactDamage = true;
        [SerializeField, Min(0f)] private float m_LightDamageMin = 0.35f;
        [SerializeField, Min(0f)] private float m_LightDamageMax = 2.25f;
        [SerializeField, Min(0f)] private float m_HeavyDamageMin = 4.5f;
        [SerializeField, Min(0f)] private float m_HeavyDamageMax = 14f;
        [SerializeField, Min(0.1f)] private float m_FullDamageSeverity = 16f;

        private RuntimeAttributeData m_RuntimeHealth;
        private bool m_IsBound;
        private bool m_HasWarnedMissingAttribute;

        public event Action<float, float> EventHealthChanged;

        public bool IsConfigured => m_Traits != null && m_ImpactAudio != null &&
            !string.IsNullOrWhiteSpace(m_HealthAttributeId);
        public float CurrentHealth => m_RuntimeHealth != null
            ? (float)m_RuntimeHealth.Value
            : 0f;
        public float MaximumHealth => m_RuntimeHealth != null
            ? (float)m_RuntimeHealth.MaxValue
            : 0f;
        public float HealthRatio => m_RuntimeHealth != null
            ? Mathf.Clamp01((float)m_RuntimeHealth.Ratio)
            : 0f;
        public bool IsDestroyed => m_RuntimeHealth != null &&
            m_RuntimeHealth.Value <= m_RuntimeHealth.MinValue + double.Epsilon;

        private void Awake()
        {
            if (m_Traits == null) m_Traits = GetComponent<Traits>();
            if (m_ImpactAudio == null)
                m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();
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
            if (amount <= 0f || m_RuntimeHealth == null) return;
            m_RuntimeHealth.Value += amount;
        }

        public void RepairFull()
        {
            if (m_RuntimeHealth == null) return;
            m_RuntimeHealth.Value = m_RuntimeHealth.MaxValue;
        }

        public void SetNormalizedHealth(float ratio)
        {
            if (m_RuntimeHealth == null) return;
            m_RuntimeHealth.Value = Mathf.Lerp(
                (float)m_RuntimeHealth.MinValue,
                (float)m_RuntimeHealth.MaxValue,
                Mathf.Clamp01(ratio)
            );
        }

        public void Configure(
            Traits traits,
            SimcadeCarImpactAudio impactAudio,
            string healthAttributeId)
        {
            m_Traits = traits;
            m_ImpactAudio = impactAudio;
            m_HealthAttributeId = string.IsNullOrWhiteSpace(healthAttributeId)
                ? "health-attribute-id"
                : healthAttributeId;
        }

        private void Bind()
        {
            if (m_IsBound) return;
            if (m_Traits == null) m_Traits = GetComponent<Traits>();
            if (m_ImpactAudio == null)
                m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();

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
                        $"Car health Attribute '{m_HealthAttributeId}' is unavailable: " +
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
            m_IsBound = true;
        }

        private void Unbind()
        {
            if (!m_IsBound) return;
            if (m_Traits != null)
                m_Traits.RuntimeAttributes.EventChange -= OnAttributeChanged;
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactAccepted -= OnImpactAccepted;
            m_RuntimeHealth = null;
            m_IsBound = false;
        }

        private void OnImpactAccepted(bool isHeavy, float severity)
        {
            if (!m_ApplyImpactDamage || m_RuntimeHealth == null) return;

            float amount = Mathf.Clamp01(severity / Mathf.Max(0.1f, m_FullDamageSeverity));
            float damage = isHeavy
                ? Mathf.Lerp(m_HeavyDamageMin, m_HeavyDamageMax, amount)
                : Mathf.Lerp(m_LightDamageMin, m_LightDamageMax, amount);
            ApplyDamage(damage);
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
            EventHealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(m_HealthAttributeId))
                m_HealthAttributeId = "health-attribute-id";
            m_LightDamageMax = Mathf.Max(m_LightDamageMin, m_LightDamageMax);
            m_HeavyDamageMax = Mathf.Max(m_HeavyDamageMin, m_HeavyDamageMax);
            m_FullDamageSeverity = Mathf.Max(0.1f, m_FullDamageSeverity);
        }
    }
}
