using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Surface Constrain")]
    [Category("Tactile/Analog Stick/Is Surface Constrain")]
    [Description("Sets the constrain of an Analog Stick's surface")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]
    
    [Parameter(
        "Is Constrain", 
        "Whether to constrain the Analog Stick's surface within the bounds of the Rect Transform"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Limit", "Clamp")]

    [Serializable]
    public class InstructionTactileIsStickSurfaceConstrain : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsConstrain = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Surface Constrain = {this.m_IsConstrain}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsConstrain = this.m_IsConstrain.Get(args);
            }

            return DefaultResult;
        }
    }
}