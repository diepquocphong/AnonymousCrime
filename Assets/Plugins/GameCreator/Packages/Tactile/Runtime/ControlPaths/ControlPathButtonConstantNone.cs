using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("None")]
    [Category("None")]
    [Description("")]

    [Image(typeof(IconNull), ColorTheme.Type.Yellow)]
    [Keywords("Nothing", "Default")]

    [Serializable]
    public class ControlPathButtonConstantNone : ControlPathButton
    {
        public override string ControlPath => string.Empty;
    }
}