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
    float SpeedMetersPerSecond { get; }

    void SetVehicleEnabled(bool state);
    void SetVehicleEnabled(bool state, bool preserveMomentum);
    void BeginExitStop();
    void CancelExitStop();
    void SetHandbrakeInput(bool active);
    void ResetVehicle();
}

/// <summary>
/// Input surface shared by Franklin's car-control HUD. Implementations keep
/// their own physics and enter/exit logic; the HUD only forwards button state.
/// </summary>
public interface IRvrVehicleInputController : IRvrVehicleDriveController
{
    void SetVirtualAccelerateInput(bool active);
    void SetVirtualSlowAccelerateInput(bool active);
    void SetVirtualBrakeReverseInput(bool active);
    void SetVirtualSteerLeftInput(bool active);
    void SetVirtualSteerRightInput(bool active);
    void SetVirtualHandbrakeInput(bool active);
    void RequestExit();
}

/// <summary>
/// Optional vehicle-state surface used by BikeEntry for presentation that only
/// applies while both wheels are clear of the ground.
/// </summary>
public interface IRvrVehicleAirborneState
{
    bool IsAirborne { get; }
}

/// <summary>
/// Optional per-vehicle collision lease used while an occupant crosses the
/// vehicle hull during authored enter/exit animation. Implementations ignore
/// only Character/vehicle collider pairs and must restore them deterministically.
/// </summary>
public interface IRvrVehicleOccupantCollisionPolicy
{
    void BeginOccupantCollisionIgnore(GameCreator.Runtime.Characters.Character character);
    void EndOccupantCollisionIgnore(GameCreator.Runtime.Characters.Character character);
}
