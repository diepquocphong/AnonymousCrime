using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Touchable Area")]
    [Category("Touchable Area")]
    [Description("Defines the area based on Touchable Area of another Tactile Control")]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]

    [Image(typeof(IconArea))]

    [Serializable]
    public class TouchableAreaTouchableArea : TTouchableArea
    {
        [SerializeField] private TactileControl m_TactileControl;

        // OVERRIDE METHODS: ----------------------------------------------------------------------

        public override bool ContainsPoint(Vector2 screenPoint) 
        {
            #if UNITY_EDITOR

            try
            {
                return this.m_TactileControl != null 
                    ? this.m_TactileControl.TouchableArea.ContainsPoint(screenPoint) 
                    : false;
            }
            catch (StackOverflowException)
            {
                this.LogWarning(this.m_TactileControl);
                return false;
            }

            #else

            return this.m_TactileControl != null 
                ? this.m_TactileControl.TouchableArea.ContainsPoint(screenPoint) 
                : false;

            #endif
        }

        // GIZMOS: --------------------------------------------------------------------------------

        #if UNITY_EDITOR

        private static System.Collections.Generic.HashSet<TactileControl> UpdatedControls = new();

        public override void DrawGizmos(TactileControl control)
        {
            if (this.m_TactileControl == null || control == null)
                return;

            if (UpdatedControls.Contains(control))
            {
                LogWarning(control);
                return;
            }

            UpdatedControls.Add(control);

            try
            {
                this.m_TactileControl.TouchableArea.DrawGizmos(this.m_TactileControl);
            }
            catch (StackOverflowException)
            {
                this.LogWarning(this.m_TactileControl);
            }
            finally
            {
                UpdatedControls.Remove(control);
            }
        }

        private void LogWarning(TactileControl control)
        {
            Debug.LogWarning(
                $"Circular reference detected in the Touchable Area of '{control}'. " +
                "This can cause a stack overflow or result in an infinite loop.",
                control
            );
        }
        
        #endif
    }
}