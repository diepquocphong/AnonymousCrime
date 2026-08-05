using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Hold")]
    [Category("Tactile/On Hold")]
    
    [Description(
        "Executed when a Tactile Control is pressed held for at least the set duration " + 
        "(which defaults to defaultHoldTime)"
    )]

    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnHold : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventHold -= this.OnHold;
            this.m_Control.TouchableArea.interaction.EventHold += this.OnHold;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventHold -= this.OnHold;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnHold()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }

    }
}