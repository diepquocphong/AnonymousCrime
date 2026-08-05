using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Delta Y")]
    [Category("Tactile/Gesture Pad/Pan Delta Y")]
    [Description("Gets the pan delta y value of a Gesture Pad")]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactilePanDeltaY : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.PanDelta.Y";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                return pad.PanDelta.y;
            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                return pad.PanDelta.y;
            return 0f;
        }

    }
}