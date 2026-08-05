using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using GameCreator.Runtime.VisualScripting;

namespace GameCreator.Runtime.VisualScripting
{
    [Title("In Vehicle")]
    [Description("Returns true if the character is already inside the vehicle (car or bike), determined by its parent being the vehicle's entry parent.")]
    [Category("Vehicles/Character in vehicle")]
    [Parameter("Vehicle", "The GameObject containing the CarEntry or BikeEntry component")]
    [Parameter("Character", "The character to check")]
    [Keywords("Car", "Bike", "Vehicle", "Inside", "Occupant")]
    [Image(typeof(IconFace), ColorTheme.Type.Blue)]
    [Serializable]
    public class ConditionInVehicle : Condition
    {
        [SerializeField] private PropertyGetGameObject m_Vehicle = new PropertyGetGameObject();
        [SerializeField] private PropertyGetGameObject m_Character = new PropertyGetGameObject();

        protected override string Summary => $"Is {this.m_Character} in vehicle {this.m_Vehicle}?";

        protected override bool Run(Args args)
        {
            GameObject vehicleObject = this.m_Vehicle.Get(args);
            GameObject characterObject = this.m_Character.Get(args);
            if (vehicleObject == null || characterObject == null) return false;

            CarEntry carEntry = vehicleObject.GetComponent<CarEntry>();
            if (carEntry != null && carEntry.entryParent != null)
            {
                if (characterObject.transform.parent == carEntry.entryParent) return true;
            }

            BikeEntry bikeEntry = vehicleObject.GetComponent<BikeEntry>();
            if (bikeEntry != null && bikeEntry.entryParent != null)
            {
                if (characterObject.transform.parent == bikeEntry.entryParent) return true;
            }

            return false;
        }
    }
}
