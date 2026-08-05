using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Activate Skill")]
    [Category("Tactile/Skill Control/On Activate Skill")]

    [Description(
        "Executed when a Skill control type activates, typically when " +
        "it is not on cooldown and not released within a cancel area"
    )]

    [Image(typeof(IconMagicWand))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnActivateSkill : TTactileEvent
    {
        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType skill) return;

            skill.EventActivateSkill -= this.OnPerform;
            skill.EventActivateSkill += this.OnPerform;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            if (this.m_Control.ControlType is not ISkillType skill) return;

            skill.EventActivateSkill -= this.OnPerform;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnPerform()
        {
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}