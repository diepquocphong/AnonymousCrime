using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(SwipeDirections))]
    public class SwipeDirectionsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new SwipeDirectionTool(property.FindPropertyRelative("m_Values"));
        }
    }
}
