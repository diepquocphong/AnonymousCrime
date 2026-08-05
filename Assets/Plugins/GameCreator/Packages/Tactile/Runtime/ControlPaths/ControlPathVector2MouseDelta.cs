using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Delta")]
    [Category("Mouse/Delta")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow)]
    [Keywords("Mice", "Position", "Changed")]

    [Serializable]
    public class ControlPathVector2MouseDelta : ControlPathVector2
    {
        public override string ControlPath => "<Mouse>/delta";

    }
}