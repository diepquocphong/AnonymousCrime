using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Scroll")]
    [Category("Mouse/Scroll")]
    [Description("")]

    [Image(typeof(IconScroll), ColorTheme.Type.Yellow)]
    [Keywords("Mice", "Zoom", "Wheel")]

    [Serializable]
    public class ControlPathVector2MouseScroll : ControlPathVector2
    {
        public override string ControlPath => "<Mouse>/scroll";
    }
}