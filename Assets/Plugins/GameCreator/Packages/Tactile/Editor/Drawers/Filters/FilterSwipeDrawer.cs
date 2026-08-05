using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(FilterSwipe))]
    public class FilterSwipeDrawer : PropertyDrawer
    {
        private const string PROP_OPTION = "m_Option";
        private const string PROP_SLOT = "m_SwipeID";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var head = new VisualElement();
            var body = new VisualElement();

            root.Add(head);
            root.Add(body);

            SerializedProperty propFilterType = property.FindPropertyRelative(PROP_OPTION);
            var fieldFilterType = new PropertyField(propFilterType, property.displayName);
            head.Add(fieldFilterType);

            fieldFilterType.RegisterValueChangeCallback(_ =>
            {
                this.UpdateBody(body, property);
            });

            this.UpdateBody(body, property);

            return root;
        }

        private void UpdateBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty propFilterType = property.FindPropertyRelative(PROP_OPTION);
            body.Clear();

            switch (propFilterType.enumValueIndex)
            {
                case 0: // Any
                    break;

                case 1: // Specific
                    SerializedProperty propSlotName = property.FindPropertyRelative(PROP_SLOT);
                    var fieldSlotName = new PropertyField(propSlotName);
                    fieldSlotName.Bind(property.serializedObject);
                    fieldSlotName.style.paddingLeft = 7f;
                    body.Add(fieldSlotName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

    }
}