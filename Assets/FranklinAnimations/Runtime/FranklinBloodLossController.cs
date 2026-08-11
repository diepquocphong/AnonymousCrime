using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using PampelGames.BloodFactory;
using UnityEngine;

namespace FranklinGame.Combat
{
    /// <summary>
    /// Converts changes to a GC2 health Attribute into Blood Factory ground decals.
    /// Damage creates an immediate ground drop and reaching the Attribute minimum
    /// creates one large blood pool. Low health never emits blood over time.
    /// </summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class FranklinBloodLossController : MonoBehaviour
    {
        public enum BleedSeverity
        {
            None = 0,
            Light = 1,
            Medium = 2,
            Heavy = 3,
            Dead = 4
        }

        private const float VALUE_EPSILON = 0.0001f;
        private const int MAX_GROUND_HITS = 12;

        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField]
        [Tooltip("GC2 Traits Attribute whose current value triggers the blood effects")]
        private string m_HealthAttributeId = "hp";

        [Header("Blood Factory")]
        [SerializeField] private GameObject m_BloodDropPrefab;
        [SerializeField] private GameObject m_DeathPoolPrefab;
        [SerializeField] private LayerMask m_GroundLayers = ~0;
        [SerializeField, Min(0.1f)] private float m_GroundRayOriginHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float m_GroundRayDistance = 4f;
        [SerializeField, Min(0f)] private float m_SurfaceOffset = 0.025f;
        [SerializeField, Min(0f)] private float m_DropScatterRadius = 0.16f;

        [Header("Health severity")]
        [SerializeField, Range(0f, 1f)] private float m_LightBleedBelow = 0.75f;
        [SerializeField, Range(0f, 1f)] private float m_MediumBleedBelow = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_HeavyBleedBelow = 0.25f;

        [Header("Blood drops")]
        [SerializeField, Min(0.1f)] private float m_DropLifetime = 10f;

        [Header("Damage and death")]
        [SerializeField, Min(0f)] private float m_MinDamageDropInterval = 0.12f;
        [SerializeField, Min(0.05f)] private float m_MinDamageDropScale = 0.45f;
        [SerializeField, Min(0.05f)] private float m_MaxDamageDropScale = 1.35f;
        [SerializeField, Range(0.01f, 1f)] private float m_DamageForMaxDrop = 0.25f;
        [SerializeField, Min(0.1f)] private float m_DeathPoolScale = 2.8f;
        [SerializeField, Min(0.1f)] private float m_DeathPoolLifetime = 45f;

        private readonly RaycastHit[] m_GroundHits = new RaycastHit[MAX_GROUND_HITS];

        private RuntimeAttributeData m_Health;
        private BleedSeverity m_Severity;
        private float m_NextDamageDropTime;
        private bool m_IsBound;
        private bool m_DeathPoolSpawned;
        private bool m_HasWarnedMissingHealth;
        private bool m_HasWarnedMissingPrefab;

        public event Action<BleedSeverity> EventSeverityChanged;

        public BleedSeverity Severity => this.m_Severity;
        public float HealthRatio => this.GetHealthRatio();

        public bool RestoreFullHealth()
        {
            if (!this.m_IsBound) this.BindHealth();
            if (this.m_Health == null) return false;

            this.m_Health.Value = this.m_Health.MaxValue;
            this.RefreshSeverity();
            return this.m_Health.Value > this.m_Health.MinValue + VALUE_EPSILON;
        }

        private void Reset()
        {
            this.m_Traits = this.GetComponentInParent<Traits>();
        }

        private void Awake()
        {
            if (this.m_Traits == null)
            {
                this.m_Traits = this.GetComponentInParent<Traits>();
            }
        }

        private void OnEnable()
        {
            this.BindHealth();
        }

        private void Start()
        {
            if (!this.m_IsBound) this.BindHealth();
        }

        private void OnDisable()
        {
            this.UnbindHealth();
        }

        private void Update()
        {
            if (!this.m_IsBound)
            {
                this.BindHealth();
                if (!this.m_IsBound) return;
            }

            // Keep severity synchronized as a safety net for direct Attribute writes.
            // Blood drops are emitted exclusively by OnHealthChanged when hp decreases.
            this.RefreshSeverity();
        }

