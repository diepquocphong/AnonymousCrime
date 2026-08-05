using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Right Stick Y")]
    [Category("Gamepad/Right Stick Y")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayY))]
    [Keywords("Look", "Joystick", "Secondary", "Analog", "Vertical")]

    [Serializable]
    public class ControlPathAxisGamepadRightStickY : ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/rightStick/y";
    }
}