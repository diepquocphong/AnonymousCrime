using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.UI;

namespace Niam.Runtime.Tactile
{
    [Title("Pinch Sensitivity")]
    [Category("Tactile/Gesture Pad/Pinch Sensitivity")]
    [Description("Sets the pinch sensitivity of a Gesture Pad")]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class SetDecimalTactilePinchSensitivity : PropertyTypeSetNumber
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Pinch.Sensitivity";

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
                return pad.PinchSensitivity;
            return 0f;
        }

        // SETTERS: -------------------------------------------------------------------------------

        public override void Set(double value, Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            this.SetSensitivity(control, (float) value);
        }

        public override void Set(double value, GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            this.SetSensitivity(control, (float) value);
        }

        private void SetSensitivity(TactileControl control, float value)
        {
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
                pad.PinchSensitivity = value;
        }

    }
}