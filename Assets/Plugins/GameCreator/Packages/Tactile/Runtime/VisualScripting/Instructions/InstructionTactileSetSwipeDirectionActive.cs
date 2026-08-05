using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Swipe Direction Active")]
    [Category("Tactile/Swipe Pad/Set Swipe Direction Active")]
    [Description("Sets the direction of a Swipe Pad enabled/disabled")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Swipe ID",
        "The string identifier of the swipe direction in Swipe Pad"
    )]

    [Parameter(
        "Active", 
        "The bool value that determines whether to enable/disable the swipe direction"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeDirectionActive : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetString m_SwipeId = GetStringString.Create;

        [SerializeField] 
        private PropertyGetBool m_Active = GetBoolValue.Create(true);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl}[{this.m_SwipeId}] Enabled = {this.m_Active}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad swipePad) 
            {
                var swipeId = new IdString(this.m_SwipeId.Get(args));
                SwipeDirection direction = swipePad.Directions.GetDirectionByHash(swipeId.Hash);
                if (direction != null) direction.IsActive = this.m_Active.Get(args);
            }

            return DefaultResult;
        }
    }
}