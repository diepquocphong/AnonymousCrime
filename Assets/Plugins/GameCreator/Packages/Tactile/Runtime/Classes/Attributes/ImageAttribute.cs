using System;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class ImageAttribute : GameCreator.Runtime.Common.ImageAttribute
    {
        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ImageAttribute(Type iconType, ColorTheme.Type color)
            : base(iconType, ColorTheme.Get(color))
        { }

        public ImageAttribute(Type iconType, ColorTheme.Type iconColor, Type overlayType)
            : base(iconType, ColorTheme.Get(iconColor), overlayType)
        { }

        public ImageAttribute(Type iconType)
            : base(iconType, Theme.MainColor, null)
        { }

        public ImageAttribute(Type iconType, Type overlayType)
            : base(iconType, Theme.MainColor, overlayType)
        { }
    }   
}
