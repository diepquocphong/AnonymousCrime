using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.UI;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Sensitivity X")]
    [Category("Tactile/Gesture Pad/Pan Sensitivity")]
    [Description("Sets the pan sensitivity x of a Gesture Pad")]
    
    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class SetDecimalTactilePanSensitivityX : PropertyTypeSetNumber
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Pan.SensitivityX";

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
                return pad.PanSensitivity.x;
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
                pad.PanSensitivity = new Vector2(value, pad.PanSensitivity.y);
        }

    }
}