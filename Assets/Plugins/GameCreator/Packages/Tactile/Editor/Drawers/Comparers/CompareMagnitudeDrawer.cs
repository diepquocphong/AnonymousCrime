using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(CompareMagnitude))]
    public class CompareMagnitudeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            SerializedProperty comparison = property.FindPropertyRelative("m_Comparison");
            SerializedProperty compareTo = property.FindPropertyRelative("m_CompareTo");

            var fieldComparison = new PropertyField(comparison);
            var fieldCompareTo = new PropertyField(compareTo, property.displayName);

            root.Add(fieldComparison);
            root.Add(fieldCompareTo);

            return root;
        }
    }
}