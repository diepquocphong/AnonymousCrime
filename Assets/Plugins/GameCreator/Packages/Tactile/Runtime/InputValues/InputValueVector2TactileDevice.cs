using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device")]
    [Category("Tactile/Tactile Device")]
    [Description("Reads the given input control from the Tactile Device")]

    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class InputValueVector2TactileDevice : TInputValueVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] FieldDeviceVector2 m_Value = new FieldDeviceVector2();
        [NonSerialized] private InputAction m_InputAction;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public InputAction InputAction
        {
            get
            {
                this.m_InputAction ??= new InputAction(
                    name: this.m_Value.DisplayName, 
                    type: InputActionType.Value,
                    binding: $"<Tactile>/{this.m_Value.Name}"
                );

                return this.m_InputAction;
            }
        }
        
        public override bool IsDeltaControl => InputAction?.activeControl is DeltaControl;
        
        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2(new InputValueVector2TactileDevice());
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