using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(TInputControl))]
    public class InputControlDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();
            
            SerializationUtils.CreateChildProperties(
                container, property,
                SerializationUtils.ChildrenMode.ShowLabelsInChildren,
                true
            );

            container.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            return container;
        }
    }
}