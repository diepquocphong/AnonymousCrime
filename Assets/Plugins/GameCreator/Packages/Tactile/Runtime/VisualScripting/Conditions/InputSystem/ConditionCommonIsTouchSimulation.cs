using System;
using UnityEngine.InputSystem.EnhancedTouch;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Touch Simulation")]
    [Category("Input System/Is Touch Simulation")]
    [Description("Returns true if the touch simulation is active; Otherwise, returns false")]

    [Image(typeof(IconFinger), ColorTheme.Type.Green)]
    [Keywords("Mobile", "Device", "Finger", "Emulate", "Simulate")]

    [Serializable]
    public class ConditionCommonIsTouchSimulation : Condition
    {
        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is Touch Simulation";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            var instance = TouchSimulation.instance;

            #if UNITY_EDITOR

            if (instance == null && UnityEngine.InputSystem.Touchscreen.current != null)
            {
                string deviceName = UnityEngine.InputSystem.Touchscreen.current.name;
                return deviceName == "Device Simulator Touchscreen" || 
                       deviceName == "Simulated Touchscreen";
            }

            #endif

            return instance != null && instance.enabled;
        }

    }
}
