using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Swipe Direction Active")]
    [Category("Tactile/Swipe Pad/Is Swipe Direction Active")]

    [Description(
        "Returns true if the direction of a Swipe Pad is active; otherwise, returns false"
    )]

    [Parameter(
        "Tactile Control",
        "The game object with Tactile Control component attached with Swipe Pad control type"
    )]

    [Parameter(
        "Swipe Id",
        "The string identifier of the swipe direction in Swipe Pad"
    )]
    
    [Image(typeof(IconSwipe), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSwipeDirectionActive : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetString m_SwipeId = GetStringString.Create;

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => 
            $"Is {this.m_TactileControl}[{this.m_SwipeId}] Active";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad swipePad)
            {
                var swipeId = new IdString(this.m_SwipeId.Get(args));
                SwipeDirection direction = swipePad.Directions.GetDirectionByHash(swipeId.Hash);
                if (direction != null && direction.IsActive) return true;
            }

            return false;
        }
    }
}
