using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(SkillCancellation))]
    public class SkillCancellationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty areaProp = property.FindPropertyRelative("m_CancelArea");
            SerializedProperty invertedProp = property.FindPropertyRelative("m_IsInverted");

            var cancellationBox = new Foldbox("Cancellation", "tactile:skill-cancellation");

            var areaField = new ObjectField(areaProp.displayName);
            var invertedField = new PropertyField(invertedProp);

            invertedField.style.display = areaProp.objectReferenceValue != null
                    ? DisplayStyle.Flex : DisplayStyle.None;

            areaField.RegisterValueChangedCallback(evt =>
            {
                invertedField.style.display = evt.newValue != null
                    ? DisplayStyle.Flex : DisplayStyle.None;
            });

            areaField.objectType = typeof(TactileControl);
            areaField.BindProperty(areaProp);
            areaField.Q(null, ObjectField.inputUssClassName)
                ?.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            
            areaField.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);

            cancellationBox.Add(areaField);
            cancellationBox.Add(invertedField);

            return cancellationBox;
        }

    }
}
