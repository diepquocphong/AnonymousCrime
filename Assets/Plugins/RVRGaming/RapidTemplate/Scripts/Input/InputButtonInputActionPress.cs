using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Input Press")]
    [Category("Input System/Input Press")]
    [Description("Fires when an Input Action of Button type is pressed (Started phase)")]
    [Image(typeof(IconBoltOutline), ColorTheme.Type.Blue, typeof(OverlayArrowLeft))]
    [Serializable]
    public class InputButtonInputActionPress : TInputButton
    {
        [SerializeField] private InputActionFromAsset m_Input = new InputActionFromAsset();

        public override void OnStartup()
        {
            base.OnStartup();
            if (m_Input?.InputAction == null) return;

            m_Input.InputAction.started -= OnInputStart;
            m_Input.InputAction.canceled -= OnInputCancel;

            m_Input.InputAction.started += OnInputStart;
            m_Input.InputAction.canceled += OnInputCancel;
        }

        public override void OnDispose()
        {
            base.OnDispose();
            if (m_Input?.InputAction == null) return;

            m_Input.InputAction.started -= OnInputStart;
            m_Input.InputAction.canceled -= OnInputCancel;
        }

        private void OnInputStart(InputAction.CallbackContext _)
        {
            ExecuteEventStart();
            ExecuteEventPerform();
        }

        private void OnInputCancel(InputAction.CallbackContext _)
        {
            ExecuteEventCancel();
        }
    }
}
