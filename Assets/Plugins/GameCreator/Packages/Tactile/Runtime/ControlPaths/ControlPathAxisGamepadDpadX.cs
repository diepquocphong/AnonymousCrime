using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad D-pad X")]
    [Category("Gamepad/D-pad X")]
    [Description("")]

    [Image(typeof(IconGamepadCross), ColorTheme.Type.Yellow, typeof(OverlayX))]
    [Keywords("Direction", "Horizontal")]

    [Serializable]
    public class ControlPathAxisGamepadDpadX : ControlPathAxis
    {
        public override string ControlPath => "<Gamepad>/dpad/x";

        public override float PreprocessInput(float input) => input switch
        {
            > 0 => 0.55f,
            < 0 => 0.30f,
            _ => input,
        };
    }
}