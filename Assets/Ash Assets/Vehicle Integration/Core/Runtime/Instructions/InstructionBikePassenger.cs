using System;
using System.Threading.Tasks;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    public enum BikePassengerAction
    {
        Enter,
        Exit,
        Toggle
    }

    [Version(1, 0, 0)]
    [Title("Bike Passenger")]
    [Description("Makes a GC2 Character enter, exit or toggle the rear passenger seat of a bike")]
    [Category("Vehicles/Bike/Passenger")]
    [Parameter("Action", "Enter, exit or toggle the rear bike seat")]
    [Parameter("Bike", "GameObject containing Franklin Bike Passenger Seat")]
    [Parameter("Character", "Player or NPC Character that uses the passenger seat")]
    [Keywords("Bike", "Passenger", "Pillion", "Rear", "Seat", "Enter", "Exit")]
    [Image(typeof(IconCharacter), ColorTheme.Type.Yellow)]
    [Serializable]
    public sealed class InstructionBikePassenger : Instruction
    {
        [SerializeField] private BikePassengerAction m_Action = BikePassengerAction.Toggle;
        [SerializeField] private PropertyGetGameObject m_Bike = new PropertyGetGameObject();
        [SerializeField] private PropertyGetGameObject m_Character =
            GetGameObjectPlayer.Create();

        public override string Title => $"{m_Action} Bike Passenger";

        protected override Task Run(Args args)
        {
            GameObject bike = m_Bike.Get(args);
            Character character = m_Character.Get<Character>(args);
            FranklinBikePassengerSeat passengerSeat =
                bike != null ? bike.GetComponent<FranklinBikePassengerSeat>() : null;
            if (passengerSeat == null || character == null) return DefaultResult;

            switch (m_Action)
            {
                case BikePassengerAction.Enter:
                    passengerSeat.RequestEnter(character);
                    break;
                case BikePassengerAction.Exit:
                    passengerSeat.RequestExit(character);
                    break;
                default:
                    passengerSeat.Toggle(character);
                    break;
            }
            return DefaultResult;
        }
    }
}
