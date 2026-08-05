using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Is Pressing")]
    [Category("Tactile/Is Pressing")]
    [Description("Gets true if a Tactile Control is being pressed; Otherwise, false")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetBoolTactileIsPressing : PropertyTypeGetBool
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"is {this.m_TactileControl} Pressing";

        // GETTERS: -------------------------------------------------------------------------------

        public override bool Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.interaction.IsPressed;
        }

        public override bool Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null && control.TouchableArea.interaction.IsPressed;
        }

    }
}