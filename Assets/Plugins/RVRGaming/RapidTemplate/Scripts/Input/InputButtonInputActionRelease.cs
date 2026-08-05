using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Input Release")]
    [Category("Input System/Input Release")]
    [Description("Fires when an Input Action of Button type is released")]
    [Image(typeof(IconBoltOutline), ColorTheme.Type.Blue, typeof(OverlayArrowRight))]
    [Serializable]
    public class InputButtonInputActionRelease : TInputButton
    {
        [SerializeField] private InputActionFromAsset m_Input = new InputActionFromAsset();

        public override void OnStartup()
        {
            base.OnStartup();
            if (m_Input?.InputAction == null) return;

            m_Input.InputAction.performed -= OnInputPerformed;
            m_Input.InputAction.canceled -= OnInputCanceled;

            m_Input.InputAction.performed += OnInputPerformed;
            m_Input.InputAction.canceled += OnInputCanceled;
        }

        public override void OnDispose()
        {
            base.OnDispose();
            if (m_Input?.InputAction == null) return;

            m_Input.InputAction.performed -= OnInputPerformed;
            m_Input.InputAction.canceled -= OnInputCanceled;
        }

        private void OnInputPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.ReadValueAsButton()) ExecuteEventPerform();
        }

        private void OnInputCanceled(InputAction.CallbackContext _)
        {
            ExecuteEventPerform();
        }
    }
}
