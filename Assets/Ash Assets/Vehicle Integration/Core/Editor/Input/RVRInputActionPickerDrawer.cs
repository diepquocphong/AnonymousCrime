// PropertyDrawer and UnityEditor are not available in a player build. The
// input-system symbol is also defined for runtime assemblies, so both guards
// are required here.
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[CustomPropertyDrawer(typeof(RVRInputActionPickerAttribute))]
public class RVRInputActionPickerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var attr = (RVRInputActionPickerAttribute)attribute;
        SerializedProperty assetProp = property.serializedObject.FindProperty(attr.AssetFieldName);
        var asset = assetProp != null ? assetProp.objectReferenceValue as InputActionAsset : null;

        EditorGUI.BeginProperty(position, label, property);

        Rect fieldRect = EditorGUI.PrefixLabel(position, label);

        string display =
            asset == null ? "Assign an Input Action Asset" :
            string.IsNullOrEmpty(property.stringValue) ? "(None)" :
            property.stringValue;

        using (new EditorGUI.DisabledScope(asset == null))
        {
            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(display), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                bool any = false;

                foreach (InputActionMap map in asset.actionMaps)
                {
                    foreach (InputAction action in map.actions)
                    {
                        any = true;
                        string path = map.name + "/" + action.name;
                        bool isOn = property.stringValue == path || property.stringValue == action.name;

                        string captured = path;
                        menu.AddItem(new GUIContent(path), isOn, () =>
                        {
                            property.stringValue = captured;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }

                if (!any)
                    menu.AddDisabledItem(new GUIContent("No actions found in this asset"));

                menu.DropDown(fieldRect);
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif
