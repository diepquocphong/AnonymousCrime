using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Cooldown Ratio")]
    [Category("Tactile/Skill Control/Cooldown Ratio")]
    [Description("Gets the completion ratio (0 to 1) of a Skill control type cooldown")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]
    
    [Image(typeof(IconPercent), typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileSkillCooldownRatio : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl} Cooldown Ratio";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                return skill.CooldownRatio;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                return skill.CooldownRatio;
            }
            
            return 0f;
        }

    }
}