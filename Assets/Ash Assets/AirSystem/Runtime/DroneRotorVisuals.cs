using System;
using UnityEngine;

namespace FranklinGame.AirSystem
{
    /// <summary>Low-frequency rotor animation that is completely idle while parked.</summary>
    [DisallowMultipleComponent]
    public sealed class DroneRotorVisuals : MonoBehaviour
    {
        [Serializable]
        private struct Rotor
        {
            [SerializeField] private Transform m_Transform;
            [SerializeField] private Vector3 m_Axis;
            [SerializeField] private bool m_Reverse;

            public void Rotate(float angle)
            {
                if (this.m_Transform == null) return;
                float direction = this.m_Reverse ? -1f : 1f;
                this.m_Transform.Rotate(this.m_Axis, angle * direction, Space.Self);
            }
        }

        [SerializeField] private DroneFlightController m_Drone;
        [SerializeField] private Rotor[] m_Rotors = Array.Empty<Rotor>();
        [SerializeField, Min(60f)] private float m_RotationSpeed = 1440f;
        [SerializeField, Range(15f, 60f)] private float m_MaxVisualUpdatesPerSecond = 30f;

        private float m_Accumulator;

        private void Awake()
        {
            if (this.m_Drone == null)
            {
                this.m_Drone = this.GetComponent<DroneFlightController>();
            }
        }

        private void OnDisable()
        {
            this.m_Accumulator = 0f;
        }

        private void OnValidate()
        {
            this.m_RotationSpeed = Mathf.Max(60f, this.m_RotationSpeed);
            this.m_MaxVisualUpdatesPerSecond = Mathf.Clamp(
                this.m_MaxVisualUpdatesPerSecond,
                15f,
                60f
            );
        }

        private void Update()
        {
            bool shouldSpin = this.m_Drone != null &&
                              (this.m_Drone.IsPiloted || this.m_Drone.IsReturningHome);
            if (!shouldSpin || this.m_Rotors.Length == 0)
            {
                this.m_Accumulator = 0f;
                return;
            }

            this.m_Accumulator += Time.deltaTime;
            float interval = 1f / this.m_MaxVisualUpdatesPerSecond;
            if (this.m_Accumulator < interval) return;

            float angle = this.m_RotationSpeed * this.m_Accumulator;
            this.m_Accumulator = 0f;
            for (int i = 0; i < this.m_Rotors.Length; ++i)
            {
                this.m_Rotors[i].Rotate(angle);
            }
        }
    }
}
