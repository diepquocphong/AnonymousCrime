using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TInputSimulate<>), true)]
    public class TInputSimulateDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var controlPath = property.FindPropertyRelative("m_ControlPath");
            var overridePath = property.FindPropertyRelative("m_OverridePath");

            var fieldControlPath = new PropertyElement(controlPath, "Input Simulate", true);
            fieldControlPath.EventChangeType += (a, b) =>
            {
                if (Application.isPlaying)
                {
                    var control = property.serializedObject.targetObject as TactileControl;
                    control.enabled = false;
                    overridePath.managedReferenceValue = null;
                    control.enabled = true;
                }
            };
            container.Add(fieldControlPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var fieldOverridePath = new TextField("Input Simulate") { isReadOnly = true };
                fieldOverridePath.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
                container.Add(fieldOverridePath);

                if (overridePath.managedReferenceValue != null)
                {
                    fieldOverridePath.SetValueWithoutNotify($"{overridePath.managedReferenceValue}");
                    fieldControlPath.style.display = DisplayStyle.None;
                    fieldOverridePath.style.display = DisplayStyle.Flex;
                }
                else
                {
                    fieldControlPath.style.display = DisplayStyle.Flex;
                    fieldOverridePath.style.display = DisplayStyle.None;
                }

                container.schedule.Execute(e =>
                {
                    try
                    {
                        if (overridePath?.managedReferenceValue != null)
                        {
                            fieldOverridePath.SetValueWithoutNotify($"{overridePath.managedReferenceValue}");
                            fieldControlPath.style.display = DisplayStyle.None;
                            fieldOverridePath.style.display = DisplayStyle.Flex;
                        }
                        else
                        {
                            fieldControlPath.style.display = DisplayStyle.Flex;
                            fieldOverridePath.style.display = DisplayStyle.None;
                        }
                    }
                    catch
                    {
                        fieldControlPath.style.display = DisplayStyle.Flex;
                        fieldOverridePath.style.display = DisplayStyle.None;
                    }

                }).Every(500);
            }

            return container;
        }
    }
}