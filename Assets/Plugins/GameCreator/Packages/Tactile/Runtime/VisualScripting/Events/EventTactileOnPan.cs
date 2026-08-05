using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Pan")]
    [Category("Tactile/Gesture Pad/On Pan")]
    
    [Description(
        "Executed when a panning is performed on a Gesture Pad, typically " +
        "involving dragging fingers across the surface"
    )]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnPan : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventPan -= this.OnPan;
            pad.EventPan += this.OnPan;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeGesturePad pad) return;

            pad.EventPan -= this.OnPan;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnPan(Vector2 value)
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }

    }
}