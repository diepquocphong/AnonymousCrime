using System;
using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{
    [CustomPropertyDrawer(typeof(NetworkingSettings), false)]
    public class NetworkSettingsDrawer : PropertyDrawer
    {
        private IIcon UnitIcon => new IconCog(ColorTheme.Type.TextLight);
        private const string PATH_STYLES = EditorPaths.CHARACTERS + "StyleSheets/";
        private bool _lastChange;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement
            {
                name = "NetworkSettings",
                style =
                {
                    display = DisplayStyle.None
                }
            };
            return root;
        }
        
        protected VisualElement DrawSettings(SerializedProperty property)
        {
            // Get parent property from the property
            if(property.GetParentObject() is BaseGameCreatorNode { useNetwork: true } node)
            {
                if (node.networkingSettings == null) property.isExpanded = false;
                node.networkingSettings ??= new NetworkingSettings(node.GUID);
                node.networkingSettings.nodeId = node.GUID;
            }
            else
            {
                return new VisualElement();
            }
            
            var root = new VisualElement();
            var head = new VisualElement();
            var body = new VisualElement();

            root.Add(head);
            root.Add(body);

            var customUSS = PathUtils.Combine(PATH_STYLES, "Unit");
            var styleSheets = StyleSheetUtils.Load(customUSS);

            foreach (var sheet in styleSheets) root.styleSheets.Add(sheet);

            root.AddToClassList("gc-character-unit-root");
            head.AddToClassList("gc-character-unit-head");
            body.AddToClassList("gc-character-unit-body");

            BuildHead(head, body, property, "Network Settings");
            BuildBody(body, property, node);

            return root;
        }

        private void BuildHead(VisualElement head, VisualElement body, SerializedProperty property,
            string headTitle)
        {
            head.Clear();

            var image = new Image
            {
                image = UnitIcon.Texture
            };

            var btnToggle = new Button
            {
                text = headTitle
            };

            btnToggle.clicked += () =>
            {
                property.isExpanded = !property.isExpanded;
                UpdateBodyState(property.isExpanded, body);
                property.serializedObject.ApplyModifiedProperties();
            };

            image.AddToClassList("gc-character-unit-head-image");
            btnToggle.AddToClassList("gc-character-unit-head-btn__toggle");

            head.Add(image);
            head.Add(btnToggle);

            head.Bind(property.serializedObject);
            UpdateBodyState(property.isExpanded, body);
        }

        protected virtual void BuildBody(VisualElement body, SerializedProperty property, BaseGameCreatorNode node)
        {
            if(body == null) return;
            if(property?.serializedObject == null) return;
            if(property.serializedObject.targetObject == null) return;
            
            try
            {

                body.Clear();

                var networkSync = property.FindPropertyRelative("networkSync");
                var networkSyncToggle = new Toggle
                {
                    label = "Network Sync"
                };
                networkSyncToggle.BindProperty(networkSync);
                _lastChange = node.networkingSettings.networkSync;

                networkSyncToggle.RegisterValueChangedCallback(changeEvent =>
                {
                    if (_lastChange == node.networkingSettings.networkSync) return;
                    _lastChange = node.networkingSettings.networkSync;
                    OnNetworkSyncChanged(node, changeEvent.newValue);
                    property.serializedObject.ApplyModifiedProperties();

                    EditorApplication.delayCall -= UpdateBody;
                    EditorApplication.delayCall += UpdateBody;
                });
                AlignLabel.On(networkSyncToggle);
                body.Add(networkSyncToggle);

                if (node.networkingSettings.networkSync)
                    SerializationUtils.CreateChildProperties(body, property,
                        SerializationUtils.ChildrenMode.ShowLabelsInChildren, true, "networkSync");
                return;

                void UpdateBody()
                {
                    EditorApplication.delayCall -= UpdateBody;
                    property.serializedObject.Update();
                    BuildBody(body, property, node);
                }
            }
            catch (Exception)
            {
                //
            }
        }

        protected virtual void OnNetworkSyncChanged(BaseGameCreatorNode node, bool enabled) {}

        // OTHER METHODS: -------------------------------------------------------------------------

        private void UpdateBodyState(bool state, VisualElement body)
        {
            body.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}