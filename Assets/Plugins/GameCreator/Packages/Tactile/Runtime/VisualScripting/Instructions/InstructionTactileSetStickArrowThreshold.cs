using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Arrow Threshold")]
    [Category("Tactile/Analog Stick/Set Arrow Threshold")]
    [Description("Sets the threshold of an Analog Stick's arrow indicator")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter(
        "Threshold", 
        "The threshold for activating the Analog Stick's arrow"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Activate", "Toggle", "Enable")]

    [Serializable]
    public class InstructionTactileSetStickArrowThreshold : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Threshold = GetDecimalDecimal.Create(0.2f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Arrow Threshold = {this.m_Threshold}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.ArrowThreshold = (float) this.m_Threshold.Get(args);
            }

            return DefaultResult;
        }
    }
}