using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.Animations
{
    public enum RunTransitionMotionMode
    {
        Gc2Motion = 0,
        AnimationRootMotion = 1
    }

    /// <summary>
    /// Adds keyboard jump/sprint through GC2's public APIs and small accents through Gestures.
    /// Movement, grounding, gravity and root motion remain owned by GC2.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class FranklinAnimationBridge : MonoBehaviour
    {
        private enum VirtualAutoRunMode
        {
            None = 0,
            Jog = 1,
            Sprint = 2
        }

        [Header("Character target")]
        [SerializeField]
        [Tooltip("The GC2 Character driven by this bridge. It is resolved from a parent when omitted.")]
        private Character m_Player;

        [Header("GTA locomotion")]
        [SerializeField]
        [Tooltip("GC2's stock Run state, used as the light jog while Left Shift is held")]
        private State m_JogState;
        [SerializeField, Range(0f, 0.5f)] private float m_JogTransition = 0.12f;
        [SerializeField, Min(0.1f)]
        [Tooltip("GC2 Run speed used as the baseline when maximum sprint starts from quick Shift taps")]
        private float m_JogSpeed = 4f;

        [Header("Sprint")]
        [SerializeField] private StateCompleteLocomotion m_SprintState;
        [SerializeField] private int m_SprintLayer = 0;
        [SerializeField, Range(0f, 0.5f)] private float m_SprintTransition = 0.15f;
        [SerializeField, Range(0.5f, 2f)] private float m_SprintAnimationSpeed = 1.15f;
        [SerializeField, Min(0.1f)]
        [Tooltip("Target movement speed while sprinting. This does not change GC2's Walk or Run states")]
        private float m_RunSpeed = 6f;
        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Maximum time between Left Shift taps that counts as a sprint tap sequence")]
        private float m_SprintTapWindow = 0.35f;
        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("How long another Left Shift tap can be delayed before sprint falls back to jog or walk")]
        private float m_SprintTapGrace = 0.45f;
        [SerializeField, Range(2, 4)]
        [Tooltip("Number of quick Left Shift taps required to start sprint")]
        private int m_SprintTapsRequired = 2;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("How closely the movement input must point towards the camera before sprint can start")]
        private float m_RunForwardInputThreshold = 0.5f;
        [SerializeField, Range(0f, 45f)]
        [Tooltip("Maximum yaw error in degrees before sprint movement and Max Yaw can begin")]
        private float m_RunCameraAlignmentAngle = 8f;
        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Smooths camera-orbit direction before it steers held Jog/Sprint. Zero follows instantly")]
        private float m_RunCameraDirectionSmoothTime = 0.08f;

        [Header("Damage locomotion")]
        [SerializeField]
        [Tooltip("GC2 Traits Attribute ID watched for health loss")]
        private string m_HealthAttributeId = "hp";
        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds without further HP loss before normal Walk/Jog/Sprint controls return")]
        private float m_DamageJogDuration = 6f;

        [Header("Run transitions")]
        [SerializeField] private AnimationClip m_RunStart;
        [SerializeField] private AnimationClip m_RunStopLeft;
        [SerializeField] private AnimationClip m_RunStopRight;
        [SerializeField]
        [Tooltip("GC2 Motion uses kinematic easing; Animation Root Motion follows RootT/RootQ from Run Start/Stop clips")]
        private RunTransitionMotionMode m_RunTransitionMotionMode =
            RunTransitionMotionMode.AnimationRootMotion;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to lerp from normal movement speed to sprint speed while Run Start plays")]
        private float m_RunStartAccelerationTime = 0.77f;
        [SerializeField, Range(1f, 4f)]
        [Tooltip("Higher values keep the beginning of Run Start slower and accelerate harder near the end")]
        private float m_RunStartAccelerationPower = 2.2f;
        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds used to ease sprint speed and GC2 deceleration into a Run Stop")]
        private float m_RunStopEaseOutTime = 1f;
        [SerializeField, Range(1f, 4f)]
        [Tooltip("Higher values shed more speed early and settle more gently near the end")]
        private float m_RunStopEaseOutPower = 2f;

        [Header("Jump combos")]
        [SerializeField] private AnimationClip m_JumpPlace;
        [SerializeField] private AnimationClip m_JumpWalkLeft;
        [SerializeField] private AnimationClip m_JumpWalkRight;
        [SerializeField] private AnimationClip m_JumpRunLeft;
        [SerializeField] private AnimationClip m_JumpRunRight;
        [SerializeField, Min(0.5f)] private float m_JumpVisualDuration = 1.05f;
        [SerializeField, Min(0.05f)]
        [Tooltip("Approximate jump apex height in meters, converted with GC2's current upward gravity")]
        private float m_JumpHeight = 1.275f;

        [Header("Idle variations")]
        [SerializeField] private AnimationClip[] m_IdleVariations = Array.Empty<AnimationClip>();
        [SerializeField, Min(1f)] private float m_IdleVariationDelayMin = 6f;
        [SerializeField, Min(1f)] private float m_IdleVariationDelayMax = 11f;

        [Header("Blending")]
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionIn = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionOut = 0.16f;

        private const float MOVEMENT_EPSILON_SQR = 0.0025f;
        private const float RUN_STOP_GRACE_SECONDS = 0.75f;
        private const float CAMERA_LOOKUP_RETRY_SECONDS = 1f;

        private Character m_Character;
        private Transform m_CharacterTransform;
        private Transform m_ModelRoot;
        private Transform m_ModelRootParent;
        private Vector3 m_ModelRootLocalPosition;
        private Quaternion m_ModelRootLocalRotation;
        private Vector3 m_ModelRootLocalScale;
        private bool m_HasModelRootBaseline;
        private Transform m_RunCameraTransform;
        private Keyboard m_Keyboard;
        private float m_RunCameraAlignmentDot;
        private float m_NextCameraLookupTime;
        private Traits m_Traits;
        private RuntimeAttributeData m_HealthAttribute;
        private double m_LastHealth;
        private bool m_IsHealthDanger;
        private float m_HealthDangerDeadline = float.NegativeInfinity;
        private bool m_IsHealthMonitorBound;
        private bool m_HasWarnedMissingHealthAttribute;
        private float m_IdleTime;
        private float m_NextIdleVariation;
        private int m_LastIdleVariation = -1;
        private bool m_WasIdle;
        private bool m_IsJogging;
        private bool m_IsSprinting;
        private bool m_VirtualJogHeld;
        private bool m_VirtualSprintHeld;
        // Kept independently from the press-and-hold touch inputs so a HUD tap persists.
        private VirtualAutoRunMode m_VirtualAutoRunMode;
        private int m_SprintTapCount;
        private float m_LastSprintTapTime = float.NegativeInfinity;
        private float m_SprintTapDeadline = float.NegativeInfinity;
        private float m_PreSprintSpeed;
        private float m_SprintTargetSpeed;
        private float m_RunStartAccelerationElapsed;
        private bool m_IsSprintAccelerating;
        private float m_RunStopEaseOutElapsed;
        private float m_RunStopStartLinearSpeed;
        private bool m_IsRunStopEasing;
        private bool m_HasRunStopMotionOverride;
        private bool m_RunStopOriginalUseAcceleration;
        private float m_RunStopOriginalDeceleration;
        private bool m_RunStartPending;
        private bool m_RunStopPending;
        private float m_RunStopDeadline;
        private bool m_HasSprintMovement;
        private bool m_HasForwardSprintInput;
        private bool m_IsRunCameraAligned;
        private bool m_HasSprintCameraMotionControl;
        private bool m_HasSmoothedRunCameraDirection;
        private Vector3 m_SmoothedRunCameraDirection;
        private bool m_HasDisabledSprintRootMotionRotation;
        private bool m_IsExternalAnimationLocked;
        private int m_RunCameraFacingLayer = -1;
        private bool m_UseLeftRunStop;
        private bool m_UseLeftJump;
        private AnimationClip m_ActiveCustomGesture;
        private float m_ActiveCustomGestureUntil;
        private bool m_IsIdleGestureActive;
        private bool m_HasWarnedMissingTransitionRootMotion;

        public float JumpHeight
        {
            get => this.m_JumpHeight;
            set
            {
                this.m_JumpHeight = Mathf.Max(0.05f, value);
                if (Application.isPlaying) this.ApplyJumpHeight();
            }
        }

        public float RunSpeed
        {
            get => this.m_RunSpeed;
            set => this.m_RunSpeed = Mathf.Max(0.1f, value);
        }

        public bool IsSprintRunning => this.m_IsSprinting &&
                                        this.m_HasSprintMovement &&
                                        this.m_HasForwardSprintInput;

        public bool IsRunCameraAligned => this.m_IsRunCameraAligned;

        public bool IsExternalAnimationLocked => this.m_IsExternalAnimationLocked;

        /// <summary>
        /// Lets a touch HUD request GC2's light jog without simulating a keyboard device.
        /// </summary>
        public void SetVirtualJogInput(bool isHeld)
        {
            this.m_VirtualJogHeld = isHeld;
        }

        /// <summary>
        /// Lets a touch HUD hold maximum sprint directly. Keyboard still uses the GTA-style
        /// repeated Shift tap sequence configured on this component.
        /// </summary>
        public void SetVirtualSprintInput(bool isHeld)
        {
            this.m_VirtualSprintHeld = isHeld;
        }

        /// <summary>
        /// Toggles touch auto-jog. GC2 moves forward in the direction the camera is facing,
        /// so no movement-stick input is needed.
        /// </summary>
        public void ToggleVirtualJogAutoRun()
        {
            this.m_VirtualAutoRunMode = this.m_VirtualAutoRunMode == VirtualAutoRunMode.Jog
                ? VirtualAutoRunMode.None
                : VirtualAutoRunMode.Jog;
        }

        /// <summary>
        /// Toggles touch auto-sprint. It replaces auto-jog and uses camera-forward movement.
        /// </summary>
        public void ToggleVirtualSprintAutoRun()
        {
            this.m_VirtualAutoRunMode = this.m_VirtualAutoRunMode == VirtualAutoRunMode.Sprint
                ? VirtualAutoRunMode.None
                : VirtualAutoRunMode.Sprint;
        }

        /// <summary>Clears the persistent touch auto-run selection.</summary>
        public void StopVirtualAutoRun()
        {
            this.m_VirtualAutoRunMode = VirtualAutoRunMode.None;
        }

        public float RunStartAccelerationTime
        {
            get => this.m_RunStartAccelerationTime;
            set => this.m_RunStartAccelerationTime = Mathf.Max(0f, value);
        }

        public float RunStartAccelerationPower
        {
            get => this.m_RunStartAccelerationPower;
            set => this.m_RunStartAccelerationPower = Mathf.Clamp(value, 1f, 4f);
        }

        public float RunStopEaseOutTime
        {
            get => this.m_RunStopEaseOutTime;
            set => this.m_RunStopEaseOutTime = Mathf.Max(0.05f, value);
        }

        public float RunStopEaseOutPower
        {
            get => this.m_RunStopEaseOutPower;
            set => this.m_RunStopEaseOutPower = Mathf.Clamp(value, 1f, 4f);
        }

        public RunTransitionMotionMode TransitionMotionMode
        {
            get => this.m_RunTransitionMotionMode;
            set => this.m_RunTransitionMotionMode = value;
        }

        /// <summary>
        /// Temporarily suppresses this bridge while another gameplay system owns the body,
        /// such as a vehicle enter or exit sequence.
        /// </summary>
        public void SetExternalAnimationLock(bool isLocked)
        {
            if (this.m_IsExternalAnimationLocked == isLocked) return;
            this.m_IsExternalAnimationLocked = isLocked;
            if (!isLocked) return;

            this.StopVirtualAutoRun();
            this.StopTrackingIdle();
            this.StopOwnedCustomGesture();
            this.m_RunStartPending = false;
            this.m_RunStopPending = false;
            this.m_HasSprintMovement = false;
            this.ReleaseSprintCameraDirection();
            this.FinishRunStopEaseOut(false);

            if (this.m_IsSprinting) this.StopSprint(0f);
            else if (this.m_IsJogging) this.StopJog(0f);
        }

        private void Awake()
        {
            if (!this.ResolveCharacter())
            {
                this.enabled = false;
                return;
            }

            this.CaptureModelRootBaseline();
            this.RefreshRuntimeCaches();
            this.ApplyJumpHeight();
        }

        /// <summary>
        /// GC2 Default Ragdoll reparents its Animator with worldPositionStays.
        /// Restore the original model-local offset without moving the Character
        /// root, so every vehicle seat receives the same skeleton height.
        /// </summary>
        public bool RestoreModelRootBaseline()
        {
            Animator animator = this.m_Character?.Animim?.Animator;
            if (!this.m_HasModelRootBaseline || animator == null ||
                animator.transform != this.m_ModelRoot || this.m_ModelRoot == null)
            {
                return false;
            }

            if (this.m_ModelRootParent != null &&
                this.m_ModelRoot.parent != this.m_ModelRootParent)
            {
                this.m_ModelRoot.SetParent(this.m_ModelRootParent, false);
            }

            this.m_ModelRoot.SetLocalPositionAndRotation(
                this.m_ModelRootLocalPosition,
                this.m_ModelRootLocalRotation
            );
            this.m_ModelRoot.localScale = this.m_ModelRootLocalScale;
            Physics.SyncTransforms();
            return true;
        }

        private void CaptureModelRootBaseline()
        {
            Animator animator = this.m_Character?.Animim?.Animator;
            if (animator == null) return;

            this.m_ModelRoot = animator.transform;
            this.m_ModelRootParent = this.m_ModelRoot.parent;
            this.m_ModelRootLocalPosition = this.m_ModelRoot.localPosition;
            this.m_ModelRootLocalRotation = this.m_ModelRoot.localRotation;
            this.m_ModelRootLocalScale = this.m_ModelRoot.localScale;
            this.m_HasModelRootBaseline = true;
        }

        private void OnValidate()
        {
            this.ResolveCharacter();
            this.m_RunSpeed = Mathf.Max(0.1f, this.m_RunSpeed);
            this.m_JogSpeed = Mathf.Max(0.1f, this.m_JogSpeed);
            this.m_SprintTapWindow = Mathf.Clamp(this.m_SprintTapWindow, 0.1f, 1f);
            this.m_SprintTapGrace = Mathf.Clamp(this.m_SprintTapGrace, 0.1f, 1f);
            this.m_SprintTapsRequired = Mathf.Clamp(this.m_SprintTapsRequired, 2, 4);
            if (string.IsNullOrWhiteSpace(this.m_HealthAttributeId)) this.m_HealthAttributeId = "hp";
            this.m_DamageJogDuration = Mathf.Max(0.1f, this.m_DamageJogDuration);
            this.m_RunForwardInputThreshold = Mathf.Clamp01(this.m_RunForwardInputThreshold);
            this.m_RunCameraAlignmentAngle = Mathf.Clamp(this.m_RunCameraAlignmentAngle, 0f, 45f);
            this.m_RunCameraDirectionSmoothTime = Mathf.Clamp(
                this.m_RunCameraDirectionSmoothTime,
                0f,
                0.5f
            );
            this.RefreshRuntimeCaches();
            this.m_JumpHeight = Mathf.Max(0.05f, this.m_JumpHeight);
            this.m_RunStartAccelerationTime = Mathf.Max(0f, this.m_RunStartAccelerationTime);
            this.m_RunStartAccelerationPower = Mathf.Clamp(
                this.m_RunStartAccelerationPower,
                1f,
                4f
            );
            this.m_RunStopEaseOutTime = Mathf.Max(0.05f, this.m_RunStopEaseOutTime);
            this.m_RunStopEaseOutPower = Mathf.Clamp(this.m_RunStopEaseOutPower, 1f, 4f);
            if (Application.isPlaying) this.ApplyJumpHeight();
        }

        private void OnEnable()
        {
            if (!this.ResolveCharacter())
            {
                this.enabled = false;
                return;
            }

            this.m_Character.EventJump += this.OnCharacterJump;
            this.m_Character.EventLand += this.OnCharacterLand;
            this.BindHealthMonitor();
        }

        private void OnDisable()
        {
            this.m_VirtualJogHeld = false;
            this.m_VirtualSprintHeld = false;
            this.StopVirtualAutoRun();
            if (this.m_Character == null) return;

            this.m_Character.EventJump -= this.OnCharacterJump;
            this.m_Character.EventLand -= this.OnCharacterLand;
            this.UnbindHealthMonitor();

            this.ReleaseSprintCameraDirection();
            this.FinishRunStopEaseOut(false);
            if (this.m_IsSprinting) this.StopSprint(0f);
            else if (this.m_IsJogging) this.StopJog(0f);
        }

        private bool ResolveCharacter()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            this.m_Character = this.m_Player;
            if (this.m_Character == null) return false;

            this.m_CharacterTransform = this.m_Character.transform;
            return true;
        }

        private void Update()
        {
            // Resolve the Input System device once for this frame. On mobile this remains null.
            this.m_Keyboard = Keyboard.current;
            if (this.m_IsExternalAnimationLocked)
            {
                this.StopTrackingIdle();
                return;
            }

            this.UpdateJumpInput();
            this.UpdateHealthDanger();
            this.UpdateIdleGestureInterruption();
            this.UpdateSprintTapInput();
            this.UpdateSprintCameraDirection();
            this.UpdateSprint();
            this.UpdateJog();
            this.UpdateRunTransitions();
            this.UpdateSprintAcceleration();
            this.UpdateRunStopEaseOut();

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
            if (this.m_Keyboard?.spaceKey.wasPressedThisFrame != true) return;
            this.RequestVirtualJump();
        }

        /// <summary>
        /// Requests one jump from the mobile HUD. GC2 still enforces grounded state and cooldown.
        /// </summary>
        public void RequestVirtualJump()
        {
            if (this.m_IsExternalAnimationLocked) return;
            if (this.m_Character?.Motion == null || this.m_Character.Jump == null) return;

            // Convert the requested apex height to GC2's launch velocity. GC2 still decides
            // whether the character is grounded, allowed to jump and off cooldown.
            this.ApplyJumpHeight();
            this.m_Character.Jump.Do();
        }

        private void ApplyJumpHeight()
        {
            if (this.m_Character?.Motion == null) return;
            this.m_Character.Motion.JumpForce = this.CalculateJumpForce();
        }

        private void RefreshRuntimeCaches()
        {
            this.m_RunCameraAlignmentDot = Mathf.Cos(
                this.m_RunCameraAlignmentAngle * Mathf.Deg2Rad
            );
        }

        private float CalculateJumpForce()
        {
            float gravity = Mathf.Abs(this.m_Character.Motion.GravityUpwards);
            if (gravity <= Mathf.Epsilon) return this.m_Character.Motion.JumpForce;

            float height = Mathf.Max(0.05f, this.m_JumpHeight);
            return Mathf.Sqrt(2f * gravity * height);
        }

        private void UpdateSprint()
        {
            bool canSprint = this.m_Character?.Driver?.IsGrounded == true &&
                             this.m_Character.Motion?.IsJumping != true;
            bool canStartSprint = this.m_HasForwardSprintInput && this.m_IsRunCameraAligned;
            // Holding Shift is only GC2's light jog. Sprint is deliberately opt-in through a
            // quick sequence of Shift taps, matching the GTA-style keyboard control scheme.
            bool wantsSprint = canSprint && this.HasSprintRequest() &&
                               (this.m_IsSprinting || canStartSprint);
            if (wantsSprint == this.m_IsSprinting) return;

            if (this.m_SprintState == null ||
                this.m_Character?.States == null ||
                this.m_Character.Motion == null)
            {
                return;
            }
            if (wantsSprint)
            {
                // A quick Shift tap may begin from the Walk state after the first Shift was
                // released. Sprint must still accelerate from the stock GC2 Run/Jog speed.
                this.m_PreSprintSpeed = Mathf.Max(
                    this.m_Character.Motion.LinearSpeed,
                    this.m_JogSpeed
                );
                this.m_IsSprinting = true;
                this.m_RunStartPending = true;
                this.m_RunStopPending = false;
                this.m_HasSprintMovement = false;

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

                // The State enables the sprint animation. The bridge owns this Player's
                // configurable run speed, then restores normal speed until Run Start begins.
                this.m_SprintTargetSpeed = this.m_RunSpeed;
                this.m_Character.Motion.LinearSpeed = this.m_PreSprintSpeed;
                this.m_RunStartAccelerationElapsed = 0f;
                this.m_IsSprintAccelerating = false;
            }
            else
            {
                this.m_RunStartPending = false;
                // If input is still held, the player simply falls back to Jog/Walk. Run Stop is
                // reserved for an actual movement stop, rather than interrupting locomotion.
                this.m_RunStopPending = this.m_HasSprintMovement && !this.HasMovementInput();
                this.m_RunStopDeadline = UnityEngine.Time.time + RUN_STOP_GRACE_SECONDS;
                this.m_HasSprintMovement = false;
                this.StopSprint(this.m_SprintTransition);
            }
        }

        private void UpdateJog()
        {
            if (this.m_IsSprinting)
            {
                // Sprint replaces Jog on the same GC2 State layer. Do not stop that layer here.
                this.m_IsJogging = false;
                return;
            }

            bool canJog = this.m_Character?.Driver?.IsGrounded == true &&
                          this.m_Character.Motion?.IsJumping != true;
            VirtualAutoRunMode autoRunMode = this.GetVirtualAutoRunMode();
            // After taking damage, Jog is the temporary baseline for both keyboard and mobile
            // joystick input. Outside danger mode, only held Shift requests the GC2 Run state.
            bool wantsJog = canJog && (this.m_IsHealthDanger ||
                                        this.m_VirtualJogHeld ||
                                        autoRunMode == VirtualAutoRunMode.Jog ||
                                        this.m_Keyboard?.leftShiftKey.isPressed == true);
            if (wantsJog == this.m_IsJogging) return;

            if (wantsJog)
            {
                if (this.m_JogState == null || this.m_Character?.States == null) return;

                _ = this.m_Character.States.SetState(
                    this.m_JogState,
                    this.m_SprintLayer,
                    BlendMode.Blend,
                    new ConfigState(
                        0f,
                        1f,
                        1f,
                        this.m_JogTransition,
                        this.m_JogTransition
                    )
                );
                this.m_IsJogging = true;
            }
            else
            {
                this.StopJog(this.m_JogTransition);
            }
        }

        private void StopJog(float transition)
        {
            if (!this.m_IsJogging) return;

            // Player's Start State is GC2 Walk at layer -1. Releasing this temporary layer
            // restores both the stock walk animation and its configured speed (2).
            this.m_Character?.States?.Stop(this.m_SprintLayer, 0f, transition);
            this.m_IsJogging = false;
        }

        private void UpdateSprintTapInput()
        {
            if (this.m_IsHealthDanger) return;

            if (UnityEngine.Time.time - this.m_LastSprintTapTime > this.m_SprintTapWindow)
            {
                this.m_SprintTapCount = 0;
            }

            if (this.m_Keyboard?.leftShiftKey.wasPressedThisFrame != true ||
                this.m_Character?.Driver?.IsGrounded != true ||
                this.m_Character.Motion?.IsJumping == true ||
                !this.HasMovementInput())
            {
                return;
            }

            float now = UnityEngine.Time.time;
            this.m_SprintTapCount = now - this.m_LastSprintTapTime <= this.m_SprintTapWindow
                ? this.m_SprintTapCount + 1
                : 1;
            this.m_LastSprintTapTime = now;

            if (this.m_SprintTapCount < this.m_SprintTapsRequired) return;

            this.m_SprintTapDeadline = now + this.m_SprintTapGrace;
        }

        private bool IsSprintTapSequenceActive()
        {
            return UnityEngine.Time.time <= this.m_SprintTapDeadline;
        }

        private bool HasSprintRequest()
        {
            VirtualAutoRunMode autoRunMode = this.GetVirtualAutoRunMode();

            // Danger mode intentionally simplifies locomotion: Jog by default, Sprint on a
            // held Left Shift. The normal GTA-style repeated-tap requirement resumes once safe.
            return this.m_IsHealthDanger
                ? this.m_VirtualSprintHeld ||
                  autoRunMode == VirtualAutoRunMode.Sprint ||
                  this.m_Keyboard?.leftShiftKey.isPressed == true
                : this.m_VirtualSprintHeld ||
                  autoRunMode == VirtualAutoRunMode.Sprint ||
                  this.IsSprintTapSequenceActive();
        }

        private void BindHealthMonitor()
        {
            if (this.m_IsHealthMonitorBound) return;

            this.m_Traits = this.m_Character != null
                ? this.m_Character.GetComponent<Traits>()
                : null;
            if (this.m_Traits == null) return;

            try
            {
                this.m_HealthAttribute = this.m_Traits.RuntimeAttributes.Get(
                    this.m_HealthAttributeId
                );
            }
            catch (Exception)
            {
                if (!this.m_HasWarnedMissingHealthAttribute)
                {
                    this.m_HasWarnedMissingHealthAttribute = true;
                    Debug.LogWarning(
                        $"Franklin damage locomotion could not find Traits Attribute " +
                        $"'{this.m_HealthAttributeId}'.",
                        this
                    );
                }
                return;
            }

            if (this.m_HealthAttribute == null) return;

            this.m_LastHealth = this.m_HealthAttribute.Value;
            this.m_Traits.RuntimeAttributes.EventChange += this.OnHealthAttributeChange;
            this.m_IsHealthMonitorBound = true;
        }

        private void UnbindHealthMonitor()
        {
            if (!this.m_IsHealthMonitorBound || this.m_Traits == null) return;

            this.m_Traits.RuntimeAttributes.EventChange -= this.OnHealthAttributeChange;
            this.m_IsHealthMonitorBound = false;
            this.m_HealthAttribute = null;
        }

        private void OnHealthAttributeChange(IdString attributeID)
        {
            if (this.m_HealthAttribute == null ||
                !string.Equals(attributeID.String, this.m_HealthAttributeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            double nextHealth = this.m_HealthAttribute.Value;
            if (nextHealth < this.m_LastHealth)
            {
                this.m_IsHealthDanger = true;
                this.m_HealthDangerDeadline = UnityEngine.Time.time + this.m_DamageJogDuration;
            }

            this.m_LastHealth = nextHealth;
        }

        private void UpdateHealthDanger()
        {
            if (!this.m_IsHealthDanger ||
                UnityEngine.Time.time < this.m_HealthDangerDeadline)
            {
                return;
            }

            this.m_IsHealthDanger = false;
        }

        private bool HasMovementInput()
        {
            if (this.GetVirtualAutoRunMode() != VirtualAutoRunMode.None) return true;
            if (this.m_Character?.Player == null) return false;

            Vector3 inputDirection = this.m_Character.Player.InputDirection;
            inputDirection.y = 0f;
            return inputDirection.sqrMagnitude > MOVEMENT_EPSILON_SQR;
        }

        private void UpdateRunTransitions()
        {
            if (this.m_Character?.Driver == null || this.m_Character.Motion == null) return;

            // Alignment only gates the initial Sprint start. Once running, camera orbit must
            // not repeatedly fire Run Stop/Run Start while the body catches up with the camera.
            bool hasMovementInput = this.m_HasForwardSprintInput;

            if (this.m_IsSprinting)
            {
                if (hasMovementInput)
                {
                    if (!this.m_HasSprintMovement)
                    {
                        this.m_HasSprintMovement = true;
                        if (this.m_RunStartPending)
                        {
                            this.m_RunStartPending = false;
                            this.BeginSprintAcceleration();
                            bool useRootMotion = this.ShouldUseRunTransitionRootMotion(
                                this.m_RunStart
                            );
                            this.TryPlayCustomGesture(
                                this.m_RunStart,
                                1f,
                                true,
                                useRootMotion
                            );
                        }
                    }
                }
                else if (this.m_HasSprintMovement)
                {
                    // Detect the input edge instead of waiting for CharacterController velocity
                    // to decay. This makes Run Stop reliable even while Left Shift stays held.
                    this.m_HasSprintMovement = false;
                    this.m_RunStartPending = true;
                    this.m_RunStopPending = false;
                    this.CancelSprintAcceleration();
                    this.PlayRunStop();
                }

                return;
            }

            if (!this.m_RunStopPending) return;
            if (UnityEngine.Time.time > this.m_RunStopDeadline)
            {
                this.m_RunStopPending = false;
                return;
            }
            if (this.m_Character.Motion.IsJumping || !this.m_Character.Driver.IsGrounded)
            {
                this.m_RunStopPending = false;
                return;
            }
            if (hasMovementInput) return;

            this.m_RunStopPending = false;
            this.PlayRunStop();
        }

        private void UpdateSprintCameraDirection()
        {
            this.m_HasForwardSprintInput = false;
            this.m_IsRunCameraAligned = false;

            bool canRun = this.m_Character?.Driver?.IsGrounded == true &&
                          this.m_Character.Motion?.IsJumping != true;
            bool hasAutoRun = this.GetVirtualAutoRunMode() != VirtualAutoRunMode.None;
            bool requestsSprint = this.HasSprintRequest();
            if (!canRun || (!hasAutoRun && !requestsSprint) ||
                !this.TryGetRunCameraForward(out Vector3 cameraForward))
            {
                this.ReleaseSprintCameraDirection();
                return;
            }

            Vector3 runCameraDirection = this.SmoothRunCameraDirection(cameraForward);

            float inputStrength = 0f;
            bool hasForwardMovement = hasAutoRun
                ? this.TryGetAutoRunInput(out inputStrength)
                : this.TryGetForwardSprintInput(out inputStrength, runCameraDirection);
            if (!hasForwardMovement)
            {
                this.ReleaseSprintCameraDirection();
                return;
            }

            this.m_HasForwardSprintInput = true;
            if (requestsSprint) this.DisableSprintRootMotionRotation();
            else this.RestoreSprintRootMotionRotation();
            if (this.m_Character.Facing != null)
            {
                this.m_RunCameraFacingLayer = this.m_Character.Facing.SetLayerDirection(
                    this.m_RunCameraFacingLayer,
                    runCameraDirection,
                    false
                );
            }

            // Both vectors are normalized. A cached cosine comparison avoids Vector3.Angle's
            // inverse-cosine calculation on every sprint frame.
            this.m_IsRunCameraAligned = Vector3.Dot(
                this.m_CharacterTransform.forward,
                runCameraDirection
            ) >= this.m_RunCameraAlignmentDot;

            // Keep locomotion continuous while the facing layer smoothly rotates the body.
            // Sending zero velocity here made Jog/Sprint stutter whenever orbit temporarily
            // moved the camera beyond the alignment threshold.
            Vector3 velocity = runCameraDirection *
                               (this.m_Character.Motion.LinearSpeed * inputStrength);
            this.m_Character.Motion.MoveToDirection(velocity, Space.World, 1);
            this.m_HasSprintCameraMotionControl = true;
        }

        private Vector3 SmoothRunCameraDirection(Vector3 cameraForward)
        {
            if (!this.m_HasSmoothedRunCameraDirection ||
                this.m_RunCameraDirectionSmoothTime <= float.Epsilon)
            {
                this.m_SmoothedRunCameraDirection = cameraForward;
                this.m_HasSmoothedRunCameraDirection = true;
                return cameraForward;
            }

            // Exponential smoothing is frame-rate independent and requires no allocations.
            // It removes touch/orbit jitter while preserving the camera as the sole run heading.
            float blend = 1f - Mathf.Exp(
                -UnityEngine.Time.deltaTime / this.m_RunCameraDirectionSmoothTime
            );
            this.m_SmoothedRunCameraDirection = Vector3.Slerp(
                this.m_SmoothedRunCameraDirection,
                cameraForward,
                blend
            );

            if (this.m_SmoothedRunCameraDirection.sqrMagnitude <= float.Epsilon)
            {
                this.m_SmoothedRunCameraDirection = cameraForward;
            }
            else
            {
                this.m_SmoothedRunCameraDirection.Normalize();
            }

            return this.m_SmoothedRunCameraDirection;
        }

        private bool TryGetAutoRunInput(out float inputStrength)
        {
            inputStrength = this.GetVirtualAutoRunMode() == VirtualAutoRunMode.None ? 0f : 1f;
            return inputStrength > 0f;
        }

        private VirtualAutoRunMode GetVirtualAutoRunMode()
        {
            // Hold controls have priority over the optional persistent auto-run choice. This
            // makes the HUD buttons work as immediate, hold-to-run actions: Sprint wins when
            // both fingers are down; releasing either button restores the remaining selection.
            if (this.m_VirtualSprintHeld) return VirtualAutoRunMode.Sprint;
            if (this.m_VirtualJogHeld) return VirtualAutoRunMode.Jog;
            return this.m_VirtualAutoRunMode;
        }

        private bool TryGetForwardSprintInput(out float inputStrength, Vector3 cameraForward)
        {
            inputStrength = 0f;
            if (this.m_Character?.Player == null) return false;

            Vector3 inputDirection = this.m_Character.Player.InputDirection;
            inputDirection.y = 0f;
            float magnitude = inputDirection.magnitude;
            if (magnitude * magnitude <= MOVEMENT_EPSILON_SQR) return false;

            Vector3 normalizedInput = inputDirection / magnitude;
            if (Vector3.Dot(normalizedInput, cameraForward) < this.m_RunForwardInputThreshold)
            {
                return false;
            }

            inputStrength = Mathf.Clamp01(magnitude);
            return true;
        }

        private bool TryGetRunCameraForward(out Vector3 cameraForward)
        {
            // UnitPlayerDirectional also uses the Main Camera, so the input test and the
            // locked sprint velocity are always evaluated in exactly the same camera space.
            if (this.m_RunCameraTransform == null &&
                UnityEngine.Time.unscaledTime >= this.m_NextCameraLookupTime)
            {
                this.m_RunCameraTransform = ShortcutMainCamera.Transform;
                if (this.m_RunCameraTransform == null)
                {
                    this.m_RunCameraTransform = ShortcutMainShot.Transform;
                }
                if (this.m_RunCameraTransform == null && Camera.main != null)
                {
                    this.m_RunCameraTransform = Camera.main.transform;
                }

                if (this.m_RunCameraTransform == null)
                {
                    this.m_NextCameraLookupTime =
                        UnityEngine.Time.unscaledTime + CAMERA_LOOKUP_RETRY_SECONDS;
                }
            }

            cameraForward = this.m_RunCameraTransform != null
                ? this.m_RunCameraTransform.forward
                : Vector3.zero;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude <= float.Epsilon) return false;

            cameraForward.Normalize();
            return true;
        }

        private void ReleaseSprintCameraDirection()
        {
            this.m_HasSmoothedRunCameraDirection = false;
            this.m_SmoothedRunCameraDirection = Vector3.zero;

            if (this.m_RunCameraFacingLayer < 0 &&
                !this.m_HasSprintCameraMotionControl &&
                !this.m_HasDisabledSprintRootMotionRotation)
            {
                this.m_HasForwardSprintInput = false;
                this.m_IsRunCameraAligned = false;
                return;
            }

            if (this.m_Character?.Facing != null && this.m_RunCameraFacingLayer >= 0)
            {
                this.m_Character.Facing.DeleteLayer(this.m_RunCameraFacingLayer);
            }
            bool preserveStopMomentum = this.m_IsSprinting &&
                                        (this.m_HasSprintMovement || this.m_IsRunStopEasing);
            if (this.m_HasSprintCameraMotionControl && this.m_Character?.Motion != null)
            {
                if (preserveStopMomentum)
                {
                    // Keep calling GC2 with a zero target so its acceleration model can ease
                    // the existing velocity out instead of cancelling it in one frame.
                    this.m_Character.Motion.MoveToDirection(Vector3.zero, Space.World, 1);
                }
                else
                {
                    this.m_Character.Motion.StopToDirection(1);
                }
            }
            if (this.m_HasDisabledSprintRootMotionRotation && this.m_Character != null)
            {
                this.RestoreSprintRootMotionRotation();
            }

            this.m_RunCameraFacingLayer = -1;
            this.m_HasSprintCameraMotionControl = preserveStopMomentum;
            this.m_HasForwardSprintInput = false;
            this.m_IsRunCameraAligned = false;
        }

        private void DisableSprintRootMotionRotation()
        {
            if (this.m_RunTransitionMotionMode != RunTransitionMotionMode.AnimationRootMotion ||
                this.m_Character == null)
            {
                return;
            }

            // MAP Run Start/Stop use root translation, but their rotation channel would otherwise
            // override GC2 Facing and prevent the Player from turning into the camera direction.
            this.m_Character.CanUseRootMotionRotation = false;
            this.m_HasDisabledSprintRootMotionRotation = true;
        }

        private void RestoreSprintRootMotionRotation()
        {
            if (!this.m_HasDisabledSprintRootMotionRotation || this.m_Character == null) return;

            this.m_Character.CanUseRootMotionRotation = true;
            this.m_HasDisabledSprintRootMotionRotation = false;
        }

        private void PlayRunStop()
        {
            AnimationClip stopClip = this.m_UseLeftRunStop
                ? this.m_RunStopLeft
                : this.m_RunStopRight;
            this.m_UseLeftRunStop = !this.m_UseLeftRunStop;
            bool useRootMotion = this.ShouldUseRunTransitionRootMotion(stopClip);
            this.BeginRunStopEaseOut(!useRootMotion);
            this.TryPlayCustomGesture(stopClip, 1f, true, useRootMotion);
        }

        private bool ShouldUseRunTransitionRootMotion(AnimationClip clip)
        {
            if (this.m_RunTransitionMotionMode != RunTransitionMotionMode.AnimationRootMotion)
            {
                return false;
            }
            if (clip != null && clip.hasRootCurves) return true;

            if (!this.m_HasWarnedMissingTransitionRootMotion)
            {
                this.m_HasWarnedMissingTransitionRootMotion = true;
                Debug.LogWarning(
                    "Run Transition Motion Mode is Animation Root Motion, but a selected clip " +
                    "has no root curves. Falling back to GC2 Motion.",
                    this
                );
            }

            return false;
        }

        private void CancelSprintAcceleration()
        {
            this.m_IsSprintAccelerating = false;
            this.m_RunStartAccelerationElapsed = 0f;
        }

        private void BeginSprintAcceleration()
        {
            if (this.m_Character?.Motion == null) return;

            this.FinishRunStopEaseOut(false);
            this.m_RunStartAccelerationElapsed = 0f;
            if (this.m_RunStartAccelerationTime <= float.Epsilon)
            {
                this.m_Character.Motion.LinearSpeed = this.m_SprintTargetSpeed;
                this.m_IsSprintAccelerating = false;
                return;
            }

            this.m_Character.Motion.LinearSpeed = this.m_PreSprintSpeed;
            this.m_IsSprintAccelerating = true;
        }

        private void UpdateSprintAcceleration()
        {
            if (!this.m_IsSprintAccelerating) return;
            if (!this.m_IsSprinting || this.m_Character?.Motion == null)
            {
                this.m_IsSprintAccelerating = false;
                return;
            }

            this.m_RunStartAccelerationElapsed += UnityEngine.Time.deltaTime;
            float progress = Mathf.Clamp01(
                this.m_RunStartAccelerationElapsed / this.m_RunStartAccelerationTime
            );
            float easedProgress = Mathf.Pow(progress, this.m_RunStartAccelerationPower);
            this.m_Character.Motion.LinearSpeed = Mathf.Lerp(
                this.m_PreSprintSpeed,
                this.m_SprintTargetSpeed,
                easedProgress
            );

            if (progress >= 1f) this.m_IsSprintAccelerating = false;
        }

        private void BeginRunStopEaseOut(bool overrideGc2Deceleration)
        {
            if (this.m_Character?.Motion == null) return;

            this.FinishRunStopEaseOut(false);

            this.m_RunStopEaseOutElapsed = 0f;
            this.m_RunStopStartLinearSpeed = this.m_Character.Motion.LinearSpeed;
            this.m_IsRunStopEasing = true;

            if (!overrideGc2Deceleration) return;

            // GC2 already eases Motion.MoveDirection using its Deceleration value. Temporarily
            // cap that rate so a high value (for example 10) cannot collapse velocity in a few
            // frames. Four exponential time constants leave roughly 2% velocity at the end.
            this.m_RunStopOriginalUseAcceleration = this.m_Character.Motion.UseAcceleration;
            this.m_RunStopOriginalDeceleration = this.m_Character.Motion.Deceleration;
            this.m_HasRunStopMotionOverride = true;

            float duration = Mathf.Max(0.05f, this.m_RunStopEaseOutTime);
            float durationRate = 4f / duration;
            float configuredRate = this.m_RunStopOriginalDeceleration > float.Epsilon
                ? this.m_RunStopOriginalDeceleration
                : durationRate;

            this.m_Character.Motion.UseAcceleration = true;
            this.m_Character.Motion.Deceleration = Mathf.Min(configuredRate, durationRate);
        }

        private void UpdateRunStopEaseOut()
        {
            if (!this.m_IsRunStopEasing || this.m_Character?.Motion == null) return;

            this.m_RunStopEaseOutElapsed += UnityEngine.Time.deltaTime;
            float progress = Mathf.Clamp01(
                this.m_RunStopEaseOutElapsed / Mathf.Max(0.05f, this.m_RunStopEaseOutTime)
            );
            float easedProgress = 1f - Mathf.Pow(
                1f - progress,
                this.m_RunStopEaseOutPower
            );

            // While Shift remains held, bring the sprint speed cap back to the normal GC2 speed.
            // GC2 itself remains responsible for easing the real MoveDirection to zero.
            if (this.m_IsSprinting)
            {
                this.m_Character.Motion.LinearSpeed = Mathf.Lerp(
                    this.m_RunStopStartLinearSpeed,
                    this.m_PreSprintSpeed,
                    easedProgress
                );
            }

            if (progress >= 1f) this.FinishRunStopEaseOut(true);
        }

        private void FinishRunStopEaseOut(bool applyNormalSpeed)
        {
            if (this.m_HasRunStopMotionOverride && this.m_Character?.Motion != null)
            {
                this.m_Character.Motion.UseAcceleration = this.m_RunStopOriginalUseAcceleration;
                this.m_Character.Motion.Deceleration = this.m_RunStopOriginalDeceleration;
            }

            if (applyNormalSpeed && this.m_Character?.Motion != null)
            {
                this.m_Character.Motion.LinearSpeed = this.m_PreSprintSpeed;
            }

            bool shouldExitSprintState = applyNormalSpeed && this.m_IsSprinting &&
                                         !this.m_HasForwardSprintInput;
            this.m_HasRunStopMotionOverride = false;
            this.m_IsRunStopEasing = false;
            this.m_RunStopEaseOutElapsed = 0f;

            if (shouldExitSprintState) this.StopSprint(this.m_SprintTransition);
        }

        private void StopSprint(float transition)
        {
            // The default GC2 Walk State runs at layer -1. Stopping Sprint reveals Walk, while
            // UpdateJog may replace it with GC2's Run State if Shift is still held.
            this.m_Character.States?.Stop(this.m_SprintLayer, 0f, transition);

            this.m_IsSprintAccelerating = false;
            this.m_RunStartAccelerationElapsed = 0f;
            this.m_IsSprinting = false;
        }

        private bool CanPlayIdleVariation()
        {
            if (this.m_Character == null) return false;
            if (this.m_IdleVariations == null || this.m_IdleVariations.Length == 0) return false;
            if (this.m_Character.Driver == null || this.m_Character.Motion == null) return false;
            if (!this.m_Character.Driver.IsGrounded || this.m_Character.Motion.IsJumping) return false;
            // Driver direction is updated after input. Check raw Player/virtual input as well so
            // an idle root-motion gesture cannot capture the first movement frame.
            if (this.HasMovementInput()) return false;
            if (this.m_Character.Driver.WorldMoveDirection.sqrMagnitude > MOVEMENT_EPSILON_SQR)
            {
                return false;
            }

            // Never replace a gesture started by GC2 gameplay, combat or interaction systems.
            return !this.m_Character.Gestures.IsPlaying;
        }

        private void UpdateIdleGestureInterruption()
        {
            if (!this.m_IsIdleGestureActive) return;

            if (UnityEngine.Time.time >= this.m_ActiveCustomGestureUntil)
            {
                this.m_IsIdleGestureActive = false;
                return;
            }

            bool hasDriverMovement = this.m_Character?.Driver != null &&
                                     this.m_Character.Driver.WorldMoveDirection.sqrMagnitude >
                                     MOVEMENT_EPSILON_SQR;
            if (!this.HasMovementInput() && !hasDriverMovement) return;

            // Idle variations are cosmetic. Movement always wins and blends out the owned
            // gesture immediately, including clips that use subtle root motion.
            this.StopOwnedCustomGesture();
            this.StopTrackingIdle();
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
            if (this.m_IsExternalAnimationLocked) return;

            bool wasSprinting = this.m_IsSprinting;
            bool wasMoving = this.m_Character?.Driver != null &&
                             this.m_Character.Driver.WorldMoveDirection.sqrMagnitude >
                             MOVEMENT_EPSILON_SQR;

            this.StopTrackingIdle();
            this.m_RunStartPending = false;
            this.m_RunStopPending = false;
            this.m_HasSprintMovement = false;
            this.ReleaseSprintCameraDirection();
            this.FinishRunStopEaseOut(false);
            if (this.m_IsSprinting) this.StopSprint(this.m_SprintTransition);
            else if (this.m_IsJogging) this.StopJog(this.m_JogTransition);

            AnimationClip jumpClip = this.SelectJumpClip(wasSprinting, wasMoving);
            float duration = Mathf.Max(0.5f, this.m_JumpVisualDuration);
            float speed = jumpClip != null ? jumpClip.length / duration : 1f;
            this.TryPlayCustomGesture(jumpClip, speed, true);
        }

        private void OnCharacterLand(float verticalSpeed)
        {
            this.StopTrackingIdle();
        }

        private void PlayGesture(AnimationClip clip)
        {
            // The selected idle clips contain RootT/RootQ curves. Let GC2 apply that subtle
            // body-weight and foot-placement motion instead of freezing the root in place.
            this.m_IsIdleGestureActive = this.TryPlayCustomGesture(
                clip,
                1f,
                false,
                true
            );
        }

        private AnimationClip SelectJumpClip(bool wasSprinting, bool wasMoving)
        {
            if (!wasMoving) return this.m_JumpPlace;

            AnimationClip clip;
            if (wasSprinting)
            {
                clip = this.m_UseLeftJump ? this.m_JumpRunLeft : this.m_JumpRunRight;
            }
            else
            {
                clip = this.m_UseLeftJump ? this.m_JumpWalkLeft : this.m_JumpWalkRight;
            }
            this.m_UseLeftJump = !this.m_UseLeftJump;
            return clip;
        }

        private bool TryPlayCustomGesture(
            AnimationClip clip,
            float speed,
            bool replaceOwnGesture,
            bool rootMotion = false)
        {
            if (this.m_IsExternalAnimationLocked) return false;
            if (clip == null || this.m_Character == null || speed <= float.Epsilon) return false;

            bool ownGestureActive = this.m_ActiveCustomGesture != null &&
                                    UnityEngine.Time.time < this.m_ActiveCustomGestureUntil;
            if (this.m_Character.Gestures.IsPlaying)
            {
                if (!ownGestureActive || !replaceOwnGesture) return false;
                this.m_Character.Gestures.Stop(this.m_ActiveCustomGesture, 0f, 0.05f);
            }

            ConfigGesture config = new ConfigGesture(
                0f,
                clip.length,
                speed,
                rootMotion,
                this.m_TransitionIn,
                this.m_TransitionOut
            );

            // Root motion is used by idle variations and, when selected, Run transitions.
            // Previous gestures created by GC2 gameplay, combat or interactions are never cancelled.
            _ = this.m_Character.Gestures.CrossFade(
                clip,
                null,
                BlendMode.Blend,
                config,
                false
            );

            this.m_ActiveCustomGesture = clip;
            this.m_ActiveCustomGestureUntil = UnityEngine.Time.time +
                                              clip.length / speed +
                                              this.m_TransitionOut;
            if (replaceOwnGesture) this.m_IsIdleGestureActive = false;
            return true;
        }

        private void StopOwnedCustomGesture()
        {
            if (this.m_ActiveCustomGesture == null) return;

            this.m_Character?.Gestures?.Stop(
                this.m_ActiveCustomGesture,
                0f,
                this.m_TransitionOut
            );
            this.m_ActiveCustomGesture = null;
            this.m_ActiveCustomGestureUntil = 0f;
            this.m_IsIdleGestureActive = false;
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
