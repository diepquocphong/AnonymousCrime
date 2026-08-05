using GameCreator.Runtime.Characters;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GameCreator.Runtime.Common
{
    [Title("Zoom For Mobile (With Player)")]
    [Category("Mobile Controller/Camera/With Player (NEW)/Zoom Touch (NEW)/Zoom For Mobile (With Player)")]

    [Description("Zoom For Mobile (With Player) commonly used to zoom the camera.")]
    [Image(typeof(IconZoom), ColorTheme.Type.Yellow)]

    [Keywords("Zoom", "Touchscreen")]

    [Serializable]
    public class InputValueVector2ZoomMobileController : TInputValueVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private InputAction m_InputAction;

        [NonSerialized] private float zoomSensitivity = .1f; // Adjust for how fast zooming occurs
        [NonSerialized] private Vector2 previousTouchDelta = Vector2.zero; // Store the previous distance

        // PROPERTIES: ----------------------------------------------------------------------------

        public InputAction InputAction
        {
            get
            {
                if (this.m_InputAction == null)
                {
                    this.m_InputAction = new InputAction(
                        "Zoom For Mobile (With Player)",
                        InputActionType.Value
                    );

                    this.m_InputAction.AddBinding("<Touchscreen>/position",
                    processors: @"invertVector2(invertX=false,invertY=true),
                          scaleVector2(x=3,y=3),
                          divideScreenSize,
                          divideDeltaTime");

                    this.m_InputAction.AddBinding("<Touchscreen>/delta",
                    processors: @"invertVector2(invertX=false,invertY=true),
                          scaleVector2(x=3,y=3),
                          divideScreenSize,
                          divideDeltaTime");
                }

                return this.m_InputAction;
            }
        }

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2(
                new InputValueVector2MotionSecondary()
            );
        }

        public override void OnStartup()
        {
            this.Enable();
        }

        public override void OnDispose()
        {
            this.Disable();
            this.InputAction?.Dispose();
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            return GetInputZoomValue();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private Vector2 GetInputZoomValue()
        {

            if (ShortcutPlayer.Instance != null && Touchscreen.current != null)
            {
                Character character = ShortcutPlayer.Instance.Get<Character>();
              
                //GET ACTIVE TOUCHES
                int activeTouches = getActiveTouches();

                if ((activeTouches == 2 && character.Player.InputDirection == Vector3.zero) || (activeTouches == 3 && character.Player.InputDirection != Vector3.zero))
                {
                    // Get the two touches
                    TouchControl touch0, touch1;
                    if (activeTouches == 3)
                    {
                         touch0 = Touchscreen.current.touches[1];
                         touch1 = Touchscreen.current.touches[2];
                    }
                    else
                    {
                         touch0 = Touchscreen.current.touches[0];
                         touch1 = Touchscreen.current.touches[1];
                    }

                    // Calculate the current and previous distance between touches
                    float prevDistance = (touch0.position.ReadValue() - touch0.delta.ReadValue() - (touch1.position.ReadValue() - touch1.delta.ReadValue())).magnitude;
                    float currentDistance = (touch0.position.ReadValue() - touch1.position.ReadValue()).magnitude;

                    // Calculate the difference in distances and adjust the zoom level
                    float zoomDelta = (prevDistance - currentDistance) * zoomSensitivity;

                    //Determine if zoom in or zoom out
                    if (zoomDelta >= 0.01)
                    {
                        return new Vector2(0f, 0.1f * zoomSensitivity);
                    }
                    else if (zoomDelta <= -0.01)
                    {
                        return new Vector2(0f, -0.1f * zoomSensitivity);
                    }
                }
                else
                {
                    // Reset when not touching
                    previousTouchDelta = Vector2.zero;
                }

            }
            return Vector2.zero;
        }

        private int getActiveTouches()
        {
            //GET ACTIVE TOUCHES
            int activeTouches = 0;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress)
                {
                    activeTouches++;
                }
            }
            return activeTouches;
        }



        private void Enable()
        {
            this.InputAction?.Enable();
        }

        private void Disable()
        {
            this.InputAction?.Disable();
        }
    }
}
