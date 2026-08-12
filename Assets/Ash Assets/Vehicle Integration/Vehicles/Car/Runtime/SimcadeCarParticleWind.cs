using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Applies low-frequency world-space wind acceleration to the prebuilt Car
    /// smoke/fire particles. It has no per-frame Update and runs only while a
    /// loop effect is visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimcadeCarParticleWind : MonoBehaviour
    {
        [SerializeField] private Rigidbody m_CarBody;
        [SerializeField] private ParticleSystem[] m_AffectedParticles =
            System.Array.Empty<ParticleSystem>();

        [Header("World Wind")]
        [SerializeField] private Vector3 m_WorldWindDirection =
            new Vector3(1f, 0f, 0.35f);
        [SerializeField, Min(0f)] private float m_WorldWindAcceleration = 0.9f;
        [SerializeField, Range(0f, 0.5f)] private float m_VehicleAirflowFactor = 0.14f;
        [SerializeField, Min(0.1f)] private float m_MaximumWindAcceleration = 6f;
        [SerializeField, Range(2f, 12f)] private float m_UpdateRateHz = 8f;

        private Coroutine m_WindRoutine;
        private bool m_ShouldRun;

        public bool IsConfigured => m_CarBody != null && m_AffectedParticles != null &&
            m_AffectedParticles.Length >= 4 && m_UpdateRateHz <= 8.01f;
        public float UpdateRateHz => m_UpdateRateHz;

        public void Configure(
            Rigidbody carBody,
            ParticleSystem[] affectedParticles)
        {
            m_CarBody = carBody;
            m_AffectedParticles = affectedParticles ?? System.Array.Empty<ParticleSystem>();
        }

        public void SetWindActive(bool active)
        {
            m_ShouldRun = active;
            if (!active)
            {
                if (m_WindRoutine != null) StopCoroutine(m_WindRoutine);
                m_WindRoutine = null;
                return;
            }

            ApplyWind();
            if (isActiveAndEnabled && m_WindRoutine == null)
                m_WindRoutine = StartCoroutine(UpdateWindAtLowFrequency());
        }

        private void OnEnable()
        {
            if (m_ShouldRun && m_WindRoutine == null)
                m_WindRoutine = StartCoroutine(UpdateWindAtLowFrequency());
        }

        private void OnDisable()
        {
            if (m_WindRoutine != null) StopCoroutine(m_WindRoutine);
            m_WindRoutine = null;
        }

        private IEnumerator UpdateWindAtLowFrequency()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
                1f / Mathf.Max(2f, m_UpdateRateHz)
            );
            while (m_ShouldRun)
            {
                yield return wait;
                ApplyWind();
            }
            m_WindRoutine = null;
        }

        private void ApplyWind()
        {
            Vector3 direction = m_WorldWindDirection;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();

            Vector3 airflow = direction * m_WorldWindAcceleration;
            if (m_CarBody != null)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(
                    m_CarBody.linearVelocity,
                    Vector3.up
                );
                airflow -= planarVelocity * m_VehicleAirflowFactor;
            }
            airflow = Vector3.ClampMagnitude(airflow, m_MaximumWindAcceleration);

            for (int i = 0; i < m_AffectedParticles.Length; ++i)
            {
                ParticleSystem particles = m_AffectedParticles[i];
                if (particles == null) continue;

                ParticleSystem.ForceOverLifetimeModule force =
                    particles.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = new ParticleSystem.MinMaxCurve(airflow.x);
                force.y = new ParticleSystem.MinMaxCurve(airflow.y);
                force.z = new ParticleSystem.MinMaxCurve(airflow.z);
            }
        }

        private void OnValidate()
        {
            m_WorldWindAcceleration = Mathf.Max(0f, m_WorldWindAcceleration);
            m_VehicleAirflowFactor = Mathf.Clamp(m_VehicleAirflowFactor, 0f, 0.5f);
            m_MaximumWindAcceleration = Mathf.Max(0.1f, m_MaximumWindAcceleration);
            m_UpdateRateHz = Mathf.Clamp(m_UpdateRateHz, 2f, 8f);
            if (m_AffectedParticles == null)
                m_AffectedParticles = System.Array.Empty<ParticleSystem>();
        }
    }
}
