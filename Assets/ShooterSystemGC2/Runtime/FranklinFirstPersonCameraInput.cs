using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// GC2 input-provider extension used by Camera Shot. It only supplies a
    /// Vector2 to ShotSystemThirdPerson; GC2 remains the sole owner of orbit,
    /// constraints, smoothing and alignment.
    /// </summary>
    [Title("Franklin Unified Camera Orbit")]
    [Category("Franklin/Camera/Unified FPS Orbit")]
    [Description(
        "Mouse, right stick and a free touch on the right half of the screen"
    )]
    [Serializable]
    public sealed class FranklinFirstPersonCameraInput : TInputValueVector2
    {
        [NonSerialized] private InputAction m_DesktopAction;

        public override bool IsDeltaControl =>
            this.m_DesktopAction?.activeControl is DeltaControl;

        private InputAction DesktopAction
        {
            get
            {
                if (this.m_DesktopAction != null) return this.m_DesktopAction;

                this.m_DesktopAction = new InputAction(
                    "Franklin Unified Camera Orbit",
                    InputActionType.Value
                );
                this.m_DesktopAction.AddBinding(
                    "<Mouse>/delta",
                    processors: "invertVector2(invertX=false,invertY=true)"
                );
                this.m_DesktopAction.AddBinding(
                    "<Gamepad>/rightStick",
                    processors: "invertVector2(invertX=false,invertY=true)"
                );
                return this.m_DesktopAction;
            }
        }

        public override void OnStartup()
        {
            this.DesktopAction.Enable();
        }

        public override void OnDispose()
        {
            this.m_DesktopAction?.Disable();
            this.m_DesktopAction?.Dispose();
            this.m_DesktopAction = null;
        }

        public override Vector2 Read()
        {
            if (TryReadTouchDelta(out Vector2 touchDelta)) return touchDelta;
            return this.DesktopAction.ReadValue<Vector2>();
        }

        /// <summary>
        /// Reports whether the user still owns a real orbit gesture. The FPS
        /// manager uses this only to gate GC2 Alignment until release + delay.
        /// </summary>
        public static bool IsOrbitGestureHeld()
        {
            if (Mouse.current?.rightButton.isPressed == true) return true;

            Vector2 gamepad = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            if (gamepad.sqrMagnitude > 0.01f) return true;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            for (int index = 0; index < touchscreen.touches.Count; index++)
            {
                TouchControl touch = touchscreen.touches[index];
                if (!IsFreeRightTouch(touch)) continue;
                return true;
            }

            return false;
        }

        private static bool TryReadTouchDelta(out Vector2 value)
        {
            value = Vector2.zero;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            bool found = false;
            float largestDelta = -1f;
            for (int index = 0; index < touchscreen.touches.Count; index++)
            {
                TouchControl touch = touchscreen.touches[index];
                if (!IsFreeRightTouch(touch)) continue;

                // The stock mobile provider always selected the first right-side
                // touch. Choosing the moving free touch lets one finger hold Fire
                // while a second finger continues orbiting.
                Vector2 delta = touch.delta.ReadValue();
                float magnitude = delta.sqrMagnitude;
                if (found && magnitude <= largestDelta) continue;

                value = new Vector2(delta.x, -delta.y);
                largestDelta = magnitude;
                found = true;
            }

            return found;
        }

        private static bool IsFreeRightTouch(TouchControl touch)
        {
            if (touch == null || !touch.press.isPressed) return false;

            Vector2 position = touch.position.ReadValue();
            if (position.x <= Screen.width * 0.5f) return false;

            int touchId = touch.touchId.ReadValue();
            return EventSystem.current == null ||
                   !EventSystem.current.IsPointerOverGameObject(touchId);
        }
    }
}
