using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Handle Deadzone")]
    [Category("Tactile/Analog Stick/Set Handle Deadzone")]
    [Description("Sets the deadzone of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter("Min", "The lower bound which value below are clamped to 0")]
    [Parameter("Max", "The higher bound which value above are clamped to 1")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Red)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetStickHandleDeadzone : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Min = GetDecimalDecimal.Create(0.125);

        [SerializeField] 
        private PropertyGetDecimal m_Max = GetDecimalDecimal.Create(0.925);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Deadzone = ({1:N3}, {2:N3})",
            this.m_TactileControl,
            this.m_Min,
            this.m_Max
        );

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.DeadzoneMin = (float) this.m_Min.Get(args);
                stick.DeadzoneMax = (float) this.m_Max.Get(args);
            }

            return DefaultResult;
        }
    }
}