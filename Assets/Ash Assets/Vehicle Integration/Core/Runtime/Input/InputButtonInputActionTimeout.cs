using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Input Timeout")]
    [Category("Input System/Input Timeout")]
    [Description("Fires after the button is held for a set duration; cancels if released early")]
    [Image(typeof(IconBoltOutline), ColorTheme.Type.Blue, typeof(OverlayHourglass))]
    [Serializable]
    public class InputButtonInputActionTimeout : TInputButton
    {
        [SerializeField] private InputActionFromAsset m_Input = new InputActionFromAsset();
        [SerializeField] private float m_Duration = 0.5f;
        [SerializeField] private bool m_UseUnscaledTime = true;
        [SerializeField] private bool m_FireOncePerHold = true;

        private bool m_IsHolding;
        private bool m_FiredThisHold;
        private float m_Elapsed;

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
            m_IsHolding = true;
            m_FiredThisHold = false;
            m_Elapsed = 0f;

            ExecuteEventStart();
        }

        private void OnInputCancel(InputAction.CallbackContext _)
        {
            if (!m_FiredThisHold) ExecuteEventCancel();

            m_IsHolding = false;
            m_FiredThisHold = false;
            m_Elapsed = 0f;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!m_IsHolding) return;

            m_Elapsed += m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (!m_FiredThisHold && m_Elapsed >= m_Duration)
            {
                ExecuteEventPerform();
                if (m_FireOncePerHold) m_FiredThisHold = true;
                else m_Elapsed = 0f; 
            }
        }
    }
}
