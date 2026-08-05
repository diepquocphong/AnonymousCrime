using System;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Gesture Pad")]
    [Category("Gesture Pad")]

    [Description(
        "A control type that provides gesture interaction such as pan, pinch, or twist, " +
        "typically involves dragging of one or more fingers across a touchable area."
    )]
    
    // TODO: FIX MULTI-TASK FIELD
    // [Parameter("Multi-task", "Enables handling of multiple gestures simultaneously")]

    [Parameter(
        "Pan", 
        "Section for configuring the panning gesture"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The Vector2 control path of the input control to be simulate"
    )]
    [Parameter(
        "↳ Fingers", 
        "<indent=1.2em>The number of fingers required to activate a pan"
    )]
    [Parameter(
        "↳ Threshold", 
        "<indent=1.2em>The minimum movement required to activate a pan"
    )]
    [Parameter(
        "↳ Sensitivity", 
        "<indent=1.2em>The multiplier that adjusts the output delta of pan"
    )]

    [Parameter(
        "Pinch", 
        "Section for configuring the pinch gesture"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The Axis control path of the input control to be simulate"
    )]
    [Parameter(
        "↳ Fingers", 
        "<indent=1.2em>The number of fingers required to activate a pinch"
    )]
    [Parameter(
        "↳ Threshold", 
        "<indent=1.2em>The minimum movement required to activate a pinch"
    )]
    [Parameter(
        "↳ Sensitivity", 
        "<indent=1.2em>The multiplier that adjusts the output delta of pinch"
    )]

    [Parameter(
        "Twist", "Section for configuring the twist gesture"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The axis control path of the input control to be simulate"
    )]
    [Parameter(
        "↳ Fingers", 
        "<indent=1.2em>The number of fingers required to activate a twist"
    )]
    [Parameter(
        "↳ Threshold", 
        "<indent=1.2em>The minimum movement required to activate a twist"
    )]
    [Parameter(
        "↳ Sensitivity", 
        "<indent=1.2em>The multiplier that adjusts the output delta of twist"
    )]

    [Image(typeof(IconGesture))]

    [Serializable]
    public class ControlTypeGesturePad : TControlType
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        // [SerializeField] private bool m_Multitask = false;

        [SerializeField] private InputSimulateVector2 m_PanInputSimulate;
        [SerializeField] private int m_PanFingers = 1;
        [SerializeField] private float m_PanThreshold = 1.5f;
        [SerializeField] private Vector2 m_PanSensitivity = Vector2.one;

        [SerializeField] private InputSimulateAxis m_PinchInputSimulate;
        [SerializeField] private int m_PinchFingers = 2;
        [SerializeField] private float m_PinchThreshold = 0.1f;
        [SerializeField] private float m_PinchSensitivity = 1f;

        [SerializeField] private InputSimulateAxis m_TwistInputSimulate;
        [SerializeField] private int m_TwistFingers = 2;
        [SerializeField] private float m_TwistThreshold = 0.1f;
        [SerializeField] private float m_TwistSensitivity = 1f;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private bool m_CanPan;
        [NonSerialized] private Vector2 m_InitPan;
        [NonSerialized] private Vector2 m_LastPan;

        [NonSerialized] private bool m_CanPinch;
        [NonSerialized] private float m_InitPinch;
        [NonSerialized] private float m_LastPinch;

        [NonSerialized] private bool m_CanTwist;
        [NonSerialized] private float m_InitTwist;
        [NonSerialized] private float m_LastTwist;

        // PROPERTIES: ----------------------------------------------------------------------------

        [field: NonSerialized] public Vector2 PanDelta { get; private set; }

        [field: NonSerialized] public float PinchDelta { get; private set; }
        [field: NonSerialized] public float PinchScale { get; private set; } = 1f;

        [field: NonSerialized] public float TwistDelta { get; private set; }
        [field: NonSerialized] public float TwistAngle { get; private set; }

        [field: NonSerialized] public bool IsPanning   { get; private set; }
        [field: NonSerialized] public bool IsPinching  { get; private set; }
        [field: NonSerialized] public bool IsTwisting  { get; private set; }
        
        public float PanThreshold 
        { 
            get => this.m_PanThreshold; 
            set => this.m_PanThreshold = value;
        }

        public Vector2 PanSensitivity 
        { 
            get => this.m_PanSensitivity; 
            set => this.m_PanSensitivity = value;
        }

        public float PinchThreshold 
        { 
            get => this.m_PinchThreshold; 
            set => this.m_PinchThreshold = value;
        }

        public float PinchSensitivity 
        { 
            get => this.m_PinchSensitivity; 
            set => this.m_PinchSensitivity = value;
        }

        public float TwistThreshold 
        { 
            get => this.m_TwistThreshold; 
            set => this.m_TwistThreshold = value;
        }

        public float TwistSensitivity 
        { 
            get => this.m_TwistSensitivity; 
            set => this.m_TwistSensitivity = value;
        }

        public InputSimulateVector2 PanInputSimulate => this.m_PanInputSimulate;
        public InputSimulateAxis PinchInputSimulate => this.m_PinchInputSimulate;
        public InputSimulateAxis TwistInputSimulate => this.m_TwistInputSimulate;

        // EVENTS: -------------------------------------------------------------------------------

        public event Action<Vector2> EventPan;
        public event Action<float> EventPinch;
        public event Action<float> EventTwist;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected internal override void Update()
        {
            this.UpdatePanDelta();
            this.UpdatePinchScale();
            this.UpdateTwistAngle();
        }

        protected internal override void Enable()
        {
            this.m_PanInputSimulate?.OnEnabled(this.m_Control);
            this.m_PinchInputSimulate?.OnEnabled(this.m_Control);
            this.m_TwistInputSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_PanInputSimulate?.OnDisabled();
            this.m_PinchInputSimulate?.OnDisabled();
            this.m_TwistInputSimulate?.OnDisabled();
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (!this.HasPressInArea) return;

            if (this.IsPanning) this.m_PanInputSimulate?.SendResetValueToControl();
            if (this.IsPinching) this.m_PinchInputSimulate?.SendResetValueToControl();
            if (this.IsTwisting) this.m_TwistInputSimulate?.SendResetValueToControl();

            this.ValidateGesture();
        }

        protected internal override void InteractAfterEnd(Touch touch)
        {
            this.ValidateGesture();

            this.PanDelta = Vector2.zero;

            this.PinchDelta = 1f;
            this.PinchScale = 1f;

            this.TwistDelta = 0f;
            this.TwistAngle = 0f;

            this.m_PanInputSimulate?.SendResetValueToControl();
            this.m_PinchInputSimulate?.SendResetValueToControl();
            this.m_TwistInputSimulate?.SendResetValueToControl();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void ValidateGesture()
        {
            int touchCount = this.FingerCount;

            this.m_CanPan = this.m_PanFingers > 0 && this.m_PanFingers == touchCount;
            this.m_CanPinch = this.m_PinchFingers > 0 && this.m_PinchFingers == touchCount;
            this.m_CanTwist = this.m_TwistFingers > 0 && this.m_TwistFingers == touchCount;

            if (this.m_CanPan)
            {
                this.m_InitPan = this.TouchableArea.fingersCentroid;
                this.m_LastPan = this.m_InitPan;
            }

            if (this.m_CanPinch)
            {
                this.m_InitPinch = this.CalculatePinchScale();
                this.m_LastPinch = this.m_InitPinch;
            }

            if (this.m_CanTwist)
            {
                this.m_InitTwist = this.CalculateTwistAngle();
                this.m_LastTwist = this.m_InitTwist;
            }
        }

        private void UpdatePanDelta()
        {
            if (!this.m_CanPan)
            {
                this.IsPanning = false;
                return;
            }

            Vector2 currentPan = this.TouchableArea.fingersCentroid;
            Vector2 panDelta = (currentPan - this.m_LastPan) * this.ScaleFactor;

            if (!this.IsPanning && panDelta.sqrMagnitude < this.PanThreshold * this.PanThreshold)
            {
                panDelta = Vector2.zero;
            }
            else
            {
                this.IsPanning = true;
                panDelta *= this.m_PanSensitivity;

                // if (!this.m_Multitask)
                // {
                //     this.m_CanPinch = false;
                //     this.m_CanTwist = false;
                // }
            }
            this.m_LastPan = currentPan;

            if (this.PanDelta == panDelta) return;

            this.PanDelta = panDelta;
            this.EventPan?.Invoke(panDelta);
            this.m_PanInputSimulate?.SendValueToControl(panDelta);
        }

        private void UpdatePinchScale()
        {
            if (!this.m_CanPinch)
            {
                this.IsPinching = false;
                return;
            }

            float currentPinch = this.CalculatePinchScale();
            float pinchDelta = this.m_LastPinch != 0 
                ? (currentPinch / this.m_LastPinch) - 1f 
                : 0f;

            if (!this.IsPinching && Mathf.Abs(pinchDelta) < this.PinchThreshold)
            {
                pinchDelta = 0f;
                this.PinchScale = 1f;
            }
            else
            {
                this.IsPinching = true;
                this.PinchScale = currentPinch / this.m_InitPinch;
                pinchDelta *= this.m_PinchSensitivity;

                // if (!this.m_Multitask)
                // {
                //     this.m_CanPan = false;
                //     this.m_CanTwist = false;
                // }

            }
            this.m_LastPinch = currentPinch;

            if (Mathf.Approximately(this.PinchDelta, pinchDelta)) return;

            this.PinchDelta = pinchDelta;
            this.EventPinch?.Invoke(pinchDelta);
            this.m_PinchInputSimulate?.SendValueToControl(pinchDelta);
        }

        private float CalculatePinchScale()
        {
            if (this.FingerCount < 2) return 0f;

            int pairsCount = 0;
            float sumDistances = 0f;

            for (int i = 0; i < this.FingerCount; i++)
            {
                for (int j = i + 1; j < this.FingerCount; j++)
                {
                    Vector2 position1 = this.TouchableArea.GetFinger(i).screenPosition;
                    Vector2 position2 = this.TouchableArea.GetFinger(j).screenPosition;
                    Vector2 difference = position1 - position2;

                    sumDistances += difference.sqrMagnitude;
                    pairsCount++;
                }
            }

            return sumDistances / pairsCount;
        }

        private void UpdateTwistAngle()
        {
            if (!this.m_CanTwist)
            {
                this.IsTwisting = false;
                return;
            }
            
            float currentTwist = this.CalculateTwistAngle();
            float twistdelta = Mathf.DeltaAngle(this.m_LastTwist, currentTwist);

            if (!this.IsTwisting && Mathf.Abs(twistdelta) < this.TwistThreshold)
            {
                twistdelta = 0f;
                this.TwistAngle = 0f;
            }
            else
            {
                this.IsTwisting = true;
                this.TwistAngle = (360f - Mathf.DeltaAngle(this.m_InitTwist, currentTwist)) % 360f;
                twistdelta *= this.m_TwistSensitivity;

                // if (!this.m_Multitask)
                // {
                //     this.m_CanPan = false;
                //     this.m_CanPinch = false;
                // }
            }
            this.m_LastTwist = currentTwist;

            if (Mathf.Approximately(this.TwistDelta, twistdelta)) return;

            this.TwistDelta = twistdelta;
            this.EventTwist?.Invoke(twistdelta);
            this.m_TwistInputSimulate?.SendValueToControl(twistdelta);
        }

        private float CalculateTwistAngle()
        {
            if (this.FingerCount < 2) return 0f;

            int pairsCount = 0;
            float sumAngles = 0f;

            for (int i = 0; i < this.FingerCount; i++)
            {
                for (int j = i + 1; j < this.FingerCount; j++)
                {
                    Vector2 position1 = this.TouchableArea.GetFinger(i).screenPosition;
                    Vector2 position2 = this.TouchableArea.GetFinger(j).screenPosition;
                    Vector2 direction = position1 - position2;

                    sumAngles += Mathf.Atan2(direction.y, direction.x);
                    pairsCount++;
                }
            }

            return sumAngles / pairsCount * Mathf.Rad2Deg;
        }

    }
}