using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Circle")]
    [Category("Primitive/Circle")]
    [Description("Defines a circle with a given radius and offset as the area")]

    [Parameter("Radius", "The radius of the circular area which can be interact with")]
    [Parameter("Offset", "The offset position of the area relative to transform position")]

    [Image(typeof(IconCircleOutline))]

    [Serializable]
    public class TouchableAreaPrimitiveCircle : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField, Min(0f)] private float m_Radius = 115f;
        [SerializeField] private Vector2 m_Offset;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                this.uiTransform, screenPoint, this.uiCamera, out Vector3 worldPoint
            );

            Vector2 scale = this.uiTransform.lossyScale;
            float maxScale = Mathf.Max(scale.x, scale.y);

            Vector3 position = this.uiTransform.position + (Vector3)(this.m_Offset * maxScale);
            Vector3 distance = 1f / maxScale * (position - worldPoint);

            return distance.sqrMagnitude <= this.m_Radius * this.m_Radius;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            Transform transform = control.transform;
            
            Vector2 scale = transform.localScale;
            float maxScale = Mathf.Max(scale.x, scale.y);
            Vector3 offset = (Vector3)(this.m_Offset * maxScale);

            float radius = this.m_Radius * maxScale;
            int segments = Mathf.Max(12, Mathf.CeilToInt(radius));
            if (segments % 4 != 0) segments += 4 - segments % 4;

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = transform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                for (var i = 0; i < segments; i += 4)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        int segIndex = i + j;
                        if (segIndex >= segments) break;

                        float angle1 = segIndex / (float)segments * Mathf.PI * 2;
                        float angle2 = (segIndex + 1) / (float)segments * Mathf.PI * 2;

                        float x1 = Mathf.Cos(angle1) * radius;
                        float y1 = Mathf.Sin(angle1) * radius;
                        var pos1 = offset + new Vector3(x1, y1, 0);

                        float x2 = Mathf.Cos(angle2) * radius;
                        float y2 = Mathf.Sin(angle2) * radius;
                        var pos2 = offset + new Vector3(x2, y2, 0);

                        UnityEditor.Handles.DrawAAPolyLine(3f, pos1, pos2);
                    }
                }
            }

            #endif
        }

    }
}