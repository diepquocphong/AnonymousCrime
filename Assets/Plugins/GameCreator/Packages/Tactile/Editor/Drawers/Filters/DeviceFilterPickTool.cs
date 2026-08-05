using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Layout = UnityEngine.InputSystem.Layouts.InputControlLayout;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;

namespace Niam.Editor.Tactile
{
    public class DeviceFilterPickTool : VisualElement
    {
        private const string USS_PATH = EditorPaths.PACKAGES + 
                                                    "Tactile/Editor/StyleSheets/device-picktool";
        
        private const string NAME_ROOT_NAME = "Tactile-Device-PickTool";
        
        private static readonly IIcon ICON_ARROW = new IconDropdown(ColorTheme.Type.TextLight);

        // MEMBERS: -------------------------------------------------------------------------------

        private Button m_FieldDropdownButton;
        private Label m_FieldDisplayLabel;
        
        private readonly SerializedProperty m_Property;
        private readonly SerializedProperty m_PropertyList;

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public DeviceFilterPickTool(SerializedProperty property)
        {
            this.m_Property = property;
            this.m_PropertyList = property.FindPropertyRelative("m_DeviceList");

            this.m_Property.serializedObject.Update();
            this.RefreshPickList();

            StyleSheet[] sheets = StyleSheetUtils.Load(USS_PATH);
            foreach (StyleSheet styleSheet in sheets) this.styleSheets.Add(styleSheet);
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void RefreshPickList()
        {
            this.Clear();

            this.m_FieldDropdownButton = new Button();
            this.m_FieldDisplayLabel = new Label();
            this.UpdateDisplay();

            m_FieldDropdownButton.AddManipulator(new MouseDropdownManipulator(context =>
            {
                foreach (string layoutName in InputSystem.ListLayouts())
                {
                    Layout layout = InputSystem.LoadLayout(layoutName);
                    if (layout.isControlLayout || !layout.isGenericTypeOfDevice) continue;
                    
                    context.menu.AppendAction(
                        layout.name,

                        menuAction =>
                        {
                            int index = this.GetDeviceElementIndex(menuAction.name);
                            if (index == -1)
                            {
                                int arraySize = this.m_PropertyList.arraySize;
                                this.m_PropertyList.InsertArrayElementAtIndex(arraySize);
                                this.m_PropertyList.GetArrayElementAtIndex(arraySize)
                                    .stringValue = menuAction.name;
                            }
                            else
                            {
                                this.m_PropertyList.DeleteArrayElementAtIndex(index);
                            }

                            SerializationUtils.ApplyUnregisteredSerialization(
                                this.m_Property.serializedObject
                            );

                            this.UpdateDisplay();
                        },

                        menuAction =>
                        {
                            return this.GetDeviceElementIndex(menuAction.name) == -1
                                ? DropdownMenuAction.Status.Normal
                                : DropdownMenuAction.Status.Checked;
                        }
                    );
                }
            }));

            m_FieldDropdownButton.RegisterCallback<GeometryChangedEvent>(callback =>
            {
                this.UpdateDisplay();
            });

            m_FieldDropdownButton.Add(this.m_FieldDisplayLabel);
            m_FieldDropdownButton.Add(new Image { image = ICON_ARROW.Texture });   

            var root = new VisualElement { name = NAME_ROOT_NAME };
            root.Add(new Label("Device Filter"));
            root.Add(m_FieldDropdownButton);

            AlignLabel.On(root);
            this.Add(root);
        }

        private int GetDeviceElementIndex(string device)
        {
            int arraySize = this.m_PropertyList.arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                if (this.m_PropertyList.GetArrayElementAtIndex(i).stringValue == device)
                    return i;
            }

            return -1;
        }

        private void UpdateDisplay()
        {
            int arraySize = this.m_PropertyList.arraySize;
            if (arraySize == 0)
            {
                this.m_FieldDisplayLabel.text = "Any Device";
                return;
            }

            this.m_FieldDisplayLabel.text = this.m_PropertyList.GetArrayElementAtIndex(0).stringValue;
            for (int i = 1; i < arraySize; i++)
            {
                string device = this.m_PropertyList.GetArrayElementAtIndex(i).stringValue;
                string newText = this.m_FieldDisplayLabel.text + $", {device}";

                if (this.m_FieldDisplayLabel.localBound.width < newText.Length * 6.5f)
                {
                    this.m_FieldDisplayLabel.text = "Mixed...";
                    return;
                }

                this.m_FieldDisplayLabel.text = newText;
            }
        }

    }
}