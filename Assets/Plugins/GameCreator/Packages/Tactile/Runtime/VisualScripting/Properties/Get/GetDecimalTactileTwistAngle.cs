using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Twist Angle")]
    [Category("Tactile/Gesture Pad/Twist Angle")]
    [Description("Gets the twist angle (0 to 360) of a Gesture Pad")]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileTwistAngle : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.TwistAngle";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return pad.TwistAngle;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return pad.TwistAngle;
            }
            
            return 0f;
        }
        
    }
}