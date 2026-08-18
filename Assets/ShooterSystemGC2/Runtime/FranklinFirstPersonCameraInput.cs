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
        "Mouse, right stick, Fire-drag and a free touch on the right half of the screen"
    )]
    [Serializable]
    public sealed class FranklinFirstPersonCameraInput : TInputValueVector2
    {
        private const float POINTER_DELTA_EPSILON = 0.0001f;

        // A free touch is classified as orbit when it first moves in the right-hand
        // gameplay area. The Fire touch becomes eligible only after EventSystem's
        // drag threshold. Once selected, keep the exact control captured until its
        // physical press ends; zero delta is not a release signal.
        private static TouchControl s_CapturedOrbitTouch;
        private static int s_CapturedOrbitTouchId = int.MinValue;
        private static int s_ReservedFireTouchId = int.MinValue;
        private static bool s_FireTouchOrbitEnabled;
        private static bool s_MouseOrbitCaptured;
        private static readonly List<RaycastResult> s_UiRaycastResults = new(16);
        private static EventSystem s_UiEventSystem;
        private static PointerEventData s_UiPointerData;
        private static int s_TouchCaptureScanFrame = -1;
        private static TouchControl s_TouchCaptureScanResult;
        private static int s_TouchCaptureScanId = int.MinValue;

        [NonSerialized] private InputAction m_DesktopAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Input providers can outlive a scene when Domain Reload is disabled in
            // the Editor. Never retain a TouchControl, EventSystem or its raycast
            // references into the next session.
            s_CapturedOrbitTouch = null;
            s_CapturedOrbitTouchId = int.MinValue;
            s_ReservedFireTouchId = int.MinValue;
            s_FireTouchOrbitEnabled = false;
            s_MouseOrbitCaptured = false;
            s_UiEventSystem = null;
            s_UiPointerData = null;
            s_UiRaycastResults.Clear();
            s_TouchCaptureScanFrame = -1;
            s_TouchCaptureScanResult = null;
            s_TouchCaptureScanId = int.MinValue;
        }

        public override bool IsDeltaControl =>
            this.m_DesktopAction?.activeControl is DeltaControl;

        public static void SetFireTouchReserved(int touchId, bool reserved)
        {
            if (touchId == 0 || touchId == int.MinValue) return;

            if (reserved)
            {
                s_ReservedFireTouchId = touchId;
                s_FireTouchOrbitEnabled = false;
                if (s_CapturedOrbitTouchId == touchId)
                {
                    s_CapturedOrbitTouch = null;
                    s_CapturedOrbitTouchId = int.MinValue;
                }

                InvalidateTouchCaptureScan();
                return;
            }

            if (s_ReservedFireTouchId != touchId) return;

            if (s_CapturedOrbitTouchId == touchId)
            {
                s_CapturedOrbitTouch = null;
                s_CapturedOrbitTouchId = int.MinValue;
            }

            s_ReservedFireTouchId = int.MinValue;
            s_FireTouchOrbitEnabled = false;
            InvalidateTouchCaptureScan();
        }

        public static void SetFireTouchOrbitEnabled(int touchId)
        {
            if (touchId == int.MinValue || touchId != s_ReservedFireTouchId) return;
            if (s_FireTouchOrbitEnabled) return;

            s_FireTouchOrbitEnabled = true;
            InvalidateTouchCaptureScan();
        }

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
                touch.touchId.ReadValue() == s_CapturedOrbitTouchId &&
                (s_CapturedOrbitTouchId != s_ReservedFireTouchId ||
                 s_FireTouchOrbitEnabled))
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

            // GC2 can ask the input provider more than once in one render frame
            // (Read + the alignment gate). Input state is frame-stable, so reuse
            // the classification and avoid repeating every touch/UI raycast.
            int frame = Time.frameCount;
            if (s_TouchCaptureScanFrame == frame)
            {
                captured = s_TouchCaptureScanResult;
                return captured != null && captured.press.isPressed &&
                       captured.touchId.ReadValue() == s_TouchCaptureScanId &&
                       (s_TouchCaptureScanId != s_ReservedFireTouchId ||
                        s_FireTouchOrbitEnabled);
            }

            s_TouchCaptureScanFrame = frame;
            s_TouchCaptureScanResult = null;
            s_TouchCaptureScanId = int.MinValue;

            float largestDelta = POINTER_DELTA_EPSILON;
            for (int index = 0; index < touchscreen.touches.Count; index++)
            {
                TouchControl touch = touchscreen.touches[index];
                if (touch == null || !touch.press.isPressed) continue;

                int touchId = touch.touchId.ReadValue();
                bool isFireOrbit = touchId == s_ReservedFireTouchId &&
                                   s_FireTouchOrbitEnabled;
                if (touchId == s_ReservedFireTouchId && !isFireOrbit) continue;

                // Capture the moving free touch instead of an unrelated held
                // finger. This preserves Fire + orbit multi-touch behavior.
                float magnitude = touch.delta.ReadValue().sqrMagnitude;
                if (magnitude <= largestDelta) continue;

                // UI raycasts are the expensive part of classifying a new orbit
                // gesture. Reject stationary/left-side touches first so held HUD
                // buttons do not repeatedly raycast the Canvas while another
                // input provider asks for the same camera state.
                Vector2 position = touch.position.ReadValue();
                if (!isFireOrbit)
                {
                    if (position.x <= Screen.width * 0.5f) continue;
                    if (IsOverInteractiveUi(position, touchId)) continue;
                }

                captured = touch;
                largestDelta = magnitude;
            }

            if (captured == null) return false;
            s_CapturedOrbitTouch = captured;
            s_CapturedOrbitTouchId = captured.touchId.ReadValue();
            s_TouchCaptureScanResult = captured;
            s_TouchCaptureScanId = s_CapturedOrbitTouchId;
            return true;
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

        private static void InvalidateTouchCaptureScan()
        {
            s_TouchCaptureScanFrame = -1;
            s_TouchCaptureScanResult = null;
            s_TouchCaptureScanId = int.MinValue;
        }
    }
}
