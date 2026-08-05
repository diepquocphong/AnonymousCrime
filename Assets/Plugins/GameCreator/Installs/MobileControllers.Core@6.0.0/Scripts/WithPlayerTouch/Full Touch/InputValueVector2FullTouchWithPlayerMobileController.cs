using GameCreator.Runtime.Characters;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameCreator.Runtime.Common
{
    [Title("Full Touch For Mobile (With Player)")]
    [Category("Mobile Controller/Camera/With Player (NEW)/Full Touch/Full Touch For Mobile (With Player)")]

    [Description("Full Touch For Mobile (With Player) commonly used to orbit the camera around the main character")]
    [Image(typeof(IconRotation), ColorTheme.Type.Yellow)]

    [Keywords("Orbit", "Touchscreen")]

    [Serializable]

    public class InputValueVector2FullTouchWithPlayerMobileController : TInputValueVector2
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
                        "Full Touch For Mobile (With Player)",
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
            if (ShortcutPlayer.Instance != null && Touchscreen.current != null)
            {

                int activeTouches = getActiveTouches();
                Character character = ShortcutPlayer.Instance.Get<Character>();
                Vector2 touchDelta = Vector2.zero;

                if (activeTouches == 1 && character.Player.InputDirection == Vector3.zero)
                {
                    touchDelta = Touchscreen.current.touches[0].delta.ReadValue();
                }
                else if (activeTouches == 2 && character.Player.InputDirection != Vector3.zero)
                {
                    touchDelta = Touchscreen.current.touches[1].delta.ReadValue();
                }
                else if (activeTouches == 3 && character.Player.InputDirection != Vector3.zero)
                {
                    touchDelta = Touchscreen.current.touches[2].delta.ReadValue();
                }
                touches = touchDelta;
            }
            return touches;
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