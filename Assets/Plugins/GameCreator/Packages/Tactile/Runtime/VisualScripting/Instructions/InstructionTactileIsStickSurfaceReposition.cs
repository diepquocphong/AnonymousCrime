using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Surface Reposition")]
    [Category("Tactile/Analog Stick/Is Surface Reposition")]
    [Description("Sets the reposition of an Analog Stick's surface")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter(
        "Is Reposition", 
        "Whether to restore the original position of the surface once release"
    )]

    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Recenter", "Restore", "Return")]

    [Serializable]
    public class InstructionTactileIsStickSurfaceReposition : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsReposition = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Surface Reposition = {this.m_IsReposition}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsReposition = this.m_IsReposition.Get(args);
            }

            return DefaultResult;
        }
    }
}