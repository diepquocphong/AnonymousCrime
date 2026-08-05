using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Delta Y")]
    [Category("Mouse/Delta Y")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow, typeof(OverlayY))]
    [Keywords("Mice", "Position", "Vertical")]

    [Serializable]
    public class ControlPathAxisMouseDeltaY : ControlPathAxis
    {
        public override string ControlPath => "<Mouse>/delta/y";
    }
}