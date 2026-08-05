using System;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device Timeout")]
    [Category("Tactile/Device Timeout")]
    
    [Description(
        "Detects when a Tactile Device key or button is pressed and held for a certain amount " +
        "of seconds"
    )]

    [Image(typeof(IconTactile), typeof(OverlayDot))]
    [Keywords("Tactile", "Key", "Button", "Timeout", "Delay", "Duration", "Hold")]
    
    [Serializable]
    public class InputValueButtonTactileDeviceTimeout : TInputButton
    {
        private enum Mode
        {
            OnReleaseButton,
            OnTimeout
        }
        
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] FieldDeviceButton m_Value = new FieldDeviceButton();
        [SerializeField] private Mode m_Mode = Mode.OnReleaseButton;
        [SerializeField] private float m_Duration = 0.5f;

        // PROPERTIES: ----------------------------------------------------------------------------

        private bool IsFired { get; set; } = false;
        private float PressTime { get; set; } = -999f;

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyButton Create()
        {
            return new InputPropertyButton(new InputValueButtonTactileDeviceTimeout());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnUpdate()
        {
            if (TactileDevice.current == null) return;
            
            var button = TactileDevice.current[this.m_Value.Name] as ButtonControl;
            if (button == null) return;

            if (button.wasPressedThisFrame)
            {
                this.IsFired = false;
                this.PressTime = Time.unscaledTime;
                
                this.ExecuteEventStart();
            }
            
            if (this.m_Mode == Mode.OnTimeout && !this.IsFired)
            {
                if (button.isPressed && this.IsTimeout())
                {
                    this.IsFired = true;
                    this.ExecuteEventPerform();
                }
            }
            
            if (button.wasReleasedThisFrame)
            {
                if (this.IsFired) return;

                switch (this.m_Mode)
                {
                    case Mode.OnReleaseButton:
                        if (this.IsTimeout()) this.ExecuteEventPerform();
                        else this.ExecuteEventCancel();
                        break;
                    
                    case Mode.OnTimeout:
                        if (!this.IsFired) this.ExecuteEventCancel();
                        break;
                    
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        // PRIVATE METHODS: -----------------------------------------------------------------------

        private bool IsTimeout()
        {
            return Time.unscaledTime - this.PressTime > this.m_Duration;
        }
    }
}