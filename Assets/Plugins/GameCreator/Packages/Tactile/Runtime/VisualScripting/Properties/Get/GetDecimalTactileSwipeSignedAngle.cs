using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Swipe Angle (Signed)")]
    [Category("Tactile/Swipe Pad/Swipe Angle (Signed)")]
    [Description("Gets the last swipe angle (-180 to 180) of a Swipe Pad")]
    
    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileSwipeSignedAngle : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.SwipeAngle.Signed";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
                return pad.SwipeSignedAngle;
            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
                return pad.SwipeSignedAngle;
            return 0f;
        }

    }
}