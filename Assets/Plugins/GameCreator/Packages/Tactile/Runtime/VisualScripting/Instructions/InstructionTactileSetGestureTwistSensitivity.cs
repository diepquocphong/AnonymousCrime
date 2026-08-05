using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Twist Sensitivity")]
    [Category("Tactile/Gesture Pad/Set Twist Sensitivity")]
    [Description("Sets the sensitivity for twisting of a Gesture Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter(
        "Sensitivity", 
        "The multiplier that adjusts the delta value of twist"
    )]

    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile", "Multiplier")]

    [Serializable]
    public class InstructionTactileSetGestureTwistSensitivity : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Sensitivity = GetDecimalDecimal.Create(1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Twist Sensitivity = {this.m_Sensitivity}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                pad.TwistSensitivity =  (float) this.m_Sensitivity.Get(args);
            }

            return DefaultResult;
        }
    }
}