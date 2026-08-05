using System;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Steering Wheel")]
    [Category("Steering Wheel")]

    [Description(
        "A control type that offers continuous motion in a single direction, featuring a wheel " +
        "that can be tilted or rotated by dragging it in the desired direction"
    )]

    [Parameter(
        "Input Simulate", 
        "The axis control path of the input control to be simulate"
    )]

    [Parameter(
        "Time Mode", 
        "The time scale that affects the rotation of Steering Wheel"
    )]

    [Parameter(
        "Wheel", 
        "Section for configuring the Steering Wheel"
    )]
    [Parameter(
        "↳ Wheel", 
        "<indent=1.2em>A reference to the Rect Transform component representing the wheel"
    )]
    [Parameter(
        "↳ Deadzone", 
        "<indent=1.2em>The radius where dragging of the Steering Wheel are ignored"
    )]

    [Parameter(
        "Steer", 
        "Section for managing the steering behavior"
    )]
    [Parameter(
        "↳ Snap Angle", 
        "<indent=1.2em>The angle at which the Steering Wheel snaps to the nearest fixed angle"
    )]
    [Parameter(
        "↳ Max Angle", 
        "<indent=1.2em>The maximum angle the Steering Wheel can rotate in either direction"
    )]
    [Parameter(
        "↳ Sensitivity", 
        "<indent=1.2em>Adjusts how responsive the Steering Wheel rotates to user dragging"
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness of the Steering Wheel's rotation during steering. The " +
        "greater the value, the smoother it will be"
    )]

    [Parameter(
        "Lift", 
        "Section for adjusting how the Steering Wheel reacts when released"
    )]
    [Parameter(
        "↳ Recenter", 
        "<indent=1.2em>Whether to restore the original rotation of the Steering Wheel"
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness of the Steering Wheel's rotation during recenter. The " +
        "greater the value, the smoother it will be"
    )]

    [Image(typeof(IconSteerWheel))]

    [Serializable]
    public class ControlTypeSteeringWheel : TControlType
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private InputSimulateAxis m_InputSimulate;
        [SerializeField] private TimeMode m_TimeMode = new (TimeMode.UpdateMode.GameTime);

        [SerializeField] private RectTransform m_Wheel;
        [SerializeField] private float  m_Deadzone = 20f;

        [SerializeField] private bool   m_CanSnap;
        [SerializeField] private float  m_SnapAngle = 45f;

        [SerializeField] private bool   m_HasMaxAngle = true;
        [SerializeField] private float  m_MaxAngle = 720f;

        [SerializeField] private float  m_Sensitivity = 1f;
        [SerializeField] private float  m_SteerDamping = 0.05f;

        [SerializeField] private bool   m_Recenter = true;
        [SerializeField] private float  m_LiftDamping = 0.2f;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private Vector2 m_WheelPosition;

        [NonSerialized] private float   m_TargetAngle;
        [NonSerialized] private float   m_CurrentAngle;
        [NonSerialized] private float   m_PreviousAngle;

        [NonSerialized] private float   m_SteerMotion;
        [NonSerialized] private float   m_SteerAngle;
        [NonSerialized] private float   m_OriginAngle;
        [NonSerialized] private float   m_DampVelocity;

        // PROPERTIES: ----------------------------------------------------------------------------

        public RectTransform Wheel => this.m_Wheel;

        public float SteerMotion => this.m_SteerMotion;
        public float SteerAngle => this.m_SteerAngle;

        public bool HasMaxAngle
        {
            get => this.m_HasMaxAngle;
            set => this.m_HasMaxAngle = value;
        }

        public float MaxAngle
        {
            get => this.m_MaxAngle;
            set => this.m_MaxAngle = value;
        }

        public bool CanSnap
        {
            get => this.m_CanSnap;
            set => this.m_CanSnap = value;
        }

        public float SnapAngle
        {
            get => this.m_SnapAngle;
            set => this.m_SnapAngle = value;
        }

        public bool Recenter
        {
            get => this.m_Recenter;
            set
            {
                if (!this.m_Recenter && value) this.m_TargetAngle = 0;
                this.m_Recenter = value;
            }
        }

        public float Deadzone
        {
            get => this.m_Deadzone;
            set => this.m_Deadzone = value;
        }

        public float Sensitivity
        {
            get => this.m_Sensitivity;
            set => this.m_Sensitivity = value;
        }

        public float SmoothTime
        {
            get => this.FingerCount > 0 || (this.FingerCount == 0 && !this.m_Recenter)
                ? this.m_SteerDamping : this.m_LiftDamping;
        }

        public InputSimulateAxis InputSimulate => this.m_InputSimulate;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected override void Awake()
        {
            this.Setup();
        }

        protected internal override void Update()
        {
            this.UpdateSteerWheel();
        }

        protected internal override void Enable()
        {
            this.m_InputSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_InputSimulate?.OnDisabled();
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (this.FingerCount > 1) return;
            if (!this.HasPressInArea) return;

            Vector2 touchPoint = this.TouchableArea.PointToPointInArea(touch.screenPosition);

            this.m_WheelPosition = this.m_Wheel.parent != this.m_Control.transform 
                ? this.TouchableArea.PointToPointInArea(this.m_Wheel.position)
                : this.m_Wheel.localPosition;

            this.m_PreviousAngle = Vector2.Angle(Vector2.up, touchPoint - this.m_WheelPosition);
            this.m_PreviousAngle *= this.m_Sensitivity;

            this.m_TargetAngle = this.m_CurrentAngle;
        }

        protected internal override void InteractAfterDrag(Touch touch)
        {
            if (this.FingerCount == 0) return;
            if (this.TouchableArea.GetFinger(0) != touch.finger) return;

            Vector2 touchPoint = this.TouchableArea.PointToPointInArea(touch.screenPosition);
            this.CalculateSteerAngle(touchPoint);
        }

        protected internal override void InteractBeforeEnd(Touch touch)
        {
            if (this.FingerCount > 1) return;
            if (this.Recenter) this.m_TargetAngle = 0;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void UpdateSteerWheel()
        {
            float targetAngle = this.CanSnap
                ? Mathf.Round(this.m_TargetAngle / this.SnapAngle) * this.SnapAngle
                : this.m_TargetAngle;

            this.m_CurrentAngle = Math.Abs(targetAngle - this.m_CurrentAngle) > 0.01f
                ? Mathf.SmoothDamp(
                    this.m_CurrentAngle, targetAngle, ref this.m_DampVelocity,
                    this.SmoothTime, Mathf.Infinity, this.m_TimeMode.DeltaTime
                )
                : targetAngle;

            this.m_SteerMotion = this.HasMaxAngle
                ? this.m_CurrentAngle / this.MaxAngle 
                : Mathf.Clamp(this.m_CurrentAngle, -1, 1);

            this.m_SteerAngle = this.m_CurrentAngle - this.m_OriginAngle;

            this.m_Wheel.localEulerAngles = Vector3.forward * -this.m_SteerAngle;

            if (Mathf.Approximately(this.m_CurrentAngle, targetAngle))
                return;

            this.m_InputSimulate?.SendValueToControl(this.m_SteerMotion);
        }

        private void CalculateSteerAngle(Vector2 position)
        {
            Vector2 direction = position - this.m_WheelPosition;
			float deltaAngle = Vector2.Angle(Vector2.up, direction) * this.m_Sensitivity;
            float targetAngle = this.m_TargetAngle;

            if (direction.sqrMagnitude >= this.Deadzone * this.Deadzone)
            {
                if (position.x > this.m_WheelPosition.x)
                    targetAngle += deltaAngle - this.m_PreviousAngle;
				else
                    targetAngle -= deltaAngle - this.m_PreviousAngle;
            }

            this.m_TargetAngle = this.HasMaxAngle
                ? Mathf.Clamp(targetAngle, -this.MaxAngle, this.MaxAngle) : targetAngle;

            this.m_PreviousAngle = deltaAngle;
        }

        private void Setup()
        {
            if (this.m_Wheel == null)
            {
                var wheel = new GameObject("Wheel");
                this.m_Wheel = wheel.AddComponent<RectTransform>();
                this.m_Wheel.SetParent(this.m_Control.transform);
                this.m_Wheel.localPosition = Vector3.zero;
                this.m_Wheel.sizeDelta = new Vector2(100f, 100f);
            }

            this.m_OriginAngle = this.m_Wheel.localEulerAngles.z;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        protected internal override void DrawGizmos(Transform transform) 
        { 
            #if UNITY_EDITOR

            if (this.m_Wheel == null) return;

            Vector3 position = this.m_Wheel.position;
            Vector3 scale = this.m_Wheel.lossyScale;
            float maxScale = Mathf.Max(scale.x, scale.y);

            using (new UnityEditor.Handles.DrawingScope(new Color(1f, 0f, 0f, 0.5f)))
            {
                float deadzone = this.Deadzone * maxScale;
                UnityEditor.Handles.DrawWireDisc(position, this.m_Wheel.forward, deadzone, 2f);
            }

            if (!Application.isPlaying) return;

            using (new UnityEditor.Handles.DrawingScope(new Color(1f, 1f, 0f, 0.5f)))
            {
                if (this.Recenter)
                {
                    float initialRadius = 5f * maxScale;
                    float radiusIncrement = 1.5f * maxScale;
                    float angleIncrement = 5f;

                    int direction = this.m_CurrentAngle >= 0 ? -1 : 1;
                    float segments = Mathf.Abs(this.m_CurrentAngle) / angleIncrement;

                    Vector3 lastPosition = position;
                    for (int i = 0; i < Mathf.FloorToInt(segments) + 1; i++)
                    {
                        float angle = i * angleIncrement * direction;
                        float radians = (angle + 90f) * Mathf.Deg2Rad;

                        float currentRadius = initialRadius + i * radiusIncrement;
                        float offsetX = Mathf.Cos(radians) * currentRadius;
                        float offsetY = Mathf.Sin(radians) * currentRadius;

                        Vector3 nextPosition = position + new Vector3(offsetX, offsetY, 0f);
                        UnityEditor.Handles.DrawAAPolyLine(5f, lastPosition, nextPosition);

                        lastPosition = nextPosition;
                    }
                }

                float targetRadians = (-this.m_TargetAngle - 90) * Mathf.Deg2Rad;
                float targetRadius = 100f * maxScale;
                float targetX = -Mathf.Cos(targetRadians) * targetRadius;
                float targetY = -Mathf.Sin(targetRadians) * targetRadius;

                Vector3 targetPos = position + new Vector3(targetX, targetY, 0f);

                UnityEditor.Handles.color = new Color(1f, 0f, 1f, 0.5f);
                UnityEditor.Handles.DrawAAPolyLine(5f, position, targetPos);
            }

            #endif
        }

    }
}