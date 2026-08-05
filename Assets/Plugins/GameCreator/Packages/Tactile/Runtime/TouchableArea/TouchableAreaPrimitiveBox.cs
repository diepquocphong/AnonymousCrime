using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Box")]
    [Category("Primitive/Box")]
    [Description("Defines a box with the given size and offset as the area")]

    [Parameter("Size", "The width and height of the box area which can be interact with")]
    [Parameter("Offset", "The offset position of the area relative to transform position")]

    [Image(typeof(IconSquareOutline))]

    [Serializable]
    public class TouchableAreaPrimitiveBox : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private Vector2 m_Size = new Vector2(200f, 200f);
        [SerializeField] private Vector2 m_Offset;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.uiTransform, screenPoint, this.uiCamera, out Vector2 touchPoint
            );

            Vector2 halfSize = this.m_Size * 0.5f;
            Vector2 minBounds = -halfSize + this.m_Offset;
            Vector2 maxBounds = halfSize + this.m_Offset;

            return touchPoint.x >= minBounds.x && touchPoint.x <= maxBounds.x &&
                   touchPoint.y >= minBounds.y && touchPoint.y <= maxBounds.y;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            Vector2 halfSize = this.m_Size * 0.5f;
            Vector2 minBounds = -halfSize + this.m_Offset;
            Vector2 maxBounds = halfSize + this.m_Offset;

            var corner0 = new Vector3(minBounds.x, minBounds.y, 0f);
            var corner1 = new Vector3(minBounds.x, maxBounds.y, 0f);
            var corner2 = new Vector3(maxBounds.x, maxBounds.y, 0f);
            var corner3 = new Vector3(maxBounds.x, minBounds.y, 0f);

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = control.transform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                this.DrawBrokenLine(corner0, corner1);
                this.DrawBrokenLine(corner1, corner2);
                this.DrawBrokenLine(corner2, corner3);
                this.DrawBrokenLine(corner3, corner0);
            }

            #endif
        }

    }
}