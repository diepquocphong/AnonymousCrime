using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TTouchableArea))]
    public class TTouchableAreaDrawer : TactileSectionDrawer
    {
        protected sealed override IIcon UnitIcon => new IconArea(ColorTheme.Type.TextLight);

        public sealed override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return this.MakePropertyGUI(property, "Touchable Area");
        }

        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            body.Clear();

            SerializationUtils.CreateChildProperties(
                body, property,
                SerializationUtils.ChildrenMode.ShowLabelsInChildren, true,
                "m_Raycast", "m_LayerMask", "m_AutoPress", "m_AutoPressOption", 
                "m_AutoRelease", "m_MultiTouch", "m_Interaction" 
            );

            this.CreateCommonPropertiesGUI(body, property);
        }

        protected void CreateCommonPropertiesGUI(VisualElement body, SerializedProperty property)
        {
            SerializedProperty raycast = property.FindPropertyRelative("m_Raycast");
            SerializedProperty layerMask = property.FindPropertyRelative("m_LayerMask");
            SerializedProperty autoPress = property.FindPropertyRelative("m_AutoPress");
            SerializedProperty autoPressOpt = property.FindPropertyRelative("m_AutoPressOption");
            SerializedProperty autoRelease = property.FindPropertyRelative("m_AutoRelease");
            SerializedProperty multiTouch = property.FindPropertyRelative("m_MultiTouch");
            SerializedProperty interaction = property.FindPropertyRelative("m_Interaction");

            if (raycast != null)
            {
                var fieldLayerMask = new PropertyField(layerMask, "");
                fieldLayerMask.style.marginRight = 2f;
                fieldLayerMask.style.flexGrow = 1f;
                
                var fieldRaycast = new Toggle(raycast.displayName);
                fieldRaycast.bindingPath = raycast.propertyPath;
                fieldRaycast.Q(className: "unity-toggle__input").Add(fieldLayerMask);
                fieldRaycast.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
                
                fieldLayerMask.style.display = raycast.boolValue 
                    ? DisplayStyle.Flex : DisplayStyle.None;

                fieldRaycast.RegisterValueChangedCallback(callback => 
                {
                    fieldLayerMask.style.display = callback.newValue 
                        ? DisplayStyle.Flex : DisplayStyle.None;
                });
                
                body.Add(fieldRaycast);
            }

            if (autoPress != null)
            {
                var fieldAutoPressOpt = new PropertyField(autoPressOpt, "");
                fieldAutoPressOpt.style.marginRight = 2f;
                fieldAutoPressOpt.style.flexGrow = 1f;
                
                var fieldAutoPress = new Toggle(autoPress.displayName);
                fieldAutoPress.bindingPath = autoPress.propertyPath;
                fieldAutoPress.Q(className: "unity-toggle__input").Add(fieldAutoPressOpt);
                AlignLabel.On(fieldAutoPress);
                
                fieldAutoPressOpt.style.display = autoPress.boolValue 
                    ? DisplayStyle.Flex : DisplayStyle.None;

                fieldAutoPress.RegisterValueChangedCallback(callback => 
                {
                    fieldAutoPressOpt.style.display = callback.newValue 
                        ? DisplayStyle.Flex : DisplayStyle.None;
                });
                
                body.Add(fieldAutoPress);
            }

            var fieldAutoRelease = new PropertyField(autoRelease);
            var fieldMultiTouch = new PropertyField(multiTouch);
            var fieldInteraction = new PropertyField(interaction);

            body.Add(fieldAutoRelease);
            body.Add(fieldMultiTouch);
            body.Add(fieldInteraction);
        }
    }
}