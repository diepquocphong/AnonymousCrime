using System;
using System.Collections;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Event-driven GC2 fuel bridge. Only an active engine owns a low-frequency
    /// coroutine, so parked Cars create no per-frame fuel work on mobile.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Traits), typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarFuel : MonoBehaviour, IFranklinFuelTank
    {
        [Header("Game Creator 2")]
        [SerializeField] private Traits m_Traits;
        [SerializeField] private string m_FuelAttributeId = "fuel-attribute-id";
        [SerializeField] private SimcadeCarDriver m_Driver;

        [Header("Starting Fuel")]
        [SerializeField] private bool m_InitializeOnFirstEnable = true;
        [SerializeField, Min(0f)] private float m_StartingFuel = 100f;

        [Header("Consumption Per Second")]
        [SerializeField, Min(0f)] private float m_IdleConsumption = 0.02f;
        [SerializeField, Min(0f)] private float m_ThrottleConsumption = 0.1f;
        [SerializeField, Min(0f)] private float m_HighSpeedConsumption = 0.03f;
        [SerializeField, Min(1f)] private float m_HighSpeedReferenceKph = 120f;
        [SerializeField, Range(0.1f, 0.5f)] private float m_ConsumptionTickInterval = 0.25f;

        private RuntimeAttributeData m_RuntimeFuel;
        private Coroutine m_ConsumptionRoutine;
        private bool m_EngineRequested;
        private bool m_HasInitializedStartingFuel;
        private bool m_IsBound;
        private bool m_HasWarnedMissingAttribute;
        private bool m_HasPublishedFuelAvailability;
        private bool m_LastPublishedHasFuel;

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
        public float ConsumptionTickInterval => m_ConsumptionTickInterval;
        public float StartingFuel => m_StartingFuel;
        public bool InitializesOnFirstEnable => m_InitializeOnFirstEnable;
        public float MaximumConsumptionPerSecond =>
            m_IdleConsumption + m_ThrottleConsumption + m_HighSpeedConsumption;
        public GameObject VehicleObject => this.gameObject;
        public float SpeedKph => this.m_Driver != null ? this.m_Driver.SpeedKph : 0f;
        public bool IsPlayerControlled => this.m_Driver != null &&
                                          this.m_Driver.IsVehicleEnabled;

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
            PublishFuel();
        }

        private void OnDisable()
        {
            StopConsumption();
            Unbind();
        }

        public void SetEngineActive(bool active)
        {
            m_EngineRequested = active;
            if (active && HasFuel) EnsureConsumption();
            else StopConsumption();
        }

        public void Consume(float amount)
        {
            if (amount <= 0f || m_RuntimeFuel == null || !HasFuel) return;
            m_RuntimeFuel.Value -= amount;
        }

        public void Refuel(float amount)
        {
            if (amount <= 0f || m_RuntimeFuel == null) return;
            m_RuntimeFuel.Value += amount;
        }

        public float TryRefuel(float requestedAmount)
        {
            if (requestedAmount <= 0f || this.m_RuntimeFuel == null) return 0f;
            float before = this.CurrentFuel;
            this.Refuel(requestedAmount);
            return Mathf.Max(0f, this.CurrentFuel - before);
        }

        public void RefuelFull()
        {
            if (m_RuntimeFuel == null) return;
            m_RuntimeFuel.Value = m_RuntimeFuel.MaxValue;
        }

        public void SetNormalizedFuel(float ratio)
        {
            if (m_RuntimeFuel == null) return;
            m_RuntimeFuel.Value = Mathf.Lerp(
                (float)m_RuntimeFuel.MinValue,
                (float)m_RuntimeFuel.MaxValue,
                Mathf.Clamp01(ratio)
            );
        }

        public void Configure(
            Traits traits,
            SimcadeCarDriver driver,
            string fuelAttributeId)
        {
            m_Traits = traits;
            m_Driver = driver;
            m_FuelAttributeId = string.IsNullOrWhiteSpace(fuelAttributeId)
                ? "fuel-attribute-id"
                : fuelAttributeId;
            m_InitializeOnFirstEnable = true;
            m_StartingFuel = 100f;
            m_IdleConsumption = 0.02f;
            m_ThrottleConsumption = 0.1f;
            m_HighSpeedConsumption = 0.03f;
            m_HighSpeedReferenceKph = 120f;
            m_ConsumptionTickInterval = 0.25f;
        }

        private void ResolveReferences()
        {
            if (m_Traits == null) m_Traits = GetComponent<Traits>();
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
        }

        private void Bind()
        {
            if (m_IsBound) return;
            ResolveReferences();

            try
            {
                m_RuntimeFuel = m_Traits != null
                    ? m_Traits.RuntimeAttributes.Get(m_FuelAttributeId)
                    : null;
            }
            catch (Exception exception)
            {
                if (!m_HasWarnedMissingAttribute)
                {
                    m_HasWarnedMissingAttribute = true;
                    Debug.LogWarning(
                        $"Car fuel Attribute '{m_FuelAttributeId}' is unavailable: " +
                        exception.Message,
                        this
                    );
                }
                return;
            }

            if (m_RuntimeFuel == null) return;
            InitializeStartingFuel();
            m_Traits.RuntimeAttributes.EventChange += OnAttributeChanged;
            m_IsBound = true;
            NotifyFuelAvailability();
            PublishFuel();
            if (m_EngineRequested && HasFuel) EnsureConsumption();
        }

        private void InitializeStartingFuel()
        {
            if (m_HasInitializedStartingFuel || m_RuntimeFuel == null) return;
            m_HasInitializedStartingFuel = true;
            if (!m_InitializeOnFirstEnable) return;

            m_RuntimeFuel.Value = Mathf.Clamp(
                m_StartingFuel,
                (float)m_RuntimeFuel.MinValue,
                (float)m_RuntimeFuel.MaxValue
            );
        }

        private void Unbind()
        {
            if (!m_IsBound) return;
            if (m_Traits != null)
                m_Traits.RuntimeAttributes.EventChange -= OnAttributeChanged;
            m_RuntimeFuel = null;
            m_IsBound = false;
            m_HasPublishedFuelAvailability = false;
        }

        private void EnsureConsumption()
        {
            if (m_ConsumptionRoutine != null || !isActiveAndEnabled) return;
            m_ConsumptionRoutine = StartCoroutine(ConsumeFuelWhileEngineRuns());
        }

        private IEnumerator ConsumeFuelWhileEngineRuns()
        {
            float interval = Mathf.Clamp(m_ConsumptionTickInterval, 0.1f, 0.5f);
            WaitForSeconds wait = new WaitForSeconds(interval);

            while (m_EngineRequested && isActiveAndEnabled && HasFuel)
            {
                yield return wait;
                if (!m_EngineRequested || !isActiveAndEnabled || !HasFuel) break;

                float throttle = m_Driver != null ? m_Driver.ThrottleMagnitude : 0f;
                float speedRatio = m_Driver != null
                    ? Mathf.Clamp01(m_Driver.SpeedKph / Mathf.Max(1f, m_HighSpeedReferenceKph))
                    : 0f;
                float consumptionPerSecond = m_IdleConsumption +
                    m_ThrottleConsumption * throttle +
                    m_HighSpeedConsumption * speedRatio;
                Consume(consumptionPerSecond * interval);
            }

            m_ConsumptionRoutine = null;
        }

        private void StopConsumption()
        {
            if (m_ConsumptionRoutine == null) return;
            StopCoroutine(m_ConsumptionRoutine);
            m_ConsumptionRoutine = null;
        }

        private void OnAttributeChanged(IdString attributeId)
        {
            if (!string.Equals(
                    attributeId.String,
                    m_FuelAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            NotifyFuelAvailability();
            PublishFuel();
            if (m_EngineRequested && HasFuel) EnsureConsumption();
        }

        private void NotifyFuelAvailability()
        {
            bool hasFuel = HasFuel;
            if (m_HasPublishedFuelAvailability &&
                m_LastPublishedHasFuel == hasFuel)
            {
                return;
            }

            m_HasPublishedFuelAvailability = true;
            m_LastPublishedHasFuel = hasFuel;
            m_Driver?.SetFuelAvailable(hasFuel);
        }

        private void PublishFuel()
        {
            if (m_RuntimeFuel == null) return;
            EventFuelChanged?.Invoke(CurrentFuel, MaximumFuel);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(m_FuelAttributeId))
                m_FuelAttributeId = "fuel-attribute-id";
            m_StartingFuel = Mathf.Max(0f, m_StartingFuel);
            m_IdleConsumption = Mathf.Max(0f, m_IdleConsumption);
            m_ThrottleConsumption = Mathf.Max(0f, m_ThrottleConsumption);
            m_HighSpeedConsumption = Mathf.Max(0f, m_HighSpeedConsumption);
            m_HighSpeedReferenceKph = Mathf.Max(1f, m_HighSpeedReferenceKph);
            m_ConsumptionTickInterval = Mathf.Clamp(
                m_ConsumptionTickInterval,
                0.1f,
                0.5f
            );
        }
    }
}
