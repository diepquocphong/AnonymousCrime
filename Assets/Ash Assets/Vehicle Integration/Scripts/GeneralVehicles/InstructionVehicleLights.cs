using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Set Vehicle Lights")]
    [Description("Turns the vehicle lights on or off on the specified GameObject")]
    [Category("Vehicles/Vehicle Lights")]
    [Parameter("Target", "The GameObject containing the VehicleLights component")]
    [Parameter("Lights On", "If true, turns the lights on; if false, turns the lights off")]
    [Keywords("Lights", "Vehicle", "Headlights", "Tail Lights", "On", "Off")]
    [Image(typeof(IconLight), ColorTheme.Type.Yellow)]
    [Serializable]
    public class InstructionVehicleLights : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_Target = new PropertyGetGameObject();
        [SerializeField] private bool m_LightsOn = true;

        public override string Title => $"Set Vehicle Lights {(m_LightsOn ? "On" : "Off")}";

        protected override async Task Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return;

            VehicleLights lights = target.GetComponent<VehicleLights>();
            if (lights != null)
            {
                if (m_LightsOn)
                {
                    lights.LightsOn();
                }
                else
                {
                    lights.LightsOff();
                }
            }

            await Task.Delay(10);
        }
    }
}
