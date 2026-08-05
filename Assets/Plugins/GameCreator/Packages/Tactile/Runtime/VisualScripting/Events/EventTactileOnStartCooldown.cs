using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Start Cooldown")]
    [Category("Tactile/Skill Control/On Start Cooldown")]
    [Description("Executed when the cooldown of a Skill control type starts")]

    [Image(typeof(IconCooldown), ColorTheme.Type.Red, typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnStartCooldown : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType cooldown) return;

            cooldown.EventStartCooldown -= this.OnStartCooldown;
            cooldown.EventStartCooldown += this.OnStartCooldown;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType cooldown) return;

            cooldown.EventStartCooldown -= this.OnStartCooldown;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnStartCooldown()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}