using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad D-Pad")]
    [Category("Gamepad/Gamepad D-Pad")]
    [Description("Reads the D-Pad keys of the Gamepad")]
    
    [Image(typeof(IconGamepadCross), ColorTheme.Type.Yellow)]
    [Keywords("Direction", "Cardinal", "Button", "Key")]
    
    [Serializable]
    public class InputValueVector2GamepadDPad : TInputValueVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private InputAction m_InputAction;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public InputAction InputAction
        {
            get
            {
                this.m_InputAction ??= new InputAction(
                    name: "D-Pad", 
                    type: InputActionType.Value,
                    binding: "<Gamepad>/dpad"
                );

                return this.m_InputAction;
            }
        }
        
        public override bool IsDeltaControl => InputAction?.activeControl is DeltaControl;

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2( new InputValueVector2GamepadDPad());
        }

        public override void OnStartup()
        {
            this.Enable();
        }

        public override void OnDispose()
        {
            this.Disable();
            this.InputAction?.Dispose();
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            return this.InputAction?.ReadValue<Vector2>() ?? Vector2.zero;
        }
        
        // PRIVATE METHODS: -----------------------------------------------------------------------
        
        private void Enable()
        {
            this.InputAction?.Enable();
        }

        private void Disable()
        {
            this.InputAction?.Disable();
        }
    }
}