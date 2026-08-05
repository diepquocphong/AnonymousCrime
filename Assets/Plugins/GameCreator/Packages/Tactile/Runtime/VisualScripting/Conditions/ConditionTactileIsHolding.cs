using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Holding")]
    [Category("Tactile/Is Holding")]

    [Description(
        "Returns true if a Tactile Control is being held; otherwise, returns false"
    )]
    
    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]

    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsHolding : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Is {this.m_TactileControl} Holding";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.interaction.IsHolding;
        }
    }
}
