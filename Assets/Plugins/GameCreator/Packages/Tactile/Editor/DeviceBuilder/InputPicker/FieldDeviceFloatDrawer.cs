using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(FieldDeviceFloat))]
    public class FieldDeviceFloatDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new DeviceControlPickTool(property, "Axis", "DpadAxis");
        }
    }
}