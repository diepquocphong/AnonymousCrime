using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Event-driven bridge between GC2 Traits health and lightweight drone impact damage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Traits))]
    public sealed class DroneHealth : MonoBehaviour
    {
        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField] private string m_HealthAttributeId = "health-attribute-id";

        [Header("Impact Damage")]
        [SerializeField] private bool m_ApplyImpactDamage = true;
        [SerializeField, Min(0.1f)] private float m_MinimumDamageSpeed = 5f;
        [SerializeField, Min(0.1f)] private float m_FullDamageSpeed = 20f;
        [SerializeField, Min(0f)] private float m_MinimumImpactDamage = 2f;
        [SerializeField, Min(0f)] private float m_MaximumImpactDamage = 24f;
        [SerializeField, Min(0f)] private float m_ImpactCooldown = 0.18f;

        private RuntimeAttributeData m_RuntimeHealth;
        private bool m_IsBound;
        private bool m_WasDestroyed;
        private bool m_IsTerminallyDestroyed;
        private bool m_HasWarnedMissingAttribute;
        private float m_NextImpactTime;

        public event Action<float, float> EventHealthChanged;
        public event Action<Vector3, Vector3, float> EventImpactAccepted;
        public event Action EventDestroyed;

        public float CurrentHealth => this.m_RuntimeHealth != null
            ? (float)this.m_RuntimeHealth.Value
            : 0f;
        public float MaximumHealth => this.m_RuntimeHealth != null
            ? (float)this.m_RuntimeHealth.MaxValue
            : 0f;
        public float HealthRatio => this.m_RuntimeHealth != null
            ? Mathf.Clamp01((float)this.m_RuntimeHealth.Ratio)
            : 0f;
        public bool IsDestroyed => this.m_IsTerminallyDestroyed ||
                                   (this.m_RuntimeHealth != null &&
                                    this.m_RuntimeHealth.Value <=
                                    this.m_RuntimeHealth.MinValue + double.Epsilon);

        private void Awake()
        {
            if (this.m_Traits == null) this.m_Traits = this.GetComponent<Traits>();
        }

        private void OnEnable()
        {
            this.Bind();
        }

        private void Start()
        {
            if (!this.m_IsBound) this.Bind();
            this.PublishHealth();
        }

        private void OnDisable()
        {
            this.Unbind();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!this.m_ApplyImpactDamage || this.IsDestroyed ||
                Time.time < this.m_NextImpactTime)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < this.m_MinimumDamageSpeed) return;

            this.m_NextImpactTime = Time.time + this.m_ImpactCooldown;
            Vector3 impactPoint = this.transform.position;
            Vector3 impactNormal = collision.relativeVelocity.sqrMagnitude > 0.0001f
                ? -collision.relativeVelocity.normalized
                : this.transform.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                impactPoint = contact.point;
                impactNormal = contact.normal;
            }
            this.EventImpactAccepted?.Invoke(
                impactPoint,
                impactNormal,
                impactSpeed
            );

            float severity = Mathf.InverseLerp(
                this.m_MinimumDamageSpeed,
                Mathf.Max(this.m_MinimumDamageSpeed + 0.1f, this.m_FullDamageSpeed),
                impactSpeed
            );
            this.ApplyDamage(Mathf.Lerp(
                this.m_MinimumImpactDamage,
                this.m_MaximumImpactDamage,
                severity
            ));
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || this.m_RuntimeHealth == null || this.IsDestroyed) return;
            this.m_RuntimeHealth.Value -= amount;
        }

        public void Repair(float amount)
        {
            if (amount <= 0f || this.m_RuntimeHealth == null ||
                this.m_IsTerminallyDestroyed)
            {
                return;
            }
            this.m_RuntimeHealth.Value += amount;
        }

        public void RepairFull()
        {
            if (this.m_RuntimeHealth == null || this.m_IsTerminallyDestroyed) return;
            this.m_RuntimeHealth.Value = this.m_RuntimeHealth.MaxValue;
        }

        public void SetTerminallyDestroyed()
        {
            this.m_IsTerminallyDestroyed = true;
            this.m_WasDestroyed = true;
            // Traits keeps an Update loop for status effects. A terminal drone
            // no longer needs that loop, but the component/data remain available
            // for GC2 inspection.
            if (this.m_Traits != null) this.m_Traits.enabled = false;
        }

        private void Bind()
        {
            if (this.m_IsBound) return;
            if (this.m_Traits == null) this.m_Traits = this.GetComponent<Traits>();

            try
            {
                this.m_RuntimeHealth = this.m_Traits != null
                    ? this.m_Traits.RuntimeAttributes.Get(this.m_HealthAttributeId)
                    : null;
            }
            catch (Exception exception)
            {
                if (!this.m_HasWarnedMissingAttribute)
                {
                    this.m_HasWarnedMissingAttribute = true;
                    Debug.LogWarning(
                        $"Drone health Attribute '{this.m_HealthAttributeId}' is unavailable: " +
                        exception.Message,
                        this
                    );
                }
                return;
            }

            if (this.m_RuntimeHealth == null) return;
            this.m_Traits.RuntimeAttributes.EventChange += this.OnAttributeChanged;
            this.m_IsBound = true;
            this.m_WasDestroyed = false;
        }

        private void Unbind()
        {
            if (!this.m_IsBound) return;
            if (this.m_Traits != null)
            {
                this.m_Traits.RuntimeAttributes.EventChange -= this.OnAttributeChanged;
            }
            this.m_RuntimeHealth = null;
            this.m_IsBound = false;
        }

        private void OnAttributeChanged(IdString attributeId)
        {
            if (!string.Equals(
                    attributeId.String,
                    this.m_HealthAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            this.PublishHealth();
        }

        private void PublishHealth()
        {
            if (this.m_RuntimeHealth == null) return;

            bool destroyed = this.IsDestroyed;
            this.EventHealthChanged?.Invoke(this.CurrentHealth, this.MaximumHealth);
            if (destroyed && !this.m_WasDestroyed)
            {
                this.m_WasDestroyed = true;
                this.EventDestroyed?.Invoke();
            }
            else if (!destroyed)
            {
                this.m_WasDestroyed = false;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(this.m_HealthAttributeId))
            {
                this.m_HealthAttributeId = "health-attribute-id";
            }
            this.m_MinimumDamageSpeed = Mathf.Max(0.1f, this.m_MinimumDamageSpeed);
            this.m_FullDamageSpeed = Mathf.Max(
                this.m_MinimumDamageSpeed + 0.1f,
                this.m_FullDamageSpeed
            );
            this.m_MaximumImpactDamage = Mathf.Max(
                this.m_MinimumImpactDamage,
                this.m_MaximumImpactDamage
            );
            this.m_ImpactCooldown = Mathf.Max(0f, this.m_ImpactCooldown);
        }
    }
}
