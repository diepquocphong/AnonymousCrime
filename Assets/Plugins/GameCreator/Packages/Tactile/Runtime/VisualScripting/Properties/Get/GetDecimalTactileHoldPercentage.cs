using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Hold Percentage")]
    [Category("Tactile/Hold Percentage")]
    [Description("Gets the hold time as percentage in decimal with a Tactile Control")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileHoldPercentage : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.HoldPercentage";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.TouchableArea.interaction.HoldPercentage : 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.TouchableArea.interaction.HoldPercentage : 0f;
        }

    }
}