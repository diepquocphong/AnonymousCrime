using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypePushButton))]
    public class ControlTypePushButtonDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_InputSimulate");
            SerializedProperty activateMode = property.FindPropertyRelative("m_InputExecution");

            var fieldInputSimulate = new PropertyField(inputSimulate);
            var fieldActivateMode = new PropertyField(activateMode);

            body.Add(fieldInputSimulate);
            body.Add(fieldActivateMode);
        }

    }
}