        private void BindHealth()
        {
            if (this.m_IsBound) return;
            if (this.m_Traits == null)
            {
                this.m_Traits = this.GetComponentInParent<Traits>();
            }

            try
            {
                this.m_Health = this.m_Traits != null
                    ? this.m_Traits.RuntimeAttributes.Get(this.m_HealthAttributeId)
                    : null;
            }
            catch (Exception exception)
            {
                if (!this.m_HasWarnedMissingHealth)
                {
                    this.m_HasWarnedMissingHealth = true;
                    Debug.LogWarning(
                        $"Blood loss could not read Player Traits Attribute " +
                        $"'{this.m_HealthAttributeId}': {exception.Message}",
                        this
                    );
                }
                return;
            }

            if (this.m_Health == null) return;

            this.m_Health.EventChange += this.OnHealthChanged;
            this.m_IsBound = true;
            this.RefreshSeverity(true);
        }

        private void UnbindHealth()
        {
            if (this.m_Health != null)
            {
                this.m_Health.EventChange -= this.OnHealthChanged;
            }

            this.m_Health = null;
            this.m_IsBound = false;
        }

        private void OnHealthChanged(IdString attributeId, double change)
        {
            if (!string.Equals(
                    attributeId.String,
                    this.m_HealthAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.RefreshSeverity();

            if (change < -VALUE_EPSILON && this.m_Severity != BleedSeverity.Dead &&
                Time.time >= this.m_NextDamageDropTime)
            {
                float range = this.GetHealthRange();
                float normalizedDamage = range > VALUE_EPSILON
                    ? Mathf.Clamp01((float)(-change) / range)
                    : 0f;
                float scaleT = Mathf.Clamp01(normalizedDamage / this.m_DamageForMaxDrop);
                float scale = Mathf.Lerp(
                    this.m_MinDamageDropScale,
                    this.m_MaxDamageDropScale,
                    scaleT
                );

                this.SpawnDrop(scale, this.m_DropScatterRadius * 0.5f);
                this.m_NextDamageDropTime = Time.time + this.m_MinDamageDropInterval;
            }
        }

        private void RefreshSeverity(bool forceEvent = false)
        {
            if (this.m_Health == null) return;

            BleedSeverity nextSeverity = this.CalculateSeverity();
            if (nextSeverity != BleedSeverity.Dead && this.m_DeathPoolSpawned)
            {
                this.m_DeathPoolSpawned = false;
            }

            if (nextSeverity == BleedSeverity.Dead && !this.m_DeathPoolSpawned)
            {
                this.m_DeathPoolSpawned = true;
                this.SpawnDeathPool();
            }

            if (!forceEvent && nextSeverity == this.m_Severity) return;

            this.m_Severity = nextSeverity;
            this.EventSeverityChanged?.Invoke(this.m_Severity);
        }

        private BleedSeverity CalculateSeverity()
        {
            if (this.m_Health.Value <= this.m_Health.MinValue + VALUE_EPSILON)
            {
                return BleedSeverity.Dead;
            }

            float ratio = this.GetHealthRatio();
            if (ratio <= this.m_HeavyBleedBelow) return BleedSeverity.Heavy;
            if (ratio <= this.m_MediumBleedBelow) return BleedSeverity.Medium;
            if (ratio <= this.m_LightBleedBelow) return BleedSeverity.Light;
            return BleedSeverity.None;
        }

        private float GetHealthRatio()
        {
            if (this.m_Health == null) return 1f;
            float range = this.GetHealthRange();
            if (range <= VALUE_EPSILON) return 0f;

            return Mathf.Clamp01(
                (float)((this.m_Health.Value - this.m_Health.MinValue) / range)
            );
        }

        private float GetHealthRange()
        {
            return this.m_Health != null
                ? Mathf.Max(0f, (float)(this.m_Health.MaxValue - this.m_Health.MinValue))
                : 0f;
        }

        private void SpawnDrop(float scale, float scatterRadius)
        {
            this.SpawnDecal(
                this.m_BloodDropPrefab,
                scale,
                this.m_DropLifetime,
                scatterRadius
            );
        }

        private void SpawnDeathPool()
        {
            this.SpawnDecal(
                this.m_DeathPoolPrefab,
                this.m_DeathPoolScale,
                this.m_DeathPoolLifetime,
                this.m_DropScatterRadius * 0.25f
            );
        }

        private void SpawnDecal(
            GameObject prefab,
            float scale,
            float lifetime,
            float scatterRadius)
        {
            if (prefab == null)
            {
                if (!this.m_HasWarnedMissingPrefab)
                {
                    this.m_HasWarnedMissingPrefab = true;
                    Debug.LogWarning(
                        "Blood loss is missing its Blood Factory decal prefab reference.",
                        this
                    );
                }
                return;
            }

            Transform playerTransform = this.m_Traits != null
                ? this.m_Traits.transform
                : this.transform;
            Vector3 samplePosition = playerTransform.position;
            if (scatterRadius > 0f)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * scatterRadius;
                Vector3 right = Vector3.ProjectOnPlane(playerTransform.right, Vector3.up);
                Vector3 forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
                if (right.sqrMagnitude > VALUE_EPSILON) right.Normalize();
                if (forward.sqrMagnitude > VALUE_EPSILON) forward.Normalize();
                samplePosition += right * scatter.x + forward * scatter.y;
            }

            this.FindGround(samplePosition, out Vector3 position, out Vector3 normal);
            Vector3 decalUp = Vector3.ProjectOnPlane(playerTransform.forward, normal);
            if (decalUp.sqrMagnitude <= VALUE_EPSILON)
            {
                decalUp = Vector3.Cross(normal, playerTransform.right);
            }
            if (decalUp.sqrMagnitude <= VALUE_EPSILON)
            {
                decalUp = Vector3.Cross(normal, Vector3.forward);
            }
            decalUp.Normalize();

            Quaternion rotation = Quaternion.LookRotation(-normal, decalUp);
            GameObject instance = Instantiate(prefab, position, rotation);
            instance.name = prefab.name + " (Franklin)";
            instance.transform.localScale *= Mathf.Max(0.05f, scale);

            DecalHandler decal = instance.GetComponent<DecalHandler>();
            if (decal != null)
            {
                decal.lifetime = Mathf.Max(0.1f, lifetime);
                decal.fadeOut = Mathf.Min(decal.fadeOut, decal.lifetime * 0.5f);
                decal.Execute();
            }

            Destroy(instance, Mathf.Max(0.1f, lifetime) + 0.25f);
        }

        private void FindGround(
            Vector3 samplePosition,
            out Vector3 position,
            out Vector3 normal)
        {
            Vector3 origin = samplePosition + Vector3.up * this.m_GroundRayOriginHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                this.m_GroundHits,
                this.m_GroundRayOriginHeight + this.m_GroundRayDistance,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestHit = default;
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hit = this.m_GroundHits[i];
                if (hit.collider == null) continue;

                Transform hitTransform = hit.collider.transform;
                Transform playerTransform = this.m_Traits != null
                    ? this.m_Traits.transform
                    : this.transform;
                if (hitTransform == playerTransform ||
                    hitTransform.IsChildOf(playerTransform) ||
                    playerTransform.IsChildOf(hitTransform))
                {
                    continue;
                }
                if (hit.distance >= nearestDistance) continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            if (nearestDistance < float.PositiveInfinity)
            {
                normal = nearestHit.normal.sqrMagnitude > VALUE_EPSILON
                    ? nearestHit.normal.normalized
                    : Vector3.up;
                position = nearestHit.point + normal * this.m_SurfaceOffset;
                return;
            }

            normal = Vector3.up;
            position = samplePosition + normal * this.m_SurfaceOffset;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(this.m_HealthAttributeId))
            {
                this.m_HealthAttributeId = "hp";
            }

            this.m_MediumBleedBelow = Mathf.Min(
                this.m_MediumBleedBelow,
                this.m_LightBleedBelow
            );
            this.m_HeavyBleedBelow = Mathf.Min(
                this.m_HeavyBleedBelow,
                this.m_MediumBleedBelow
            );
            this.m_DamageForMaxDrop = Mathf.Max(0.01f, this.m_DamageForMaxDrop);
            this.m_MaxDamageDropScale = Mathf.Max(
                this.m_MinDamageDropScale,
                this.m_MaxDamageDropScale
            );
            this.m_DeathPoolLifetime = Mathf.Max(0.1f, this.m_DeathPoolLifetime);
        }
    }
}
