using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Slow Tap")]
    [Category("Tactile/On Slow Tap")]
 
    [Description(
        "Executed when a Tactile Control is pressed and held for at least the set duration " + 
        "(which defaults to defaultSlowTapTime) and then released"
    )]

    [Image(typeof(IconTactile), typeof(OverlayHourglass))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnSlowTap : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventSlowTap -= this.OnSlowTap;
            this.m_Control.TouchableArea.interaction.EventSlowTap += this.OnSlowTap;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventSlowTap -= this.OnSlowTap;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnSlowTap()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}