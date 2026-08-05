using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    internal class InputControlTool : TPolymorphicItemTool
    {
        // PROPERTIES: ----------------------------------------------------------------------------

        protected override object Value => this.m_Property.GetValue<TInputControl>();

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public InputControlTool(IPolymorphicListTool parentTool, int index) 
            : base(parentTool, index)
        { }

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        protected override void SetupHead()
        {
            this.m_HeadDisabled = new Button();
            this.m_HeadDisabled.Add(new Image
            {
                image = new IconCancel(ColorTheme.Type.TextLight).Texture,
                focusable = false
            });

            this.m_HeadDisabled.tooltip = "A disabled element is ignored";
            this.m_HeadDisabled.AddToClassList(CLASS_HEAD_DISABLE);
            this.m_Head.Add(this.m_HeadDisabled);

            base.SetupHead();

            this.m_Head.Insert(
                EditorApplication.isPlayingOrWillChangePlaymode 
                    ? this.m_Head.childCount - 1 : this.m_Head.childCount - 3, 
                this.m_HeadDisabled
            );
        }

        protected override void UpdateHead()
        {
            if (this.ParentTool is DeviceBuilderTool tool)
            {
                tool.CheckForDuplicatePath();

                this.m_HeadButton.style.opacity = this.IsEnabled ? 1f : 0.25f;
                this.m_HeadDisabled.style.display = this.IsEnabled
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }

            base.UpdateHead();
        }
        
        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void RefreshHead() => this.UpdateHead();

    }
}
