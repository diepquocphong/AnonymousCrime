using System;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Full Screen")]
    [Category("Screen/Full")]
    [Description("Defines the entire surface of the screen as the area")]

    [Image(typeof(IconScreenFull))]

    [Serializable]
    public class TouchableAreaScreenFull : TTouchableArea
    {
        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            return screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                   screenPoint.y >= 0 && screenPoint.y <= Screen.height;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            var camera = Camera.main;
            if (camera == null) return;

            Rect pixelRect = camera.pixelRect;
            var displaySize = new Vector2(pixelRect.width, pixelRect.height);

            const float pad = 2f;
            var corner0 = new Vector3(pad, pad, 0);
            var corner1 = new Vector3(pad, displaySize.y - pad, 0);
            var corner2 = new Vector3(displaySize.x - pad, displaySize.y - pad, 0);
            var corner3 = new Vector3(displaySize.x - pad, pad, 0);

            Color color = this.GetGizmosColor(control);
            using (new UnityEditor.Handles.DrawingScope(color))
            {
                float segmentLength = 7f * Mathf.Min(displaySize.x / 1280f, displaySize.y / 720f);

                this.DrawBrokenLine(corner0, corner1, segmentLength);
                this.DrawBrokenLine(corner1, corner2, segmentLength);
                this.DrawBrokenLine(corner2, corner3, segmentLength);
                this.DrawBrokenLine(corner3, corner0, segmentLength);
            }

            #endif
        }
    }
}