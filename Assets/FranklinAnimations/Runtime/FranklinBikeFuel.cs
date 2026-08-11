using System;
using System.Collections;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven GC2 fuel bridge for Arcade Bike Physics Pro. Fuel is sampled at
    /// low frequency and never creates per-frame work while the Bike engine is off.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Traits), typeof(FranklinArcadeBikeDriver))]
    public sealed class FranklinBikeFuel : MonoBehaviour
    {
        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField] private string m_FuelAttributeId = "fuel-attribute-id";
        [SerializeField] private FranklinArcadeBikeDriver m_Driver;

        [Header("Starting Fuel")]
        [SerializeField] private bool m_InitializeOnFirstEnable = true;
        [SerializeField, Min(0f)] private float m_StartingFuel = 100f;

        [Header("Consumption Per Second")]
        [SerializeField, Min(0f)] private float m_IdleConsumption = 0.015f;
        [SerializeField, Min(0f)] private float m_ThrottleConsumption = 0.08f;
        [SerializeField, Min(0f)] private float m_HighSpeedConsumption = 0.02f;
        [SerializeField, Min(1f)] private float m_HighSpeedReferenceKph = 120f;
        [SerializeField, Range(0.1f, 0.5f)] private float m_ConsumptionTickInterval = 0.25f;

        private RuntimeAttributeData m_RuntimeFuel;
        private Coroutine m_ConsumptionRoutine;
        private bool m_EngineRequested;
        private bool m_HasInitializedStartingFuel;
        private bool m_IsBound;
        private bool m_HasWarnedMissingAttribute;

        public event Action<float, float> EventFuelChanged;

        public bool IsConfigured => m_Traits != null && m_Driver != null &&
            !string.IsNullOrWhiteSpace(m_FuelAttributeId);
        public float CurrentFuel => m_RuntimeFuel != null
            ? (float)m_RuntimeFuel.Value
            : 0f;
        public float MaximumFuel => m_RuntimeFuel != null
            ? (float)m_RuntimeFuel.MaxValue
            : 0f;
        public float FuelRatio => m_RuntimeFuel != null
            ? Mathf.Clamp01((float)m_RuntimeFuel.Ratio)
            : 0f;
        public bool HasFuel => m_RuntimeFuel != null &&
            m_RuntimeFuel.Value > m_RuntimeFuel.MinValue + double.Epsilon;

        private void Awake()
        {
            this.ResolveReferences();
        }

        private void OnEnable()
        {
            this.Bind();
        }

        private void Start()
        {
            if (!this.m_IsBound) this.Bind();
            this.PublishFuel();
        }

        private void OnDisable()
        {
            this.StopConsumption();
            this.Unbind();
        }

        public void Configure(
            Traits traits,
            FranklinArcadeBikeDriver driver,
            string fuelAttributeId)
        {
            this.m_Traits = traits;
            this.m_Driver = driver;
            this.m_FuelAttributeId = string.IsNullOrWhiteSpace(fuelAttributeId)
                ? "fuel-attribute-id"
                : fuelAttributeId;
            this.m_InitializeOnFirstEnable = true;
            this.m_StartingFuel = 100f;
            this.m_IdleConsumption = 0.015f;
            this.m_ThrottleConsumption = 0.08f;
            this.m_HighSpeedConsumption = 0.02f;
            this.m_HighSpeedReferenceKph = 120f;
            this.m_ConsumptionTickInterval = 0.25f;
        }

        public void SetEngineActive(bool active)
        {
            this.m_EngineRequested = active;
            if (active && this.HasFuel) this.EnsureConsumption();
            else this.StopConsumption();
        }

        public void Consume(float amount)
        {
            if (amount <= 0f || this.m_RuntimeFuel == null || !this.HasFuel) return;
            this.m_RuntimeFuel.Value -= amount;
        }

        public void Refuel(float amount)
        {
            if (amount <= 0f || this.m_RuntimeFuel == null) return;
            this.m_RuntimeFuel.Value += amount;
        }

        public void RefuelFull()
        {
            if (this.m_RuntimeFuel == null) return;
            this.m_RuntimeFuel.Value = this.m_RuntimeFuel.MaxValue;
        }

        public void SetNormalizedFuel(float ratio)
        {
            if (this.m_RuntimeFuel == null) return;
            this.m_RuntimeFuel.Value = Mathf.Lerp(
                (float)this.m_RuntimeFuel.MinValue,
                (float)this.m_RuntimeFuel.MaxValue,
                Mathf.Clamp01(ratio)
            );
        }

        private void ResolveReferences()
        {
            if (this.m_Traits == null) this.m_Traits = this.GetComponent<Traits>();
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponent<FranklinArcadeBikeDriver>();
        }

        private void Bind()
        {
            if (this.m_IsBound) return;
            this.ResolveReferences();

            try
            {
                this.m_RuntimeFuel = this.m_Traits != null
                    ? this.m_Traits.RuntimeAttributes.Get(this.m_FuelAttributeId)
                    : null;
            }
            catch (Exception exception)
            {
                if (!this.m_HasWarnedMissingAttribute)
                {
                    this.m_HasWarnedMissingAttribute = true;
                    Debug.LogWarning(
                        $"Bike fuel Attribute '{this.m_FuelAttributeId}' is unavailable: " +
                        exception.Message,
                        this
                    );
                }
                return;
            }

            if (this.m_RuntimeFuel == null) return;
            this.InitializeStartingFuel();
            this.m_Traits.RuntimeAttributes.EventChange += this.OnAttributeChanged;
            this.m_IsBound = true;
            this.NotifyFuelAvailability();
            this.PublishFuel();
            if (this.m_EngineRequested && this.HasFuel) this.EnsureConsumption();
        }

        private void InitializeStartingFuel()
        {
            if (this.m_HasInitializedStartingFuel || this.m_RuntimeFuel == null) return;
            this.m_HasInitializedStartingFuel = true;
            if (!this.m_InitializeOnFirstEnable) return;

            this.m_RuntimeFuel.Value = Mathf.Clamp(
                this.m_StartingFuel,
                (float)this.m_RuntimeFuel.MinValue,
                (float)this.m_RuntimeFuel.MaxValue
            );
        }

        private void Unbind()
        {
            if (!this.m_IsBound) return;
            if (this.m_Traits != null)
                this.m_Traits.RuntimeAttributes.EventChange -= this.OnAttributeChanged;
            this.m_RuntimeFuel = null;
            this.m_IsBound = false;
        }

        private void EnsureConsumption()
        {
            if (this.m_ConsumptionRoutine != null || !this.isActiveAndEnabled) return;
            this.m_ConsumptionRoutine = this.StartCoroutine(
                this.ConsumeFuelWhileEngineRuns()
            );
        }

        private IEnumerator ConsumeFuelWhileEngineRuns()
        {
            float interval = Mathf.Clamp(this.m_ConsumptionTickInterval, 0.1f, 0.5f);
            WaitForSeconds wait = new WaitForSeconds(interval);

            while (this.m_EngineRequested && this.isActiveAndEnabled && this.HasFuel)
            {
                yield return wait;
                if (!this.m_EngineRequested || !this.isActiveAndEnabled || !this.HasFuel)
                    break;

                float throttle = this.m_Driver != null
                    ? this.m_Driver.ThrottleMagnitude
                    : 0f;
                float speedRatio = this.m_Driver != null
                    ? Mathf.Clamp01(
                        this.m_Driver.SpeedKph /
                        Mathf.Max(1f, this.m_HighSpeedReferenceKph)
                    )
                    : 0f;
                float consumptionPerSecond = this.m_IdleConsumption +
                    this.m_ThrottleConsumption * throttle +
                    this.m_HighSpeedConsumption * speedRatio;
                this.Consume(consumptionPerSecond * interval);
            }

            this.m_ConsumptionRoutine = null;
        }

        private void StopConsumption()
        {
            if (this.m_ConsumptionRoutine == null) return;
            this.StopCoroutine(this.m_ConsumptionRoutine);
            this.m_ConsumptionRoutine = null;
        }

        private void OnAttributeChanged(IdString attributeId)
        {
            if (!string.Equals(
                    attributeId.String,
                    this.m_FuelAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.NotifyFuelAvailability();
            this.PublishFuel();
            if (this.m_EngineRequested && this.HasFuel) this.EnsureConsumption();
        }

        private void NotifyFuelAvailability()
        {
            this.m_Driver?.SetFuelAvailable(this.HasFuel);
        }

        private void PublishFuel()
        {
            if (this.m_RuntimeFuel == null) return;
            this.EventFuelChanged?.Invoke(this.CurrentFuel, this.MaximumFuel);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(this.m_FuelAttributeId))
                this.m_FuelAttributeId = "fuel-attribute-id";
            this.m_StartingFuel = Mathf.Max(0f, this.m_StartingFuel);
            this.m_IdleConsumption = Mathf.Max(0f, this.m_IdleConsumption);
            this.m_ThrottleConsumption = Mathf.Max(0f, this.m_ThrottleConsumption);
            this.m_HighSpeedConsumption = Mathf.Max(0f, this.m_HighSpeedConsumption);
            this.m_HighSpeedReferenceKph = Mathf.Max(1f, this.m_HighSpeedReferenceKph);
            this.m_ConsumptionTickInterval = Mathf.Clamp(
                this.m_ConsumptionTickInterval,
                0.1f,
                0.5f
            );
        }
    }
}
