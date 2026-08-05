using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Start Cooldown")]
    [Category("Tactile/Skill Control/Start Cooldown")]
    [Description("Starts the cooldown of a Skill control type")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]
    
    [Parameter(
        "Force",
        "Whether to start the cooldown even if it is already in progress"
    )]

    [Image(typeof(IconCooldown), ColorTheme.Type.Red, typeof(OverlayBolt))]
    [Keywords("Tactile", "Ability", "Timer", "Wait")]

    [Serializable]
    public class InstructionTactileSkillStartCooldown : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField]
        private PropertyGetBool m_Force = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Start {this.m_TactileControl} Cooldown";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                skill.StartCooldown(this.m_Force.Get(args));
            }

            return DefaultResult;
        }
    }
}