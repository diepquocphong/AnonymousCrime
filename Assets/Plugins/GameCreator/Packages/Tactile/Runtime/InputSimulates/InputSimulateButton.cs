using System;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class InputSimulateButton : TInputSimulate<float>
    { 
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeReference] private ControlPathButton m_ControlPath;
        
        [SerializeReference, HideInInspector] 
        private ControlPathButton m_OverridePath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override TControlPath<float> ControlPath => 
            this.m_OverridePath ?? this.m_ControlPath;

        public bool IsOverriden => this.m_OverridePath != null;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputSimulateButton()
        {
            this.m_ControlPath = new ControlPathButtonConstantNone();
        }
        
        public InputSimulateButton(ControlPathButton controlPath)
        {
            this.m_ControlPath = controlPath ?? new ControlPathButtonConstantNone();
        }

        // OVERRIDES: -----------------------------------------------------------------------------

        protected override void InputCollisionValue(ref float value)
        {
            float lastValue = this.ReadValueFromControl();

            if (value == 0f && lastValue == 1f)
            {
                value = 0f;
                return;
            }

            value = Mathf.Clamp01(value + lastValue);
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void OverrideControlPath(ControlPathButton controlPath)
        {
            this.OnDisabled();

            this.m_OverridePath = controlPath;

            this.OnEnabled();
        }

        public void ResetOverrideControlPath()
        {
            this.OverrideControlPath(null);
        }

        // STRING: --------------------------------------------------------------------------------

        public override string ToString() => this.m_ControlPath.ToString();

    }
}