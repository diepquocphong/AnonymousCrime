using UnityEditor;
using UnityEngine.UIElements;

using GameCreator.Runtime.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(TControlType))]
    public class TControlTypeDrawer : TactileSectionDrawer
    {
        protected sealed override IIcon UnitIcon => new IconRobotArm(ColorTheme.Type.TextLight);

        public sealed override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return this.MakePropertyGUI(property, "Control Type");
        }
    }
}