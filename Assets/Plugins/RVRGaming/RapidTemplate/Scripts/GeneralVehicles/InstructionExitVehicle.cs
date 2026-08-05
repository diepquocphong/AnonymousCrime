using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("Exit Vehicle")]
    [Description("Triggers a character to exit the vehicle on the specified GameObject using its CarEntry or BikeEntry component")]
    [Category("Vehicles/Exit Vehicle")]
    [Parameter("Target", "The GameObject containing the CarEntry or BikeEntry component")]
    [Parameter("Character", "The character that should exit the vehicle")]
    [Keywords("Car", "Bike", "Vehicle", "Exit")]
    [Image(typeof(IconExit), ColorTheme.Type.Red)]
    [Serializable]
    public class InstructionExitVehicle : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_Target = new PropertyGetGameObject();
        [SerializeField] private PropertyGetGameObject m_Character = new PropertyGetGameObject();

        public override string Title => "Exit Vehicle";

        protected override async Task Run(Args args)
        {
            GameObject target = this.m_Target.Get(args);
            if (target == null) return;

            GameObject characterGO = this.m_Character.Get(args);
            if (characterGO == null) return;

            Character character = characterGO.GetComponent<Character>();
            if (character == null) return;

            // Try using CarEntry first. If not, try BikeEntry.
            CarEntry carEntry = target.GetComponent<CarEntry>();
            if (carEntry != null)
            {
                carEntry.ExitCar(character);
            }
            else
            {
                BikeEntry bikeEntry = target.GetComponent<BikeEntry>();
                if (bikeEntry != null)
                {
                    bikeEntry.ExitBike(character);
                }
            }

            await Task.Delay(10);
        }
    }
}
