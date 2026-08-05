using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("While Pressing")]
    [Category("Tactile/While Pressing")]
    [Description("Executed continuously as long as a Tactile Control is being pressed")]

    [Image(typeof(IconTactile), ColorTheme.Type.Blue, typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileWhilePressing : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventWhilePressing -= this.WhilePressing;
            this.m_Control.TouchableArea.interaction.EventWhilePressing += this.WhilePressing;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventWhilePressing -= this.WhilePressing;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void WhilePressing()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}