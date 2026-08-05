using System;
using UnityEngine.UIElements;
using UnityEditor;
using GameCreator.Runtime.Common;

namespace Niam.Editor.Tactile 
{
    internal class InputControlTypeSelectorElement : Button
    {
        private static readonly IIcon ICON_ADD = new IconRadioOn(ColorTheme.Type.TextLight);
        
        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public InputControlTypeSelectorElement(SerializedProperty propertyList, DeviceBuilderTool tool)
        {
            this.Add(new Image { image = ICON_ADD.Texture });   
            this.Add(new Label { text = "Add Input Control..." });

            var typeSelector = new InputControlTypeSelector(propertyList, this);
            typeSelector.EventChange += (prevType, newType) =>
            {
                object instance = Activator.CreateInstance(newType);
                tool.InsertItem(propertyList.arraySize, instance);
            };
        }
    }
}
