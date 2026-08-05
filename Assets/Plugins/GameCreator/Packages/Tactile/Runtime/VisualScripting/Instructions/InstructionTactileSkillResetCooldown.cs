using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Cooldown")]
    [Category("Tactile/Skill Control/Reset Cooldown")]
    [Description("Resets the cooldown of a Skill control type")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]

    [Image(typeof(IconCooldown), ColorTheme.Type.Green, typeof(OverlayBolt))]
    [Keywords("Tactile", "Ability", "Timer", "Wait")]

    [Serializable]
    public class InstructionTactileSkillResetCooldown : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Reset {this.m_TactileControl} Cooldown";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                skill.ResetCooldown();
            }

            return DefaultResult;
        }
    }
}