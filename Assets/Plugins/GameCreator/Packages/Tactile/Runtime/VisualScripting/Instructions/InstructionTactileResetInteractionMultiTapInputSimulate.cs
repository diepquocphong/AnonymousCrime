using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Multi-Tap Input Simulate")]
    [Category("Tactile/Interaction Pad/Reset Multi-Tap Input Simulate")]
    [Description("Resets the control path of a Interaction Pad's Multi-Tap Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Interaction Pad"
    )]

    [Parameter("Index", "The index of a desired multi-tap to reset")]

    [Image(typeof(IconCharacterInteract), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileResetInteractionMultiTapInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField, Min(0)] 
        private int m_Index = 0;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Reset {this.m_TactileControl} Multi-Tap[{this.m_Index}] Input Simulate";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeInteractionPad pad) 
            {
                pad.GetMultiTapInputSimulate(this.m_Index)?.ResetOverrideControlPath();
            }

            return DefaultResult;
        }
    }
}