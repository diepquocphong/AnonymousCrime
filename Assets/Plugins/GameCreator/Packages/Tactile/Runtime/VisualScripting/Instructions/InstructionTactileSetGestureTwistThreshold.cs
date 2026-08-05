using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Twist Threshold")]
    [Category("Tactile/Gesture Pad/Set Twist Threshold")]
    [Description("Sets the threshold for activating a twist of a Gesture Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter(
        "Threshold", 
        "The threshold for activating the twist gesture"
    )]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Activate")]

    [Serializable]
    public class InstructionTactileSetGestureTwistThreshold : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Threshold = GetDecimalDecimal.Create(0.1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Twist Threshold = {this.m_Threshold}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                pad.TwistThreshold = (float) this.m_Threshold.Get(args);
            }

            return DefaultResult;
        }
    }
}