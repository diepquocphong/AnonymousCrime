using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Input While Pressing")]
    [Category("Input System/Input While Pressing")]
    [Description("Continuously fires while an Input Action of Button type is held down")]
    [Image(typeof(IconBoltOutline), ColorTheme.Type.Blue, typeof(OverlayDot))]
    [Serializable]
    public class InputButtonInputActionWhilePressing : TInputButton
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

        private void OnInputStart(InputAction.CallbackContext _) => ExecuteEventStart();
        private void OnInputCancel(InputAction.CallbackContext _) => ExecuteEventCancel();

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (m_Input?.InputAction == null) return;

            if (m_Input.InputAction.IsPressed() || m_Input.InputAction.WasReleasedThisFrame())
                ExecuteEventPerform();
        }
    }
}
