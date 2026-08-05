using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

using Niam.Runtime.Tactile;
using UnityEditor.UIElements;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TouchableAreaTouchableArea))]
    public class TouchableAreaTouchableAreaDrawer : TTouchableAreaDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            body.Clear();

            SerializedProperty propControl = property.FindPropertyRelative("m_TactileControl");
            var fieldControl = new PropertyField(propControl);
            body.Add(fieldControl);

            // TODO: Needs circular reference protection
            fieldControl.RegisterValueChangeCallback(evt =>
            {
                if (evt.changedProperty.objectReferenceValue is not TactileControl control) 
                    return;

                if (control.TouchableArea is not TouchableAreaTouchableArea touchableArea)
                    return;

            });

            this.CreateCommonPropertiesGUI(body, property);
        }

    }
}