using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Common refueling contract shared by Franklin Cars and Bikes.
    /// </summary>
    public interface IFranklinFuelTank
    {
        GameObject VehicleObject { get; }
        float CurrentFuel { get; }
        float MaximumFuel { get; }
        float FuelRatio { get; }
        float SpeedKph { get; }
        bool IsPlayerControlled { get; }

        /// <returns>The amount of fuel that was actually accepted.</returns>
        float TryRefuel(float requestedAmount);
    }
}
