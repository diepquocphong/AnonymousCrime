using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Surface Dynamic")]
    [Category("Tactile/Analog Stick/Is Surface Dynamic")]
    [Description("Sets the dynamic of an Analog Stick's surface")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]
    
    [Parameter(
        "Is Dynamic", 
        "Whether to move the position of the surface towards the touched point"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Reposition", "Move", "Transition")]

    [Serializable]
    public class InstructionTactileIsStickSurfaceDynamic : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsDynamic = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Surface Dynamic = {this.m_IsDynamic}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsDynamic = this.m_IsDynamic.Get(args);
            }

            return DefaultResult;
        }
    }
}