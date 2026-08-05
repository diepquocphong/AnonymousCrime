using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Arrow Offset")]
    [Category("Tactile/Analog Stick/Set Arrow Offset")]

    [Description(
        "Sets the angle offset in degree that adjusts the alignment steps of a Analog Stick's " +
        "arrow indicator"
    )]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter("Offset", "The angle in degree to offset the arrow steps")]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Activate", "Toggle", "Enable")]

    [Serializable]
    public class InstructionTactileSetStickArrowOffset : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Offset = GetDecimalDecimal.Create(0f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Arrow Offset = {this.m_Offset}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.ArrowDegreeOffset = (float) this.m_Offset.Get(args);
            }

            return DefaultResult;
        }
    }
}