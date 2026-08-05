using System;

using UnityEngine;
using UnityEngine.InputSystem;

using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using Event = GameCreator.Runtime.VisualScripting.Event;

namespace Niam.Runtime.Tactile
{
    [Title("On Device Change")]
    [Category("Input System/On Device Change")]
    [Description("Executed when the device setup in the system changes")]

    [Image(typeof(IconBoltSolid), ColorTheme.Type.Yellow)]
    [Keywords("Added", "Removed", "Disconnected", "Reconnected", "Enabled", "Disabled")]
    [Keywords("Usage Changed", "Configuration Changed", "Soft Reset", "Hard Reset")]

    [Serializable]
    public class EventInputOnDeviceChange : Event
    {
        // ENUM: ----------------------------------------------------------------------------------

        [Flags]
        public enum ChangeFilter
        {
            Added                   = 1 << 0, // 0b0000000001
            Removed                 = 1 << 1, // 0b0000000010
            Disconnected            = 1 << 2, // 0b0000000100
            Reconnected             = 1 << 3, // 0b0000001000
            Enabled                 = 1 << 4, // 0b0000010000
            Disabled                = 1 << 5, // 0b0000100000
            UsageChanged            = 1 << 6, // 0b0001000000
            ConfigurationChanged    = 1 << 7, // 0b0010000000
            SoftReset               = 1 << 8, // 0b0100000000
            HardReset               = 1 << 9  // 0b1000000000
        }

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private FilterDevice m_DeviceFilter = new FilterDevice();
        [SerializeField] private ChangeFilter m_ChangeFilter = (ChangeFilter) 0x3;

        // INITIALIZERS: --------------------------------------------------------------------------

        protected override void OnEnable(Trigger trigger)
        {
            base.OnEnable(trigger);
            InputSystem.onDeviceChange -= this.OnChange;
            InputSystem.onDeviceChange += this.OnChange;
        }

        protected override void OnDisable(Trigger trigger)
        {
            base.OnDisable(trigger);
            InputSystem.onDeviceChange -= this.OnChange;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnChange(InputDevice device, InputDeviceChange change)
        {
            if (ApplicationManager.IsExiting) return;
            if (!this.m_ChangeFilter.HasFlag((ChangeFilter) (1 << (int) change))) return;
            if (!this.m_DeviceFilter.HasDevice(device.layout)) return;

            _ = this.m_Trigger.Execute(this.Self);
        }

    }

}