using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Is Holding")]
    [Category("Tactile/Is Holding")]
    [Description("Gets true if a Tactile Control is being held; Otherwise, false")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetBoolTactileIsHolding : PropertyTypeGetBool
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"is {this.m_TactileControl} Holding";

        // GETTERS: -------------------------------------------------------------------------------

        public override bool Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.interaction.IsHolding;
        }

        public override bool Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null && control.TouchableArea.interaction.IsHolding;
        }

    }
}