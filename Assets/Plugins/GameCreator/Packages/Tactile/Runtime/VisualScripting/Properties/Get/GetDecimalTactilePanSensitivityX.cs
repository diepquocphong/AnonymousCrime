using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Sensitivity X")]
    [Category("Tactile/Gesture Pad/Pan Sensitivity X")]
    [Description("Gets the pan x sensitivity of a Gesture Pad")]
    
    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactilePanSensitivityX : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.Pan.Sensitivity.X";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return this.GetSensitivityX(control);
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return this.GetSensitivityX(control);
        }

        private float GetSensitivityX(TactileControl control)
        {
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                return pad.PanSensitivity.x;
            return 0f;
        }

    }
}