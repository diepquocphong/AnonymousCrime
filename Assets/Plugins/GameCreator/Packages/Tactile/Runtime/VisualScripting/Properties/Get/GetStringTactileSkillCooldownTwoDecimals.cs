using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Two Decimals Format")]
    [Category("Tactile/Skill Control/Two Decimals Format")]
    [Description("Gets the cooldown text format {0.00} for Skill control type")]

    [Image(typeof(IconCooldown), typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable] [HideLabelsInEditor]
    public class GetStringTactileSkillCooldownTwoDecimals : PropertyTypeGetString
    {
        private const string FORMAT = "{0.00}";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => FORMAT;

        // GET & CREATE: --------------------------------------------------------------------------

        public override string Get(Args args) => FORMAT;
        public override string Get(GameObject gameObject) => FORMAT;

        public static PropertyGetString Create => new PropertyGetString(
            new GetStringTactileSkillCooldownTwoDecimals()
        );
    }

}