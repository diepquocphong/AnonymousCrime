using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Left Stick X")]
    [Category("Gamepad/Left Stick X")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayX))]
    [Keywords("Move", "Joystick", "Primary", "Analog", "Horizontal")]

    [Serializable]
    public class ControlPathAxisGamepadLeftStickX : ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/leftStick/x";
    }
}