using System;

using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Layout = UnityEngine.InputSystem.Layouts.InputControlLayout;
using PopulateEvent = UnityEngine.UIElements.ContextualMenuPopulateEvent;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;

namespace Niam.Editor.Tactile
{
    public class DeviceControlPickTool : VisualElement
    {
        private const string USS_PATH = EditorPaths.PACKAGES + 
                                                    "Tactile/Editor/StyleSheets/device-picktool";
        
        private const string NAME_ROOT_NAME = "Tactile-Device-PickTool";
        
        private static readonly IIcon ICON_ARROW = new IconDropdown(ColorTheme.Type.TextLight);

        // MEMBERS: -------------------------------------------------------------------------------

        private Label m_FieldDisplayLabel;
        
        private readonly string[] m_TypeLookup;
        private readonly SerializedProperty m_Property;
        private readonly SerializedProperty m_PropertyName;
        private readonly SerializedProperty m_PropertyDisplayName;

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public DeviceControlPickTool(SerializedProperty property)
            : this(property, Array.Empty<string>())
        { }

        public DeviceControlPickTool(SerializedProperty property, params string[] layouts)
        {
            this.m_Property = property;
            this.m_PropertyName = property.FindPropertyRelative("m_Name");
            this.m_PropertyDisplayName = property.FindPropertyRelative("m_DisplayName");
            this.m_TypeLookup = layouts;

            this.m_Property.serializedObject.Update();
            this.RefreshPickList();

            StyleSheet[] sheets = StyleSheetUtils.Load(USS_PATH);
            foreach (StyleSheet styleSheet in sheets) this.styleSheets.Add(styleSheet);
        }

        // // PROTECTED METHODS: ---------------------------------------------------------------------

        private void RefreshPickList()
        {
            this.Clear();

            var dropdownBtn = new Button();
            this.m_FieldDisplayLabel = new Label();

            string name = this.m_PropertyName.stringValue;
            if (string.IsNullOrEmpty(name))
            {
                this.m_FieldDisplayLabel.text = "(unset)";
            }
            else
            {
                string displayName = this.m_PropertyDisplayName.stringValue;
                var layout = InputSystem.LoadLayout("Tactile");
                if (layout == null || layout.FindControl(new (name.Split('/')[0])) == null) 
                {
                    displayName = $"{name} (missing)";
                }

                this.m_FieldDisplayLabel.text = displayName;
            }

            dropdownBtn.AddManipulator(new MouseDropdownManipulator(context =>
            {
                var layout = InputSystem.LoadLayout("Tactile");
                if (layout == null) 
                {
                    context.menu.AppendAction(
                        "Tactile Device not found.", null,
                        DropdownMenuAction.Status.Disabled
                    );
                    return;
                }

                this.RecursiveFindLayout(context, layout);
            }));

            dropdownBtn.Add(this.m_FieldDisplayLabel);
            dropdownBtn.Add(new Image { image = ICON_ARROW.Texture });   

            var nameContainer = new VisualElement { name = NAME_ROOT_NAME };
            nameContainer.Add(new Label(" "));
            nameContainer.Add(dropdownBtn);

            AlignLabel.On(nameContainer);
            this.Add(nameContainer);
        }

        private void RecursiveFindLayout(
            PopulateEvent context, Layout layout, string namePath = "", string displayPath = "")
        {
            foreach (var control in layout.controls)
            {
                if (string.IsNullOrEmpty(control.layout)) continue;

                var newNamePath = string.IsNullOrEmpty(namePath) 
                    ? control.name : $"{namePath}/{control.name}";

                var newDisplayPath = string.IsNullOrEmpty(displayPath) 
                    ? control.displayName : $"{displayPath}/{control.displayName}";

                var childLayout = InputSystem.LoadLayout(control.layout);
                if (Array.IndexOf(this.m_TypeLookup, control.layout) != -1)
                {
                    this.AppendMenuAction(newNamePath, newDisplayPath, context);
                }

                this.RecursiveFindLayout(context, childLayout, newNamePath, newDisplayPath);
            }
        }

        private void AppendMenuAction(
            string namePath, string displayPath, PopulateEvent context)
        {
            context.menu.AppendAction(
                namePath,

                menuAction =>
                {
                    string name = menuAction.name;
                    string display = displayPath.Replace("/", " / ");

                    this.m_PropertyName.stringValue = name;
                    this.m_PropertyDisplayName.stringValue = display;
                    this.m_FieldDisplayLabel.text = display;

                    SerializationUtils.ApplyUnregisteredSerialization(
                        this.m_Property.serializedObject
                    );
                },

                menuAction =>
                {
                    return menuAction.name != this.m_PropertyName.stringValue
                        ? DropdownMenuAction.Status.Normal 
                        : DropdownMenuAction.Status.Checked;
                }
            );
        }

    }
}