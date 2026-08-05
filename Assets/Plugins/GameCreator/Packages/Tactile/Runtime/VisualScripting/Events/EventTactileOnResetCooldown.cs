using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Reset Cooldown")]
    [Category("Tactile/Skill Control/On Reset Cooldown")]
    [Description("Executed when the cooldown of a Skill control type resets to 0")]

    [Image(typeof(IconCooldown), ColorTheme.Type.Green,typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnResetCooldown : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType cooldown) return;

            cooldown.EventResetCooldown -= this.OnResetCooldown;
            cooldown.EventResetCooldown += this.OnResetCooldown;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType cooldown) return;

            cooldown.EventResetCooldown -= this.OnResetCooldown;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnResetCooldown()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}