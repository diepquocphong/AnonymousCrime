using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Scroll Y")]
    [Category("Mouse/Scroll Y")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow, typeof(OverlayY))]
    [Keywords("Mice", "Zoom", "Wheel", "Vertical")]

    [Serializable]
    public class ControlPathAxisMouseScrollY : ControlPathAxis
    {
        public override string ControlPath => "<Mouse>/scroll/y";
    }
}