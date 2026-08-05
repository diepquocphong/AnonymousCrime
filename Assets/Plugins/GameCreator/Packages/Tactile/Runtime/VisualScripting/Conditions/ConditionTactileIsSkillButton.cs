using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Skill Button")]
    [Category("Tactile/Skill Control/Is Skill Button")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to Skill Button; " +
        "otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconPushButton), ColorTheme.Type.Green, typeof(OverlayBolt))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSkillButton : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} a Skill Button";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypeSkillButton;
        }
    }
}
