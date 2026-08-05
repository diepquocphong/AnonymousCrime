using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Rounded Box")]
    [Category("Primitive/Rounded Box")]
    [Description("Defines a rounded corner box with the given size, offset and radius as the area")]

    [Parameter("Size", "The width and height of the box area which can be interact with")]
    [Parameter("Offset", "The offset position of the area relative to transform position")]
    [Parameter("Radius", "The radius of each corner of the box area")]

    [Image(typeof(IconSquareOutline), typeof(OverlayDot))]

    [Serializable]
    public class TouchableAreaPrimitiveRoundBox : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private Vector2 m_Size = new Vector2(200f, 200f);
        [SerializeField] private Vector2 m_Offset;
        [SerializeField] private float m_Radius = 20f;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.uiTransform, screenPoint, this.uiCamera, out Vector2 touchPoint
            );

            Vector2 halfSize = this.m_Size * 0.5f;
            Vector2 minBounds = -halfSize + this.m_Offset;
            Vector2 maxBounds = halfSize + this.m_Offset;

            if (touchPoint.x < minBounds.x || touchPoint.x > maxBounds.x ||
                touchPoint.y < minBounds.y || touchPoint.y > maxBounds.y)
                return false;

            float radius = Mathf.Clamp(this.m_Radius, 0f, Mathf.Min(halfSize.x, halfSize.y));

            float innerLeft = minBounds.x + radius;
            float innerRight = maxBounds.x - radius;
            float innerBottom = minBounds.y + radius;
            float innerTop = maxBounds.y - radius;

            if (touchPoint.x >= innerLeft && touchPoint.x <= innerRight &&
                touchPoint.y >= innerBottom && touchPoint.y <= innerTop)
                return true;

            var cornerCenter = Vector2.zero;
            if (touchPoint.x < innerLeft)
            {
                if (touchPoint.y < innerBottom)
                    cornerCenter = new Vector2(innerLeft, innerBottom);
                else if (touchPoint.y > innerTop)
                    cornerCenter = new Vector2(innerLeft, innerTop);
            }
            else if (touchPoint.x > innerRight)
            {
                if (touchPoint.y < innerBottom)
                    cornerCenter = new Vector2(innerRight, innerBottom);
                else if (touchPoint.y > innerTop)
                    cornerCenter = new Vector2(innerRight, innerTop);
            }

            if (cornerCenter != Vector2.zero)
                return (touchPoint - cornerCenter).sqrMagnitude <= radius * radius;

            return true;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control)
        {
            #if UNITY_EDITOR

            Vector2 halfSize = this.m_Size * 0.5f;
            if (halfSize.x < 0) halfSize.x = 0;
            if (halfSize.y < 0) halfSize.y = 0;

            Vector2 minBounds = -halfSize + this.m_Offset;
            Vector2 maxBounds = halfSize + this.m_Offset;

            float radius = Mathf.Clamp(this.m_Radius, 0f, Mathf.Min(halfSize.x, halfSize.y));

            var arcCenter0 = new Vector2(minBounds.x + radius, minBounds.y + radius);
            var arcCenter1 = new Vector2(minBounds.x + radius, maxBounds.y - radius);
            var arcCenter2 = new Vector2(maxBounds.x - radius, maxBounds.y - radius);
            var arcCenter3 = new Vector2(maxBounds.x - radius, minBounds.y + radius);

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = control.transform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                this.DrawBrokenLine(
                    new Vector3(arcCenter0.x, minBounds.y, 0f),
                    new Vector3(arcCenter3.x, minBounds.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(minBounds.x, arcCenter0.y, 0f),
                    new Vector3(minBounds.x, arcCenter1.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(maxBounds.x, arcCenter3.y, 0f),
                    new Vector3(maxBounds.x, arcCenter2.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(arcCenter1.x, maxBounds.y, 0f),
                    new Vector3(arcCenter2.x, maxBounds.y, 0f)
                );

                this.DrawBrokenArc(arcCenter0, radius, 180f, 90f);
                this.DrawBrokenArc(arcCenter1, radius, 90f, 90f);
                this.DrawBrokenArc(arcCenter2, radius, 0f, 90f);
                this.DrawBrokenArc(arcCenter3, radius, 270f, 90f);
            }

            #endif
        }

    }
}