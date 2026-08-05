using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    [CustomPropertyDrawer(typeof(TouchableAreaPrimitivePolygon))]
    public class TouchableAreaPrimitivePolygonDrawer : TTouchableAreaDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            body.Clear();
            body.Add(new PolygonPathTool(property));

            this.CreateCommonPropertiesGUI(body, property);
        }

    }
}