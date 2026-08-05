using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Hold Radius")]
    [Category("Tactile/Set Hold Radius")]

    [Description(
        "Set the maximum radius that a touch contact may be moved from its origin to " +
        "evaluate to a hold-interaction"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Hold Radius", "The radius for a hold interaction")]

    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetHoldRadius : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetDecimal m_HoldRadius = GetDecimalDecimal.Create(0.1f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Hold Radius = {this.m_HoldRadius}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.TouchableArea != null)
            {
                control.TouchableArea.interaction.HoldRadius = (float)this.m_HoldRadius.Get(args);
            }

            return DefaultResult;
        }
    }
}