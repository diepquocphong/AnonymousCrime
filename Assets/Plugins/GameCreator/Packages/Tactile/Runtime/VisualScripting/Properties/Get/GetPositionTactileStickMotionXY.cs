using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Motion XY")]
    [Category("Tactile/Analog Stick/Stick Motion XY")]
    [Description("Gets the value of an Analog Stick along the XY axis")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetPositionTactileStickMotionXY : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.StickMotion.XY";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick) 
            {
                return stick.StickMotion;
            }

            return Vector3.zero;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IStickType stick) 
            {
                return stick.StickMotion;
            }
            
            return Vector3.zero;
        }

    }
}