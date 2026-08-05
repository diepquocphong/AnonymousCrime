using UnityEditor;
using UnityEngine.UIElements;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(FilterDevice))]
    public class FilterDeviceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new DeviceFilterPickTool(property);
        }
    }
}