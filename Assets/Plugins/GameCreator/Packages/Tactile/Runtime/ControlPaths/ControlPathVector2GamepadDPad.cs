using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad D-pad")]
    [Category("Gamepad/D-pad")]
    [Description("")]

    [Image(typeof(IconGamepadCross), ColorTheme.Type.Yellow)]
    [Keywords("Direction", "Ordinal", "Cross")]

    [Serializable]
    public class ControlPathVector2GamepadDPad : ControlPathVector2
    {
        public override string ControlPath => "<Gamepad>/dpad";
    }
}