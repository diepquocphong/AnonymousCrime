using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Min Sample Distance")]
    [Category("Tactile/Swipe Pad/Set Min Sample Distance")]
    [Description("Sets the minimum sample points distance of a Swipe Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Min Sample Distance", 
        "The minimum distance between sample points"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeMinSampleDistance : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_MinSampleDistance = GetDecimalDecimal.Create(50f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Min Sample Distance = {this.m_MinSampleDistance}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                pad.MinSampleDistance = (float) this.m_MinSampleDistance.Get(args);
            }

            return DefaultResult;
        }
    }
}