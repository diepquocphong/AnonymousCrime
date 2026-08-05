using System;
using System.Collections.Generic;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Swipe Pad")]
    [Category("Swipe Pad")]

    [Description(
        "A control type that performs actions or responds based on the swipe direction, " +
        "typically involving the rapid movement of one or more fingers across a touchable area"
    )]

    [Parameter(
        "Continuous", 
        "If enabled, swipe actions keep triggering without lifting a finger. Otherwise, " +
        "only triggered once per swipe"
    )]

    [Parameter(
        "Directions", 
        "Section for configuring specific swipe direction parameters"
    )]
    [Parameter(
        "↳ Input Simulate", 
        "<indent=1.2em>The button control path for simulating input on swipe"
    )]
    [Parameter(
        "↳ Required Fingers", 
        "<indent=1.2em>The number of fingers required to perform a given swipe direction"
    )]
    [Parameter(
        "↳ Swipe ID", 
        "<indent=1.2em>A unique identifier for distinguishing different swipes"
    )]
    [Parameter(
        "↳ Angle", 
        "<indent=1.2em>Defines the direction of the swipe based on angle"
    )]
    [Parameter(
        "↳ Arc", 
        "<indent=1.2em>Specifies the angular range around the swipe angle"
    )]

    [Parameter(
        "Validation", 
        "Section for setting the criteria to validate swipe gestures"
    )]
    [Parameter(
        "↳ Min Swipe Distance", 
        "<indent=1.2em>The minimum distance of the straight line to be consider as a swipe"
    )]
    [Parameter(
        "↳ Max Swipe Duration", 
        "<indent=1.2em>The maximum allowed time (ms) to complete the swipe gesture"
    )]
    [Parameter(
        "↳ Min Sample Distance", 
        "<indent=1.2em>The minimum required distance between consecutive sample points"
    )]
    [Parameter(
        "↳ Max Sample Deviation", 
        "<indent=1.2em>The maximum allowable deviation of any sample point from the straight line"
    )]

    [Image(typeof(IconSwipe))]

    [Serializable]
    public class ControlTypeSwipePad : TControlType
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private bool m_Continuous = false;
        [SerializeField] private SwipeDirections m_Directions = new SwipeDirections();

        [SerializeField] private float m_MinSwipeDistance = 150f;
        [SerializeField] private float m_MaxSwipeDuration = 0.4f;
        [SerializeField] private float m_MinSampleDistance = 50f;
        [SerializeField] private float m_MaxSampleDeviation = 150f;

        [SerializeField] private float m_DirectionReversalThreshold = 2800f;

        // MEMBERS: -------------------------------------------------------------------------------

        private List<Vector2> m_DeltaPoints;

        [NonSerialized] private float m_SwipeTimeOut;
        [NonSerialized] private float m_SwipeStartTime;
        [NonSerialized] private float m_SwipeSignedAngle;

        [NonSerialized] private int m_StartPointIndex;
        [NonSerialized] private Vector2 m_SwipeDirection;

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool IsContinuous
        {
            get => this.m_Continuous;
            set => this.m_Continuous = value;
        }

        public float MinSwipeDistance
        {
            get => this.m_MinSwipeDistance;
            set => this.m_MinSwipeDistance = value;
        }

        public float MaxSwipeDuration
        {
            get => this.m_MaxSwipeDuration;
            set => this.m_MaxSwipeDuration = value;
        }

        public float MinSampleDistance
        {
            get => this.m_MinSampleDistance;
            set => this.m_MinSampleDistance = value;
        }

        public float MaxSampleDeviation
        {
            get => this.m_MaxSampleDeviation;
            set => this.m_MaxSampleDeviation = value;
        }

        public float SwipeAngle
        {
            get => (360f - this.m_SwipeSignedAngle) % 360f;
        }

        public float SwipeSignedAngle
        {
            get => this.m_SwipeSignedAngle;
        }

        public Vector2 SwipeDirection
        {
            get => this.m_SwipeDirection;
        }

        public SwipeDirections Directions => this.m_Directions;

        private Vector2 StartPoint => this.m_DeltaPoints[this.m_StartPointIndex];
        private Vector2 EndPoint => this.m_DeltaPoints[^1];

        // EVENTS: -------------------------------------------------------------------------------

        public event Action<int> EventSwipe;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected override void Awake()
        {
            this.m_DeltaPoints = new List<Vector2>(20);
        }

        protected internal override void Enable()
        {
            this.m_Directions.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_Directions.OnDisabled();
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (!this.HasPressInArea) return;
            this.InitializeSwipe();
        }

        protected internal override void InteractAfterDrag(Touch touch)
        {
            if (this.FingerCount == 0) return;

            float currentTime = Time.unscaledTime;
            if (touch.phase == TouchPhase.Stationary) 
            {
                if (this.m_Continuous && this.m_SwipeTimeOut < currentTime)
                {
                    this.PerformSwipe();

                    this.TouchableArea.RegisterFinger(touch.finger);
                    this.InitializeSwipe();

                    this.m_SwipeTimeOut = float.MaxValue;
                    this.m_SwipeStartTime = currentTime;
                }

                return;
            }

            this.RecordSamplePoint();

            if (this.m_Continuous) this.m_SwipeTimeOut = currentTime + 0.1f;
            if (this.m_DeltaPoints.Count <= 2) this.m_SwipeStartTime = currentTime;
        }

        protected internal override void InteractBeforeEnd(Touch touch)
        {
            this.PerformSwipe();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void InitializeSwipe()
        {
            this.m_StartPointIndex = 0;
            this.m_SwipeStartTime = Time.unscaledTime;

            this.m_DeltaPoints.Clear();
            this.m_DeltaPoints.Add(this.TouchableArea.fingersCentroid);
        }

        private void PerformSwipe()
        {
            this.m_DeltaPoints.Add(this.TouchableArea.fingersCentroid);

            if (this.IsSwipeValid())
            {
                this.m_SwipeDirection = (this.EndPoint - this.StartPoint) * this.ScaleFactor;
                this.m_SwipeSignedAngle = Vector2.SignedAngle(Vector2.up, this.m_SwipeDirection);
                int hash = this.m_Directions.CheckForDirections(this.SwipeAngle, this.FingerCount);
                this.EventSwipe?.Invoke(hash);
            }
            else
            {
                this.m_SwipeDirection = Vector2.zero;
            }

            this.TouchableArea.RequestClearFingers();
        }

        private void RecordSamplePoint()
        {
            Vector2 currentSwipePoint = this.TouchableArea.fingersCentroid;
            Vector2 deltaSwipePoint = (this.EndPoint - currentSwipePoint) * this.ScaleFactor;

            float sqrThreshold = this.m_MinSampleDistance * this.m_MinSampleDistance;
            if (deltaSwipePoint.sqrMagnitude < sqrThreshold) return;

            this.m_DeltaPoints.Add(currentSwipePoint);
            if (this.m_DeltaPoints.Count < 3) return;

            Vector2 lhs = this.m_DeltaPoints[^1] - this.m_DeltaPoints[^2];
            Vector2 rhs = this.m_DeltaPoints[^2] - this.m_DeltaPoints[^3];

            float projection = Vector2.Dot(lhs, rhs);
            if (projection < this.m_DirectionReversalThreshold)
            {
                this.m_SwipeStartTime = Time.unscaledTime;
                this.m_StartPointIndex = this.m_DeltaPoints.Count - 2;
            }
        }

        private bool IsSwipeValid()
        {
            Vector2 direction = (this.EndPoint - this.StartPoint) * this.ScaleFactor;
            float sqrMinDistance = this.m_MinSwipeDistance * this.m_MinSwipeDistance;
            if (direction.sqrMagnitude < sqrMinDistance) return false;
            
            direction.Normalize();
            for (int i = this.m_StartPointIndex; i < this.m_DeltaPoints.Count; i++)
            {
                if (!this.IsSamplePointValid(this.m_DeltaPoints[i], direction))
                {
                    return false;
                }
            }

            float duration = Time.unscaledTime - this.m_SwipeStartTime;
            if (duration > this.m_MaxSwipeDuration) return false;

            return true;
        }

        private bool IsSamplePointValid(Vector2 point, Vector2 normal)
        {
            Vector2 toPoint = point - this.StartPoint;
            float projection = Vector2.Dot(toPoint, normal);

            Vector2 closestPoint = this.StartPoint + normal * projection;
            float sqrDeviation = (closestPoint - point).sqrMagnitude;

            return sqrDeviation <= this.m_MaxSampleDeviation * this.m_MaxSampleDeviation;
        }

        // GIZMOS: --------------------------------------------------------------------------------
        
        #if UNITY_EDITOR

        protected internal override void DrawGizmos(Transform transform) 
        {
            if (!Application.isPlaying || this.m_DeltaPoints.Count < 3) return;
            RectTransform rect = (RectTransform) transform;

            float width = 5f;
            float maxScale = 1f;
            
            int pointCount = this.m_DeltaPoints.Count;
            var pointList = new Vector3[pointCount];
            bool isZeroFingers = this.FingerCount == 0;

            for (int i = 0; i < pointCount; i++) pointList[i] = this.m_DeltaPoints[i];

            Vector3 swipeDir = isZeroFingers 
                ? (this.EndPoint - this.StartPoint).normalized 
                : Vector3.zero;

            using (new UnityEditor.Handles.DrawingScope(Color.white))
            {
                if (isZeroFingers)
                {
                    const float HEAD_ANGLE = 45f;
                    const float HEAD_LENGTH = 50f;

                    Vector3 lineStart = pointList[this.m_StartPointIndex];
                    Vector3 lineEnd = pointList[^1];

                    Vector3 rightRot = Quaternion.Euler(0, 0, HEAD_ANGLE) * -swipeDir;
                    Vector3 leftRot = Quaternion.Euler(0, 0, -HEAD_ANGLE) * -swipeDir;
                    float headLength = HEAD_LENGTH * maxScale;

                    Vector3 rightHead = lineEnd + Quaternion.LookRotation(
                        Vector3.forward) * rightRot * headLength;

                    Vector3 leftHead = lineEnd + Quaternion.LookRotation(
                        Vector3.forward) * leftRot * headLength;

                    UnityEditor.Handles.DrawAAPolyLine(width, lineStart, lineEnd);
                    UnityEditor.Handles.DrawAAPolyLine(width, rightHead, lineEnd, leftHead);
                }

                UnityEditor.Handles.DrawAAPolyLine(width, pointList);

                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 deltaPoint = pointList[i];

                    if (isZeroFingers)
                    {
                        UnityEditor.Handles.color = (i < this.m_StartPointIndex) 
                            ? Color.white : this.IsSamplePointValid(deltaPoint, swipeDir) 
                                ? Color.green : Color.red;
                    }

                    UnityEditor.Handles.DrawSolidDisc(deltaPoint, Vector3.forward, 10f * maxScale);
                }
            }
        }

        #endif

    }
}