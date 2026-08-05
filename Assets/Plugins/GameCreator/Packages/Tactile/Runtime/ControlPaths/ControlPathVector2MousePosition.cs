using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Position")]
    [Category("Mouse/Position")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow)]
    [Keywords("Mice", "Position")]

    [Serializable]
    public class ControlPathVector2MousePosition : ControlPathVector2
    {
        public override string ControlPath => "<Mouse>/position";

    }
}