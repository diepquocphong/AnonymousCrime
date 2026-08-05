using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Collections;

namespace Niam.Runtime.Tactile
{
    public abstract class TInputSimulate<TValue> : BaseInputSimulate where TValue : struct
    {
        // MEMBERS: -------------------------------------------------------------------------------

        protected InputControl<TValue> m_Control;
        protected InputEventPtr m_EventPtr;
        protected int m_PathHash;

        // PROPERTIES: ---------------------------------------------------------------------------- 

        public abstract TControlPath<TValue> ControlPath { get; }

        // INITIALIZERS: --------------------------------------------------------------------------

        public void OnEnabled(UnityEngine.Object logContext = null)
        {
            string path = this.ControlPath?.ControlPath;
            if (string.IsNullOrEmpty(path)) return;

            var deviceLayout = InputControlPath.TryGetDeviceLayout(path);
            if (deviceLayout == null)
            {
                Debug.LogError(
                    $"Cannot determine device layout to use based on control path '{path}'",
                    logContext
                );
                return;
            }

            InputDevice device;
            int index = this.GetDeviceInfoIndex(deviceLayout);

            if (index == -1)
            {
                try
                {
                    string name = string.Equals(deviceLayout, "Tactile", StringComparison.OrdinalIgnoreCase)
                        ? "Tactile Device" : $"Tactile {deviceLayout}";

                    device = InputSystem.AddDevice(deviceLayout, name);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Could not create a virtual device with layout '{deviceLayout}'", 
                        logContext
                    );

                    Debug.LogException(exception);
                    return;
                }

                InputSystem.AddDeviceUsage(device, TACTILE_USAGE);
                
                NativeArray<byte> buffer = StateEvent.From(
                    device, out InputEventPtr eventPtr, Allocator.Persistent
                );
                
                s_InputDevices.Add(new DeviceInfo
                {
                    device = device, eventPtr = eventPtr, buffer = buffer
                });

                index = s_InputDevices.Count - 1;
            }
            else
            {
                device = s_InputDevices[index].device;
            }

            this.m_Control = InputControlPath.TryFindControl(device, path) as InputControl<TValue>;
            if (this.m_Control == null)
            {
                Debug.LogWarning(
                    $"Cannot find control with path '{path}' on '{deviceLayout}' device", 
                    logContext
                );

                if (s_InputDevices[index].firstControl == null)
                {
                    s_InputDevices[index].Dispose();
                    s_InputDevices.RemoveAt(index);
                }
            }
            else
            {
                this.m_EventPtr = s_InputDevices[index].eventPtr;
                s_InputDevices[index] = s_InputDevices[index].AddControl(this);
                
                this.m_PathHash = Animator.StringToHash(path);
            }
        }

        public void OnDisabled()
        { 
            if (this.m_Control == null) return;

            InputDevice device = this.m_Control.device;
            for (int i = 0; i < s_InputDevices.Count; i++)
            {
                if (s_InputDevices[i].device != device) continue;

                DeviceInfo value = s_InputDevices[i].RemoveControl(this);
                if (value.firstControl == null)
                {
                    s_InputDevices[i].Dispose();
                    s_InputDevices.RemoveAt(i);
                }
                else
                {
                    s_InputDevices[i] = value;
                    if (device.added && !this.m_Control.CheckStateIsAtDefault())
                    {
                        this.SendResetValueToControl();
                    }
                }
            }

            this.m_PathHash = 0;
            this.m_Control = null;
            this.m_EventPtr = default;
        }

        // VALUE METHODS: -------------------------------------------------------------------------

        public void SendValueToControl(TValue value)
        {
            if (this.m_Control == null) return;

            if (s_UpdatedControls.Contains(this.m_PathHash))
                this.InputCollisionValue(ref value);
            else
                s_UpdatedControls.Add(this.m_PathHash);

            this.m_EventPtr.time = InputState.currentTime;
            TValue processedValue = this.ControlPath.PreprocessInput(value);
            this.m_Control.WriteValueIntoEvent(processedValue, this.m_EventPtr);
            
            InputSystem.QueueEvent(this.m_EventPtr);
            // InputState.Change(this.m_Control.device, this.m_EventPtr);
        }

        public void SendDefaultValueToControl()
        {
            if (this.m_Control == null) return;
            this.SendValueToControl(this.m_Control.ReadDefaultValue());
        }

        public void SendResetValueToControl()
        {
            if (this.m_Control == null) return;

            this.m_EventPtr.time = InputState.currentTime;
            this.m_Control.ResetToDefaultStateInEvent(this.m_EventPtr);

            InputSystem.QueueEvent(this.m_EventPtr);
            // InputState.Change(this.m_Control.device, this.m_EventPtr);
        }

        public TValue ReadValueFromControl()
        {
            if (this.m_Control is not InputControl<TValue> control)
                return default;

            return control.ReadValueFromEvent(this.m_EventPtr);
        }

        protected virtual void InputCollisionValue(ref TValue value)
        { }

        // STRING: --------------------------------------------------------------------------------

        public abstract override string ToString();

    }
}