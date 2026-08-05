using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Analog Stick")]
    [Category("Analog Stick")]

    [Description(
        "A control type that allows continuous motion in any direction. It consists of a " +
        "surface area and a movable handle that can be dragged freely within a defined range"
    )]

    [Parameter(
        "Input Simulate", 
        "The vector2 control path of the input control to be simulate"
    )]

    [Parameter(
        "Surface", 
        "Section for configuring the analog stick surface"
    )]
    [Parameter(
        "↳ Surface", 
        "<indent=1.2em>A reference to the Rect Transform component representing the surface"
    )]
    [Parameter(
        "↳ Padding", 
        "<indent=1.2em>Defines the padding or margin around the surface area"
    )]
    [Parameter(
        "↳ Dynamic", 
        "<indent=1.2em>Whether to move the position of the surface towards the touched point " +
        "on press"
    )]
    [Parameter(
        "↳ Constrain", 
        "<indent=1.2em>Whether to constrain the surface within the bounds of the rect transform"
    )]
    [Parameter(
        "↳ Reposition", 
        "<indent=1.2em>Whether to restore the original position of the surface once release"
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness applied to the surface's movement. The greater the value, " +
        "the smoother it will be"
    )]

    [Parameter(
        "Handle", 
        "Section for configuring the analog stick handle"
    )]
    [Parameter(
        "↳ Handle", 
        "<indent=1.2em>A reference to the Rect Transform component representing the handle"
    )]
    [Parameter(
        "↳ Axis", 
        "<indent=1.2em>Defines the axis of movement for the handle (e.g., X, Y, or both)"
    )]
    [Parameter(
        "↳ Relative", 
        "<indent=1.2em>Whether the handle's movement is relative to the initial touched point"
    )]
    [Parameter(
        "↳ Sensitive", 
        "<indent=1.2em>Whether to immediately respond to user input upon pressed or only " +
        "when dragging"
    )]
    [Parameter(
        "↳ Deadzone", 
        "<indent=1.2em>Whether to override the default deadzone. The values below the " +
        "min are clamped to 0, while values above the max are clamped to 1"
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness applied to the handle's movement for dragging or " +
        "recentering. The greater the value, the smoother it will be. This value will not " +
        "influence the analog stick's output; it is solely for visual"
    )]

    [Parameter(
        "Arrow", 
        "Section for configuring the analog stick arrow indicator"
    )]
    [Parameter(
        "↳ Arrow", 
        "<indent=1.2em>A reference to the Rect Transform component representing the arrow. " + 
        "It is Optional"
    )]
    [Parameter(
        "↳ Origin", 
        "<indent=1.2em>The origin direction from which the arrow rotates"
    )]
    [Parameter(
        "↳ Steps", 
        "<indent=1.2em>The number of discrete steps in the circular range that snap the arrow " +
        "rotation movement"
    )]
    [Parameter(
        "↳ Offset", 
        "<indent=1.2em>The angle offset in degree that adjusts the alignment of the steps"
    )]
    [Parameter(
        "↳ Threshold", 
        "<indent=1.2em>The threshold value to show the arrow indicator. When set to 0 the Arrow " +
        "will always visible; otherwise, becomes hidden after being released."
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness applied to the arrow's movement. The greater the value, " +
        "the smoother it will be"
    )]

    [Image(typeof(IconJoystick))]

    [Serializable]
    public class ControlTypeAnalogStick : TStickType
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private float          m_SPadding;
        [SerializeField] private bool           m_SDynamic;
        [SerializeField] private bool           m_SConstrain = true;
        [SerializeField] private bool           m_SReposition;
        [SerializeField] private float          m_SDamping;

        [SerializeField] private HandleAxis     m_HAxis = HandleAxis.BothXY;
        [SerializeField] private bool           m_HRelative;
        [SerializeField] private bool           m_HSensitive = true;
        [SerializeField] private bool           m_HOverrideDeadzone;
        [SerializeField] private Vector2        m_HDeadzone = new (0f, 1f);
        [SerializeField] private Vector2        m_HDamping = new (0.02f, 0f);

        [SerializeField] private ArrowDirection m_AOrigin;
        [SerializeField] private int            m_ADirectionSteps;
        [SerializeField] private float          m_ADegreeOffset;
        [SerializeField] private float          m_AThreshold = 0.28f;
        [SerializeField] private float          m_ADamping = 0.02f;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private Vector2         m_SurfaceOrigin;
        [NonSerialized] private Vector3         m_SurfaceVelocity;
        [NonSerialized] private Vector3         m_SurfaceTargetPos;

        [NonSerialized] private bool            m_HandleLocked;
        [NonSerialized] private Vector3         m_HandleVelocity;
        [NonSerialized] private Vector2         m_HandleTargetPos;
        [NonSerialized] private Vector2         m_HandleTargetRawPos;

        [NonSerialized] private float           m_ArrowVelocity;

        // PROPERTIES: ----------------------------------------------------------------------------

        public HandleAxis Axis
        {
            get => this.m_HAxis;
            set => this.m_HAxis = value;
        }

        public Vector2 SurfaceSize
        {
            get => new Vector2(
                this.m_Surface.sizeDelta.x - this.m_SPadding, 
                this.m_Surface.sizeDelta.y - this.m_SPadding
            );
        }

        public bool IsDynamic
        {
            get => this.m_SDynamic;
            set => this.m_SDynamic = value;
        }

        public bool IsConstrain
        {
            get => this.m_SDynamic && this.m_SConstrain;
            set => this.m_SConstrain = value;
        }

        public bool IsReposition
        {
            get => this.m_SDynamic && this.m_SReposition;
            set => this.m_SReposition = value;
        }

        public bool IsRelative
        {
            get => this.m_HRelative;
            set => this.m_HRelative = value;
        }

        public bool IsSensitive
        {
            get => this.m_HSensitive;
            set => this.m_HSensitive = value;
        }

        public float SurfaceDamping
        {
            get => this.m_SDamping;
            set => this.m_SDamping = value;
        }

        public float ArrowDamping
        {
            get => this.m_ADamping;
            set => this.m_ADamping = value;
        }

        public Vector2 HandleDamping
        {
            get => this.m_HDamping;
            set => this.m_HDamping = value;
        }

        public float DeadzoneMin
        {
            get => this.m_HOverrideDeadzone ? this.m_HDeadzone.x
                    : InputSystem.settings.defaultDeadzoneMin;

            set
            {
                this.m_HOverrideDeadzone = true;
                this.m_HDeadzone.x = value;
            }
        }

        public float DeadzoneMax
        {
            get => this.m_HOverrideDeadzone ? this.m_HDeadzone.y
                    : InputSystem.settings.defaultDeadzoneMax;

            set
            {
                this.m_HOverrideDeadzone = true;
                this.m_HDeadzone.y = value;
            }
        }

        public bool IsLocked
        {
            get => this.m_HandleLocked;

            set
            {
                if (!value && this.m_HandleLocked && this.FingerCount == 0)
                {
                    this.m_InputSimulate?.SendResetValueToControl();
                }
                this.m_HandleLocked = value;
            }
        }

        public Vector2 ArrowOrigin
        {
            get => this.m_AOrigin switch
            {
                ArrowDirection.Top => Vector2.up,
                ArrowDirection.Right => Vector2.right,
                ArrowDirection.Down => Vector2.down,
                ArrowDirection.Left => Vector2.left,
                _ => throw new NotImplementedException()
            };
        }

        public int ArrowDirectionSteps
        {
            get => this.m_ADirectionSteps;
            set => this.m_ADirectionSteps = value;
        }

        public float ArrowDegreeOffset
        {
            get => this.m_ADegreeOffset;
            set => this.m_ADegreeOffset = value;
        }

        public float ArrowThreshold
        {
            get => this.m_AThreshold;
            set => this.m_AThreshold = value;
        }

        public override Vector2 StickSize => this.SurfaceSize - this.m_Handle.sizeDelta;
        public override Vector2 StickMotion => this.m_HandleTargetPos;
        public override Vector2 StickRawMotion => this.m_HandleTargetRawPos;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected override void Awake()
        {
            this.Setup();
            
            this.m_SurfaceOrigin = this.m_Surface.localPosition;
            this.m_SurfaceTargetPos = this.m_SurfaceOrigin;
        }

        protected internal override void Update()
        {
            this.UpdateSurface();
            this.UpdateHandle();
            this.UpdateArrow();
        }

        protected internal override void Enable()
        {
            this.m_InputSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_InputSimulate?.OnDisabled();
            this.Reset();

            this.m_Handle.localPosition = this.m_HandleTargetPos;
            this.m_Surface.localPosition = this.m_SurfaceTargetPos;
            if (this.m_Arrow != null) this.m_Arrow.gameObject.SetActive(false);
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (this.FingerCount > 1) return;

            Vector3 touchPoint = this.TouchableArea.PointToPointInArea(touch.screenPosition);
            bool isInSurface = this.IsPointInsideSurface(touchPoint);

            if (!this.HasPressInArea)
            {
                if (!this.m_SDynamic || !isInSurface) return;

                Vector2 touchableArea = this.TouchableArea.uiTransform.sizeDelta;
                Vector2 surfaceArea = this.m_Surface.sizeDelta;

                if (touchableArea.x * touchableArea.y < surfaceArea.x * surfaceArea.y) return;
                this.TouchableArea.ForceInteract(touch);
            }

            if (this.m_SDynamic)
            {
                if (this.m_HandleLocked)
                {
                    Vector3 lockedPosition = this.m_Surface.position - this.m_Handle.position;
                    Vector3 scaleFactor = this.TouchableArea.uiTransform.lossyScale;

                    touchPoint += new Vector3(
                        lockedPosition.x * (1f / scaleFactor.x),
                        lockedPosition.y * (1f / scaleFactor.y),
                        lockedPosition.z * (1f / scaleFactor.z)
                    );
                }

                this.ConstrainSurface(ref touchPoint);

                this.m_SurfaceTargetPos = touchPoint;
                this.UpdateSurface();
            }

            this.m_HandleLocked = false;
        }

        protected internal override void InteractAfterDrag(Touch touch)
        {
            if (this.FingerCount == 0) return;
            if (this.TouchableArea.GetFinger(0) != touch.finger) return;
            if (touch.phase == TouchPhase.Stationary && !this.m_HSensitive) return;

            Vector2 touchPoint = touch.screenPosition;
            if (this.m_HRelative)
            {
                touchPoint -= touch.startScreenPosition;
            }
            else
            {
                touchPoint = this.TouchableArea.PointToPointInArea(touchPoint);
                touchPoint -= (Vector2) this.m_SurfaceTargetPos;
            }

            this.CalculateHandleTarget(touchPoint);
            this.m_InputSimulate?.SendValueToControl(this.m_HandleTargetPos);
        }

        protected internal override void InteractAfterEnd(Touch touch)
        {
            if (this.FingerCount > 0) return;
            this.Reset();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void UpdateSurface()
        {
            if (this.m_Surface.localPosition != this.m_SurfaceTargetPos)
            {
                this.m_Surface.localPosition = Vector3.SmoothDamp(
                    this.m_Surface.localPosition, this.m_SurfaceTargetPos,
                    ref this.m_SurfaceVelocity, this.m_SDamping, 
                    Mathf.Infinity, Time.unscaledDeltaTime
                );
            }
        }

        private bool IsPointInsideSurface(Vector2 localPoint)
        {
            Vector2 halfSize = this.m_Surface.sizeDelta * 0.5f;
            Vector2 deltaPoint = localPoint - (Vector2) this.m_Surface.localPosition;

            return deltaPoint.x * deltaPoint.x / (halfSize.x * halfSize.x) +
                   deltaPoint.y * deltaPoint.y / (halfSize.y * halfSize.y) <= 1;
        }

        private void ConstrainSurface(ref Vector3 position)
        {
            RectTransform rect = this.TouchableArea.uiTransform;
            Vector3[] corners = this.TouchableArea.worldCorners;

            float xMin = corners[0].x;
            float xMax = corners[2].x;
            float yMin = corners[0].y;
            float yMax = corners[2].y;

            Vector2 surfaceSize = Vector2.zero;
            Vector2 areaSize = rect.rect.size;
            areaSize *= rect.lossyScale * 0.5f;

            if (this.m_SConstrain)
            {
                surfaceSize = this.m_Surface.sizeDelta;
                surfaceSize *= this.m_Surface.lossyScale * 0.5f;
            }

            if (this.m_SConstrain && areaSize.x < surfaceSize.x)
            {
                float xMid = (xMax + xMin) * 0.5f;
                xMin = xMax = xMid;
            }
            else
            {
                xMin += surfaceSize.x;
                xMax -= surfaceSize.x;
            }

            if (this.m_SConstrain && areaSize.y < surfaceSize.y) 
            {
                float yMid = (yMax + yMin) * 0.5f;
                yMin = yMax = yMid;
            }
            else
            {
                yMin += surfaceSize.y;
                yMax -= surfaceSize.y;
            }

            Matrix4x4 localToWorld = rect.localToWorldMatrix;
            Vector3 worldPosition = localToWorld.MultiplyPoint3x4(position);
            worldPosition.x = Mathf.Clamp(worldPosition.x, xMin, xMax);
            worldPosition.y = Mathf.Clamp(worldPosition.y, yMin, yMax);

            Matrix4x4 worldToLocal = rect.worldToLocalMatrix;
            position = worldToLocal.MultiplyPoint3x4(worldPosition);
        }

        private void UpdateHandle()
        {
            Vector3 handleTargetPos = this.m_HandleTargetPos;
            handleTargetPos *= this.StickSize * 0.5f;

            if (this.m_Handle.localPosition != handleTargetPos)
            {
                this.m_Handle.localPosition = this.FingerCount != 0
                    ? Vector3.SmoothDamp(
                        this.m_Handle.localPosition, handleTargetPos,
                        ref this.m_HandleVelocity, this.m_HDamping.x, 
                        Mathf.Infinity, Time.unscaledDeltaTime)

                    : Vector3.SmoothDamp(
                        this.m_Handle.localPosition, handleTargetPos,
                        ref this.m_HandleVelocity, this.m_HDamping.y, 
                        Mathf.Infinity, Time.unscaledDeltaTime);
            }
        }

        private void CalculateHandleTarget(Vector2 position)
        {
            if (this.m_HAxis == HandleAxis.YOnly) position.x = 0f;
            else if (this.m_HAxis == HandleAxis.XOnly) position.y = 0f;

            this.m_HandleTargetRawPos = position / this.StickSize * 2f;

            float deadzoneMin = this.DeadzoneMin;
            float deadzoneMax = this.DeadzoneMax;

            float sqrMagnitude = this.m_HandleTargetRawPos.sqrMagnitude;
            if (sqrMagnitude > deadzoneMax * deadzoneMax)
            {
                float magnitude = (float) Math.Sqrt(sqrMagnitude);
                float xPos = this.m_HandleTargetRawPos.x / magnitude;
                float yPos = this.m_HandleTargetRawPos.y / magnitude;

                this.m_HandleTargetPos = new Vector2(xPos, yPos);
            }
            else if (sqrMagnitude < deadzoneMin * deadzoneMin)
            {
                this.m_HandleTargetPos = Vector2.zero;
            }
            else
            {
                this.m_HandleTargetPos = this.m_HandleTargetRawPos;
            }
        }

        private void UpdateArrow()
        {
            if (this.m_Arrow == null) return;

            GameObject arrow = this.m_Arrow.gameObject;
            bool isArrowActive = arrow.activeSelf;
            float sqrMagnitude = this.m_HandleTargetPos.sqrMagnitude;

            if (this.m_AThreshold > 0)
            {
                float sqrThreshold = Mathf.Max(this.m_AThreshold * this.m_AThreshold, 0.0001f);
                if (Math.Round(sqrMagnitude, 6) < sqrThreshold)
                {
                    if (isArrowActive) arrow.SetActive(false);
                    return;
                }
            }
            else if (sqrMagnitude == 0)
            {
                return;
            }

            float targetAngle = this.CalculateArrowAngle();
            float currentAngle = this.m_Arrow.localEulerAngles.z;
            
            float smoothAngle = isArrowActive 
                ? Mathf.SmoothDampAngle(
                    currentAngle, targetAngle, ref this.m_ArrowVelocity, 
                    this.m_ADamping, Mathf.Infinity, Time.unscaledDeltaTime) 
                : targetAngle;

            this.m_Arrow.localEulerAngles = Vector3.forward * smoothAngle;
            if (!isArrowActive) arrow.SetActive(true);
        }
        
        private float CalculateArrowAngle()
        {
            float angle = Vector2.SignedAngle(this.ArrowOrigin, this.m_HandleTargetPos);
            if (this.m_ADirectionSteps == 0) return angle;

            float directionStep = 360f / this.m_ADirectionSteps;
            float adjustedAngle = angle + (directionStep * 0.5f) - this.m_ADegreeOffset;
            float quantizedAngle = Mathf.Floor(adjustedAngle / directionStep) * directionStep;
            
            return quantizedAngle + this.m_ADegreeOffset;
        }

        internal void Setup()
        {
            if (this.m_Surface == null)
            {
                var surface = new GameObject("Surface");
                this.m_Surface = surface.AddComponent<RectTransform>();
                this.m_Surface.SetParent(this.m_Control.transform);
                this.m_Surface.localPosition = Vector3.zero;

                const float SURFACE_SIZE = 200f;
                this.m_Surface.sizeDelta = new Vector2(SURFACE_SIZE, SURFACE_SIZE);
            }

            if (this.m_Handle == null)
            {
                var handle = new GameObject("Handle");
                this.m_Handle = handle.AddComponent<RectTransform>();
                this.m_Handle.SetParent(this.m_Surface);
                this.m_Handle.localPosition = Vector3.zero;

                const float HANDLE_SIZE = 90f;
                this.m_Handle.sizeDelta = new Vector2(HANDLE_SIZE, HANDLE_SIZE);
            }

            var VECTOR2_HALF = new Vector2(0.5f, 0.5f);
            this.m_Handle.SetParent(this.m_Surface);
            this.m_Handle.anchorMax = VECTOR2_HALF;
            this.m_Handle.anchorMin = VECTOR2_HALF;
            this.m_Handle.pivot = VECTOR2_HALF;

            if (this.m_Arrow != null) 
            {
                Vector2 size = this.m_Arrow.sizeDelta;
                Vector2 point = this.m_Arrow.InverseTransformPoint(this.m_Surface.position);
                var pivot = new Vector2(0.5f + (point.x / size.x), 0.5f + (point.y / size.y));

                this.m_Arrow.pivot = pivot;
                this.m_Arrow.anchoredPosition += (this.m_Arrow.pivot - pivot) * size;
                this.m_Arrow.localPosition = Vector3.zero;
            }
        }

        private void Reset()
        {
            if (!this.m_SDynamic || this.m_SReposition) 
            {
                this.m_SurfaceTargetPos = this.m_SurfaceOrigin;
            }

            if (!this.m_HandleLocked) 
            {
                this.m_HandleTargetPos = Vector3.zero;
                this.m_HandleTargetRawPos = Vector3.zero;
                this.m_InputSimulate?.SendResetValueToControl();
            }
        }

        // GIZMOS: --------------------------------------------------------------------------------

        #if UNITY_EDITOR

        protected internal override void DrawGizmos(Transform transform)
        {
            if (this.m_Surface == null || this.m_Handle == null) return;
            
            Vector3 surfacePos, handlePos;
            Vector3 handleOffset = this.StickSize * this.m_HandleTargetPos * 0.5f;

            if (Application.isPlaying)
            {
                surfacePos = transform.TransformPoint(this.m_SurfaceTargetPos);
                handlePos = transform.TransformPoint(this.m_SurfaceTargetPos + handleOffset);
            }
            else
            {
                surfacePos = this.m_Surface.position;
                handlePos = this.m_Handle.position;
            }

            Quaternion surfaceRot = this.m_Surface.rotation;
            Vector3 lossyScale = this.m_Surface.lossyScale;
            Vector3 stickScl = this.StickSize * lossyScale;
            this.DrawWireDisc(surfacePos, surfaceRot, stickScl, Color.yellow, 0.5f);

            Vector3 deadzone = stickScl * 0.5f;
            this.DrawWireDisc(surfacePos, surfaceRot, deadzone, Color.red, this.DeadzoneMax);
            this.DrawWireDisc(surfacePos, surfaceRot, deadzone, Color.red, this.DeadzoneMin);

            Vector3 surfaceScl = this.SurfaceSize * lossyScale * 0.5f;
            this.DrawSurfaceCross(surfacePos, surfaceRot, surfaceScl, new Color(1f, 1f, 1f, 0.3f));

            Quaternion handleRot = this.m_Handle.rotation;
            Vector3 scl = this.m_Handle.sizeDelta * this.m_Handle.lossyScale * 0.5f;
            this.DrawHandleCross(handlePos, handleRot, scl, Color.blue);
        }

        private void DrawWireDisc(Vector3 pos, Quaternion rot, Vector3 scl, Color color, float radius)
        {
            var matrix = Matrix4x4.TRS(pos, rot, scl);
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
            }
        }

        private void DrawSurfaceCross(Vector3 pos, Quaternion rot, Vector3 scl, Color color)
        {
            var matrix = Matrix4x4.TRS(pos, rot, scl);
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                UnityEditor.Handles.DrawLine(Vector3.up, Vector3.down, 0.5f);
                UnityEditor.Handles.DrawLine(Vector3.left, Vector3.right, 0.5f);
                UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1f);
            }
        }

        private void DrawHandleCross(Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(pos, rot, scale);
            using (new UnityEditor.Handles.DrawingScope(color, matrix))
            {
                const float point = 0.707107f;

                UnityEditor.Handles.DrawLine(
                    new Vector3(point, -point, 0), new Vector3(-point, point, 0), 0.5f
                );

                UnityEditor.Handles.DrawLine(
                    new Vector3(point, point, 0), new Vector3(-point, -point, 0), 0.5f
                );

                UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1f);
            }
        }

        #endif

    }
}