using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Unity.Collections;

namespace Niam.Runtime.Tactile
{
    public abstract class BaseInputSimulate
    {
        protected const string TACTILE_USAGE = "Virtual";

        // MEMBERS: -------------------------------------------------------------------------------

        private BaseInputSimulate m_NextControl;

        // STATIC: --------------------------------------------------------------------------------

        protected static List<DeviceInfo> s_InputDevices = new List<DeviceInfo>();
        protected static HashSet<int> s_UpdatedControls = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemsInit()
        {
            s_InputDevices = new List<DeviceInfo>();
            s_UpdatedControls = new HashSet<int>();

            InputSystem.onBeforeUpdate += s_UpdatedControls.Clear;

            #if UNITY_EDITOR
            foreach (var device in InputSystem.devices)
            {
                if (device == null) continue;
                if (device is TactileDevice || 
                    device.usages.Contains(new InternedString(TACTILE_USAGE)))
                {
                    InputSystem.RemoveDevice(device);
                }
            }
            
            UnityEditor.EditorApplication.playModeStateChanged += DeviceCleanup;
            #endif
        }

        #if UNITY_EDITOR
        private static void DeviceCleanup(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;

            foreach (var device in InputSystem.devices)
            {
                if (device == null) continue;
                if (device is TactileDevice || 
                    device.usages.Contains(new InternedString(TACTILE_USAGE)))
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            InputSystem.onBeforeUpdate -= s_UpdatedControls.Clear;
            UnityEditor.EditorApplication.playModeStateChanged -= DeviceCleanup;
        }
        #endif

        // STRUCT: --------------------------------------------------------------------------------

        protected struct DeviceInfo
        {
            public InputDevice device;
            public InputEventPtr eventPtr;
            public NativeArray<byte> buffer;
            public BaseInputSimulate firstControl;

            public DeviceInfo AddControl(BaseInputSimulate control)
            {
                control.m_NextControl = firstControl;
                firstControl = control;
                return this;
            }

            public DeviceInfo RemoveControl(BaseInputSimulate control)
            {
                if (firstControl == control)
                {
                    firstControl = control.m_NextControl;
                }
                else
                {
                    for (BaseInputSimulate current = firstControl.m_NextControl, previous = firstControl;
                        current != null; previous = current, current = current.m_NextControl)
                    {
                        if (current != control) continue;

                        previous.m_NextControl = current.m_NextControl;
                        break;
                    }
                }

                control.m_NextControl = null;
                return this;
            }

            public void Dispose()
            {
                if (buffer.IsCreated)
                {
                    buffer.Dispose();
                }

                if (device != null)
                {
                    InputSystem.RemoveDevice(device);
                }

                device = null;
                buffer = default;
            }
        }

        // METHODS: -------------------------------------------------------------------------------

        protected int GetDeviceInfoIndex(string deviceLayout)
        {
            var internedString = new InternedString(deviceLayout);

            for (int i = 0; i < s_InputDevices.Count; i++)
            {
                if (s_InputDevices[i].device.layout == internedString)
                {
                    return i;
                }
            }

            return -1;
        }
        
    }
}