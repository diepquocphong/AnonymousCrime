using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using GameCreator.Runtime.VisualScripting;

[Title("Is Vehicle Enabled")]
[Description("Returns true if the target GameObject has a PhysicsCarController or PhysicsBikeController component and the vehicle is enabled.")]
[Category("Vehicles/Vehicle Enabled")]
[Parameter("Game Object", "The GameObject containing the vehicle controller component")]
[Keywords("Car", "Bike", "Vehicle", "Enabled", "Condition")]
[Image(typeof(IconPhysics), ColorTheme.Type.Blue)]
[Serializable]
public class ConditionCarEnabled : Condition
{
    [SerializeField] private PropertyGetGameObject m_Target = new PropertyGetGameObject();

    protected override string Summary => $"Vehicle Enabled on {this.m_Target}";

    protected override bool Run(Args args)
    {
        GameObject target = this.m_Target.Get(args);
        if (target == null) return false;

        foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IRvrVehicleDriveController externalController)
            {
                return externalController.IsVehicleEnabled;
            }
        }

        PhysicsCarController carController = target.GetComponent<PhysicsCarController>();
        if (carController != null)
        {
            return carController.isVehicleEnabled;
        }

        PhysicsBikeController bikeController = target.GetComponent<PhysicsBikeController>();
        if (bikeController != null)
        {
            return bikeController.isVehicleEnabled;
        }

        HoverVehicleController hoverController = target.GetComponent<HoverVehicleController>();
        if (hoverController != null)
        {
            return hoverController.isVehicleEnabled;
        }


        return false;
    }
}
