using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Min Swipe Distance")]
    [Category("Tactile/Swipe Pad/Set Min Swipe Distance")]
    [Description("Sets the minimum swipe distance of a Swipe Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Min Swipe Distance", 
        "The minimum distance of the ideal line to be consider a swipe"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeMinDistance : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_MinSwipeDistance = GetDecimalDecimal.Create(150f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Min Swipe Distance = {this.m_MinSwipeDistance}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                pad.MinSwipeDistance = (float) this.m_MinSwipeDistance.Get(args);
            }

            return DefaultResult;
        }
    }
}