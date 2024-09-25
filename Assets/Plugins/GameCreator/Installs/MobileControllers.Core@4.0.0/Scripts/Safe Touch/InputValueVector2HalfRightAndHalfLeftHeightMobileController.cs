using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Safe Touch For Mobile")]
    [Category("Mobile Controller/Camera/Safe Touch/Safe Touch For Mobile")]

    [Description("Safe Touch For Mobile commonly used to orbit the camera around the main character, This touch ignore the half-left width and half-height bottom of the screen.")]
    [Image(typeof(IconRotation), ColorTheme.Type.Yellow)]

    [Keywords("Orbit", "Touchscreen")]

    [Serializable]
    public class InputValueVector2HalfRightAndHalfLeftHeightMobileController : TInputValueVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private InputAction m_InputAction;

        // PROPERTIES: ----------------------------------------------------------------------------

        public InputAction InputAction
        {
            get
            {
                if (this.m_InputAction == null)
                {
                    this.m_InputAction = new InputAction(
                        "Safe Touch For Mobile",
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
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                return GetLeftHalfTouchInput();
            }
            else
            {
                return this.InputAction?.ReadValue<Vector2>() ?? Vector2.zero;
            }

        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private Vector2 GetLeftHalfTouchInput()
        {
            Vector2 touches = Vector2.zero;
            if (Touchscreen.current != null)
            {

                int activeTouches = Touchscreen.current.touches.Count;

                for (int i = 0; i < activeTouches; i++)
                {
                    // Get the position and delta of the current touch
                    Vector2 touchPosition = Touchscreen.current.touches[i].position.ReadValue();
                    Vector2 touchDelta = Touchscreen.current.touches[i].delta.ReadValue();
                    if (touchPosition.x > Screen.width / 2 || (touchPosition.y > Screen.height / 2 && touchPosition.x < Screen.width / 2))
                    {
                        touches = touchDelta;
                    break;
                    }
                }
            }

            return touches;
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
