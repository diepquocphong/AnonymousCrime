using System;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class InputSimulateAxis : TInputSimulate<float>
    { 
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeReference] private ControlPathAxis m_ControlPath;

        [SerializeReference, HideInInspector] 
        private ControlPathAxis m_OverridePath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override TControlPath<float> ControlPath => 
            this.m_OverridePath ?? this.m_ControlPath;

        public bool IsOverriden => this.m_OverridePath != null;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputSimulateAxis()
        {
            this.m_ControlPath = new ControlPathAxisConstantNone();
        }

        public InputSimulateAxis(ControlPathAxis controlPath)
        {
            this.m_ControlPath = controlPath ?? new ControlPathAxisConstantNone();
        }

        // OVERRIDES: -----------------------------------------------------------------------------

        protected override void InputCollisionValue(ref float value)
        {
            value += this.ReadValueFromControl();
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void OverrideControlPath(ControlPathAxis controlPath)
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