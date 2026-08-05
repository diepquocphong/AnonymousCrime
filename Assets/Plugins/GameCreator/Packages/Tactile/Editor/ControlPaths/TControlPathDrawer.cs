using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TControlPath<>), true)]
    public class TControlPathDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new PropertyElement(property, "Input Simulate", true);
        }
    }
}