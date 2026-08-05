using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Swipe Direction")]
    [Category("Tactile/Swipe Pad/Swipe Direction")]
    [Description("Gets the last swipe direction of a Swipe Pad")]
    
    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDirectionTactileSwipeDirection : PropertyTypeGetDirection
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.SwipeDirection";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                return pad.SwipeDirection.normalized;
            }

            return Vector3.zero;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                return pad.SwipeDirection.normalized;
            }
            
            return Vector3.zero;
        }

    }
}