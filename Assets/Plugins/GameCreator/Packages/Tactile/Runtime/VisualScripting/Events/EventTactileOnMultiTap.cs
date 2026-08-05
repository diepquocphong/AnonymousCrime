using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Multi-Tap")]
    [Category("Tactile/On Multi-Tap")]

    [Description(
        "Executed when a Tactile Control detects the specified number of taps for at least the " + 
        "set duration (which defaults to MultiTapDelayTime) after last tap"
    )]

    [Parameter(
        "Tap Count", 
        "The number of taps required to perform the interaction"
    )]
    
    [Parameter(
        "Continuous", 
        "Whether to not wait until the full tap sequence is performed to be executed again"
    )]

    [Image(typeof(IconTactile), typeof(OverlayBolt))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnMultiTap : TTactileEvent
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField, Min(2)] 
        private int m_TapCount = 2;

        [SerializeField] 
        private bool m_Continuous = false;

        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventMultiTap -= this.OnMultiTap;
            this.m_Control.TouchableArea.interaction.EventMultiTap += this.OnMultiTap;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventMultiTap -= this.OnMultiTap;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnMultiTap(int tapCount)
        {
            bool canExecute = this.m_Continuous 
                ? tapCount % this.m_TapCount == 0 : this.m_TapCount == tapCount;

            if (!canExecute) return;
            _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }
    }
}