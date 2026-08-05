using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(SkillCooldown))]
    public class SkillCooldownDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty isManual = property.FindPropertyRelative("m_IsManual");
            SerializedProperty fill = property.FindPropertyRelative("m_Fill");
            SerializedProperty textType = property.FindPropertyRelative("m_TextType");
            SerializedProperty format = property.FindPropertyRelative("m_Format");
            SerializedProperty duration = property.FindPropertyRelative("m_Duration");
            SerializedProperty timeMode = property.FindPropertyRelative("m_TimeMode");
            SerializedProperty amount = property.FindPropertyRelative("m_Amount");

            var cdBox = new Foldbox("Cooldown", "tactile:skill-cooldown");

            var fieldIsManual = new PropertyField(isManual);
            var fieldFill = new PropertyField(fill);
            var fieldFormat = new PropertyField(format);

            var fieldText = new VisualElement();
            var labelText = new Label("Text");
            var fieldTextType = new PropertyField(textType);
            var inputText = new VisualElement();

            fieldText.style.marginRight = 0;
            fieldText.style.marginBottom = 0;
            fieldText.style.overflow = Overflow.Visible;
            fieldTextType.label = string.Empty;
            fieldTextType.style.marginLeft = -3f;
            inputText.style.marginRight = 0f;
            inputText.style.overflow = Overflow.Visible;

            fieldText.AddToClassList("unity-base-field");
            labelText.AddToClassList("unity-base-field__label");
            inputText.AddToClassList("unity-base-field__input");

            fieldText.Add(labelText);
            fieldText.Add(fieldTextType);
            fieldText.Add(inputText);
            AlignLabel.On(fieldText);

            fieldTextType.RegisterValueChangeCallback(
                e => this.RefreshTextReferences(inputText, property)
            );

            this.RefreshTextReferences(inputText, property);

            var fieldDuration = new PropertyField(duration);
            var fieldTimeMode = new PropertyField(timeMode);
            var fieldAmount = new PropertyField(amount);

            cdBox.Add(fieldFill);
            cdBox.Add(fieldText);
            cdBox.Add(fieldFormat);
            cdBox.Add(new SpaceSmall());

            cdBox.Add(fieldDuration);
            cdBox.Add(fieldTimeMode);
            cdBox.Add(fieldAmount);

            cdBox.Add(new SpaceSmall());
            cdBox.Add(fieldIsManual);

            return cdBox;
        }

        private void RefreshTextReferences(VisualElement content, SerializedProperty property)
        {
            content.Clear();
            property.serializedObject.Update();
            switch (property.FindPropertyRelative("m_TextType").intValue)
            {
                case 0: // TextType.Text
                    var propertyText = property.FindPropertyRelative("m_TextLegacy");
                    var fieldText = new PropertyField(propertyText, string.Empty);
                    fieldText.Bind(property.serializedObject);
                    content.Add(fieldText);
                    break;

                case 1: // TextType.TMP
                    var propertyTMP = property.FindPropertyRelative("m_TextTMP");
                    var fieldTMP = new PropertyField(propertyTMP, string.Empty);
                    fieldTMP.Bind(property.serializedObject);
                    content.Add(fieldTMP);
                    break;
            }
        }
        
    }
}
