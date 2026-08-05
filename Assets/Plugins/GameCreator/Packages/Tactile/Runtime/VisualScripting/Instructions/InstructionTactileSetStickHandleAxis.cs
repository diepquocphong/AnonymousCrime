using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

using HandleAxis = Niam.Runtime.Tactile.ControlTypeAnalogStick.HandleAxis;

namespace Niam.Runtime.Tactile
{
    [Title("Set Handle Axis")]
    [Category("Tactile/Analog Stick/Set Handle Axis")]
    [Description("Sets the axis of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter(
        "Axis", 
        "The axis of movement for the Analog Stick's handle (e.g., X, Y, or both)"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetStickHandleAxis : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private HandleAxis m_Axis = HandleAxis.BothXY;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Handle Axis to {this.m_Axis}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.Axis = this.m_Axis;
            }

            return DefaultResult;
        }
    }
}