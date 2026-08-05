using System;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using ImageAttribute = GameCreator.Runtime.Common.ImageAttribute;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    public abstract class TactileSectionDrawer : PropertyDrawer
    {
        private const string PATH_USS = EditorPaths.PACKAGES + 
                                        "Tactile/Editor/StyleSheets/tactile-section";
        
        private static readonly IIcon ICON_ARROW = new IconDropdown(ColorTheme.Type.TextLight);
        private static readonly IIcon ICON_HELP = new IconHelpOutline(Theme.MainColor);

        // ABSTRACT PROPERTIES: -------------------------------------------------------------------
        
        protected abstract IIcon UnitIcon { get; }

        // PROTECTED METHODS: ---------------------------------------------------------------------

        protected VisualElement MakePropertyGUI(SerializedProperty property, string headTitle)
        {
            var root = new VisualElement();
            var head = new VisualElement();
            var body = new VisualElement();

            root.Add(head);
            root.Add(body);

            root.AddToClassList("tactile-section-root");
            head.AddToClassList("tactile-section-head");
            body.AddToClassList("tactile-section-body");

            this.BuildHead(head, body, property, headTitle);
            this.BuildBody(body, property);

            StyleSheet[] styleSheets = StyleSheetUtils.Load(PATH_USS);
            foreach (StyleSheet sheet in styleSheets) root.styleSheets.Add(sheet);

            return root;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void BuildHead(VisualElement head, VisualElement body, 
            SerializedProperty property, string headTitle)
        {
            head.Clear();

            Type typeFull = TypeUtils.GetTypeFromProperty(property, true);
            Type typeField = TypeUtils.GetTypeFromProperty(property, false);

            var iconAttr = typeFull?.GetCustomAttributes<ImageAttribute>().FirstOrDefault();

            var image = new Image { image = this.UnitIcon.Texture };
            var btnToggle = new Button { text = headTitle };

            btnToggle.clicked += () =>
            {
                property.isExpanded = !property.isExpanded;
                this.UpdateBodyState(property.isExpanded, body);
            };

            var btnToggleRightLabel = new Label(TypeUtils.GetTitleFromType(typeFull));

            var btnOpenDocs = new Button();
            var imageQuestion = new Image { image = ICON_HELP.Texture };
            btnOpenDocs.clicked += () => DocumentationPopup.Open(typeFull);

            var btnChangeType = new Button();
            btnChangeType.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);

            btnChangeType.clicked += () => TypeSelectorFancyPopup.Open(
                head, typeField,
                newType =>
                {
                    if (newType == null) return;
                    property.serializedObject.Update();

                    var unit = Activator.CreateInstance(newType);
                    property.SetValue(unit);

                    SerializationUtils.ApplyUnregisteredSerialization(property.serializedObject);

                    this.BuildHead(head, body, property, headTitle);
                    this.BuildBody(body, property);

                    SceneView.RepaintAll();
                }
            );

            var imageChangeType = new Image
            {
                image = iconAttr != null ? iconAttr.Image : Texture2D.whiteTexture
            };

            var imageChevron = new Image { image = ICON_ARROW.Texture };

            imageQuestion.AddToClassList("tactile-section-head-image");
            imageChangeType.AddToClassList("tactile-section-head-image");
            imageChevron.AddToClassList("tactile-section-head-arrow");

            btnOpenDocs.Add(imageQuestion);
            btnChangeType.Add(imageChangeType);
            btnChangeType.Add(imageChevron);

            image.AddToClassList("tactile-section-head-image");
            btnToggle.AddToClassList("tactile-section-head-btn__toggle");
            btnToggleRightLabel.AddToClassList("tactile-section-head-label__unit");
            btnOpenDocs.AddToClassList("tactile-section-head-btn__change");
            btnChangeType.AddToClassList("tactile-section-head-btn__change");

            btnToggle.contentContainer.Add(btnToggleRightLabel);

            head.Add(image);
            head.Add(btnToggle);
            head.Add(btnChangeType);
            head.Add(btnOpenDocs);

            head.Bind(property.serializedObject);
            this.UpdateBodyState(property.isExpanded, body);

            this.OnBuildHead(head, property);
        }

        private void UpdateBodyState(bool state, VisualElement body)
        {
            body.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
        }
                
        // VIRTUAL METHODS: -----------------------------------------------------------------------

        protected virtual void OnBuildHead(VisualElement head, SerializedProperty property)
        { }

        protected virtual void BuildBody(VisualElement body, SerializedProperty property)
        {
            body.Clear();

            SerializationUtils.CreateChildProperties(
                body,
                property,
                SerializationUtils.ChildrenMode.ShowLabelsInChildren,
                true
            );
        }

    }
}