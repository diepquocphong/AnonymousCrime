using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine.InputSystem;

namespace Niam.Runtime.Tactile
{
    [Title("Has Touchscreen")]
    [Category("Input System/Has Touchscreen")]
    [Description("Returns true if a Touchscreen device is found; Otherwise, returns false")]

    [Image(typeof(IconFinger), ColorTheme.Type.Green)]
    [Keywords("Mobile", "Device", "Finger")]

    [Serializable]
    public class ConditionCommonHasTouchscreen : Condition
    {
        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Has Touchscreen";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            return Touchscreen.current != null;
        }
    }
}
