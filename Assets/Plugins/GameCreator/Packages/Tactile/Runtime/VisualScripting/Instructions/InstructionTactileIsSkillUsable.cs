using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Usable")]
    [Category("Tactile/Skill Control/Is Usable")]
    [Description("Sets whether a Skill control Type is usable or not")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]

    [Parameter(
        "Is Usable", 
        "Whether to enabled Is Usable of Skill control type or not"
    )]

    [Image(typeof(IconMagicWand))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileIsSkillUsable : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsUsable = GetBoolFalse.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Is {this.m_TactileControl} Usable = {this.m_IsUsable}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
            {
                skill.IsUsable = this.m_IsUsable.Get(args);
            }

            return DefaultResult;
        }
    }
}