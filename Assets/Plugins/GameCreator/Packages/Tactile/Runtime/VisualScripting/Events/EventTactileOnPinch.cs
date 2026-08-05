using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Pinch")]
    [Category("Tactile/Gesture Pad/On Pinch")]

    [Description(
        "Executed when a pinching is performed on a Gesture Pad, typically " +
        "involving two or more fingers moving closer together or farther apart"
    )]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnPinch : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventPinch -= this.OnPinch;
            pad.EventPinch += this.OnPinch;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventPinch -= this.OnPinch;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnPinch(float value)
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }

    }
}