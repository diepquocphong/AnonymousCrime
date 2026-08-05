using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Deadzone")]
    [Category("Tactile/Steering Wheel/Set Deadzone")]
    [Description("Sets the deadzone of a Steering Wheel")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Steering Wheel"
    )]

    [Parameter(
        "Deadzone", 
        "The radius where dragging of the Steering Wheel are ignored"
    )]

    [Image(typeof(IconSteerWheel), ColorTheme.Type.Red)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetWheelDeadzone : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Deadzone = GetDecimalDecimal.Create(60f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Deadzone = {this.m_Deadzone}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steerWheel) 
            {
                steerWheel.MaxAngle = (float) this.m_Deadzone.Get(args);
            }

            return DefaultResult;
        }
    }
}