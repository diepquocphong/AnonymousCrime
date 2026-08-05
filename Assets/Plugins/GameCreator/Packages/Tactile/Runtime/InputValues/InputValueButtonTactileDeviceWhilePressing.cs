using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device While Pressing")]
    [Category("Tactile/Device While Pressing")]
    [Description("Detects while a Tactile Device key or button is being held down")]

    [Image(typeof(IconTactile), ColorTheme.Type.Blue, typeof(OverlayDot))]
    [Keywords("Tactile", "Key", "Button", "Down", "Held", "Hold")]

    [Serializable]
    public class InputValueButtonTactileDeviceWhilePressing : TInputButton
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] FieldDeviceButton m_Value = new FieldDeviceButton();

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyButton Create()
        {
            return new InputPropertyButton(new InputValueButtonTactileDeviceWhilePressing());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnUpdate()
        {
            if (TactileDevice.current == null) return;
            if (TactileDevice.current[this.m_Value.Name] is not ButtonControl button) return;

            if (button.wasPressedThisFrame)
            {
                this.ExecuteEventStart();   
            }
            
            if (button.IsPressed())
            {
                this.ExecuteEventPerform();
            }
        }
    }
}