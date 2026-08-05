using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Right Stick")]
    [Category("Gamepad/Right Stick")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayArrowRight))]
    [Keywords("Look", "Joystick", "Secondary", "Analog")]

    [Serializable]
    public class ControlPathVector2GamepadRightStick : ControlPathVector2
    {
        public override string ControlPath => "<Gamepad>/rightStick";
    }
}