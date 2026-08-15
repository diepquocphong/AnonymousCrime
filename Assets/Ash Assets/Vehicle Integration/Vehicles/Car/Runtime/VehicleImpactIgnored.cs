using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Marks lightweight physics props that may still collide and play their own
    /// effects, but must not enter the shared Car/Bike damage-impact pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleImpactIgnored : MonoBehaviour
    { }
}
