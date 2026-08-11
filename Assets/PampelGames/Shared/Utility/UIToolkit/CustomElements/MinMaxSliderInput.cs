// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Utility
{
    public class MinMaxSliderInput : MinMaxSlider
    {
        private VisualElement m_InputContainer;
        private FloatField m_MinValueField;
        private FloatField m_MaxValueField;

        public MinMaxSliderInput(string label, float minValue = 0.0f, float maxValue = 10f,
            float minLimit = float.MinValue, float maxLimit = float.MaxValue)
            : base(label, minValue, maxValue, minLimit, maxLimit)
        {
            CreateValueFields();
        }

        private void CreateValueFields()
        {
            AddToClassList(ussClassName);

            m_InputContainer = new VisualElement();
            m_InputContainer.style.flexDirection = FlexDirection.Row;

            m_MinValueField = new FloatField();
            m_MinValueField.value = minValue;
            m_MinValueField.style.marginLeft = 9f;
            m_MinValueField.formatString = "F2";
            m_MinValueField.RegisterValueChangedCallback(OnMinValueFieldChanged);
            m_MinValueField.style.width = 34f;

            m_MaxValueField = new FloatField();
            m_MaxValueField.value = maxValue;
            m_MaxValueField.style.marginRight = 2f;
            m_MaxValueField.formatString = "F2";
            m_MaxValueField.style.width = 34f;
            m_MaxValueField.RegisterValueChangedCallback(OnMaxValueFieldChanged);

            m_InputContainer.Add(m_MinValueField);
            m_InputContainer.Add(m_MaxValueField);

            Add(m_InputContainer);

            this.RegisterValueChangedCallback(OnSliderValueChanged);
        }

        private void OnMinValueFieldChanged(ChangeEvent<float> evt)
        {
            minValue = evt.newValue;

            if (!Mathf.Approximately(m_MaxValueField.value, maxValue)) m_MaxValueField.SetValueWithoutNotify(maxValue);
        }

        private void OnMaxValueFieldChanged(ChangeEvent<float> evt)
        {
            maxValue = evt.newValue;

            if (!Mathf.Approximately(m_MinValueField.value, minValue)) m_MinValueField.SetValueWithoutNotify(minValue);
        }

        private void OnSliderValueChanged(ChangeEvent<Vector2> evt)
        {
            if (!Mathf.Approximately(m_MinValueField.value, evt.newValue.x)) m_MinValueField.SetValueWithoutNotify(evt.newValue.x);

            if (!Mathf.Approximately(m_MaxValueField.value, evt.newValue.y)) m_MaxValueField.SetValueWithoutNotify(evt.newValue.y);
        }

        public override void SetValueWithoutNotify(Vector2 newValue)
        {
            base.SetValueWithoutNotify(newValue);

            m_MinValueField?.SetValueWithoutNotify(newValue.x);
            m_MaxValueField?.SetValueWithoutNotify(newValue.y);
        }
    }
}