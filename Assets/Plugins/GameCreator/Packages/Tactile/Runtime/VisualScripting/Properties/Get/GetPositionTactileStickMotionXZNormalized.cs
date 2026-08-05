using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Motion XZ (Normalized)")]
    [Category("Tactile/Analog Stick/Stick Motion XZ (Normalized)")]
    [Description("Gets the normalized value of an Analog Stick along the XZ axis")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetPositionTactileStickMotionXZNormalized : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.StickMotion.Normalize.XZ";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return this.GetStickValueXZ(control).normalized;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return this.GetStickValueXZ(control).normalized;
        }
        
        private Vector3 GetStickValueXZ(TactileControl control)
        {
            if (control != null && control.ControlType is IStickType stick) 
            {
                return new Vector3(stick.StickMotion.x, 0, stick.StickMotion.y);
            }
                
            return Vector3.zero;
        }

        // EDITOR: --------------------------------------------------------------------------------

        public override Vector3 EditorValue
        {
            get
            {
                GameObject target = this.m_TactileControl.EditorValue;
                if (target == null) return Vector3.zero;

                var control = target.GetComponent<TactileControl>();
                return this.GetStickValueXZ(control).normalized;
            }
        }

    }
}