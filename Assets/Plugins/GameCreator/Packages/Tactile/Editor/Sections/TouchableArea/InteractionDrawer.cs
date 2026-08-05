using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TouchInteraction))]
    public class InteractionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty useTapTime = property.FindPropertyRelative("m_UseTapTime");
            SerializedProperty useTapRadius = property.FindPropertyRelative("m_UseTapRadius");
            SerializedProperty useSlowTapTime = property.FindPropertyRelative("m_UseSlowTapTime");
            SerializedProperty useMultiTapTime = property.FindPropertyRelative("m_UseMultiTapDelayTime");
            SerializedProperty useHoldTime = property.FindPropertyRelative("m_UseHoldTime");

            SerializedProperty tapTime = property.FindPropertyRelative("m_TapTime");
            SerializedProperty tapRadius = property.FindPropertyRelative("m_TapRadius");
            SerializedProperty slowTapTime = property.FindPropertyRelative("m_SlowTapTime");
            SerializedProperty multiTapTime = property.FindPropertyRelative("m_MultiTapDelayTime");
            SerializedProperty holdTime = property.FindPropertyRelative("m_HoldTime");
            SerializedProperty holdRadius = property.FindPropertyRelative("m_HoldRadius");

            var foldBox = new Foldbox("Interaction Config", "tactile:override-interaction-config");

            var fieldFloat = new FloatField("Hold Radius") 
            { 
                bindingPath = holdRadius.propertyPath,
                style = { marginRight = 0f, flexGrow = 1f }
            };
            fieldFloat.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);

            this.DrawEnablerField("Tap Time", foldBox, useTapTime, tapTime, 0);
            this.DrawEnablerField("Slow Tap Time", foldBox, useSlowTapTime, slowTapTime, 1);
            this.DrawEnablerField("Multi Tap Time", foldBox, useMultiTapTime, multiTapTime, 2);
            this.DrawEnablerField("Hold Time", foldBox, useHoldTime, holdTime, 3);
            foldBox.Add(new SpaceSmall());
            this.DrawEnablerField("Tap Radius", foldBox, useTapRadius, tapRadius, 4);
            foldBox.Add(fieldFloat);

            return foldBox;
        }

        private void DrawEnablerField(
            string label, VisualElement container, SerializedProperty toggle, SerializedProperty field, int a)
        {
            var fieldFloat = new FloatField(string.Empty)
            {
                style = { marginRight = 0f, flexGrow = 1f }
            };

            var fieldToggle = new Toggle(label) 
            { 
                bindingPath = toggle.propertyPath,
                style = { height = 21f, marginBottom = 1f, unityTextAlign = TextAnchor.MiddleLeft }
            };
            fieldToggle.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
            fieldToggle.Q(className: "unity-toggle__input").Add(fieldFloat);
            fieldToggle.RegisterValueChangedCallback(callback => 
            {
                this.UpdateEnablerField(toggle.boolValue, fieldFloat, field, a);
            });
            this.UpdateEnablerField(toggle.boolValue, fieldFloat, field, a);

            container.Add(fieldToggle);
        }

        private void UpdateEnablerField(bool enabled, FloatField field, SerializedProperty property, int a)
        {
            if (enabled) 
            {
                field.BindProperty(property);
            }
            else
            {
                var settings = InputSystem.settings;
                float time = a switch 
                {
                    0 => settings.defaultTapTime,
                    1 => settings.defaultSlowTapTime,
                    2 => settings.multiTapDelayTime,
                    3 => settings.defaultHoldTime,
                    4 => settings.tapRadius,
                    _ => 0f
                };
                field.SetValueWithoutNotify(time);
            }

            field.SetEnabled(enabled);
        }
    }
}