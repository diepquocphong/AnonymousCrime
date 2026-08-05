using System;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Are Vehicle Lights On")]
    [Description("Returns true if the target GameObject has a VehicleLights component and its lights are on")]
    [Category("Vehicles/Vehicle Lights")]
    [Parameter("Game Object", "The GameObject that contains the VehicleLights component")]
    [Keywords("Lights", "Vehicle", "On", "Condition")]
    [Image(typeof(IconLight), ColorTheme.Type.Yellow)]
    [Serializable]
    public class ConditionVehicleLightsOn : Condition
    {
        [SerializeField] private PropertyGetGameObject m_Target = new PropertyGetGameObject();

        protected override string Summary => $"Vehicle Lights On on {this.m_Target}";

        protected override bool Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return false;

            VehicleLights vehicleLights = target.GetComponent<VehicleLights>();
            if (vehicleLights == null) return false;

            return vehicleLights.AreLightsOn;
        }
    }
}
