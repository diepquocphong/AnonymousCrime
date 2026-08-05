using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Delta XZ")]
    [Category("Tactile/Gesture Pad/Pan Delta XZ")]

    [Description(
        "Gets the pan delta value of a Gesture Pad along the XZ axis"
    )]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetPositionTactilePanDeltaXZ : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.PanDelta.XZ";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return new Vector3(pad.PanDelta.x, 0, pad.PanDelta.y);
            }
            
            return Vector3.zero;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return new Vector3(pad.PanDelta.x, 0, pad.PanDelta.y);
            }
            
            return Vector3.zero;
        }

    }
}