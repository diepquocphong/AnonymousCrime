using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Pinch Sensitivity")]
    [Category("Tactile/Gesture Pad/Set Pinch Sensitivity")]
    [Description("Sets the sensitivity for pinching of a Gesture Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter(
        "Sensitivity", 
        "The multiplier that adjusts the delta value of pinch"
    )]

    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile", "Multiplier")]

    [Serializable]
    public class InstructionTactileSetGesturePinchSensitivity : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Sensitivity = GetDecimalDecimal.Create(1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Pinch Sensitivity = {this.m_Sensitivity}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                pad.PinchSensitivity =  (float) this.m_Sensitivity.Get(args);
            }

            return DefaultResult;
        }
    }
}