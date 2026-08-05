using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(FieldDeviceVector2))]
    public class FieldDeviceVector2Drawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new DeviceControlPickTool(property, "Vector2", "Stick", "Delta", "Dpad");
        }
    }
}