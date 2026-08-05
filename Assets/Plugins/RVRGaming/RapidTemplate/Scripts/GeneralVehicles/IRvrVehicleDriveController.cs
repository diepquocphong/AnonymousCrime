using UnityEngine;

/// <summary>
/// Small compatibility surface used by RapidTemplate's entry/exit flow.
/// Vehicle-specific driving packages can implement this without making the
/// shared RVR vehicle scripts depend on that package's assembly.
/// </summary>
public interface IRvrVehicleDriveController
{
    bool IsVehicleEnabled { get; }
    bool UseSeatEntryAlignment { get; }
    Transform VehicleBody { get; }

    void SetVehicleEnabled(bool state);
    void SetHandbrakeInput(bool active);
    void ResetVehicle();
}
