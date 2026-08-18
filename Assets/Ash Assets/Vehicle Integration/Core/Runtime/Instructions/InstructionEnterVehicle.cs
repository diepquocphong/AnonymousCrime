using System;
using System.Threading.Tasks;
using FranklinGame.Animations;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Enter Vehicle")]
    [Description("Triggers a character to enter the vehicle on the specified GameObject using its CarEntry or BikeEntry component")]
    [Category("Vehicles/Enter Vehicle")]
    [Parameter("Target", "The GameObject containing the CarEntry or BikeEntry component")]
    [Parameter("Character", "The character that should enter the vehicle")]
    [Keywords("Car", "Bike", "Vehicle", "Enter")]
    [Image(typeof(IconExit), ColorTheme.Type.Green)]
    [Serializable]
    public class InstructionEnterVehicle : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_Target = new PropertyGetGameObject();
        [SerializeField] private PropertyGetGameObject m_Character = new PropertyGetGameObject();

        public override string Title => "Enter Vehicle";

        protected override async Task Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return;

            GameObject characterGO = this.m_Character.Get(args);
            if (characterGO == null) return;

            Character character = characterGO.GetComponent<Character>();
            if (character == null) return;

            CarEntry carEntry = target.GetComponent<CarEntry>();
            BikeEntry bikeEntry = carEntry == null
                ? target.GetComponent<BikeEntry>()
                : null;
            Component vehicleEntry = carEntry != null
                ? carEntry
                : bikeEntry;
            if (vehicleEntry == null) return;

            if (character.IsPlayer)
            {
                FranklinVehicleInteractionManager manager =
                    character.GetComponentInChildren<
                        FranklinVehicleInteractionManager
                    >(true);
                if (manager != null)
                {
                    manager.RequestSpecificVehicleInteraction(vehicleEntry);
                    await Task.Yield();
                    return;
                }
            }

            if (carEntry != null)
            {
                carEntry.EnterCar(character);
            }
            else if (bikeEntry != null)
            {
                bikeEntry.EnterBike(character);
            }

            await Task.Delay(10);
        }
    }
}
