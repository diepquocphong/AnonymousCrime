using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Runtime trigger generated at a fuel-station Marker. Contact counting is
    /// handled by the station because a vehicle can own several colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinFuelStationZone : MonoBehaviour
    {
        private FranklinFuelStation m_Station;

        internal void Initialize(FranklinFuelStation station)
        {
            this.m_Station = station;
        }

        private void OnTriggerEnter(Collider other)
        {
            this.m_Station?.RegisterContact(other);
        }

        private void OnTriggerExit(Collider other)
        {
            this.m_Station?.UnregisterContact(other);
        }
    }
}
