using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Castable")]
    [Category("Tactile/Skill Control/Is Castable")]

    [Description(
        "Returns true if a Skill control type is usable and not cooldown; otherwise, returns false"
    )]
    
    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]

    [Image(typeof(IconMagicWand))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSkillCastable : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Is {this.m_TactileControl} Castable";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
                return skill.IsUsable && !skill.IsCooldown;

            return false;
        }
    }
}
