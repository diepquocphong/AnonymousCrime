using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Arrow Steps")]
    [Category("Tactile/Analog Stick/Set Arrow Steps")]

    [Description(
        "Sets the number of rotation steps in circular range of an Analog Stick's arrow indicator"
    )]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter(
        "Steps", 
        "The number of rotation steps of the Analog Stick's arrow indicator"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Activate", "Toggle", "Enable")]

    [Serializable]
    public class InstructionTactileSetStickArrowSteps : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Steps = GetDecimalInteger.Create(0);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Arrow Steps = {this.m_Steps}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.ArrowDirectionSteps = (int) this.m_Steps.Get(args);
            }

            return DefaultResult;
        }
    }
}