using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("While Holding")]
    [Category("Tactile/While Holding")]
    [Description("Executed continuously as long as a Tactile Control is being held")]

    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileWhileHolding : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventWhileHolding -= this.WhileHolding;
            this.m_Control.TouchableArea.interaction.EventWhileHolding += this.WhileHolding;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventWhileHolding -= this.WhileHolding;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void WhileHolding()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
        
    }
}