using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad D-pad Y")]
    [Category("Gamepad/D-pad Y")]
    [Description("")]

    [Image(typeof(IconGamepadCross), ColorTheme.Type.Yellow, typeof(OverlayY))]
    [Keywords("Direction", "Vertical")]

    [Serializable]
    public class ControlPathAxisGamepadDpadY: ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/dpad/y";

        public override float PreprocessInput(float input) => input switch
        {
            > 0 => 0.10f,
            < 0 => 0.15f,
            _ => input,
        };
    }
}