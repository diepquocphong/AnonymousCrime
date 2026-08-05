using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Input Simulate")]
    [Category("Tactile/Analog Stick/Reset Input Simulate")]
    [Description("Resets the control path of an Analog Stick's Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileResetStickInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Reset {this.m_TactileControl} Input Simulate";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.InputSimulate.ResetOverrideControlPath();
            }

            return DefaultResult;
        }
    }
}