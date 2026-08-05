using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Pinch Input Simulate")]
    [Category("Tactile/Gesture Pad/Reset Pinch Input Simulate")]
    [Description("Resets the control path of a Gesture Pad's Pinch Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Image(typeof(IconGesture), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileResetGesturePinchInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Reset {this.m_TactileControl} Pinch Input Simulate";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad gesture) 
            {
                gesture.PinchInputSimulate.ResetOverrideControlPath();
            }

            return DefaultResult;
        }
    }
}