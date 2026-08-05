using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GameCreator.Runtime.Common;
using UnityEngine.InputSystem.Controls;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device")]
    [Category("Tactile/Tactile Device")]
    [Description("Reads the given input control from the Tactile Device")]

    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class InputValueFloatTactileDevice : TInputValueFloat
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] FieldDeviceFloat m_Value = new FieldDeviceFloat();
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

        public static InputPropertyValueFloat Create()
        {
            return new InputPropertyValueFloat(new InputValueFloatTactileDevice());
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

        public override float Read()
        {
            return this.InputAction?.ReadValue<float>() ?? 0f;
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