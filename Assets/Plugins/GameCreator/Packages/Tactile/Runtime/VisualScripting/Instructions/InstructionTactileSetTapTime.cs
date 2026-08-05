using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Tap Time")]
    [Category("Tactile/Set Tap Time")]
    
    [Description(
        "Set the tap interaction time. Set this to a negative value to use " +
        "the default tap time from the Input System settings"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Tap Time", "The time (in seconds) within which a press and release has to occur")]

    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetTapTime : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetDecimal m_TapTime = GetDecimalDecimal.Create(0.2f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Set {this.m_TactileControl} Tap Time = {this.m_TapTime}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.TouchableArea != null)
            {
                control.TouchableArea.interaction.TapTime = (float)this.m_TapTime.Get(args);
            }

            return DefaultResult;
        }
    }
}