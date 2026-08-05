using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Left Stick")]
    [Category("Gamepad/Left Stick")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayArrowLeft))]
    [Keywords("Move", "Joystick", "Primary", "Analog")]

    [Serializable]
    public class ControlPathVector2GamepadLeftStick : ControlPathVector2
    {
        public override string ControlPath => "<Gamepad>/leftStick";
    }
}