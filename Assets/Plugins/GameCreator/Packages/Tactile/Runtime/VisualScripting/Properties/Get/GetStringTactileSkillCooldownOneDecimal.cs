using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("One Decimal Format")]
    [Category("Tactile/Skill Control/One Decimal Format")]
    [Description("Gets the cooldown text format {0.0} for Skill control type")]

    [Image(typeof(IconCooldown), typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable] [HideLabelsInEditor]
    public class GetStringTactileSkillCooldownOneDecimal : PropertyTypeGetString
    {
        private const string FORMAT = "{0.0}";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => FORMAT;

        // GET & CREATE: --------------------------------------------------------------------------

        public override string Get(Args args) => FORMAT;
        public override string Get(GameObject gameObject) => FORMAT;

        public static PropertyGetString Create => new PropertyGetString(
            new GetStringTactileSkillCooldownOneDecimal()
        );
    }

}