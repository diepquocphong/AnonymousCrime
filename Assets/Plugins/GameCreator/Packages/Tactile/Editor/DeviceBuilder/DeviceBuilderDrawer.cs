using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(DeviceBuilder))]
    public class DeviceBuilderDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            container.Add(new LabelTitle("Device Layout"));
            container.Add(new SpaceSmaller());
            
            var foldbox = new Foldbox("Input Controls", "tactile:device-layout-controls"); 
            foldbox.Add(new DeviceBuilderTool(property));
            container.Add(foldbox);

            return container;
        }
    }
}