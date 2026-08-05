using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Cooldown")]
    [Category("Tactile/Skill Control/Is Cooldown")]

    [Description(
        "Returns true if a Skill control type is in cooldown; otherwise, returns false"
    )]
    
    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]

    [Image(typeof(IconCooldown), typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSkillCooldown : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Is {this.m_TactileControl} Cooldown";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
                return skill.IsCooldown;

            return false;
        }
    }
}
