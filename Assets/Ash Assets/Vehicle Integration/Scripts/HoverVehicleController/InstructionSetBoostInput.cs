using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using UnityEngine;
using GameCreator.Runtime.VisualScripting;

[Title("Set Vehicle Boost Input")]
[Description("Sets the boost input state on a HoverVehicle component. Use this to enable or disable the boost externally.")]
[Category("Vehicles/Set Boost Input")]
[Parameter("Target", "The GameObject with the HoverVehicle component")]
[Parameter("Active", "If true, boost is activated; if false, it is deactivated")]
[Keywords("Boost", "Vehicle", "HoverVehicle", "Physics", "Controller", "Set Boost")]
[Image(typeof(IconMove), ColorTheme.Type.Green)]
public class InstructionSetBoostInput : Instruction
{
    [SerializeField] private PropertyGetGameObject m_Target = GetGameObjectPlayer.Create();
    [SerializeField] private bool m_Active = true;

    public override string Title => $"Set Boost Input to {(m_Active ? "Active" : "Inactive")} on {this.m_Target}";

    protected override async Task Run(Args args)
    {
        GameObject target = this.m_Target.Get(args);
        if (target == null) return;

        HoverVehicleController hover = target.GetComponent<HoverVehicleController>();
        if (hover != null)
        {
            hover.SetBoostInput(this.m_Active);
            await Task.Yield();
        }
    }
}
