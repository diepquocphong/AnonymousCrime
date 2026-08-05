using System;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Control Types")]

    [Serializable]
    public abstract class TControlType
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [NonSerialized] protected TactileControl m_Control;

        // PROPERTIES: ----------------------------------------------------------------------------

        protected Args Args => this.m_Control.Args;
        protected TTouchableArea TouchableArea => this.m_Control.TouchableArea;

        protected bool HasPressInArea => this.TouchableArea.isLastPressIsInArea;
        protected bool HasReleaseInArea => this.TouchableArea.isLastReleaseIsInArea;
        
        protected int FingerCount => this.TouchableArea.fingerCount;
        protected float ScaleFactor => GeneralRepository.Get.ScaleFactor;

        // INITIALIZE: ----------------------------------------------------------------------------

        internal void Initialize(TactileControl control) 
        { 
            this.m_Control = control;
            this.Awake();
        }

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected virtual void Awake() 
        { }

        protected internal virtual void Start() 
        { }

        protected internal virtual void Update() 
        { }

        protected internal virtual void Enable() 
        { }
        
        protected internal virtual void Disable() 
        { }

        protected internal virtual void Destroy() 
        { }

        // INTERACTIONS: --------------------------------------------------------------------------

        protected internal virtual void InteractBeforeBegin(Touch touch)
        { }

        protected internal virtual void InteractAfterBegin(Touch touch)
        { }

        protected internal virtual void InteractBeforeDrag(Touch touch)
        { }

        protected internal virtual void InteractAfterDrag(Touch touch)
        { }

        protected internal virtual void InteractBeforeEnd(Touch touch)
        { }

        protected internal virtual void InteractAfterEnd(Touch touch)
        { }

        // GIZMOS: --------------------------------------------------------------------------------

        protected internal virtual void DrawGizmos(Transform transform) 
        { }
        
    }
}