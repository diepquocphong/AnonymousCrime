using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Tap")]
    [Category("Tactile/On Tap")]

    [Description(
        "Executed when a Tactile Control is pressed held for at least the set duration " + 
        "(which defaults to defaultTapTime) and then released"
    )]

    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnTap : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventTap -= this.OnTap;
            this.m_Control.TouchableArea.interaction.EventTap += this.OnTap;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventTap -= this.OnTap;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnTap()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}