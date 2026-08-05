using System;

namespace Niam.Runtime.Tactile 
{
    interface ISkillType
    {
        public bool IsUsable { get; set; }
        public bool IsCooldown { get; }
        public float CooldownRatio { get; }
        public float CooldownRemaining { get; }

        event Action EventActivateSkill;
        event Action EventStartCooldown;
        event Action EventResetCooldown;
        
        public void StartCooldown(bool force = false);
        public void ResetCooldown();
    }
}