using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Interaction Pad")]
    [Category("Interaction Pad")]

    [Description(
        "A control type that can simulates the behavior of a physical key or button, " +
        "responding to a interactions such as Tap, Slow Tap, Multi-Tap, and Hold."
    )]

    [Parameter(
        "Tap", 
        "Section for configuring the Tap interaction"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The button control path of the input control to be simulate"
    )]

    [Parameter(
        "Slow Tap", 
        "Section for configuring the Slow Tap interaction"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The button control path of the input control to be simulate"
    )]

    [Parameter(
        "Multi Tap", 
        "Section for configuring the Multi Tap interaction"
    )]
    [Parameter(
        "↳ Tap Count", 
        "<indent=1.2em>The number of taps required to simulate input"
    )]
    [Parameter(
        "↳ Continuous", 
        "<indent=1.2em>Whether to not wait until the full tap sequence is performed " +
        "to be executed again"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The button control path of the input control to be simulate"
    )]

    [Parameter(
        "Hold", 
        "Section for configuring the Hold interaction"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The button control path of the input control to be simulate"
    )]

    [Image(typeof(IconCharacterInteract))]

    [Serializable]
    public class ControlTypeInteractionPad : TControlType
    { 
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private InputSimulateButton     m_TapInputSimulate;
        [SerializeField] private InputSimulateButton     m_SlowTapInputSimulate;

        [SerializeField] 
        private MultiTapInputSimulate[] m_MultiTapInputSimulates = new MultiTapInputSimulate[1];

        [SerializeField] private InputSimulateButton     m_HoldInputSimulate;

        // PROPERTIES: ----------------------------------------------------------------------------

        public InputSimulateButton TapInputSimulate => this.m_TapInputSimulate;
        public InputSimulateButton SlowInputSimulate => this.m_SlowTapInputSimulate;
        public InputSimulateButton HoldInputSimulate => this.m_HoldInputSimulate;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected override void Awake() 
        {
            this.TouchableArea.interaction.EventTap += this.OnTap;
            this.TouchableArea.interaction.EventSlowTap += this.OnSlowTap;
            this.TouchableArea.interaction.EventMultiTap += this.OnMultiTap;
            this.TouchableArea.interaction.EventHold += this.OnHold;
        }

        protected internal override void Enable()
        {
            this.m_TapInputSimulate?.OnEnabled(this.m_Control);
            this.m_SlowTapInputSimulate?.OnEnabled(this.m_Control);

            for (int i = 0; i < this.m_MultiTapInputSimulates.Length; i++)
            {
                this.m_MultiTapInputSimulates[i]?.OnEnabled(this.m_Control);
            }

            this.m_HoldInputSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_TapInputSimulate?.OnDisabled();
            this.m_SlowTapInputSimulate?.OnDisabled();

            for (int i = 0; i < this.m_MultiTapInputSimulates.Length; i++)
            {
                this.m_MultiTapInputSimulates[i]?.OnDisabled();
            }

            this.m_HoldInputSimulate?.OnDisabled();
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public InputSimulateButton GetMultiTapInputSimulate(int index)
        {
            if (index < 0 || index >= this.m_MultiTapInputSimulates.Length) 
                return null;

            return this.m_MultiTapInputSimulates[index].InputSimulate;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnTap()
        {
            this.m_TapInputSimulate?.SendValueToControl(1f);
            this.m_TapInputSimulate?.SendValueToControl(0f);
        }

        private void OnSlowTap()
        {
            this.m_SlowTapInputSimulate?.SendValueToControl(1f);
            this.m_SlowTapInputSimulate?.SendValueToControl(0f);
        }

        private void OnMultiTap(int tapCount)
        {
            for (int i = 0; i < this.m_MultiTapInputSimulates.Length; i++)
            {
                this.m_MultiTapInputSimulates[i]?.TrySendValueToControl(tapCount);
            }
        }

        private void OnHold()
        {
            this.m_HoldInputSimulate?.SendValueToControl(1f);
            this.m_HoldInputSimulate?.SendValueToControl(0f);
        }

        // PRIVATE CLASS: -------------------------------------------------------------------------

        [Serializable]
        private class MultiTapInputSimulate
        {
            [Min(2)]
            [SerializeField] private int m_TapCount = 2;
            [SerializeField] private bool m_Continuous;
            [SerializeField] private InputSimulateButton m_InputSimulate;

            // PROPERTIES: ------------------------------------------------------------------------

            public InputSimulateButton InputSimulate => this.m_InputSimulate;

            // PUBLIC METHODS: --------------------------------------------------------------------

            public void OnEnabled(UnityEngine.Object logContext = null)
            {
                this.m_InputSimulate?.OnEnabled(logContext);
            }

            public void OnDisabled()
            {
                this.m_InputSimulate?.OnDisabled();
            }

            public void TrySendValueToControl(int tapCount)
            {
                bool canExecute = this.m_Continuous 
                    ? tapCount % this.m_TapCount == 0 : this.m_TapCount == tapCount;

                if (!canExecute) return;

                this.m_InputSimulate?.SendValueToControl(1f);
                this.m_InputSimulate?.SendValueToControl(0f);
            }
        }

    }
}