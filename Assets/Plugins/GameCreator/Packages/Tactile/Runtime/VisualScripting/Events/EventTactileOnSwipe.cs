using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Swipe")]
    [Category("Tactile/Swipe Pad/On Swipe")]

    [Description(
        "Executed when a swipe is performed on a Swipe Pad, typically " +
        "involving a quick, directional movement across the surface"
    )]

    [Parameter("Filter", "Whether to detect any, or specific swipe")]
    
    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnSwipe : TTactileEvent
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [UnityEngine.Serialization.FormerlySerializedAs("m_Filter")]
        [SerializeField] private FilterSwipe m_FilterSwipe = new FilterSwipe();

        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeSwipePad pad) return;

            pad.EventSwipe -= this.OnSwipe;
            pad.EventSwipe += this.OnSwipe;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ControlTypeSwipePad pad) return;

            pad.EventSwipe -= this.OnSwipe;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnSwipe(int swipeHash)
        {
            if (!this.m_FilterSwipe.Match(swipeHash)) return;
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}