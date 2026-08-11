// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using PampelGames.Shared.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PampelGames.Shared.Utility
{
    public static class PGEditorAutoSetup
    {
        /// <summary>
        ///     Method to dynamically create and bind UI elements for a specified class type.
        /// </summary>
        public static void CreateAndBindClassElements<T>(SerializedObject serializedObject, VisualElement parentElement)
        {
            CreateAndBindClassElementsInternal<T>(serializedObject, null, parentElement);
        }

        /// <summary>
        ///     Method to dynamically create and bind UI elements for a specified class type.
        /// </summary>
        public static void CreateAndBindClassElements<T>(SerializedProperty property, VisualElement parentElement)
        {
            CreateAndBindClassElementsInternal<T>(null, property, parentElement);
        }

        /********************************************************************************************************************************/

        private static void CreateAndBindClassElementsInternal<T>(SerializedObject serializedObject, SerializedProperty property,
            VisualElement parentElement)
        {
            var isSerializedObject = serializedObject != null;

            var insertAtClasses = new List<InsertAtClass>();

            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                /********************************************************************************************************************************/
                // Custom Attributes

                var headerAttr = field.GetCustomAttribute<PGHeaderAttribute>();
                var headerUsed = false;
                if (headerAttr != null)
                {
                    var label = new Label(CreateLabelString(headerAttr.Header));
                    label.name = field.Name;
                    if (headerAttr.HeaderType == HeaderType.Small) label.PGHeaderSmall();
                    else label.PGHeaderBig();
                    parentElement.Add(label);
                    headerUsed = true;
                }

                if (field.GetCustomAttribute<PGHideAttribute>() != null) continue;

                var fieldLabel = field.Name;
                var nameAttr = field.GetCustomAttribute<PGLabelAttribute>();
                if (nameAttr != null) fieldLabel = nameAttr.Label;
                else fieldLabel = CreateLabelString(fieldLabel);

                var isTagField = field.GetCustomAttribute<PGTagFieldAttribute>() != null;
                if (isTagField && field.FieldType != typeof(string))
                {
                    Debug.LogWarning("The PGTagField attribute can only be used on strings!\n" +
                                     "Field: " + field.Name);
                    continue;
                }

                var isLayerField = field.GetCustomAttribute<PGLayerFieldAttribute>() != null;
                if (isLayerField && field.FieldType != typeof(int))
                {
                    Debug.LogWarning("The PGLayerField attribute can only be used on ints!\n" +
                                     "Field: " + field.Name);
                    continue;
                }

                var vector2ComponentLabel = field.GetCustomAttribute<PGVectorComponentLabelAttribute>();
                var isVector2ComponentLabel = vector2ComponentLabel != null;

                var pgSlider = field.GetCustomAttribute<PGSliderAttribute>();
                var isSlider = pgSlider != null;
                if (isSlider && field.FieldType != typeof(float) && field.FieldType != typeof(int))
                {
                    Debug.LogWarning("The PGSlider attribute can only be used on floats or integers!\n" +
                                     "Field: " + field.Name);
                    continue;
                }

                var pgMinMaxSlider = field.GetCustomAttribute<PGMinMaxSliderAttribute>();
                var isMinMaxSlider = pgMinMaxSlider != null;
                if (isMinMaxSlider && field.FieldType != typeof(Vector2))
                {
                    Debug.LogWarning("The PGSlider attribute can only be used on Vector2!\n" +
                                     "Field: " + field.Name);
                    continue;
                }

                var pgClamp = field.GetCustomAttribute<PGClampAttribute>();
                var isClamped = pgClamp != null;

                var pgHelpBox = field.GetCustomAttribute<PGHelpBoxAttribute>();
                
                var pgInsertAt = field.GetCustomAttribute<PGInsertAtAttribute>();
                var pgMargin = field.GetCustomAttribute<PGMarginAttribute>();
                var pgBottomLine = field.GetCustomAttribute<PGBottomLineAttribute>();
                var pgTopLine = field.GetCustomAttribute<PGTopLineAttribute>();

                var pgButtomMethod = field.GetCustomAttribute<PGButtonMethodAttribute>();
                var isButtomMethod = pgButtomMethod != null;

                var pgGroup = field.GetCustomAttribute<PGGroupAttribute>();
                var isGroup = pgGroup != null;

                /********************************************************************************************************************************/
                // Binding Fields

                if (field.FieldType == typeof(LayerMask))
                {
                    var layerField = new LayerMaskField(fieldLabel);
                    layerField.name = field.Name;
                    BindElement(layerField, field.Name);
                    AddToParent(layerField);
                }
                else if (field.FieldType == typeof(string) && isTagField)
                {
                    var tagField = new TagField(fieldLabel);
                    tagField.name = field.Name;
                    BindElement(tagField, field.Name);
                    AddToParent(tagField);
                }
                else if (field.FieldType == typeof(int) && isLayerField)
                {
                    var layerField = new LayerField(fieldLabel);
                    layerField.name = field.Name;
                    BindElement(layerField, field.Name);
                    AddToParent(layerField);
                }
                else if (field.FieldType == typeof(string) && isButtomMethod)
                {
                    var button = new Button();
                    button.text = fieldLabel;
                    button.style.height = pgButtomMethod.Height;
                    var methodName = pgButtomMethod.MethodName;
                    button.clicked += () =>
                    {
                        var target = isSerializedObject ? serializedObject.targetObject : property.serializedObject.targetObject;
                        var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        method?.Invoke(target, null);
                    };
                    AddToParent(button);
                }
                else if (field.FieldType == typeof(string) && pgHelpBox != null)
                {
                    var helpBox = new HelpBox(pgHelpBox.HelpBoxText, pgHelpBox.MessageType);
                    helpBox.name = field.Name;
                    AddToParent(helpBox);
                }
                else if (field.FieldType == typeof(string))
                {
                    var textField = new TextField(fieldLabel);
                    textField.name = field.Name;
                    BindElement(textField, field.Name);
                    AddToParent(textField);
                }
                else if (field.FieldType.IsSubclassOf(typeof(Object)))
                {
                    var objectField = new ObjectField(fieldLabel);
                    objectField.name = field.Name;
                    objectField.objectType = field.FieldType;
                    BindElement(objectField, field.Name);
                    AddToParent(objectField);
                }
                else if (field.FieldType == typeof(bool))
                {
                    var pgToolbarToggle = field.GetCustomAttribute<PGToolbarToggleAttribute>();
                    if (pgToolbarToggle != null)
                    {
                        var toolbarButton = new ToolbarToggle();
                        toolbarButton.name = field.Name;
                        toolbarButton.text = fieldLabel;
                        toolbarButton.PGBorderWidth(1f);
                        BindElement(toolbarButton, field.Name);
                        AddToParent(toolbarButton);
                    }
                    else
                    {
                        var toggle = new Toggle(fieldLabel);
                        toggle.PGToggleStyleDefault();
                        toggle.name = field.Name;
                        BindElement(toggle, field.Name);
                        AddToParent(toggle);
                    }
                }
                else if (field.FieldType == typeof(int) && isSlider)
                {
                    var slider = new SliderInt(fieldLabel);
                    slider.name = field.Name;
                    slider.lowValue = Mathf.RoundToInt(pgSlider.LowValue);
                    slider.highValue = Mathf.RoundToInt(pgSlider.HighValue);
                    slider.showInputField = true;
                    BindElement(slider, field.Name);
                    AddToParent(slider);
                }
                else if (field.FieldType == typeof(int))
                {
                    var integerField = new IntegerField(fieldLabel);
                    integerField.name = field.Name;
                    if (isClamped) integerField.PGClampValue(Mathf.RoundToInt(pgClamp.MinValue), Mathf.RoundToInt(pgClamp.MaxValue));
                    BindElement(integerField, field.Name);
                    AddToParent(integerField);
                }
                else if (field.FieldType == typeof(float) && isSlider)
                {
                    var slider = new Slider(fieldLabel);
                    slider.name = field.Name;
                    slider.lowValue = pgSlider.LowValue;
                    slider.highValue = pgSlider.HighValue;
                    slider.showInputField = true;
                    BindElement(slider, field.Name);
                    AddToParent(slider);
                }
                else if (field.FieldType == typeof(float))
                {
                    var floatField = new FloatField(fieldLabel);
                    floatField.name = field.Name;
                    if (isClamped) floatField.PGClampValue(pgClamp.MinValue, pgClamp.MaxValue);
                    BindElement(floatField, field.Name);
                    AddToParent(floatField);
                }
                else if (field.FieldType == typeof(Vector2) && isMinMaxSlider)
                {
                    var minMaxSlider = new MinMaxSliderInput(fieldLabel);
                    minMaxSlider.name = field.Name;
                    minMaxSlider.lowLimit = pgMinMaxSlider.LowLimit;
                    minMaxSlider.highLimit = pgMinMaxSlider.HighLimit;
                    BindElement(minMaxSlider, field.Name);
                    AddToParent(minMaxSlider);
                }
                else if (field.FieldType == typeof(Vector2))
                {
                    var vector2Field = new Vector2Field(fieldLabel);
                    vector2Field.name = field.Name;
                    if (isClamped) vector2Field.PGClampValue(pgClamp.MinValue, pgClamp.MaxValue);
                    if (isVector2ComponentLabel)
                        vector2Field.PGVector2ComponentLabel(vector2ComponentLabel.LabelX, vector2ComponentLabel.LabelY,
                            vector2ComponentLabel.LabelFlexGrow, vector2ComponentLabel.TextAnchor);
                    BindElement(vector2Field, field.Name);
                    AddToParent(vector2Field);
                }
                else if (field.FieldType == typeof(Vector3))
                {
                    var vector3Field = new Vector3Field(fieldLabel);
                    vector3Field.name = field.Name;
                    if (isClamped) vector3Field.PGClampValue(pgClamp.MinValue, pgClamp.MaxValue);
                    BindElement(vector3Field, field.Name);
                    AddToParent(vector3Field);
                }
                else if (field.FieldType == typeof(Vector2Int))
                {
                    var vector2IntField = new Vector2IntField(fieldLabel);
                    vector2IntField.name = field.Name;
                    if (isClamped) vector2IntField.PGClampValue(Mathf.RoundToInt(pgClamp.MinValue), Mathf.RoundToInt(pgClamp.MaxValue));
                    if (isVector2ComponentLabel)
                        vector2IntField.PGVector2ComponentLabel(vector2ComponentLabel.LabelX, vector2ComponentLabel.LabelY,
                            vector2ComponentLabel.LabelFlexGrow, vector2ComponentLabel.TextAnchor);
                    BindElement(vector2IntField, field.Name);
                    AddToParent(vector2IntField);
                }
                else if (field.FieldType == typeof(Vector3Int))
                {
                    var vector3IntField = new Vector3IntField(fieldLabel);
                    vector3IntField.name = field.Name;
                    if (isClamped) vector3IntField.PGClampValue(Mathf.RoundToInt(pgClamp.MinValue), Mathf.RoundToInt(pgClamp.MaxValue));
                    BindElement(vector3IntField, field.Name);
                    AddToParent(vector3IntField);
                }
                else if (field.FieldType.IsEnum)
                {
                    var enumField = new EnumField(fieldLabel);
                    enumField.name = field.Name;
                    BindElement(enumField, field.Name);
                    if (field.FieldType == typeof(PGTweenEaseShared.Ease))
                    {
                        var EaseParent = new VisualElement();
                        EaseParent.name = nameof(EaseParent);
                        EaseParent.style.flexDirection = FlexDirection.Row;
                        Button easeTypesButton = new();
                        easeTypesButton.name = nameof(easeTypesButton);
                        easeTypesButton.text = "?";
                        easeTypesButton.tooltip = "Sheet for Easing Functions.";
                        easeTypesButton.clicked += PGTweenDocumentation.OpenEasingTypes;
                        enumField.style.flexGrow = 1f;
                        easeTypesButton.style.flexGrow = 0f;
                        EaseParent.Add(enumField);
                        EaseParent.Add(easeTypesButton);
                        AddToParent(EaseParent);
                    }
                    else
                    {
                        AddToParent(enumField);
                    }
                }
                else if (field.FieldType == typeof(Color))
                {
                    var colorField = new ColorField(fieldLabel);
                    colorField.name = field.Name;
                    BindElement(colorField, field.Name);
                    AddToParent(colorField);
                }
                else if (field.FieldType == typeof(AnimationCurve))
                {
                    var curveField = new CurveField(fieldLabel);
                    curveField.name = field.Name;
                    BindElement(curveField, field.Name);
                    AddToParent(curveField);
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var listProperty = isSerializedObject ? serializedObject.FindProperty(field.Name) : property.FindPropertyRelative(field.Name);
                    var listView = new ListView();
                    listView.name = field.Name;
                    listView.BindProperty(listProperty);
                    listView.PGObjectListViewStyle(headerUsed ? null : fieldLabel);
                    listView.reorderMode = ListViewReorderMode.Animated;
                    listView.reorderable = true;
                    AddToParent(listView);

                    var pgListViewSlider = field.GetCustomAttribute<PGListViewLODAttribute>();
                    if (pgListViewSlider != null) CreateLODList(listView, listProperty);
                }
                else if (field.FieldType.IsClass)
                {
                    var propertyField = new PropertyField();
                    propertyField.name = field.Name;
                    BindElement(propertyField, field.Name);
                    AddToParent(propertyField);
                }

                continue;

                void AddToParent(VisualElement element)
                {
                    var _parentElement = parentElement;

                    if (isGroup)
                    {
                        _parentElement = parentElement.Q<VisualElement>(pgGroup.GroupName);
                        if (_parentElement == null)
                        {
                            _parentElement = new VisualElement();
                            _parentElement.name = pgGroup.GroupName;
                            _parentElement.style.flexDirection = pgGroup.FlexDirection;
                            parentElement.Add(_parentElement);
                        }
                    }

                    if (pgInsertAt == null) _parentElement.Add(element);
                    else insertAtClasses.Add(new InsertAtClass(element, pgInsertAt.Index));

                    if (pgMargin != null)
                    {
                        if (pgMargin.MarginLeft > -9999f) element.style.marginLeft = pgMargin.MarginLeft;
                        if (pgMargin.MarginRight > -9999f) element.style.marginRight = pgMargin.MarginRight;
                        if (pgMargin.MarginTop > -9999f) element.style.marginTop = pgMargin.MarginTop;
                        if (pgMargin.MarginBottom > -9999f) element.style.marginBottom = pgMargin.MarginBottom;
                    }

                    if (pgBottomLine != null) element.PGDrawBottomLine();
                    if (pgTopLine != null) element.PGDrawTopLine();
                }
            }

            insertAtClasses = insertAtClasses.OrderBy(i => i.index).ToList();

            for (var i = 0; i < insertAtClasses.Count; i++)
            {
                var insertClass = insertAtClasses[i];
                parentElement.Insert(insertClass.index, insertClass.element);
            }


            foreach (var field in fields) // PGDisplaySelection
            {
                var pgDisplaySelection = field.GetCustomAttribute<PGDisplaySelectionAttribute>();
                if (pgDisplaySelection == null) continue;

                var fieldElement = parentElement.Q(field.Name);

                // Booleans
                var _toggles = new Toggle[pgDisplaySelection.ElementNames.Length];
                for (var i = 0; i < pgDisplaySelection.ElementNames.Length; i++)
                    _toggles[i] = parentElement.Q<Toggle>(pgDisplaySelection.ElementNames[i]);

                for (var i = 0; i < _toggles.Length; i++)
                {
                    var _toggle = _toggles[i];

                    _toggle?.RegisterValueChangedCallback(evt =>
                    {
                        var _display = pgDisplaySelection.DisplayBool;
                        var _any = pgDisplaySelection.Any;
                        if (!_display) _any = !_any;

                        if (_any)
                        {
                            var anyTrue = _toggles.Any(t => t.value);
                            fieldElement.style.display = (_display && anyTrue) || (!_display && !anyTrue)
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        }
                        else
                        {
                            var allTrue = _toggles.All(t => t.value);
                            fieldElement.style.display = (_display && allTrue) || (!_display && !allTrue)
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        }
                    });
                }

                // Enums
                var _enums = new EnumField[pgDisplaySelection.ElementNames.Length];
                for (var i = 0; i < pgDisplaySelection.ElementNames.Length; i++)
                    _enums[i] = parentElement.Q<EnumField>(pgDisplaySelection.ElementNames[i]);

                for (var i = 0; i < _enums.Length; i++)
                {
                    var _enum = _enums[i];

                    _enum?.RegisterValueChangedCallback(evt =>
                    {
                        var _displays = pgDisplaySelection.DisplayEnums;
                        var _any = pgDisplaySelection.Any;

                        if (_any)
                        {
                            var anyMatch = _enums.Any(e => e != null && _displays.Contains(Convert.ToInt32(e.value)));
                            fieldElement.style.display = anyMatch ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                        else
                        {
                            var allMatch = _enums.All(e => e == null || _displays.Contains(Convert.ToInt32(e.value)));
                            fieldElement.style.display = allMatch ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                    });
                }
            }


            return;

            /********************************************************************************************************************************/

            void BindElement(IBindable element, string fieldName)
            {
                if (isSerializedObject) element.PGSetupBindProperty(serializedObject, fieldName);
                else element.PGSetupBindPropertyRelative(property, fieldName);
            }
        }

        private static string CreateLabelString(string labelName)
        {
            if (string.IsNullOrEmpty(labelName)) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(char.ToUpper(labelName[0]));

            for (var i = 1; i < labelName.Length; i++)
            {
                if (char.IsUpper(labelName[i])) sb.Append(' ');
                sb.Append(labelName[i]);
            }

            return sb.ToString();
        }

        private class InsertAtClass
        {
            public readonly VisualElement element;
            public readonly int index;

            public InsertAtClass(VisualElement element, int index)
            {
                this.element = element;
                this.index = index;
            }
        }

        /********************************************************************************************************************************/

        private static void CreateLODList(ListView listView, SerializedProperty lodListProperty)
        {
            listView.headerTitle = "LOD";
            listView.showBoundCollectionSize = false;
            listView.reorderable = false;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.showFoldoutHeader = true;

            listView.makeItem = () =>
            {
                var item = new VisualElement();
                var slider = new Slider();
                item.Add(slider);
                return item;
            };

            listView.bindItem = (element, index) =>
            {
                lodListProperty.serializedObject.Update();
                var slider = element.Q<Slider>();
                slider.tooltip = "Transition (inverted % of screen) to the next item.\n" + "For the last item, this is the transition to cull.";
                slider.showInputField = true;
                slider.lowValue = 0f;
                slider.highValue = 0.999f;
                slider.label = "LOD" + index;
                slider.style.marginRight = 3f;
                slider.BindProperty(lodListProperty.GetArrayElementAtIndex(index));
            };
        }
    }
}
#endif