using System;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Push Button")]
    [Category("Push Button")]
    
    [Description(
        "A control type that simulates the behavior of a physical key or button providing " +
        "simple, discrete input for triggering actions like jumping, shooting, or interacting " +
        "with in-game objects."
    )]

    [Parameter(
        "Input Simulate", 
        "The button control path of the input control to be simulate"
    )]
    
    [Parameter(
        "Input Execution", 
        "Defines how the value transitions or updates during button press and release"
    )]

    [Image(typeof(IconPushButton))]

    [Serializable]
    public class ControlTypePushButton : TButtonType
    {
        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractBeforeBegin(Touch touch)
        {
            if (this.FingerCount > 1) return;
            if (!this.HasPressInArea) return;

            if (this.m_InputExecution != InputExecution.ReleasePulse)
            {
                this.Value = 1;
            }
        }

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (this.FingerCount > 1) return;
            if (!this.HasPressInArea) return;

            if (this.m_InputExecution == InputExecution.PressPulse)
            {
                this.Value = 0;
            }
        }

        protected internal override void InteractBeforeEnd(Touch touch)
        {
            if (this.FingerCount > 1) return;

            switch (this.m_InputExecution)
            {
                case InputExecution.Momentary:
                    this.Value = 0;
                    break;

                case InputExecution.PressPulse:
                    break;

                case InputExecution.ReleasePulse:
                    if (this.HasReleaseInArea)
                    {
                        this.Value = 1;
                        this.Value = 0;
                    }
                    break;
            }
        }

    }
}