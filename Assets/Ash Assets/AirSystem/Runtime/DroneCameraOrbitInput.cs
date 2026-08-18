using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// AirSystem input provider for GC2 ShotSystemThirdPerson. GC2 remains the
    /// owner of orbit smoothing, pitch limits and automatic alignment.
    /// </summary>
    [Title("Franklin Drone Camera Orbit")]
    [Category("Franklin/Air/Drone Camera Orbit")]
    [Description("Touch drag, right mouse drag and the gamepad right stick")]
    [Serializable]
    public sealed class DroneCameraOrbitInput : TInputValueVector2
    {
        private const float GAMEPAD_DELTA_SCALE = 15f;

        private static Vector2 s_TouchDelta;
        private static int s_TouchDeltaFrame = -1;

        public override bool IsDeltaControl => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetTouchInput();
        }

        public override Vector2 Read()
        {
            if (s_TouchDeltaFrame == Time.frameCount) return s_TouchDelta;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.001f)
                {
                    return new Vector2(stick.x, -stick.y) * GAMEPAD_DELTA_SCALE;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                return new Vector2(delta.x, -delta.y);
            }

            return Vector2.zero;
        }

        public static void SubmitTouchDelta(Vector2 screenDelta)
        {
            Vector2 delta = new(screenDelta.x, -screenDelta.y);
            s_TouchDelta = s_TouchDeltaFrame == Time.frameCount
                ? s_TouchDelta + delta
                : delta;
            s_TouchDeltaFrame = Time.frameCount;
        }

        public static void ResetTouchInput()
        {
            s_TouchDelta = Vector2.zero;
            s_TouchDeltaFrame = -1;
        }
    }
}
