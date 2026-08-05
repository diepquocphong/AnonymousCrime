using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Hold Time")]
    [Category("Tactile/Set Hold Time")]
    [Description(
        "Set the hold-interaction time. Set this to a negative value to use the " +
        "default hold time from the Input System settings"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Hold Time", "The time it takes for a hold interaction to be recognized")]

    [Image(typeof(IconTactile), ColorTheme.Type.Purple, typeof(OverlayHourglass))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetHoldTime : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetDecimal m_HoldTime = GetDecimalDecimal.Create(1.0f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Hold Time = {this.m_HoldTime}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.TouchableArea != null)
            {
                control.TouchableArea.interaction.HoldTime = (float)this.m_HoldTime.Get(args);
            }

            return DefaultResult;
        }
    }
}