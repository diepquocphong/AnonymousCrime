using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Twist")]
    [Category("Tactile/Gesture Pad/On Twist")]

    [Description(
        "Executed when a twisting gesture is detected on a Gesture Pad, " +
        "typically involving a rotational movement of fingers on the surface"
    )]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnTwist : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventTwist -= this.OnTwist;
            pad.EventTwist += this.OnTwist;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventTwist -= this.OnTwist;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnTwist(float value)
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }

    }
}