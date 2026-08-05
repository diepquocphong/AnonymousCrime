using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Twist Sensitivity")]
    [Category("Tactile/Gesture Pad/Twist Sensitivity")]
    [Description("Gets the twist sensitivity of a Gesture Pad")]
    
    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileTwistSensitivity : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.Twist.Sensitivity";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return this.GetSensitivity(control);
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return this.GetSensitivity(control);
        }

        private float GetSensitivity(TactileControl control)
        {
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                return pad.TwistSensitivity;
            return 0f;
        }

    }
}