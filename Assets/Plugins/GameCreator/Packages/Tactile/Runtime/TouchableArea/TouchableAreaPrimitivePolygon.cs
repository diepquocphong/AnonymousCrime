using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Polygon")]
    [Category("Primitive/Polygon")]
    [Description("Defines an arbitrary polygon with the given points as the area")]

    [Parameter("Points", "The corner points that define the shape in local space")]

    [Image(typeof(IconPolygon))]

    [Serializable]
    public class TouchableAreaPrimitivePolygon : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private Vector2[] m_Points = new [] 
        {
            new Vector2(-100f, 100f),
            new Vector2(100f, 100f),
            new Vector2(100f, -100f),
            new Vector2(-100f, -100f),
        };

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            int pointCount = this.m_Points.Length;
            if (pointCount < 3) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.uiTransform, screenPoint, this.uiCamera, out Vector2 localPoint
            );

            bool isInside = false;
            Vector2 p1 = this.m_Points[0];
            for (int i = 1; i <= pointCount; i++)
            {
                Vector2 p2 = this.m_Points[i % pointCount];

                if ((localPoint.y > Mathf.Min(p1.y, p2.y)) &&
                    (localPoint.y <= Mathf.Max(p1.y, p2.y)) &&
                    (localPoint.x <= Mathf.Max(p1.x, p2.x)) &&
                    (p1.y != p2.y))
                {
                    float xinters = (localPoint.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;
                    if (p1.x == p2.x || localPoint.x <= xinters) isInside = !isInside;
                }

                p1 = p2;
            }

            return isInside;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control)
        {
            #if UNITY_EDITOR
            
            int pointCount = this.m_Points.Length;
            if (pointCount < 3) return;

            var lines = new Vector3[pointCount * 2];
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 currentPoint = this.m_Points[i];
                Vector2 nextPoint = this.m_Points[(i + 1) % pointCount];

                lines[i * 2] = currentPoint;
                lines[i * 2 + 1] = nextPoint;
            }

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = control.transform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                for (int i = 0; i < lines.Length; i += 2)
                {
                    this.DrawBrokenLine(lines[i], lines[i + 1]);
                }
            }

            #endif
        }

    }
}