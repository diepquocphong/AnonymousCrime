using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Direction")]
    [Category("Tactile/Analog Stick/Stick Direction")]
    [Description("Gets the direction of a Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDirectionTactileStickDirection : PropertyTypeGetDirection
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.StickDirection";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick) 
            {
                return stick.StickMotion.normalized;
            }

            return Vector3.zero;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IStickType stick) 
            {
                return stick.StickMotion.normalized;
            }

            return Vector3.zero;
        }

    }
}