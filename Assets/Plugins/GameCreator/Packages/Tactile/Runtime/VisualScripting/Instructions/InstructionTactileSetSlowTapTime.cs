using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Slow Tap Time")]
    [Category("Tactile/Set Slow Tap Time")]
    [Description(
        "Set the slow-tap interaction time. Set this to a negative value to use the default " +
        "slow tap time from the Input System settings"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Slow Tap Time", "The time it takes for a slow tap interaction to be recognized")]

    [Image(typeof(IconTactile), typeof(OverlayHourglass))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSlowTapTime : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetDecimal m_SlowTapTime = GetDecimalDecimal.Create(0.5f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Slow Tap Time = {this.m_SlowTapTime}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.TouchableArea != null)
            {
                control.TouchableArea.interaction.SlowTapTime = (float)this.m_SlowTapTime.Get(args);
            }

            return DefaultResult;
        }
    }
}