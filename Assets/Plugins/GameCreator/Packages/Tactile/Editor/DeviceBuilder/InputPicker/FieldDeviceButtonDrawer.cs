using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(FieldDeviceButton))]
    public class FieldDeviceButtonDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new DeviceControlPickTool(property, "Button");
        }
    }
}