using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Left Stick Y")]
    [Category("Gamepad/Left Stick Y")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayY))]
    [Keywords("Move", "Joystick", "Primary", "Analog", "Vertical")]

    [Serializable]
    public class ControlPathAxisGamepadLeftStickY : ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/leftStick/y";
    }
}