using System;
using GameCreator.Runtime.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Adds keyboard jump/sprint through GC2's public APIs and small accents through Gestures.
    /// Movement, grounding, gravity and root motion remain owned by GC2.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    [DefaultExecutionOrder(100)]
    public sealed class FranklinAnimationBridge : MonoBehaviour
    {
        [Header("Sprint")]
        [SerializeField] private StateCompleteLocomotion m_SprintState;
        [SerializeField] private int m_SprintLayer = 0;
        [SerializeField, Range(0f, 0.5f)] private float m_SprintTransition = 0.15f;
        [SerializeField, Range(0.5f, 2f)] private float m_SprintAnimationSpeed = 1.15f;

        [Header("Idle variations")]
        [SerializeField] private AnimationClip[] m_IdleVariations = Array.Empty<AnimationClip>();
        [SerializeField, Min(1f)] private float m_IdleVariationDelayMin = 6f;
        [SerializeField, Min(1f)] private float m_IdleVariationDelayMax = 11f;

        [Header("Blending")]
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionIn = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionOut = 0.16f;

        private const float MOVEMENT_EPSILON_SQR = 0.0025f;

        private Character m_Character;
        private float m_IdleTime;
        private float m_NextIdleVariation;
        private int m_LastIdleVariation = -1;
        private bool m_WasIdle;
        private bool m_IsSprinting;
        private float m_PreSprintSpeed;

        private void Awake()
        {
            this.m_Character = this.GetComponent<Character>();
        }

        private void OnEnable()
        {
            if (this.m_Character == null) this.m_Character = this.GetComponent<Character>();

            this.m_Character.EventJump += this.OnCharacterJump;
            this.m_Character.EventLand += this.OnCharacterLand;
        }

        private void OnDisable()
        {
            if (this.m_Character == null) return;

            this.m_Character.EventJump -= this.OnCharacterJump;
            this.m_Character.EventLand -= this.OnCharacterLand;

            if (this.m_IsSprinting) this.StopSprint(0f);
        }

        private void Update()
        {
            this.UpdateJumpInput();
            this.UpdateSprint();

            if (!this.CanPlayIdleVariation())
            {
                this.StopTrackingIdle();
                return;
            }

            if (!this.m_WasIdle)
            {
                this.m_WasIdle = true;
                this.m_IdleTime = 0f;
                this.ScheduleIdleVariation();
            }

            this.m_IdleTime += UnityEngine.Time.deltaTime;
            if (this.m_IdleTime < this.m_NextIdleVariation) return;

            AnimationClip clip = this.SelectIdleVariation();
            this.m_IdleTime = 0f;
            this.ScheduleIdleVariation();
            this.PlayGesture(clip);
        }

        private void UpdateJumpInput()
        {
            if (Keyboard.current?.spaceKey.wasPressedThisFrame != true) return;

            // GC2 decides whether the character is grounded, allowed to jump and off cooldown.
            this.m_Character?.Jump?.Do();
        }

        private void UpdateSprint()
        {
            bool canSprint = this.m_Character?.Driver?.IsGrounded == true &&
                             this.m_Character.Motion?.IsJumping != true;
            bool wantsSprint = Keyboard.current?.leftShiftKey.isPressed == true && canSprint;
            if (wantsSprint == this.m_IsSprinting) return;

            if (this.m_SprintState == null ||
                this.m_Character?.States == null ||
                this.m_Character.Motion == null)
            {
                return;
            }
            if (wantsSprint)
            {
                this.m_PreSprintSpeed = this.m_Character.Motion.LinearSpeed;
                this.m_IsSprinting = true;

                ConfigState config = new ConfigState(
                    0f,
                    this.m_SprintAnimationSpeed,
                    1f,
                    this.m_SprintTransition,
                    this.m_SprintTransition
                );

                _ = this.m_Character.States.SetState(
                    this.m_SprintState,
                    this.m_SprintLayer,
                    BlendMode.Blend,
                    config
                );
            }
            else
            {
                this.StopSprint(this.m_SprintTransition);
            }
        }

        private void StopSprint(float transition)
        {
            this.m_Character.States?.Stop(this.m_SprintLayer, 0f, transition);

            // No custom base State is installed. Restore the speed that GC2/player used
            // immediately before sprint started, then let GC2 continue normally.
            if (this.m_Character.Motion != null)
            {
                this.m_Character.Motion.LinearSpeed = this.m_PreSprintSpeed;
            }

            this.m_IsSprinting = false;
        }

        private bool CanPlayIdleVariation()
        {
            if (this.m_Character == null) return false;
            if (this.m_IdleVariations == null || this.m_IdleVariations.Length == 0) return false;
            if (this.m_Character.Driver == null || this.m_Character.Motion == null) return false;
            if (!this.m_Character.Driver.IsGrounded || this.m_Character.Motion.IsJumping) return false;
            if (this.m_Character.Driver.WorldMoveDirection.sqrMagnitude > MOVEMENT_EPSILON_SQR)
            {
                return false;
            }

            // Never replace a gesture started by GC2 gameplay, combat or interaction systems.
            return !this.m_Character.Gestures.IsPlaying;
        }

        private AnimationClip SelectIdleVariation()
        {
            int count = this.m_IdleVariations.Length;
            int index = UnityEngine.Random.Range(0, count);

            // Avoid showing the same idle twice in a row when alternatives are available.
            if (count > 1 && index == this.m_LastIdleVariation)
            {
                index = (index + UnityEngine.Random.Range(1, count)) % count;
            }

            this.m_LastIdleVariation = index;
            return this.m_IdleVariations[index];
        }

        private void OnCharacterJump(float force)
        {
            this.StopTrackingIdle();
            if (this.m_IsSprinting) this.StopSprint(this.m_SprintTransition);
        }

        private void OnCharacterLand(float verticalSpeed)
        {
            this.StopTrackingIdle();
        }

        private void PlayGesture(AnimationClip clip)
        {
            if (clip == null || this.m_Character == null) return;

            ConfigGesture config = new ConfigGesture(
                0f,
                clip.length,
                1f,
                false,
                this.m_TransitionIn,
                this.m_TransitionOut
            );

            // Root motion is disabled and previous GC2 gestures are never cancelled.
            _ = this.m_Character.Gestures.CrossFade(
                clip,
                null,
                BlendMode.Blend,
                config,
                false
            );
        }

        private void StopTrackingIdle()
        {
            if (!this.m_WasIdle) return;

            this.m_WasIdle = false;
            this.m_IdleTime = 0f;
        }

        private void ScheduleIdleVariation()
        {
            float minimum = Mathf.Min(this.m_IdleVariationDelayMin, this.m_IdleVariationDelayMax);
            float maximum = Mathf.Max(this.m_IdleVariationDelayMin, this.m_IdleVariationDelayMax);
            this.m_NextIdleVariation = UnityEngine.Random.Range(minimum, maximum);
        }
    }
}
