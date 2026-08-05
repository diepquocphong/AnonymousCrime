using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Is Usable")]
    [Category("Tactile/Skill Control/Is Usable")]

    [Description(
        "Gets true if a Skill Control Type is usable; Otherwise, false"
    )]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a Skill control type"
    )]
    
    [Image(typeof(IconMagicWand))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetBoolTactileSkillIsUsable : PropertyTypeGetBool
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"is {this.m_TactileControl} Usable";

        // GETTERS: -------------------------------------------------------------------------------

        public override bool Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ISkillType skill) 
                return skill.IsUsable;

            return false;
        }

        public override bool Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ISkillType skill) 
                return skill.IsUsable;

            return false;
        }

    }
}