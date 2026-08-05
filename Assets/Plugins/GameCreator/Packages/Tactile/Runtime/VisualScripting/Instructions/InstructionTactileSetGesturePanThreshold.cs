using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Pan Threshold")]
    [Category("Tactile/Gesture Pad/Set Pan Threshold")]
    [Description("Sets the threshold for activating a pan of a Gesture Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter(
        "Threshold", 
        "The threshold for activating the pan gesture"
    )]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Activate")]

    [Serializable]
    public class InstructionTactileSetGesturePanThreshold : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Threshold = GetDecimalDecimal.Create(0.1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Pan Threshold = {this.m_Threshold}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                pad.PanThreshold = (float) this.m_Threshold.Get(args);
            }

            return DefaultResult;
        }
    }
}