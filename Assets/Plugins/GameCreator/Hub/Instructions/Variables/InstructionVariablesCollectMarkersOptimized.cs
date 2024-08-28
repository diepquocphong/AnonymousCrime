using System;
using System.Collections.Generic;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Version(1, 0, 3)]

    [Title("Collect Markers [Optimized]")]
    [Category("Variables/Collect Markers [Optimized]")]
    [Description("Optimized version of the default 'Collect Markers' instruction")]
    
    [Image(typeof(IconMarker), ColorTheme.Type.Teal, typeof(OverlayBolt))]
    
    [Serializable]
    public class InstructionVariablesCollectMarkersOptimized : TInstructionVariablesCollect
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private List<ISpatialHash> m_Results = new List<ISpatialHash>();
        [NonSerialized] private List<GameObject> m_Buffer = new List<GameObject>();
        
        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string TitleTarget => "Markers";

        // COLLECT METHOD: ------------------------------------------------------------------------

        protected override List<GameObject> Collect(Vector3 origin, float maxRadius, float minRadius)
        {
            this.m_Buffer.Clear();
            SpatialHashMarkers.Find(origin, maxRadius, this.m_Results);

            for (int i = 0; i < m_Results.Count; i++)
            {
                Vector3 offset = this.m_Results[i].Position - origin;
                if (offset.sqrMagnitude <= minRadius * minRadius) continue;
                
                Marker marker = this.m_Results[i] as Marker;
                if (marker == null) continue;
                
                this.m_Buffer.Add(marker.gameObject);
            }

            return this.m_Buffer;
        }
    }
}