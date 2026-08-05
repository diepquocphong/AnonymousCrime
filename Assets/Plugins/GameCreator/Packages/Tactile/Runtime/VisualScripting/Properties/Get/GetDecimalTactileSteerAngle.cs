using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Steer Angle")]
    [Category("Tactile/Steering Wheel/Steer Angle")]
    [Description("Gets the current angle of a Steering Wheel")]
    
    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileSteerAngle : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.SteerAngle";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steeringWheel) 
            {
                return steeringWheel.SteerAngle;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steeringWheel) 
            {
                return steeringWheel.SteerAngle;
            }
            
            return 0f;
        }

    }
}