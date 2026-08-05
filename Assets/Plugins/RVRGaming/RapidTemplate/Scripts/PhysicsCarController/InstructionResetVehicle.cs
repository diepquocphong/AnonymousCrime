using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Reset Vehicle")]
    [Description("Resets the Vehicle's orientation and velocity, making it upright.")]
    [Category("Vehicles/Reset")]
    [Parameter("Target", "The GameObject with a PhysicsCarController, PhysicsBikeController, or HoverVehicleController component")]
    [Keywords("Car", "Bike", "Hover", "Reset", "Upright", "Physics", "Controller")]
    [Image(typeof(IconInstructions), ColorTheme.Type.Blue, typeof(OverlayArrowUp))]
    public class InstructionResetVehicle : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_Target = GetGameObjectPlayer.Create();

        public override string Title => $"Reset Vehicle on {this.m_Target}";

        protected override async Task Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return;

            foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IRvrVehicleDriveController externalController)
                {
                    externalController.ResetVehicle();
                    await Task.Yield();
                    return;
                }
            }

            // Car
            PhysicsCarController carController = target.GetComponent<PhysicsCarController>();
            if (carController != null)
            {
                carController.ResetVehicle();
            }
            else
            {
                // Bike
                PhysicsBikeController bikeController = target.GetComponent<PhysicsBikeController>();
                if (bikeController != null)
                {
                    bikeController.ResetVehicle();
                }
            }

            await Task.Yield();
        }
    }
}
