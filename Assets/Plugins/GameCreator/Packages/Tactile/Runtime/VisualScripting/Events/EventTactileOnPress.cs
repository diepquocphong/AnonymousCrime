using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("On Press")]
    [Category("Tactile/On Press")]
    [Description("Executed when a Tactile Control is pressed")]

    [Parameter("Filter", "Whether to detect only the first, or each finger")]

    [Image(typeof(IconTactile), ColorTheme.Type.Green, typeof(OverlayArrowDown))]
    [Keywords("Tactile")]

    [Serializable]
    public class EventTactileOnPress : TTactileEvent
    {
        private enum FilterFinger : byte { FirstFingerOnly, ForEachFinger }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [UnityEngine.Serialization.FormerlySerializedAs("m_Filter")]
        [SerializeField] private FilterFinger m_FilterFinger = FilterFinger.FirstFingerOnly;

        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void WhenEnabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventPress -= this.OnPress;
            this.m_Control.TouchableArea.interaction.EventPress += this.OnPress;
        }

        protected override void WhenDisabled(Trigger trigger)
        {
            this.m_Control.TouchableArea.interaction.EventPress -= this.OnPress;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnPress(int pressCount)
        {
            bool checkFinger = this.m_FilterFinger switch
            {
                FilterFinger.ForEachFinger => true,
                FilterFinger.FirstFingerOnly => this.m_Control != null && 
                                          this.m_Control.TouchableArea.fingerCount == 1,
                _ => throw new NotImplementedException(),
            };

            if (checkFinger) _ = this.m_Trigger.Execute(this.m_Control.gameObject);
        }

    }
}