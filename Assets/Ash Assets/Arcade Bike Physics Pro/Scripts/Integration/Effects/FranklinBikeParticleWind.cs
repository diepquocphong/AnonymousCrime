using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Low-frequency world-space wind for Bike smoke and fire. It only runs while
    /// a loop effect is visible and therefore avoids a permanent per-frame cost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinBikeParticleWind : MonoBehaviour
    {
        private const float MobileMaximumUpdateRateHz = 4f;

        [SerializeField] private Rigidbody m_BikeBody;
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
        private bool m_HasAppliedAirflow;
        private Vector3 m_LastAppliedAirflow;
        private bool[] m_WasParticleActive = System.Array.Empty<bool>();

        public bool IsConfigured => m_BikeBody != null &&
            m_AffectedParticles != null && m_AffectedParticles.Length >= 4 &&
            m_UpdateRateHz <= 8.01f;
        public float UpdateRateHz => m_UpdateRateHz;

        public void Configure(Rigidbody bikeBody, ParticleSystem[] affectedParticles)
        {
            m_BikeBody = bikeBody;
            m_AffectedParticles = affectedParticles ??
                System.Array.Empty<ParticleSystem>();
            ResetWindCache();
        }

        public void SetWindActive(bool active)
        {
            if (m_ShouldRun == active)
            {
                if (active && isActiveAndEnabled && m_WindRoutine == null)
                {
                    ApplyWind();
                    m_WindRoutine = StartCoroutine(UpdateWindAtLowFrequency());
                }
                return;
            }

            m_ShouldRun = active;
            if (!active)
            {
                if (m_WindRoutine != null) StopCoroutine(m_WindRoutine);
                m_WindRoutine = null;
                ResetParticleActivityCache();
                return;
            }

            ApplyWind();
            if (isActiveAndEnabled && m_WindRoutine == null)
                m_WindRoutine = StartCoroutine(UpdateWindAtLowFrequency());
        }

        private void OnEnable()
        {
            if (m_ShouldRun && m_WindRoutine == null)
            {
                ApplyWind();
                m_WindRoutine = StartCoroutine(UpdateWindAtLowFrequency());
            }
        }

        private void OnDisable()
        {
            if (m_WindRoutine != null) StopCoroutine(m_WindRoutine);
            m_WindRoutine = null;
        }

        private IEnumerator UpdateWindAtLowFrequency()
        {
            float updateRateHz = Mathf.Max(2f, m_UpdateRateHz);
            if (Application.isMobilePlatform)
                updateRateHz = Mathf.Min(updateRateHz, MobileMaximumUpdateRateHz);
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
                1f / updateRateHz
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
            if (m_BikeBody != null)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(
                    m_BikeBody.linearVelocity,
                    Vector3.up
                );
                airflow -= planarVelocity * m_VehicleAirflowFactor;
            }
            airflow = Vector3.ClampMagnitude(airflow, m_MaximumWindAcceleration);

            EnsureParticleActivityCache();
            bool airflowChanged = !m_HasAppliedAirflow ||
                (airflow - m_LastAppliedAirflow).sqrMagnitude > 0.0001f;

            for (int i = 0; i < m_AffectedParticles.Length; ++i)
            {
                ParticleSystem particles = m_AffectedParticles[i];
                bool active = particles != null &&
                    particles.gameObject.activeInHierarchy && particles.isPlaying;
                bool becameActive = active && !m_WasParticleActive[i];
                m_WasParticleActive[i] = active;
                if (!active || (!airflowChanged && !becameActive)) continue;

                ParticleSystem.ForceOverLifetimeModule force =
                    particles.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = new ParticleSystem.MinMaxCurve(airflow.x);
                force.y = new ParticleSystem.MinMaxCurve(airflow.y);
                force.z = new ParticleSystem.MinMaxCurve(airflow.z);
            }

            m_LastAppliedAirflow = airflow;
            m_HasAppliedAirflow = true;
        }

        private void EnsureParticleActivityCache()
        {
            int count = m_AffectedParticles?.Length ?? 0;
            if (m_WasParticleActive == null || m_WasParticleActive.Length != count)
                m_WasParticleActive = new bool[count];
        }

        private void ResetParticleActivityCache()
        {
            EnsureParticleActivityCache();
            System.Array.Clear(m_WasParticleActive, 0, m_WasParticleActive.Length);
        }

        private void ResetWindCache()
        {
            m_HasAppliedAirflow = false;
            m_LastAppliedAirflow = Vector3.zero;
            m_WasParticleActive = System.Array.Empty<bool>();
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
