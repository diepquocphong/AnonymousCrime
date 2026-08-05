using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Magnitude")]
    [Category("Tactile/Analog Stick/Stick Magnitude")]
    [Description("Gets the magnitude value of an Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileStickMagnitude : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private bool m_Unclamped = false;

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => 
            $"{this.m_TactileControl}.StickMagnitude{(this.m_Unclamped ? ".Unclamp" : "" )}";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick)
            {
                return this.m_Unclamped 
                    ? stick.StickRawMotion.magnitude
                    : stick.StickMotion.magnitude;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IStickType stick) 
            {
                return this.m_Unclamped 
                    ? stick.StickRawMotion.magnitude
                    : stick.StickMotion.magnitude;
            }

            return 0f;
        }

    }
}