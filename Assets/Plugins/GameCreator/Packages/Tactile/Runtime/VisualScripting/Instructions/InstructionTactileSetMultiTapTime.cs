using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Multi Tap Time")]
    [Category("Tactile/Set Multi Tap Time")]
    [Description(
        "Set multi-tap interaction time. Set this to a negative value to use the default multi " +
        "tap delay time from the Input System settings"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Multi Tap Time", "The maximum duration that may pass between taps")]

    [Image(typeof(IconTactile), typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetMultiTapTime : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetDecimal m_MultiTapTime = GetDecimalDecimal.Create(0.75f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Multi Tap Time = {this.m_MultiTapTime}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.TouchableArea != null)
            {
                control.TouchableArea.interaction.MultiTapDelayTime = (float)this.m_MultiTapTime.Get(args);
            }

            return DefaultResult;
        }
    }
}