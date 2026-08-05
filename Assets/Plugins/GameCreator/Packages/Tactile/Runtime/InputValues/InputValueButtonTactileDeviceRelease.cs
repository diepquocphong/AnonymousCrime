using System;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device Release")]
    [Category("Tactile/Device Release")]
    [Description("Detects when a Tactile Device key or button is released")]

    [Image(typeof(IconTactile), typeof(OverlayArrowUp))]
    [Keywords("Tactile", "Key", "Button", "Up")]
    
    [Serializable]
    public class InputValueButtonTactileDeviceRelease : TInputButton
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] FieldDeviceButton m_Value = new FieldDeviceButton();

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyButton Create()
        {
            return new InputPropertyButton(new InputValueButtonTactileDeviceRelease());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnUpdate()
        {
            if (TactileDevice.current == null) return;

            var button = TactileDevice.current[this.m_Value.Name] as ButtonControl;
            if (button == null || !button.wasReleasedThisFrame) return;

            this.ExecuteEventStart();
            this.ExecuteEventPerform();
        }
    }
}