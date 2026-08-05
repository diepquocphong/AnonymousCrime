using System.Reflection;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine.UIElements;

using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;

namespace Niam.Editor.Tactile 
{
    using Niam.Runtime.Tactile;

    internal class DeviceBuilderTool : TPolymorphicListTool
    {
        private const string NAME_BUTTON_ADD = "Tactile-Device-Foot-Add";
        private const string NAME_BUTTON_APPLY = "Tactile-Device-Foot-Apply";

        private static readonly IIcon ICON_APPLY = new IconDiskSolid(ColorTheme.Type.TextLight);

        // MEMBERS: -------------------------------------------------------------------------------

        private Button m_ButtonAdd;
        private Button m_ButtonApply;

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string ElementNameHead => "Tactile-Device-Head";
        protected override string ElementNameBody => "Tactile-Device-Body";
        protected override string ElementNameFoot => "Tactile-Device-Foot";

        protected override List<string> CustomStyleSheetPaths => new List<string>
        {
            EditorPaths.PACKAGES + "Tactile/Editor/StyleSheets/device-builder"
        };

        public override bool AllowReordering => !this.IsPlaying;
        public override bool AllowDuplicating => !this.IsPlaying;
        public override bool AllowDeleting  => !this.IsPlaying;
        public override bool AllowContextMenu => !this.IsPlaying;
        public override bool AllowCopyPaste => !this.IsPlaying;
        public override bool AllowInsertion => !this.IsPlaying;
        public override bool AllowBreakpoint => false;
        public override bool AllowDisable => false;
        public override bool AllowDocumentation => false;

        private bool IsPlaying => EditorApplication.isPlayingOrWillChangePlaymode;

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public DeviceBuilderTool(SerializedProperty property)
            : base(property, "m_InputControls")
        {
            this.SerializedObject.Update();

            var container = new VisualElement();

            container.Add(this.m_Head);
            container.Add(this.m_Body);
            container.Add(this.m_Foot);

            this.hierarchy.Add(container);

            this.OnChangePlayMode(EditorApplication.isPlaying
                ? PlayModeStateChange.EnteredPlayMode : PlayModeStateChange.ExitingPlayMode
            );

            EditorApplication.playModeStateChanged += this.OnChangePlayMode;
        }

        ~DeviceBuilderTool()
        {
            EditorApplication.playModeStateChanged -= this.OnChangePlayMode;
        }

        // PROTECTED METHODS: ---------------------------------------------------------------------

        protected override VisualElement MakeItemTool(int index)
        {
            return new InputControlTool(this, index);
        }

        protected override void SetupFoot()
        {
            base.SetupFoot();

            this.m_ButtonAdd = new InputControlTypeSelectorElement(this.PropertyList, this)
            {
                name = NAME_BUTTON_ADD
            };

            this.m_ButtonApply = new Button()
            {
                name = NAME_BUTTON_APPLY
            };

            this.m_ButtonApply.clicked += () =>
            {
                this.SerializedObject.ApplyModifiedPropertiesWithoutUndo();
                this.BuildDeviceLayout();
            };

            this.m_ButtonApply.Add(new Image { image = ICON_APPLY.Texture });   
            this.m_ButtonApply.Add(new Label { text = "Apply Changes..." });

            this.m_Foot.Add(this.m_ButtonAdd);
            this.m_Foot.Add(this.m_ButtonApply);
        }

        private void OnChangePlayMode(PlayModeStateChange state)
        { 
            this.m_ButtonAdd.SetEnabled(!this.IsPlaying);
            this.m_ButtonApply.SetEnabled(!this.IsPlaying);
            
            this.Refresh();
        }

        private void DisableItemWithDuplicateName()
        {
            foreach (VisualElement child in this.Children())
            {
                var element = child as InputControlTool;
                int index = element.Index;

                string value = this.PropertyList.GetArrayElementAtIndex(index)
                               .FindPropertyRelative("m_Name").stringValue;

                bool isEnabled = true;
                bool isActive = element.IsEnabled;

                for (int i = 0; i < this.PropertyList.arraySize; ++i)
                {
                    if (i == index) continue;

                    SerializedProperty item = this.PropertyList.GetArrayElementAtIndex(i);
                    SerializedProperty name = item.FindPropertyRelative("m_Name");

                    if (!string.IsNullOrEmpty(value) && name.stringValue != value) 
                        continue;

                    isEnabled = false;
                    break;
                }

                if (isEnabled != isActive)
                {
                    element.IsEnabled = isEnabled;
                    element.RefreshHead();
                }
            }
        }

        private void BuildDeviceLayout()
        {            
            DeviceBuilder deviceBuilder = GeneralRepository.Get?.DeviceBuilder;
            if (deviceBuilder == null) return;

            typeof(DeviceBuilder).GetMethod("BuildDeviceLayout", (BindingFlags)0x24)?
                                 .Invoke(deviceBuilder, null);
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void CheckForDuplicatePath()
        {
            EditorApplication.delayCall -= this.DisableItemWithDuplicateName;
            EditorApplication.delayCall += this.DisableItemWithDuplicateName;
        }

    }
}