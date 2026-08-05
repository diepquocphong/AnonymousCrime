using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Full Touch For Mobile (Invert Y)")]
    [Category("Mobile Controller/Camera/Full Touch/Full Touch For Mobile (Invert Y)")]

    [Description("Full Touch For Mobile commonly used to orbit the camera around the main character")]
    [Image(typeof(IconRotation), ColorTheme.Type.Yellow)]

    [Keywords("Orbit", "Touchscreen")]

    [Serializable]
    public class InputValueVector2FullTouchMobileControllerY : TInputValueVector2
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
                        "Full Touch For Mobile",
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
                    touches = new Vector2(touchDelta.x, -touchDelta.y);
                    break;
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
