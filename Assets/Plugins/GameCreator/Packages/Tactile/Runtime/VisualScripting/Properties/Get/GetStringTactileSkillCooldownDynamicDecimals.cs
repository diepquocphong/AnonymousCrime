using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Dynamic Decimals Format")]
    [Category("Tactile/Skill Control/Dynamic Decimals Format")]

    [Description(
        "Gets the cooldown text format {0.0} if the cooldown remaining of a Skill control type " +
        "is less than 1; Otherwise, gets cooldown text format {0}"
    )]

    [Image(typeof(IconCooldown), typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable] [HideLabelsInEditor]
    public class GetStringTactileSkillCooldownDynamicDecimals : PropertyTypeGetString
    {
        private const string FORMAT_A = "{0}";
        private const string FORMAT_B = "{0.0}";
        
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectSelf.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String =>
            $"{this.m_TactileControl} Cooldown Remaining < 1 ? {{0.0}} : {{0}}";

        // GET & CREATE: --------------------------------------------------------------------------

        public override string Get(Args args)
        {
            var control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                return skill.CooldownRemaining < 1f ? FORMAT_B : FORMAT_A;
            }

            return FORMAT_A;
        }

        public override string Get(GameObject gameObject)
        {
            var control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                return skill.CooldownRemaining < 1f ? FORMAT_B : FORMAT_A;
            }

            return FORMAT_A;
        }

        public static PropertyGetString Create => new PropertyGetString(
            new GetStringTactileSkillCooldownDynamicDecimals()
        );
    }

}