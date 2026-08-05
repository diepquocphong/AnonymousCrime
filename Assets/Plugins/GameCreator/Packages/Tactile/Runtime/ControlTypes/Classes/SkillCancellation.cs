using System;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Niam.Runtime.Tactile 
{
    [Serializable]
    public class SkillCancellation
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------
        
        [SerializeField] private bool m_IsInverted;
        [SerializeField] private TactileControl m_CancelArea;

        [NonSerialized] private TactileControl m_Control;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public SkillCancellation()
        { }

        public SkillCancellation(bool inverseArea)
        { 
            this.m_IsInverted = inverseArea;
        }

        public SkillCancellation(TactileControl cancelArea, bool inverseArea = false)
        {
            this.m_CancelArea = cancelArea;
            this.m_IsInverted = inverseArea;
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void Setup(TactileControl control)
        {
            if (control == null || this.m_CancelArea == null) return;
            if (this.m_CancelArea == control) return;

            this.m_Control = control;
        }

        public bool IsWithinArea(Vector2 screenPoint)
        {
            if (this.m_CancelArea == null) return false;

            bool isWithinArea = this.m_CancelArea.TouchableArea.ContainsPoint(screenPoint);
            return this.m_IsInverted ? !isWithinArea : isWithinArea;
        }

        public void SendCancelPress(Touch touch)
        {
            if (this.m_CancelArea != null)
            {
                this.m_CancelArea.TouchableArea.ForceInteract(touch);
            }
        }

        public void SendCancelRelease(Touch touch)
        {
            if (this.m_CancelArea != null)
            {
                this.m_CancelArea.TouchableArea.InteractBeforeEnd(touch);
                this.m_CancelArea.TouchableArea.InteractAfterEnd(touch);
            }
        }

        // GIZMOS: --------------------------------------------------------------------------------

        #if UNITY_EDITOR

        public void DrawGizmos(Transform transform)
        { }

        #endif

    }
}