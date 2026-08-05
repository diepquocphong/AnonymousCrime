using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Niam.Runtime.Tactile 
{
    [Serializable]
    public class TouchInteraction
    {
        private const float EPSILON_TIME = 0.2f;

        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private bool m_UseTapTime;
        [SerializeField] private bool m_UseTapRadius;
        [SerializeField] private bool m_UseSlowTapTime;
        [SerializeField] private bool m_UseMultiTapDelayTime;
        [SerializeField] private bool m_UseHoldTime;

        [SerializeField] private float m_TapTime = 0.2f;
        [SerializeField] private float m_TapRadius = 5f;
        [SerializeField] private float m_SlowTapTime = 0.5f;
        [SerializeField] private float m_MultiTapDelayTime = 0.75f;
        [SerializeField] private float m_HoldTime = 0.4f;
        [SerializeField] private float m_HoldRadius = 15f;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private bool m_IsBegun;
        [NonSerialized] private bool m_IsHeld;
        [NonSerialized] private bool m_CanHold;
        [NonSerialized] private bool m_IsPressed;

        [NonSerialized] private int m_ConsecutiveTapCount;
        [NonSerialized] private int m_PressCountThisFrame;
        [NonSerialized] private int m_ReleaseCountThisFrame;

        [NonSerialized] private float m_LastTapTime;
        [NonSerialized] private float m_LastPressTime;
        [NonSerialized] private float m_LastReleaseTime;
        [NonSerialized] private float m_HoldPercentage;

        // PROPERTIES: ----------------------------------------------------------------------------

        public float TapTime
        {
            get => this.m_UseTapTime
                ? this.m_TapTime
                : InputSystem.settings.defaultTapTime;

            set
            {
                if (value < 0f)
                {
                    this.m_UseTapTime = false;
                    return;
                }

                this.m_TapTime = value;
            }
        }

        public float TapRadius
        {
            get => this.m_UseTapRadius
                ? this.m_TapRadius
                : InputSystem.settings.tapRadius;

            set
            {
                if (value < 0f)
                {
                    this.m_UseTapRadius = false;
                    return;
                }

                this.m_UseTapRadius = true;
                this.m_TapRadius = value;
            }
        }

        public float SlowTapTime
        {
            get => this.m_UseSlowTapTime
                ? this.m_SlowTapTime
                : InputSystem.settings.defaultSlowTapTime;

            set
            {
                if (value < 0f)
                {
                    this.m_UseSlowTapTime = false;
                    return;
                }
                
                this.m_UseSlowTapTime = true;
                this.m_SlowTapTime = value;
            }

        }

        public float MultiTapDelayTime
        {
            get => this.m_UseMultiTapDelayTime
                ? this.m_MultiTapDelayTime
                : InputSystem.settings.multiTapDelayTime;

            set
            {
                if (value < 0f)
                {
                    this.m_UseMultiTapDelayTime = false;
                    return;
                }

                this.m_UseMultiTapDelayTime = true;
                this.m_MultiTapDelayTime = value;
            }
        }

        public float HoldTime
        {
            get => this.m_UseHoldTime
                ? this.m_HoldTime
                : InputSystem.settings.defaultHoldTime;

            set
            {
                if (value < 0f)
                {
                    this.m_UseHoldTime = false;
                    return;
                }

                this.m_UseHoldTime = true;
                this.m_HoldTime = value;
            }
        }

        public float HoldRadius
        {
            get => this.m_HoldRadius;
            set => this.m_HoldRadius = value;
        }

        public bool IsHolding
        {
            get => this.m_IsHeld;
        }

        public bool IsPressed
        {
            get => this.m_IsPressed;
        }

        public bool IsBegun
        {
            get => this.m_IsBegun;
        }

        internal bool CanHold
        {
            get => this.m_CanHold;
            set => this.m_CanHold = value;
        }

        public int LastTapCount => this.m_ConsecutiveTapCount;
        public float HoldPercentage => this.m_HoldPercentage;
        private float CurrentTime => Time.unscaledTime;

        // EVENTS: --------------------------------------------------------------------------------

        public event Action<int> EventPress;
        public event Action<int> EventRelease;

        public event Action EventTap;
        public event Action EventHold;
        public event Action EventSlowTap;
        public event Action<int> EventMultiTap;

        public event Action EventWhileHolding;
        public event Action EventWhilePressing;

        // PRIVATE METHODS: -----------------------------------------------------------------------

        internal void ExecuteTapEvent(TactileControl control)
        {
            float elapseTime = this.CurrentTime - this.m_LastPressTime;

            if (elapseTime <= this.TapTime)
            {
                bool isMultiTap = this.CurrentTime - this.m_LastTapTime <= this.MultiTapDelayTime;
                this.m_ConsecutiveTapCount = isMultiTap ? this.m_ConsecutiveTapCount + 1 : 1;

                this.EventTap?.Invoke();
                if (isMultiTap) this.EventMultiTap?.Invoke(this.m_ConsecutiveTapCount);
                this.m_LastTapTime = this.CurrentTime;
            }
            else
            {
                if (elapseTime <= this.TapTime + this.SlowTapTime)
                {
                    this.EventSlowTap?.Invoke();
                }

                this.m_ConsecutiveTapCount = 0;
            }
        }

        internal void ExecuteHoldEvent(TactileControl control)
        {
            if (!this.m_IsBegun)
                return;

            if (this.m_CanHold)
            {
                float elapseTime = this.CurrentTime - this.m_LastPressTime;
                this.m_HoldPercentage = elapseTime / this.HoldTime;

                if (!this.m_IsHeld && elapseTime >= this.HoldTime)
                {
                    this.m_IsHeld = true;
                    this.EventHold?.Invoke();
                }

                if (this.m_IsHeld)
                {
                    this.EventWhileHolding?.Invoke();
                }
            }

            this.EventWhilePressing?.Invoke();
        }

        internal void ExecutePressEvent(TactileControl control)
        {
            if (this.CurrentTime - this.m_LastPressTime <= EPSILON_TIME)
            {
                this.m_PressCountThisFrame++;
            }
            else
            {
                this.m_PressCountThisFrame = 1;
            }

            this.m_IsBegun = true;
            this.m_IsPressed = true;

            this.m_IsHeld = false;
            this.m_CanHold = true;
            this.m_HoldPercentage = 0f;

            this.m_ReleaseCountThisFrame = 0;
            this.m_LastPressTime = this.CurrentTime;

            this.EventPress?.Invoke(this.m_PressCountThisFrame);
        }

        internal void ExecuteBerforeReleaseEvent(int fingerCount, TactileControl control)
        {
            if (this.CurrentTime - this.m_LastReleaseTime <= EPSILON_TIME)
            {
                this.m_ReleaseCountThisFrame++;
            }
            else
            {
                this.m_ReleaseCountThisFrame = 1;
            }

            this.m_PressCountThisFrame = 0;
            this.m_LastReleaseTime = this.CurrentTime;

            this.m_IsPressed = fingerCount > 1;
            this.EventRelease?.Invoke(this.m_ReleaseCountThisFrame);
        }
        
        internal void ExecuteArfterReleaseEvent(int fingerCount, TactileControl control)
        {
            this.m_IsBegun = fingerCount != 0;
            this.m_IsHeld = fingerCount != 0;

            if (!this.m_IsBegun)
            {
                this.m_ReleaseCountThisFrame = 0;
            }
        }
        
    }
}