using System;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device Press")]
    [Category("Tactile/Device Press")]
    [Description("Detects when a Tactile Device key or button is pressed")]

    [Image(typeof(IconTactile), typeof(OverlayArrowDown))]
    [Keywords("Tactile", "Key", "Button", "Down")]
    
    [Serializable]
    public class InputValueButtonTactileDevicePress : TInputButton
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] FieldDeviceButton m_Value = new FieldDeviceButton();

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyButton Create()
        {
            return new InputPropertyButton(new InputValueButtonTactileDevicePress());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnUpdate()
        {
            if (TactileDevice.current == null) return;

            var button = TactileDevice.current[this.m_Value.Name] as ButtonControl;
            if (button == null || !button.wasPressedThisFrame) return;
            
            this.ExecuteEventStart();
            this.ExecuteEventPerform();
        }
    }
}