using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Arrow Damping")]
    [Category("Tactile/Analog Stick/Set Arrow Damping")]
    [Description("Sets the damping of an Analog Stick's arrow")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter("Damping", "The smoothness applied to the Analog Stick's arrow movement")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Blue)]
    [Keywords("Tactile", "Lerp", "Interpolate", "Smoothness")]

    [Serializable]
    public class InstructionTactileSetStickArrowDamping : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Damping = GetDecimalDecimal.Create(0.125);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Arrow Damping = {this.m_Damping:N3}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.ArrowDamping = (float)this.m_Damping.Get(args);
            }

            return DefaultResult;
        }
    }
}