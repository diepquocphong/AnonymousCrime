using System;
using System.Collections.Generic;
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
        private const float POINTER_DELTA_EPSILON = 0.0001f;

        // A touch is only classified as an orbit gesture when it first moves in
        // the free right-hand gameplay area. After that, keep the exact control
        // captured until its physical press ends. Rechecking screen-half/UI on
        // every frame made a stationary finger look released after it crossed a
        // boundary, which let GC2 auto-align while the finger was still down.
        private static TouchControl s_CapturedOrbitTouch;
        private static int s_CapturedOrbitTouchId = int.MinValue;
        private static bool s_MouseOrbitCaptured;
        private static readonly List<RaycastResult> s_UiRaycastResults = new(16);
        private static EventSystem s_UiEventSystem;
        private static PointerEventData s_UiPointerData;

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

            // Device Simulator can report the same physical gesture as both a
            // Touchscreen press and Mouse delta. Never let the mouse binding
            // bypass touch ownership/UI classification, otherwise GC2 receives
            // orbit input while the alignment gate sees no held gesture.
            if (HasAnyPressedTouch()) return ReadGamepadOrbit();

            Vector2 desktop = this.DesktopAction.ReadValue<Vector2>();
            Mouse mouse = Mouse.current;
            if (this.m_DesktopAction.activeControl?.device == mouse &&
                !s_MouseOrbitCaptured && mouse != null &&
                IsOverInteractiveUi(mouse.position.ReadValue(), -1))
            {
                return ReadGamepadOrbit();
            }

            return desktop;
        }

        /// <summary>
        /// Reports whether the user still owns a real orbit gesture. The FPS
        /// manager uses this only to gate GC2 Alignment until release + delay.
        /// </summary>
        public static bool IsOrbitGestureHeld()
        {
            if (IsCapturedTouchHeld()) return true;
            if (TryCaptureMovingTouch(out _)) return true;

            if (IsMouseOrbitGestureHeld()) return true;

            Vector2 gamepad = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            if (gamepad.sqrMagnitude > 0.01f) return true;
            return false;
        }

        /// <summary>
        /// True only while the currently owned orbit control has real input in
        /// this frame. The FPS manager uses this rising motion to cancel GC2's
        /// remaining auto-alignment damping before GC2 consumes the same delta.
        /// </summary>
        public static bool HasOrbitInputThisFrame()
        {
            TouchControl touch = GetCapturedTouch();
            if (touch != null &&
                touch.delta.ReadValue().sqrMagnitude > POINTER_DELTA_EPSILON)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (s_MouseOrbitCaptured && mouse != null &&
                (mouse.rightButton.isPressed || mouse.leftButton.isPressed) &&
                mouse.delta.ReadValue().sqrMagnitude > POINTER_DELTA_EPSILON)
            {
                return true;
            }

            Vector2 gamepad = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            return gamepad.sqrMagnitude > 0.01f;
        }

        private static Vector2 ReadGamepadOrbit()
        {
            Vector2 value = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            return new Vector2(value.x, -value.y);
        }

        private static bool TryReadTouchDelta(out Vector2 value)
        {
            value = Vector2.zero;
            TouchControl touch = GetCapturedTouch();
            if (touch == null && !TryCaptureMovingTouch(out touch)) return false;

            Vector2 delta = touch.delta.ReadValue();
            value = new Vector2(delta.x, -delta.y);
            return true;
        }

        private static TouchControl GetCapturedTouch()
        {
            if (!IsCapturedTouchHeld()) return null;
            return s_CapturedOrbitTouch;
        }

        private static bool IsCapturedTouchHeld()
        {
            TouchControl touch = s_CapturedOrbitTouch;
            if (touch != null && touch.device is Touchscreen touchscreen &&
                touchscreen.added && touch.press.isPressed &&
                touch.touchId.ReadValue() == s_CapturedOrbitTouchId)
            {
                return true;
            }

            s_CapturedOrbitTouch = null;
            s_CapturedOrbitTouchId = int.MinValue;
            return false;
        }

        private static bool TryCaptureMovingTouch(out TouchControl captured)
        {
            captured = null;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            float largestDelta = POINTER_DELTA_EPSILON;
            for (int index = 0; index < touchscreen.touches.Count; index++)
            {
                TouchControl touch = touchscreen.touches[index];
                if (!CanBeginOrbit(touch)) continue;

                // Capture the moving free touch instead of an unrelated held
                // finger. This preserves Fire + orbit multi-touch behavior.
                float magnitude = touch.delta.ReadValue().sqrMagnitude;
                if (magnitude <= largestDelta) continue;

                captured = touch;
                largestDelta = magnitude;
            }

            if (captured == null) return false;
            s_CapturedOrbitTouch = captured;
            s_CapturedOrbitTouchId = captured.touchId.ReadValue();
            return true;
        }

        private static bool CanBeginOrbit(TouchControl touch)
        {
            if (touch == null || !touch.press.isPressed) return false;

            Vector2 position = touch.position.ReadValue();
            if (position.x <= Screen.width * 0.5f) return false;

            int touchId = touch.touchId.ReadValue();
            return !IsOverInteractiveUi(position, touchId);
        }

        private static bool IsMouseOrbitGestureHeld()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                s_MouseOrbitCaptured = false;
                return false;
            }

            bool rightPressed = mouse.rightButton.isPressed;
            bool leftPressed = mouse.leftButton.isPressed;
            if (!rightPressed && !leftPressed)
            {
                s_MouseOrbitCaptured = false;
                return false;
            }

            if (s_MouseOrbitCaptured) return true;

            // Keep the existing desktop right-button behavior. Left-button
            // capture is only a Simulator/Game-view fallback when no real touch
            // is currently pressed.
            if (rightPressed)
            {
                s_MouseOrbitCaptured = true;
                return true;
            }

            if (HasAnyPressedTouch()) return false;

            if (mouse.delta.ReadValue().sqrMagnitude <= POINTER_DELTA_EPSILON)
                return false;
            if (mouse.position.ReadValue().x <= Screen.width * 0.5f)
                return false;
            if (IsOverInteractiveUi(mouse.position.ReadValue(), -1)) return false;

            s_MouseOrbitCaptured = true;
            return true;
        }

        private static bool HasAnyPressedTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            for (int index = 0; index < touchscreen.touches.Count; index++)
            {
                if (touchscreen.touches[index].press.isPressed) return true;
            }

            return false;
        }

        private static bool IsOverInteractiveUi(Vector2 position, int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (s_UiEventSystem != eventSystem || s_UiPointerData == null)
            {
                s_UiEventSystem = eventSystem;
                s_UiPointerData = new PointerEventData(eventSystem);
            }
            else
            {
                s_UiPointerData.Reset();
            }

            s_UiPointerData.pointerId = pointerId;
            s_UiPointerData.position = position;
            s_UiRaycastResults.Clear();
            eventSystem.RaycastAll(s_UiPointerData, s_UiRaycastResults);

            for (int index = 0; index < s_UiRaycastResults.Count; index++)
            {
                GameObject target = s_UiRaycastResults[index].gameObject;
                if (target == null) continue;

                if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IBeginDragHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IDragHandler>(target) != null)
                {
                    s_UiRaycastResults.Clear();
                    return true;
                }
            }

            s_UiRaycastResults.Clear();
            return false;
        }
    }
}
