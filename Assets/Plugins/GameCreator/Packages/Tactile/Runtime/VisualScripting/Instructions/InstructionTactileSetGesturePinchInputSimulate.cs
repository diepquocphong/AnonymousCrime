using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Pinch Input Simulate")]
    [Category("Tactile/Gesture Pad/Set Pinch Input Simulate")]
    [Description("Overrides the control path of a Gesture Pad's Pinch Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter("Control Path", "The Axis control path to be set in Input Simulate")]

    [Image(typeof(IconGesture), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetGesturePinchInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeReference] 
        private ControlPathAxis m_ControlPath = new ControlPathAxisConstantNone();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Pinch Input Simulate = {this.m_ControlPath}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad gesture) 
            {
                gesture.PinchInputSimulate.OverrideControlPath(this.m_ControlPath);
            }

            return DefaultResult;
        }
    }
}