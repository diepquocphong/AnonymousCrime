using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Max Swipe Duration")]
    [Category("Tactile/Swipe Pad/Set Max Swipe Duration")]
    [Description("Sets the maximum swipe duration of a Swipe Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Max Swipe Duration", 
        "The maximum duration allowed to complete a swipe"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeMaxDuration : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_MaxSwipeDuration = GetDecimalDecimal.Create(0.4f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Max Swipe Duration = {this.m_MaxSwipeDuration}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                pad.MaxSwipeDuration = (float) this.m_MaxSwipeDuration.Get(args);
            }

            return DefaultResult;
        }
    }
}