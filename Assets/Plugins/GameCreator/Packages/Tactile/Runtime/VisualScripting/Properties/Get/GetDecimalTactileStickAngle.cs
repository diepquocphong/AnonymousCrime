using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Angle")]
    [Category("Tactile/Analog Stick/Stick Angle")]
    [Description("Gets the absolute angle (0 to 360) of an Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileStickAngle : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.StickAngle";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick)
            {
                Vector2 stickDirection = stick.StickRawMotion.normalized;
                float angle = Vector2.SignedAngle(Vector2.up, stickDirection);
                return (angle < 0f) ? 360f + angle : angle;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IStickType stick)
            {
                Vector2 stickDirection = stick.StickRawMotion.normalized;
                float angle = Vector2.SignedAngle(Vector2.up, stickDirection);
                return (angle < 0f) ? 360f + angle : angle;
            }

            return 0f;
        }

    }
}