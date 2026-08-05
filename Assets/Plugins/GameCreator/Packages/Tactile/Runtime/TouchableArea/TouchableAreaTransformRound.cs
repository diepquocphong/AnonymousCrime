using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Rounded Transform")]
    [Category("Transform/Rounded Transform")]
    [Description("Defines the rounded corner bounds of the given Rect Transform as the area")]

    [Parameter(
        "Rect Transfom", 
        "A reference to the Rect Transform component which defines the area"
    )]

    [Parameter("Radius", "The radius of each corner of the Rect Transform's bounds")]

    [Image(typeof(IconRectTransform), typeof(OverlayDot))]

    [Serializable]
    public class TouchableAreaTransformRound : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_RectTransfom = GetGameObjectSelf.Create();

        [SerializeField, Min(0)] 
        private float m_Radius = 20f;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            var rectTransform = this.m_RectTransfom.Get<RectTransform>(this.m_Control.Args);
            if (rectTransform == null) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, this.uiCamera, out Vector2 localPoint
            );

            Vector2 sizeDelta = rectTransform.rect.size;
            Vector2 halfSize = sizeDelta * 0.5f;

            localPoint += (rectTransform.pivot - new Vector2(0.5f, 0.5f)) * sizeDelta;
            
            if (localPoint.x < -halfSize.x || localPoint.x > halfSize.x ||
                localPoint.y < -halfSize.y || localPoint.y > halfSize.y)
                return false;

            float radius = Mathf.Clamp(this.m_Radius, 0f, Mathf.Min(halfSize.x, halfSize.y));

            float innerLeft = -halfSize.x + radius;
            float innerRight = halfSize.x - radius;
            float innerBottom = -halfSize.y + radius;
            float innerTop = halfSize.y - radius;

            if (localPoint.x >= innerLeft && localPoint.x <= innerRight &&
                localPoint.y >= innerBottom && localPoint.y <= innerTop)
                return true;

            var cornerCenter = Vector2.zero;
            if (localPoint.x < innerLeft)
            {
                if (localPoint.y < innerBottom)
                    cornerCenter = new Vector2(innerLeft, innerBottom);
                else if (localPoint.y > innerTop)
                    cornerCenter = new Vector2(innerLeft, innerTop);
            }
            else if (localPoint.x > innerRight)
            {
                if (localPoint.y < innerBottom)
                    cornerCenter = new Vector2(innerRight, innerBottom);
                else if (localPoint.y > innerTop)
                    cornerCenter = new Vector2(innerRight, innerTop);
            }

            if (cornerCenter != Vector2.zero)
                return (localPoint - cornerCenter).sqrMagnitude <= radius * radius;

            return true;
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
            Vector2 halfSize = sizeDelta * 0.5f;
            Vector2 offset = (new Vector2(0.5f, 0.5f) - rectTransform.pivot) * sizeDelta;

            float radius = Mathf.Clamp(this.m_Radius, 0f, Mathf.Min(halfSize.x, halfSize.y));
            var arcCenter0 = new Vector2(-halfSize.x + radius, -halfSize.y + radius) + offset;
            var arcCenter1 = new Vector2(-halfSize.x + radius, halfSize.y - radius) + offset;
            var arcCenter2 = new Vector2(halfSize.x - radius, halfSize.y - radius) + offset;
            var arcCenter3 = new Vector2(halfSize.x - radius, -halfSize.y + radius) + offset;

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = rectTransform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                this.DrawBrokenLine(
                    new Vector3(arcCenter0.x, -halfSize.y + offset.y, 0f),
                    new Vector3(arcCenter3.x, -halfSize.y + offset.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(-halfSize.x + offset.x, arcCenter0.y, 0f),
                    new Vector3(-halfSize.x + offset.x, arcCenter1.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(halfSize.x + offset.x, arcCenter3.y, 0f),
                    new Vector3(halfSize.x + offset.x, arcCenter2.y, 0f)
                );

                this.DrawBrokenLine(
                    new Vector3(arcCenter1.x, halfSize.y + offset.y, 0f),
                    new Vector3(arcCenter2.x, halfSize.y + offset.y, 0f)
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