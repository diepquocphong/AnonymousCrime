using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Last Tap Count")]
    [Category("Tactile/Last Tap Count")]
    [Description("Gets the last total consecutive taps of a Tactile Control")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileLastTapCount : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.LastTapCount";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.TouchableArea.interaction.LastTapCount : 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.TouchableArea.interaction.LastTapCount : 0f;
        }

    }
}