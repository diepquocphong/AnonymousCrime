using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeSteeringWheel))]
    public class ControlTypeSteeringWheelDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_InputSimulate");
            SerializedProperty timeMode = property.FindPropertyRelative("m_TimeMode");
            
            var fieldInputimulate = new PropertyField(inputSimulate);
            var fieldTimeMode = new PropertyField(timeMode);
            
            body.Add(fieldInputimulate);
            body.Add(fieldTimeMode);
            body.Add(new SpaceSmallest());

            this.CreateWheelDrawer(body, property);
            this.CreateSteerDrawer(body, property);
            this.CreateLiftDrawer(body, property);
        }

        private void CreateWheelDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty wheel = property.FindPropertyRelative("m_Wheel");
            SerializedProperty deadzone = property.FindPropertyRelative("m_Deadzone");

            var wheelBox = new Foldbox("Wheel", "tactile:steering-wheel-wheel");
            var fieldWheel = new PropertyField(wheel);
            var fieldDeadzone = new PropertyField(deadzone);

            body.Add(wheelBox);
            wheelBox.Add(fieldWheel);
            wheelBox.Add(fieldDeadzone);
        }

        private void CreateSteerDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty canSnapAngle = property.FindPropertyRelative("m_CanSnap");
            SerializedProperty snapAngle = property.FindPropertyRelative("m_SnapAngle");
            SerializedProperty hasMaxAngle = property.FindPropertyRelative("m_HasMaxAngle");
            SerializedProperty maxAngle = property.FindPropertyRelative("m_MaxAngle");
            SerializedProperty sensitivity = property.FindPropertyRelative("m_Sensitivity");
            SerializedProperty damping = property.FindPropertyRelative("m_SteerDamping");

            var steerBox = new Foldbox("Steer", "tactile:steering-wheel-steer");
            var fieldSensitivity = new PropertyField(sensitivity); 
            var fieldDamping = new PropertyField(damping, "Damping");

            body.Add(steerBox);

            this.CreateEnablerField(steerBox, canSnapAngle, snapAngle);
            this.CreateEnablerField(steerBox, hasMaxAngle, maxAngle);

            steerBox.Add(fieldSensitivity);
            steerBox.Add(fieldDamping);
        }

        private void CreateLiftDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty recenter = property.FindPropertyRelative("m_Recenter");
            SerializedProperty damping = property.FindPropertyRelative("m_LiftDamping");

            var liftBox = new Foldbox("Lift", "tactile:steering-wheel-lift");
            var fieldRecenter = new PropertyField(recenter); 
            var fieldDamping = new PropertyField(damping, "Damping");

            body.Add(liftBox);
            liftBox.Add(fieldRecenter);
            liftBox.Add(fieldDamping);
        }

        private void CreateEnablerField(VisualElement container, SerializedProperty toggleProp, 
            SerializedProperty fieldProp)
        {
            if (toggleProp == null) return;

            var fieldInput = new PropertyField(fieldProp, "")
            {
                style = { marginRight = 2f, flexGrow = 1f }
            };

            var fieldToggle = new Toggle(fieldProp.displayName)
            {
                bindingPath = toggleProp.propertyPath,
            };
            fieldToggle.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
            fieldToggle.Q(className: "unity-toggle__input").Add(fieldInput);

            fieldInput.style.display = toggleProp.boolValue
                ? DisplayStyle.Flex : DisplayStyle.None;

            fieldToggle.RegisterValueChangedCallback(callback =>
            {
                fieldInput.style.display = callback.newValue
                    ? DisplayStyle.Flex : DisplayStyle.None;
            });

            container.Add(fieldToggle);
        }
    }
}