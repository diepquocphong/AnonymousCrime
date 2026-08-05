using System.Reflection;

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    internal class SwipeDirectionTool : Listbox
    {
        protected override IIcon IconItem => new IconArrowRight(ColorTheme.Type.TextNormal);
        protected override IIcon IconFooter => new IconTranslation(ColorTheme.Type.TextLight);
        protected override string FooterText => "Add Direction...";

        private const BindingFlags BINDINGS_PID = (BindingFlags) 38;
        private FieldInfo m_ValuesInfo = typeof(SwipeDirections).GetField("m_Values", BINDINGS_PID);
        private FieldInfo m_SimulateInfo = typeof(SwipeDirection).GetField("m_InputSimulate", BINDINGS_PID);
        private FieldInfo m_DirectionsInfo = typeof(ControlTypeSwipePad).GetField("m_Directions", BINDINGS_PID);

        private TactileControl m_Control;

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public SwipeDirectionTool(SerializedProperty propertyList) : base(propertyList)
        { 
            this.m_Control = this.SerializedObject.targetObject as TactileControl;

            // var scrollView = this.Q<ScrollView>();
            // scrollView.style.maxHeight = 901f;
            // scrollView.mouseWheelScrollSize = 6.66667f;
            // scrollView.verticalPageSize = 20f;
        }

        // IMPLEMENTATIONS: -----------------------------------------------------------------------

        public override void AddItem()
        {
            this.SerializedObject.Update();

            int insertIndex = this.PropertyList.arraySize;
            this.PropertyList.InsertArrayElementAtIndex(insertIndex);
            var property = this.PropertyList.GetArrayElementAtIndex(insertIndex);

            property.FindPropertyRelative("m_Id")
                    .FindPropertyRelative("m_String")
                    .stringValue = $"swipe-{insertIndex}";

            property.FindPropertyRelative("m_Arc")
                    .floatValue = insertIndex > 0 
                        ? this.PropertyList 
                            .GetArrayElementAtIndex(insertIndex - 1)
                            .FindPropertyRelative("m_Arc")
                            .floatValue
                        : 90f;

            this.ApplyModifiedProperties();
            
            if (this.m_Control != null)
            {
                var swipePad = this.m_Control.ControlType as ControlTypeSwipePad;
                var swipeDirections = this.m_DirectionsInfo?.GetValue(swipePad) as SwipeDirections;
                var swipeValues = this.m_ValuesInfo?.GetValue(swipeDirections) as SwipeDirection[];
                this.m_SimulateInfo?.SetValue(swipeValues[insertIndex], new InputSimulateButton());
            }

            this.SerializedObject.ApplyModifiedPropertiesWithoutUndo();

            this.SetItemState(insertIndex, true);
            this.RefreshCollapseExpandIcon();
            this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        protected override VisualElement MakeItemContent()
        {
            var container = new VisualElement();

            var fieldSimulate = new PropertyField() { name = "swipe-direction__simulate" };
            var fieldFingers = new PropertyField() { name = "swipe-direction__fingers" };
            var fieldIsEnabled = new PropertyField() { name = "swipe-direction__is-active" };
            var fieldId = new PropertyField() { name = "swipe-direction__id" };
            var fieldAngle = new PropertyField() { name = "swipe-direction__angle" };
            var fieldArc = new PropertyField() { name = "swipe-direction__arc" };
            
            fieldSimulate.style.marginBottom = 1f;
            fieldSimulate.style.paddingLeft = 1f;

            fieldIsEnabled.RegisterCallback<SerializedPropertyChangeEvent>(this.OnChangeValue);
            fieldId.RegisterCallback<SerializedPropertyChangeEvent>(this.OnChangeValue);
            fieldAngle.RegisterCallback<SerializedPropertyChangeEvent>(this.OnRotateIcon);

            container.Add(fieldSimulate);
            container.Add(fieldFingers);
            container.Add(new SpaceSmall());
            container.Add(fieldIsEnabled);
            container.Add(fieldId);
            container.Add(fieldAngle);
            container.Add(fieldArc);

            return container;
        }

        protected override void BindContent(SerializedProperty property, VisualElement element, int index)
        {
            if (element.Q("swipe-direction__simulate") is PropertyField fieldSimulate)
            {
                fieldSimulate.BindProperty(property.FindPropertyRelative("m_InputSimulate"));
            }

            if (element.Q("swipe-direction__fingers") is PropertyField fieldFingers)
            {
                var prop = property.FindPropertyRelative("m_Fingers");

                fieldFingers.label = "Required Fingers";
                fieldFingers.BindProperty(prop);
            }

            if (element.Q("swipe-direction__is-active") is PropertyField fieldIsActive)
            {
                var prop = property.FindPropertyRelative("m_IsActive");

                fieldIsActive.label = prop.displayName;
                fieldIsActive.BindProperty(prop);
            }

            if (element.Q("swipe-direction__id") is PropertyField fieldId)
            {
                var prop = property.FindPropertyRelative("m_Id");
                var value = prop.FindPropertyRelative("m_String");

                fieldId.label = "Swipe ID";
                fieldId.BindProperty(value);
            }

            if (element.Q("swipe-direction__angle") is PropertyField fieldAngle)
            {
                var prop = property.FindPropertyRelative("m_Angle");
                fieldAngle.BindProperty(prop);
                this.RotateIcon(prop.floatValue, element);
            }

            if (element.Q("swipe-direction__arc") is PropertyField fieldArc)
            {
                fieldArc.BindProperty(property.FindPropertyRelative("m_Arc"));
            }
        }

        protected override void BindTitle(SerializedProperty property, VisualElement element, int index)
        {
            var label = element.Q<Label>("listbox-item-head-title");
            var icon = element.Q<Image>("listbox-item-head-image");

            SerializedProperty propId = property.FindPropertyRelative("m_Id");
            SerializedProperty value = propId.FindPropertyRelative("m_String");
            SerializedProperty propIsEnabled = property.FindPropertyRelative("m_IsActive");

            label.text = !string.IsNullOrEmpty(value.stringValue) 
                ? $"{TextUtils.Humanize(value.stringValue)}" 
                : "<unspecified>";

            if (!propIsEnabled.boolValue)
            {
                label.text += " (Disabled)";
                label.style.opacity = 0.3f;
                icon.style.opacity = 0.3f;
            }
            else
            {
                label.style.opacity = 1f;
                icon.style.opacity = 1f;
            }
        }

        protected override void UnbindTitle(VisualElement element, int index)
        { }

        protected override void UnbindContent(VisualElement element, int index)
        { 
            if (element.Q("swipe-direction__simulate") is PropertyField field)
            {
                field.Unbind();
            }

            if (element.Q("swipe-direction__fingers") is PropertyField field2)
            {
                field2.Unbind();
            }

            if (element.Q("swipe-direction__id") is PropertyField field3)
            {
                field3.Unbind();
            }

            if (element.Q("swipe-direction__angle") is PropertyField field4)
            {
                field4.Unbind();
            }

            if (element.Q("swipe-direction__arc") is PropertyField field5)
            {
                field5.Unbind();
            }
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void OnChangeValue(SerializedPropertyChangeEvent evt)
        {
            if (evt.currentTarget is not PropertyField field) return;

            var element = field.parent.parent;
            int index = int.Parse(element.name.Replace(NameItemIndexer, ""));
            if (index < 0) return;

            var property = this.PropertyList.GetArrayElementAtIndex(index);
            this.BindTitle(property, element, index);
        }

        private void OnRotateIcon(SerializedPropertyChangeEvent evt)
        {
            if (evt.currentTarget is not PropertyField field) return;
            this.RotateIcon(evt.changedProperty.floatValue, field.parent.parent);
        }

        private void RotateIcon(float angle, VisualElement element)
        {
            if (element.Q("listbox-item-head-image") is not Image image) return;
            image.style.rotate = new Rotate(angle - 90f);
        }

    }
}