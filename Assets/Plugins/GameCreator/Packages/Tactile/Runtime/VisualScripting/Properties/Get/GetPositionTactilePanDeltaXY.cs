using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Delta XY")]
    [Category("Tactile/Gesture Pad/Pan Delta XY")]

    [Description(
        "Gets the pan delta value of a Gesture Pad along the XY axis"
    )]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetPositionTactilePanDeltaXY : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.PanDelta.XY";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return pad.PanDelta;
            }

            return Vector3.zero;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                return pad.PanDelta;
            }

            return Vector3.zero;
        }

    }
}