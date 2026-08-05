using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Handle Locked")]
    [Category("Tactile/Analog Stick/Is Handle Locked")]
    [Description("Sets the locking state of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]
    
    [Parameter(
        "Is Lock", 
        "Whether the Analog Stick's handle should remain in its current position instead of " +
        "returning to the center"
    )]

    [Image(typeof(IconJoystick), ColorTheme.Type.Red)]
    [Keywords("Tactile", "In Place", "Remain", "Stay")]

    [Serializable]
    public class InstructionTactileIsStickHandleLocked : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsLocked = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Handle Locked = {this.m_IsLocked}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.IsLocked = this.m_IsLocked.Get(args);
            }

            return DefaultResult;
        }
    }
}