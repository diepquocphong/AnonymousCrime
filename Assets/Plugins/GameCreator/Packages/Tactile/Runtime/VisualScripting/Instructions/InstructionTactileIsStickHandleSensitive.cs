using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Handle Sensitive")]
    [Category("Tactile/Analog Stick/Is Handle Sensitive")]

    [Description("Sets the sensitive value of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]
    
    [Parameter(
        "Is Sensitive", 
        "Whether to immediately respond to user input upon pressed or only when dragging"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Responsive", "Immediate")]

    [Serializable]
    public class InstructionTactileIsStickHandleSensitive : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsSensitive = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Handle Sensitive = {this.m_IsSensitive}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsRelative = this.m_IsSensitive.Get(args);
            }

            return DefaultResult;
        }
    }
}