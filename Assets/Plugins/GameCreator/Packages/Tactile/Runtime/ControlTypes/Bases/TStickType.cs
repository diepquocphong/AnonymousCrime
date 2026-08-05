using System;
using UnityEngine;

namespace Niam.Runtime.Tactile 
{ 
    [Serializable]
    public abstract class TStickType : TControlType, IStickType
    {
        // ENUMS: ---------------------------------------------------------------------------------

        protected enum ArrowDirection      
        { 
            Top, 
            Right, 
            Down, 
            Left 
        }

        public enum HandleAxis     
        { 
            BothXY, 
            XOnly, 
            YOnly 
        }

        // EXPOSED MEMBERS: -----------------------------------------------------------------------
    
        [SerializeField] protected InputSimulateVector2 m_InputSimulate;

        [SerializeField] protected RectTransform m_Surface;
        [SerializeField] protected RectTransform m_Handle;
        [SerializeField] protected RectTransform m_Arrow;

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public abstract Vector2 StickSize { get; }
        public abstract Vector2 StickMotion { get; }
        public abstract Vector2 StickRawMotion { get; }

        public RectTransform Surface => this.m_Surface;
        public RectTransform Handle => this.m_Handle;
        public RectTransform Arrow => this.m_Arrow;

        public InputSimulateVector2 InputSimulate => this.m_InputSimulate;

    }
}