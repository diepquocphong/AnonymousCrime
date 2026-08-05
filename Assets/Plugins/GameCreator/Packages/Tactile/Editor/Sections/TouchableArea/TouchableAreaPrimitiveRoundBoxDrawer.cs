using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;
using GameCreator.Editor.Common;
using UnityEngine;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TouchableAreaPrimitiveRoundBox))]
    public class TouchableAreaPrimitiveRoundBoxDrawer : TTouchableAreaDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            body.Clear();

            SerializedProperty size = property.FindPropertyRelative("m_Size");
            SerializedProperty offset = property.FindPropertyRelative("m_Offset");
            SerializedProperty radius = property.FindPropertyRelative("m_Radius");

            if (size == null || offset == null || radius == null) return;

            var fieldSize = new Vector2Field(size.displayName);
            var fieldOffset = new Vector2Field(offset.displayName);
            var fieldRadius = new FloatField(radius.displayName);

            fieldSize.RegisterValueChangedCallback(this.OnValueChanged);
            fieldRadius.RegisterValueChangedCallback(this.OnValueChanged);

            fieldSize.bindingPath = size.propertyPath;
            fieldOffset.bindingPath = offset.propertyPath;
            fieldRadius.bindingPath = radius.propertyPath;

            fieldSize.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
            fieldOffset.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
            fieldRadius.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);

            body.Add(fieldSize);
            body.Add(fieldOffset);
            body.Add(fieldRadius);

            this.CreateCommonPropertiesGUI(body, property);
        }

        private void OnValueChanged<T>(ChangeEvent<T> evt)
        {
            if (evt is ChangeEvent<Vector2> evtV2 && evt.target is Vector2Field fieldV2)
            {
                Vector2 newValue = evtV2.newValue;
                if (newValue.x < 0) newValue.x = 0;
                if (newValue.y < 0) newValue.y = 0;

                fieldV2.SetValueWithoutNotify(newValue);
            }

            if (evt is ChangeEvent<float> evtFl && evt.target is FloatField fieldFl)
            {
                if (evtFl.newValue < 0) fieldFl.SetValueWithoutNotify(0);
            }
        }

    }
}