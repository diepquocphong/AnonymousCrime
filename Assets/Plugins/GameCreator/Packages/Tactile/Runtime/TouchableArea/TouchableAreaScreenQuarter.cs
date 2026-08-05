using System;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Quarter Screen")]
    [Category("Screen/Quarter")]
    [Description("Defines the given quarter of the screen as the area")]

    [Parameter("Screen Side", "Which quarter area of the screen can be interact with")]

    [Image(typeof(IconScreenQuarter))]

    [Serializable]
    public class TouchableAreaScreenQuarter : TTouchableArea
    {
        private enum ScreenSide
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private ScreenSide m_ScreenSide = ScreenSide.BottomLeft;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            return this.m_ScreenSide switch
            {
                ScreenSide.TopLeft =>     screenPoint.x >= 0 && 
                                          screenPoint.x < Screen.width / 2 &&
                                          screenPoint.y > Screen.height / 2 && 
                                          screenPoint.y <= Screen.height,

                ScreenSide.TopRight =>    screenPoint.x >= Screen.width / 2 && 
                                          screenPoint.x <= Screen.width &&
                                          screenPoint.y > Screen.height / 2 && 
                                          screenPoint.y <= Screen.height,

                ScreenSide.BottomLeft =>  screenPoint.x >= 0 && 
                                          screenPoint.x < Screen.width / 2 &&
                                          screenPoint.y >= 0 && 
                                          screenPoint.y <= Screen.height / 2,

                ScreenSide.BottomRight => screenPoint.x >= Screen.width / 2 && 
                                          screenPoint.x <= Screen.width &&
                                          screenPoint.y >= 0 && 
                                          screenPoint.y <= Screen.height / 2,

                _ => false,
            };
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void DrawGizmos(TactileControl control) 
        {
            #if UNITY_EDITOR

            var camera = Camera.main;
            if (camera == null) return;

            Rect pixelRect = camera.pixelRect;
            var displaySize = new Vector2(pixelRect.width, pixelRect.height);

            Vector3 corner0;
            Vector3 corner1;
            Vector3 corner2;
            Vector3 corner3;
            
            const float pad = 2f;
            switch (this.m_ScreenSide)
            {
                case ScreenSide.TopLeft:
                    corner0 = new Vector3(pad, displaySize.y * 0.5f, 0);
                    corner1 = new Vector3(pad, displaySize.y - pad, 0);
                    corner2 = new Vector3(displaySize.x * 0.5f, displaySize.y - pad, 0);
                    corner3 = new Vector3(displaySize.x * 0.5f, displaySize.y * 0.5f, 0);
                    break;

                case ScreenSide.TopRight:
                    corner0 = new Vector3(displaySize.x * 0.5f, displaySize.y * 0.5f, 0);
                    corner1 = new Vector3(displaySize.x * 0.5f, displaySize.y - pad, 0);
                    corner2 = new Vector3(displaySize.x - pad, displaySize.y - pad, 0);
                    corner3 = new Vector3(displaySize.x - pad, displaySize.y * 0.5f, 0);
                    break;

                case ScreenSide.BottomLeft:
                    corner0 = new Vector3(pad, pad, 0);
                    corner1 = new Vector3(pad, displaySize.y * 0.5f, 0);
                    corner2 = new Vector3(displaySize.x * 0.5f, displaySize.y * 0.5f, 0);
                    corner3 = new Vector3(displaySize.x * 0.5f, pad, 0);
                    break;

                case ScreenSide.BottomRight:
                    corner0 = new Vector3(displaySize.x * 0.5f, pad, 0);
                    corner1 = new Vector3(displaySize.x * 0.5f, displaySize.y * 0.5f, 0);
                    corner2 = new Vector3(displaySize.x - pad, displaySize.y * 0.5f, 0);
                    corner3 = new Vector3(displaySize.x - pad, pad, 0);
                    break;

                default:
                    return;
            }

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