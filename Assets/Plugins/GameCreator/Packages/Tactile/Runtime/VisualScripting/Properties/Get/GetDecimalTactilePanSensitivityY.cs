using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Sensitivity Y")]
    [Category("Tactile/Gesture Pad/Pan Sensitivity Y")]
    [Description("Gets the pan y sensitivity of a Gesture Pad")]
    
    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactilePanSensitivityY : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.Pan.Sensitivity.Y";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return this.GetSensitivityY(control);
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return this.GetSensitivityY(control);
        }

        private float GetSensitivityY(TactileControl control)
        {
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                return pad.PanSensitivity.y;
            return 0f;
        }

    }
}