using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Rect Transform")]
    [Category("Transform/Rect Transform")]
    [Description("Defines the rect bounds of the specified Rect Transform as the area")]

    [Parameter(
        "Rect Transfom", 
        "A reference to the Rect Transform component which defines the area"
    )]

    [Image(typeof(IconRectTransform))]

    [Serializable]
    public class TouchableAreaTransformRect : TTouchableArea
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_RectTransfom = GetGameObjectSelf.Create();

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            var rectTransform = this.m_RectTransfom.Get<RectTransform>(this.m_Control.Args);
            return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform, screenPoint, this.uiCamera
            );
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            GameObject target = this.m_RectTransfom.EditorValue; 
            if (target == null) target = control.gameObject; 

            var rectTransform = target.transform as RectTransform; 
            if (rectTransform == null) return; 

            var corners = new Vector3[4];
            rectTransform.GetLocalCorners(corners);

            Color color = this.GetGizmosColor(control);
            Matrix4x4 matrix = rectTransform.localToWorldMatrix;
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                this.DrawBrokenLine(corners[0], corners[1]);
                this.DrawBrokenLine(corners[1], corners[2]);
                this.DrawBrokenLine(corners[2], corners[3]);
                this.DrawBrokenLine(corners[3], corners[0]);
            }

            #endif
        }
    }
}