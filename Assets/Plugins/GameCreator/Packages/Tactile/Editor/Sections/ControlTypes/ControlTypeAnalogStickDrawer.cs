using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeAnalogStick))]
    public class ControlTypeAnalogStickDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_InputSimulate");
            
            var fieldInputimulate = new PropertyField(inputSimulate);
            
            body.Add(fieldInputimulate);
            body.Add(new SpaceSmallest());

            this.CreateSurfaceDrawer(body, property);
            this.CreateHandleDrawer(body, property);
            this.CreateArrowDrawer(body, property);
        }

        private void CreateSurfaceDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty surface = property.FindPropertyRelative("m_Surface");
            SerializedProperty padding = property.FindPropertyRelative("m_SPadding");
            SerializedProperty dynamic = property.FindPropertyRelative("m_SDynamic");
            SerializedProperty constrain = property.FindPropertyRelative("m_SConstrain");
            SerializedProperty reposition = property.FindPropertyRelative("m_SReposition");
            SerializedProperty damping = property.FindPropertyRelative("m_SDamping");

            var surfaceBox = new Foldbox("Surface", "tactile:analog-stick-surface");
            var fieldSurface = new PropertyField(surface);
            var fieldPadding = new PropertyField(padding, "Padding");

            body.Add(surfaceBox);
            surfaceBox.Add(fieldSurface);
            surfaceBox.Add(fieldPadding);
            
            var groupDynamic = new VisualElement();
            var fieldDynamic = new PropertyField(dynamic, "Dynamic");
            var fieldConstrain = new PropertyField(constrain, "Constrain");
            var fieldReposition = new PropertyField(reposition, "Reposition");
            var fieldDamping = new PropertyField(damping, "Damping");

            fieldDynamic.RegisterValueChangeCallback(callback => 
            {
                groupDynamic.style.display = callback.changedProperty.boolValue 
                        ? DisplayStyle.Flex : DisplayStyle.None;
            });

            groupDynamic.style.display = dynamic?.boolValue ?? false
                        ? DisplayStyle.Flex : DisplayStyle.None;

            surfaceBox.Add(fieldDynamic);
            groupDynamic.Add(fieldConstrain);
            groupDynamic.Add(fieldReposition);
            groupDynamic.Add(fieldDamping);
            surfaceBox.Add(groupDynamic);
        }

        private void CreateHandleDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty handle = property.FindPropertyRelative("m_Handle");
            SerializedProperty axis = property.FindPropertyRelative("m_HAxis");
            SerializedProperty relative = property.FindPropertyRelative("m_HRelative");
            SerializedProperty sensitive = property.FindPropertyRelative("m_HSensitive");
            SerializedProperty ovrdDeadzone = property.FindPropertyRelative("m_HOverrideDeadzone");
            SerializedProperty deadzone = property.FindPropertyRelative("m_HDeadzone");
            SerializedProperty damping = property.FindPropertyRelative("m_HDamping");

            var handleBox = new Foldbox("Handle", "tactile:analog-stick-handle");
            var fieldHandle = new PropertyField(handle);
            var fieldAxis = new PropertyField(axis, "Axis");
            var fieldRelative = new PropertyField(relative, "Relative");
            var fieldSensitive = new PropertyField(sensitive, "Sensitive");

            body.Add(handleBox);
            handleBox.Add(fieldHandle);
            handleBox.Add(fieldAxis);
            handleBox.Add(fieldRelative);
            handleBox.Add(fieldSensitive);

            if (ovrdDeadzone != null)
            {
                var fieldDeadzoneVal = new Vector2Field(string.Empty)
                {
                    style = { marginRight = 0f, flexGrow = 1f },
                };

                var inputs = fieldDeadzoneVal.Q(className: "unity-base-field__input");
                if (inputs[0] is FloatField x) x.label = string.Empty;

                if (inputs[1] is FloatField y)
                {
                    y.label = string.Empty;
                    y.style.marginRight = 0f;
                }

                inputs.RemoveAt(2);

                var fieldDeadzoneTog = new Toggle("Deadzone") 
                {
                    bindingPath = ovrdDeadzone.propertyPath
                };
                fieldDeadzoneTog.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
                fieldDeadzoneTog.Q(null, "unity-toggle__input").Add(fieldDeadzoneVal);
                fieldDeadzoneTog.RegisterValueChangedCallback(callback => 
                {
                    this.UpdateDeadzone(ovrdDeadzone.boolValue, fieldDeadzoneVal, deadzone);
                });
                this.UpdateDeadzone(ovrdDeadzone.boolValue, fieldDeadzoneVal, deadzone);

                fieldDeadzoneVal.RegisterCallback<FocusOutEvent>(callback => 
                {
                    if (!ovrdDeadzone.boolValue) fieldDeadzoneTog.value = true;

                    Vector2 value = deadzone.vector2Value;
                    value.x = Mathf.Clamp01(value.x);
                    value.y = Mathf.Clamp01(value.y);

                    if (value.x > value.y) value.x = value.y;
                    fieldDeadzoneVal.value = value;
                });

                handleBox.Add(fieldDeadzoneTog);
            }

            if (damping != null)
            {
                var fieldDamping = new Vector2Field("Damping")
                {
                    bindingPath = damping.propertyPath
                };
                fieldDamping.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);

                var inputs = fieldDamping.Q(null, "unity-base-field__input");
                inputs.style.marginLeft = 2f;

                if (inputs[0] is FloatField x)
                {
                    x.label = "D";
                    x.tooltip = "Drag";
                }

                if (inputs[1] is FloatField y)
                {
                    y.label = "R";
                    y.tooltip = "Recenter";
                    y.style.marginLeft = 2f;
                    y.style.marginRight = 0f;
                }
                
                handleBox.Add(fieldDamping);
            }
        }

        private void UpdateDeadzone(bool isEnabled, Vector2Field field, SerializedProperty property)
        {
            if (isEnabled) 
            {
                field.BindProperty(property);
            }
            else
            {
                var settings = InputSystem.settings;
                float deadzoneMin = settings.defaultDeadzoneMin;
                float deadzoneMax = settings.defaultDeadzoneMax;
                field.SetValueWithoutNotify(new Vector2(deadzoneMin, deadzoneMax));
            }

            field.SetEnabled(isEnabled);
        }
        
        private void CreateArrowDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty arrow = property.FindPropertyRelative("m_Arrow");
            SerializedProperty origin = property.FindPropertyRelative("m_AOrigin");
            SerializedProperty steps = property.FindPropertyRelative("m_ADirectionSteps");
            SerializedProperty offset = property.FindPropertyRelative("m_ADegreeOffset");
            SerializedProperty threshold = property.FindPropertyRelative("m_AThreshold");
            SerializedProperty damping = property.FindPropertyRelative("m_ADamping");

            var arrowBox = new Foldbox("Arrow", "tactile:analog-stick-arrow");
            var fieldArrow = new PropertyField(arrow);
            var groupArrow = new VisualElement();
            var fieldOrigin = new PropertyField(origin, "Origin");
            var fieldSteps = new PropertyField(steps, "Steps");
            var fieldOffset = new PropertyField(offset, "Offset");
            var fieldDamping = new PropertyField(damping, "Damping");

            body.Add(arrowBox);
            arrowBox.Add(fieldArrow);
            arrowBox.Add(groupArrow);
            groupArrow.Add(fieldOrigin);
            groupArrow.Add(fieldSteps);
            groupArrow.Add(fieldOffset);

            if (threshold != null)
            {
                Slider fieldThreshold = new Slider("Threshold", 0f, 1f)
                {
                    bindingPath = threshold.propertyPath,
                    showInputField = true,
                    style = { marginBottom = 0f }
                };
                
                fieldThreshold.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
                groupArrow.Add(fieldThreshold);
            }
            groupArrow.Add(fieldDamping);

            groupArrow.style.display = arrow?.objectReferenceValue
                ? DisplayStyle.Flex : DisplayStyle.None;

            fieldArrow.RegisterValueChangeCallback(callback => 
            {
                groupArrow.style.display = arrow?.objectReferenceValue 
                    ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }
        
    }
}