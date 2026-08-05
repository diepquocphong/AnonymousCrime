using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Angle (Signed)")]
    [Category("Tactile/Analog Stick/Stick Angle (Signed)")]
    [Description("Gets the signed angle (180 to -180) of an Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileStickSignedAngle : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.StickAngle.Signed";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick)
            {
                Vector2 stickDirection = stick.StickRawMotion.normalized;
                return Vector2.SignedAngle(Vector2.up, stickDirection);
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IStickType stick)
            {
                Vector2 stickDirection = stick.StickRawMotion.normalized;
                return Vector2.SignedAngle(Vector2.up, stickDirection);
            }

            return 0f;
        }

    }
}