using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Handle Relative")]
    [Category("Tactile/Analog Stick/Is Handle Relative")]

    [Description("Sets the relative value of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]
    
    [Parameter(
        "Is Relative", 
        "Whether the Analog Stick's handle movement is relative to the initial touched point"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "In Place", "Remain")]

    [Serializable]
    public class InstructionTactileIsStickHandleRelative : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsRelative = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Handle Relative = {this.m_IsRelative}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsRelative = this.m_IsRelative.Get(args);
            }

            return DefaultResult;
        }
    }
}