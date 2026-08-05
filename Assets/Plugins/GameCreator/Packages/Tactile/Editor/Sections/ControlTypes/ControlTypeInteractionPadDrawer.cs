using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeInteractionPad))]
    public class ControlTypeInteractionPadDrawer : TControlTypeDrawer
    {
        private const string SIMULATE = "Input Simulate"; 

        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            this.CreateTapDrawer(body, property);
            this.CreateSlowTapDrawer(body, property);
            this.CreateMultiTapDrawer(body, property);
            this.CreateHoldTapDrawer(body, property);
        }

        private void CreateTapDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_TapInputSimulate");

            var tapBox = new Foldbox("Tap", "tactile:interaction-pad-tap");
            var fieldInputSimulate = new PropertyField(inputSimulate, SIMULATE);

            body.Add(tapBox);
            tapBox.Add(fieldInputSimulate); 
        }

        private void CreateSlowTapDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_SlowTapInputSimulate");

            var slowTapBox = new Foldbox("Slow Tap", "tactile:interaction-pad-slow-tap");
            var fieldInputSimulate = new PropertyField(inputSimulate, SIMULATE);

            body.Add(slowTapBox);
            slowTapBox.Add(fieldInputSimulate); 
        }

        private void CreateMultiTapDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulates = property.FindPropertyRelative("m_MultiTapInputSimulates");
            if (inputSimulates == null) return;

            var multiTapBox = new Foldbox("Multi Tap", "tactile:interaction-pad-multi-tap");

            var fieldInputSimulates = new ListView
            {
                showBorder = false,
                reorderable = false,
                showFoldoutHeader = false,
                showAddRemoveFooter = true,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                selectionType = SelectionType.None,
                bindingPath = inputSimulates.propertyPath,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeNoneElement = () => null,
                makeItem = () =>
                {
                    var container = new VisualElement();

                    var fieldTapCount = new PropertyField
                    {
                        name = "TapCountField",
                        label = "Tap Count"
                    };

                    var continuous = new PropertyField
                    {
                        name = "ContinuousField",
                        label = "Continuous"
                    };

                    var fieldInputSimulate = new PropertyField
                    {
                        name = "InputSimulateField",
                        label = "Input Simulate"
                    };

                    container.Add(fieldTapCount);
                    container.Add(continuous);
                    container.Add(fieldInputSimulate);

                    return container;
                },
                bindItem = (element, index) =>
                {
                    property.serializedObject.Update();

                    SerializedProperty item = inputSimulates.GetArrayElementAtIndex(index);
                    SerializedProperty tapCount = item.FindPropertyRelative("m_TapCount");
                    SerializedProperty continuous = item.FindPropertyRelative("m_Continuous");
                    SerializedProperty inputSimulate = item.FindPropertyRelative("m_InputSimulate");

                    if (element.Q("TapCountField") is PropertyField field1)
                    {
                        field1.BindProperty(tapCount);
                    }

                    if (element.Q("ContinuousField") is PropertyField field2)
                    {
                        field2.BindProperty(continuous);
                    }

                    if (element.Q("InputSimulateField") is PropertyField field3)
                    {
                        field3.BindProperty(inputSimulate);
                    }
                }
            };

            fieldInputSimulates.Bind(property.serializedObject);

            fieldInputSimulates.selectionType = SelectionType.None;
            fieldInputSimulates.allowRemove = inputSimulates.arraySize > 1;
            fieldInputSimulates.itemsAdded += (items) =>
            {
                fieldInputSimulates.allowRemove = true;

                property.serializedObject.Update();
                int arraySize = inputSimulates.arraySize - 1;

                SerializedProperty item1 = inputSimulates.GetArrayElementAtIndex(arraySize - 1);
                SerializedProperty tapCount1 = item1.FindPropertyRelative("m_TapCount");

                SerializedProperty item2 = inputSimulates.GetArrayElementAtIndex(arraySize);
                SerializedProperty tapCount = item2.FindPropertyRelative("m_TapCount");

                tapCount.intValue = tapCount1.intValue + 1;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            };

            fieldInputSimulates.itemsRemoved += (items) =>
            {
                property.serializedObject.Update();
                fieldInputSimulates.allowRemove = inputSimulates.arraySize > 2;
            };

            var scrollView = fieldInputSimulates.Q<ScrollView>(null, ScrollView.ussClassName);
            scrollView.style.backgroundColor = default;
            scrollView.contentViewport.style.overflow = Overflow.Visible;
            scrollView.contentContainer.style.overflow = Overflow.Visible;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            body.Add(multiTapBox);
            multiTapBox.Add(fieldInputSimulates); 
        }

        private void CreateHoldTapDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty inputSimulate = property.FindPropertyRelative("m_HoldInputSimulate");

            var holdBox = new Foldbox("Hold", "tactile:interaction-pad-hold");
            var fieldInputSimulate = new PropertyField(inputSimulate, SIMULATE);

            body.Add(holdBox);
            holdBox.Add(fieldInputSimulate); 
        }

    }
}