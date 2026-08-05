using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Release")]
    [Category("Tactile/On Release")]
    [Description("Executed when a Tactile Control is released")]
    [Parameter("Filter Finger", "Whether to detect only the last, or each finger")]

    [Image(typeof(IconTactile), ColorTheme.Type.Red, typeof(OverlayArrowUp))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnRelease : TTactileEvent
    {
        private enum FilterFinger : byte { LastFingerOnly, ForEachFinger }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [UnityEngine.Serialization.FormerlySerializedAs("m_Filter")]
        [SerializeField] private FilterFinger m_FilterFinger = FilterFinger.LastFingerOnly;

        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventRelease -= this.OnRelease;
            this.m_Control.TouchableArea.interaction.EventRelease += this.OnRelease;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventRelease -= this.OnRelease;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnRelease(int releaseCount)
        {
            bool checkFinger = this.m_FilterFinger switch
            {
                FilterFinger.ForEachFinger => true,
                FilterFinger.LastFingerOnly => this.m_Control.TouchableArea.fingerCount == 1,
                _ => throw new NotImplementedException(),
            };

            if (checkFinger)
            {
                _ = this.m_Trigger.Execute(this.m_Control.gameObject);
            }
        }

    }
}