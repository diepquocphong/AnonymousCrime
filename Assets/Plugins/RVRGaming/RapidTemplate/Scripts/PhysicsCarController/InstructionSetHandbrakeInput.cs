using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Set Vehicle Handbrake Input")]
    [Description("Sets the handbrake input state on a PhysicsCarController or PhysicsBikeController. Use this to enable or disable the handbrake externally.")]
    [Category("Vehicles/Set Handbrake Input")]
    [Parameter("Target", "The GameObject with the PhysicsCarController or PhysicsBikeController component")]
    [Parameter("Active", "If true, the handbrake is activated; if false, it is deactivated")]
    [Keywords("Car", "Bike", "Handbrake", "Physics", "Controller", "Set Handbrake")]
    [Image(typeof(IconMove), ColorTheme.Type.Blue)]
    public class InstructionSetHandbrakeInput : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_Target = GetGameObjectPlayer.Create();
        [SerializeField] private bool m_Active = true;

        public override string Title => $"Set Handbrake Input to {(m_Active ? "Active" : "Inactive")} on {this.m_Target}";

        protected override async Task Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return;

            foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IRvrVehicleDriveController externalController)
                {
                    externalController.SetHandbrakeInput(m_Active);
                    await Task.Yield();
                    return;
                }
            }

            PhysicsCarController carController = target.GetComponent<PhysicsCarController>();
            if (carController != null)
            {
                carController.SetHandbrakeInput(m_Active);
                await Task.Yield();
                return;
            }

            PhysicsBikeController bikeController = target.GetComponent<PhysicsBikeController>();
            if (bikeController != null)
            {
                bikeController.SetHandbrakeInput(m_Active);
                await Task.Yield();
            }
        }
    }
}
