using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class FilterDevice
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private List<string> m_DeviceList = new List<string>();

        // PROPERTIES: ----------------------------------------------------------------------------

        public int Length => this.m_DeviceList.Count;

        // PUBLIC METHODS: ------------------------------------------------------------------------
        
        public bool HasDevice(string name, bool includeBase = true)
        {
            if (Length == 0 || this.m_DeviceList.Contains(name)) 
                return true;

            if (includeBase && InputSystem.LoadLayout(name) is InputControlLayout layout)
            {
                var baseLayouts = layout.baseLayouts;
                foreach (var baseLayout in baseLayouts)
                {
                    if (this.m_DeviceList.Contains(baseLayout)) 
                        return true;
                }
            }

            return false;
        }

        public int GetDeviceIndex(string name)
        {
            return this.m_DeviceList.IndexOf(name);
        }
    }
}