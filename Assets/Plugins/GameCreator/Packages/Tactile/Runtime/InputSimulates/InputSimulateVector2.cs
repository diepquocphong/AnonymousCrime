using System;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class InputSimulateVector2 : TInputSimulate<Vector2>
    { 
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeReference] private ControlPathVector2 m_ControlPath;

        [SerializeReference, HideInInspector] 
        private ControlPathVector2 m_OverridePath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override TControlPath<Vector2> ControlPath => 
            this.m_OverridePath ?? this.m_ControlPath;

        public bool IsOverriden => this.m_OverridePath != null;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputSimulateVector2()
        {
            this.m_ControlPath = new ControlPathVector2ConstantNone();
        }

        public InputSimulateVector2(ControlPathVector2 controlPath)
        {
            this.m_ControlPath = controlPath ?? new ControlPathVector2ConstantNone();
        }

        // OVERRIDES: -----------------------------------------------------------------------------

        protected override void InputCollisionValue(ref Vector2 value)
        {
            value += this.ReadValueFromControl();
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void OverrideControlPath(ControlPathVector2 controlPath)
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