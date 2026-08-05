using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Skill Type")]
    [Category("Tactile/Skill Control/Is Skill Type")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to a Skill control type; " +
        "otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconMagicWand), ColorTheme.Type.Green)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSkillType : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} a Skill Type";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypeSkillStick;
        }
    }
}
