using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>Trigger installed on each GC2 Marker in the mobile garage.</summary>
    [DisallowMultipleComponent]
    public sealed class FranklinGarageZone : MonoBehaviour
    {
        private FranklinGarageService m_Garage;

        internal void Initialize(FranklinGarageService garage)
        {
            this.m_Garage = garage;
        }

        private void OnTriggerEnter(Collider other)
        {
            this.m_Garage?.RegisterContact(other);
        }

        private void OnTriggerExit(Collider other)
        {
            this.m_Garage?.UnregisterContact(other);
        }
    }
}
