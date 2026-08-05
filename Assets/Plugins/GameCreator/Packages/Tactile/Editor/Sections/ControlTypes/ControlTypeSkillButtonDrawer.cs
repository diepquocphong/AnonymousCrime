using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeSkillButton))]
    public class ControlTypeSkillButtonDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_InputSimulate");
            SerializedProperty execution = property.FindPropertyRelative("m_InputExecution");
            SerializedProperty isUsable = property.FindPropertyRelative("m_IsUsable");
            SerializedProperty cooldown = property.FindPropertyRelative("m_Cooldown");
            SerializedProperty cancellation = property.FindPropertyRelative("m_Cancellation");

            var fieldIsUsable = new PropertyField(isUsable);
            var fieldInputSimulate = new PropertyField(inputSimulate);
            var fieldExecution = new PropertyField(execution);
            var fieldCooldown = new PropertyField(cooldown);
            var fieldCancellation = new PropertyField(cancellation);

            body.Add(fieldInputSimulate);
            body.Add(fieldExecution);
            body.Add(fieldIsUsable);
            body.Add(fieldCooldown);
            body.Add(fieldCancellation);
        }
    }
}