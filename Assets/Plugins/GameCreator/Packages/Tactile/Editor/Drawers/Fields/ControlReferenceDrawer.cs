using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(ControlReference))]
    public class ControlReferenceDrawer : PropertyDrawer
    {
        private const string PATH_USS = EditorPaths.PACKAGES + 
                                        "Tactile/Editor/StyleSheets/control-reference";

        private const string PROP_OPTION = "m_Option";
        private const string PROP_CONTROL = "m_Control";
        private const string PROP_ID = "m_ID";

        private const string NAME_ROOT = "Tactile-Control-Reference";

        private int m_currentOption;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement() { name = NAME_ROOT };
            var content = new VisualElement();

            StyleSheet[] sheets = StyleSheetUtils.Load(PATH_USS);
            foreach (StyleSheet sheet in sheets) root.styleSheets.Add(sheet);

            SerializedProperty option = property.FindPropertyRelative(PROP_OPTION);
            this.m_currentOption = option.enumValueIndex;

            var fieldLabel = new Label(property.displayName);
            var fieldOption = new PropertyField(option, string.Empty);

            fieldOption.RegisterValueChangeCallback(callback =>
            {
                this.Refresh(content, property);
            });

            this.Refresh(content, property);

            root.Add(fieldLabel);
            root.Add(fieldOption);
            root.Add(content);

            AlignLabel.On(root);
            
            return root;
        }

        private void Refresh(VisualElement body, SerializedProperty property)
        {
            body.Clear();

            property.serializedObject.Update();

            SerializedProperty controlRef = property.FindPropertyRelative(PROP_CONTROL);
            SerializedProperty controlID = property.FindPropertyRelative(PROP_ID);
            int option = property.FindPropertyRelative(PROP_OPTION).enumValueIndex;

            switch (option)
            {
                case 0: // By Ref
                    var fieldControlRef = new PropertyField(controlRef, string.Empty);
                    fieldControlRef.Bind(property.serializedObject);
                    body.Add(fieldControlRef);

                    if (this.m_currentOption == 1 && !EditorApplication.isPlaying) 
                    {
                        var stringID = controlID.FindPropertyRelative("m_String");
                        stringID.stringValue = null;
                        property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        property.serializedObject.Update();
                    }

                    break;

                case 1: // By ID
                    var fieldControlID = new PropertyField(controlID, string.Empty);
                    fieldControlID.Bind(property.serializedObject);
                    body.Add(fieldControlID);

                    if (this.m_currentOption == 0 && !EditorApplication.isPlaying) 
                    {
                        controlRef.objectReferenceValue = null;
                        property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        property.serializedObject.Update();
                    }

                    break;
            }

            this.m_currentOption = option;
        }

    }
}