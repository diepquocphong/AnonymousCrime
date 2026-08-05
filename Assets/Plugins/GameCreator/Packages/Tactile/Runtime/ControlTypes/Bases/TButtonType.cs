using System;
using UnityEngine;

namespace Niam.Runtime.Tactile 
{ 
    [Serializable]
    public abstract class TButtonType : TControlType, IButtonType
    {
        public enum InputExecution 
        {
            Momentary,
            PressPulse,
            ReleasePulse
        };

        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] protected InputSimulateButton m_InputSimulate;
        [SerializeField] protected InputExecution m_InputExecution;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private float m_Value;

        // PROPERTIES: ----------------------------------------------------------------------------

        public float Value
        {
            get => this.m_Value;

            protected set
            {
                if (this.m_Value == value) return;

                this.m_Value = value;
                this.m_InputSimulate?.SendValueToControl(value);
            }
        }

        public InputSimulateButton InputSimulate => this.m_InputSimulate;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected internal override void Enable()
        {
            this.m_InputSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_InputSimulate?.OnDisabled();
        }

    }
}