using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Delta X")]
    [Category("Mouse/Delta X")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow, typeof(OverlayX))]
    [Keywords("Mice", "Position", "Horizontal")]

    [Serializable]
    public class ControlPathAxisMouseDeltaX : ControlPathAxis
    {
        public override string ControlPath => "<Mouse>/delta/x";
    }
}