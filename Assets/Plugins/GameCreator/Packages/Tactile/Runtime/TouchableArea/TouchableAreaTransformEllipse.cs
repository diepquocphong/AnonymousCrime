using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Ellipse Transform")]
    [Category("Transform/Ellipse Transform")]
    [Description("Defines the ellipse bounds of the given Rect Transform as the area")]

    [Parameter(
        "Rect Transfom", 
        "A reference to the Rect Transform component which defines the area"
    )]

    [Image(typeof(IconRectTransform), typeof(OverlayDot))]

    [Serializable]
    public class TouchableAreaTransformEllipse : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_RectTransfom = GetGameObjectSelf.Create();

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            var rectTransform = this.m_RectTransfom.Get<RectTransform>(this.m_Control.Args);
            if (rectTransform == null) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, this.uiCamera, out Vector2 localPoint
            );

            Vector2 sizeDelta = rectTransform.rect.size;
            localPoint += (rectTransform.pivot - new Vector2(0.5f, 0.5f)) * sizeDelta;
            
            Vector2 halfSize = sizeDelta * 0.5f;
            return localPoint.x * localPoint.x / (halfSize.x * halfSize.x) +
                   localPoint.y * localPoint.y / (halfSize.y * halfSize.y) <= 1;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            GameObject target = this.m_RectTransfom.EditorValue; 
            if (target == null) target = control.gameObject;

            var rectTransform = target.transform as RectTransform; 
            if (rectTransform == null) return; 

            Vector2 sizeDelta = rectTransform.rect.size;
            Vector3 center = (new Vector2(0.5f, 0.5f) - rectTransform.pivot) * sizeDelta;
            Vector2 halfSize = sizeDelta * 0.5f;

            int segments = Mathf.Max(12, Mathf.CeilToInt(Mathf.Max(halfSize.x, halfSize.y))); 
            if (segments % 4 != 0) segments += 4 - segments % 4;
            float angleStep = 360f / segments;

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = rectTransform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                for (int i = 0; i < segments; i += 4)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int segIndex = i + j;
                        if (segIndex >= segments) break;

                        float angle1 = Mathf.Deg2Rad * (segIndex * angleStep);
                        float angle2 = Mathf.Deg2Rad * ((segIndex + 1) * angleStep);

                        float x1 = Mathf.Cos(angle1) * halfSize.x;
                        float y1 = Mathf.Sin(angle1) * halfSize.y;

                        float x2 = Mathf.Cos(angle2) * halfSize.x;
                        float y2 = Mathf.Sin(angle2) * halfSize.y;
                        
                        var point1 = new Vector3(center.x + x1, center.y + y1, center.z);
                        var point2 = new Vector3(center.x + x2, center.y + y2, center.z);

                        UnityEditor.Handles.DrawAAPolyLine(3f, point1, point2);
                    }
                }
            }

            #endif
        }

    }
}