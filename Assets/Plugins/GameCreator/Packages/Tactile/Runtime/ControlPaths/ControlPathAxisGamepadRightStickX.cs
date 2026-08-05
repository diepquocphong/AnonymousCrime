using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Right Stick X")]
    [Category("Gamepad/Right Stick X")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayX))]
    [Keywords("Look", "Joystick", "Secondary", "Analog", "Horizontal")]

    [Serializable]
    public class ControlPathAxisGamepadRightStickX : ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/rightStick/x";
    }
}