using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;

namespace Niam.Editor.Tactile
{
    internal class Foldbox : Foldout
    {
        private const string USS_PATH = EditorPaths.PACKAGES + "Tactile/Editor/Stylesheets/foldbox";
        
        private static readonly IIcon ICON_EXPAND = new IconArrowDropDown(ColorTheme.Type.TextLight);
        private static readonly IIcon ICON_COLLAPSE = new IconArrowDropRight(ColorTheme.Type.TextLight);

        // MEMBERS: -------------------------------------------------------------------------------

        protected string m_ViewDataKey;
        protected Label m_TitleElement;
        protected Image m_CollapseExpandIcon;

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool FoldState
        {
            get => !string.IsNullOrEmpty(this.m_ViewDataKey)
                   && SessionState.GetBool(this.m_ViewDataKey, false);

            set
            {
                if (string.IsNullOrEmpty(this.m_ViewDataKey)) return;
                SessionState.SetBool(this.m_ViewDataKey, value);
            }
        }

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public Foldbox(string title) : this(title, "")
        { }

        public Foldbox(string title, string viewDataKey, bool withBorder = true) : base()
        {
            this.name = "Foldbox";
            this.m_ViewDataKey = viewDataKey;
            this.SetValueWithoutNotify(this.FoldState);

            VisualElement toggle = this.Q(className: toggleUssClassName);
            VisualElement content = this.Q(null, contentUssClassName);

            this.m_CollapseExpandIcon = new Image();
            this.m_TitleElement = new Label(title);

            toggle.Clear();
            toggle.Add(this.m_CollapseExpandIcon);
            toggle.Add(this.m_TitleElement);
            toggle.RegisterCallback<ClickEvent>(this.OnToggleClick);
            toggle.AddToClassList("foldbox-head");
            content.AddToClassList("foldbox-body");

            if (withBorder)
            {
                toggle.AddToClassList("foldbox-head__with-border");
                content.AddToClassList("foldbox-body__with-border");
            }

            this.RefreshFold();

            StyleSheet[] sheets = StyleSheetUtils.Load(USS_PATH);
            for (int i = 0; i < sheets.Length; i++) this.styleSheets.Add(sheets[i]);
        }

        // CALLBACKS: -----------------------------------------------------------------------------

        private void OnToggleClick(ClickEvent evt)
        {
            this.RefreshFold();
            evt.StopPropagation();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void RefreshFold()
        {
            if (this.m_CollapseExpandIcon == null) return;

            this.m_CollapseExpandIcon.image = this.value 
                ? ICON_EXPAND.Texture : ICON_COLLAPSE.Texture;
            
            this.FoldState = value;
        }
        
    }
}