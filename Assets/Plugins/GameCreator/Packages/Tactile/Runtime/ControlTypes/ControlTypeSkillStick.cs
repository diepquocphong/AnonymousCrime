using System;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Skill Stick")]
    [Category("Skill Stick")]
    
    [Description(
        "A Skill control type and a simplified Analog Stick for directional skill " +
        "aiming. Supports skill activation, cooldowns, and cancellation."
    )]

    [Parameter(
        "Input Simulate",
        "The vector2 control path of the input control to be simulate when dragging"
    )]

    [Parameter(
        "Button Simulate", 
        "The button control path of the input control to be simulate when activating the skill"
    )]
    [Parameter(
        "↳ Is Usable",
        "<indent=1.2em>Determines whether the Skill Stick can be used"
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
        "↳ Damping", 
        "<indent=1.2em>The smoothness applied to the handle's movement. The greater the value, " +
        "the smoother it will be. This value will not influence the analog stick's output; " +
        "it is solely for visual"
    )]

    [Parameter(
        "Arrow", 
        "Section for configuring the analog stick arrow indicator"
    )]
    [Parameter(
        "↳ Arrow", 
        "<indent=1.2em>A reference to the Rect Transform component representing the arrow"
    )]
    [Parameter(
        "↳ Origin", 
        "<indent=1.2em>The origin direction from which the arrow rotates"
    )]
    [Parameter(
        "↳ Threshold", 
        "<indent=1.2em>The threshold for activating the arrow indicator"
    )]
    [Parameter(
        "↳ Damping", 
        "<indent=1.2em>The smoothness applied to the arrow's movement. The greater the value, " +
        "the smoother it will be"
    )]

    [Parameter(
        "Cooldown", 
        "Section for configuring the cooldown"
    )]
    [Parameter(
        "↳ Fill",
        "<indent=1.2em>A reference to a image component with image type set to filled"
    )]
    [Parameter(
        "↳ Text", 
        "<indent=1.2em>A reference to a text or TMP component to display the remaining cooldown"
    )]
    [Parameter(
        "↳ Format",
        "<indent=1.2em>The number format used when displaying the cooldown as text." +
        "\ne.g." +
        "\n    {0} → 12" +
        "\n    {0.0} or {1} → 12.3" +
        "\n    {0.00} or {2} → 12.34"
    )]
    [Parameter(
        "↳ Duration", 
        "<indent=1.2em>The total time duration to finish the cooldown"
    )]
    [Parameter(
        "↳ Time Mode", 
        "<indent=1.2em>The time scale that affects the duration of the cooldown"
    )]
    [Parameter(
        "↳ Is Manual",
        "<indent=1.2em>Determines whether the cooldown is managed manually (via Instruction " +
        "or scripting). If set to false, the cooldown starts automatically after casting."
    )]

    [Parameter(
        "Cancellation", 
        "Section for configuring the skill cancellation"
    )]
    [Parameter(
        "↳ Cancel Area", 
        "<indent=1.2em>A reference to a Tactile Control component. Interactions such as press, " +
        "release, etc. of a Cancel Area will also trigger regardless if its interactable or not " +
        "when using the Skill Stick"
    )]
    [Parameter(
        "↳ Is Inverted",
        "<indent=1.2em>If true, cancellation is triggered outside the Cancel Area. " +
        "If false, it is triggered inside the area."
    )]

    [Image(typeof(IconJoystick), typeof(OverlayBolt))]

    [Serializable]
    public class ControlTypeSkillStick : TStickType, ISkillType
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private InputSimulateButton  m_ButtonSimulate;
        [SerializeField] private bool                 m_IsUsable = true;

        [SerializeField] private float                m_SPadding;

        [SerializeField] private HandleAxis           m_HAxis = HandleAxis.BothXY;
        [SerializeField] private float                m_HDamping = 0.02f;

        [SerializeField] private ArrowDirection       m_AOrigin;
        [SerializeField] private float                m_AThreshold = 0.28f;
        [SerializeField] private float                m_ADamping = 0.02f;

        [SerializeField] private SkillCooldown        m_Cooldown = new SkillCooldown();
        [SerializeField] private SkillCancellation    m_Cancellation = new SkillCancellation();

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private Vector2 m_SurfaceOrigin;
        [NonSerialized] private Vector3 m_SurfaceTargetPos;

        [NonSerialized] private Vector3 m_HandleVelocity;
        [NonSerialized] private Vector2 m_HandleTargetPos;
        [NonSerialized] private Vector2 m_HandleTargetRawPos;

        [NonSerialized] private bool    m_ArrowExist;
        [NonSerialized] private float   m_ArrowVelocity;

        [NonSerialized] private bool    m_CanBypassReset;
        [NonSerialized] private bool    m_IsCooldownOnPressed;

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

        public override Vector2 StickSize => this.SurfaceSize - this.m_Handle.sizeDelta;
        public override Vector2 StickMotion => this.m_HandleTargetPos;
        public override Vector2 StickRawMotion => this.m_HandleTargetRawPos;

        public bool IsUsable
        {
            get => this.m_IsUsable;
            set
            {
                if (this.m_IsUsable && !value)
                {
                    this.m_IsCooldownOnPressed = true;

                    this.Reset();
                    this.m_Control.ForceEndInteractAll();
                }

                this.m_IsUsable = value;
            }
        }

        public bool IsCooldown => this.m_Cooldown.IsCooldown;
        public float CooldownRatio => this.m_Cooldown.Ratio;
        public float CooldownRemaining => this.m_Cooldown.Remaining;

        public InputSimulateButton ButtonSimulate => this.m_ButtonSimulate;

        // EVENTS: -------------------------------------------------------------------------------

        public event Action EventStartCooldown
        {
            add => this.m_Cooldown.EventStart += value;
            remove => this.m_Cooldown.EventStart -= value;
        }
        
        public event Action EventResetCooldown
        {
            add => this.m_Cooldown.EventReset += value;
            remove => this.m_Cooldown.EventReset -= value;
        }

        public event Action EventActivateSkill;

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected override void Awake()
        {
            this.Setup();
            this.m_Cooldown.Setup(this.Args);
            this.m_Cancellation.Setup(this.m_Control);
            
            this.m_SurfaceOrigin = this.m_Surface.localPosition;
            this.m_SurfaceTargetPos = this.m_SurfaceOrigin;
        }

        protected internal override void Update()
        {
            this.m_Cooldown.Update();

            this.UpdateHandle();
            this.UpdateArrow();
        }

        protected internal override void Enable()
        {
            this.m_InputSimulate?.OnEnabled(this.m_Control);
            this.m_ButtonSimulate?.OnEnabled(this.m_Control);
        }

        protected internal override void Disable()
        {
            this.m_InputSimulate?.OnDisabled();
            this.m_ButtonSimulate?.OnDisabled();

            this.Reset();

            this.m_Handle.localPosition = this.m_HandleTargetPos;
            this.m_Surface.localPosition = this.m_SurfaceTargetPos;
            if (this.m_Arrow != null) this.m_Arrow.gameObject.SetActive(false);
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (!this.IsUsable) return;
            if (!this.HasPressInArea) return;
            if (this.FingerCount > 1) return;

            this.m_CanBypassReset = false;
            this.m_IsCooldownOnPressed = this.IsCooldown;
            if (this.m_IsCooldownOnPressed) return;

            this.m_SurfaceTargetPos = this.TouchableArea.PointToPointInArea(touch.screenPosition);
            this.m_Surface.localPosition = this.m_SurfaceTargetPos;
            this.m_Surface.gameObject.SetActive(true);

            this.m_Cancellation.SendCancelPress(touch);
        }

        protected internal override void InteractAfterDrag(Touch touch)
        {
            if (!this.IsUsable || this.FingerCount == 0) return;
            if (this.m_IsCooldownOnPressed || this.IsCooldown) return;
            if (touch.phase == TouchPhase.Stationary) return;
            if (this.TouchableArea.GetFinger(0) != touch.finger) return;

            Vector2 localPoint = this.TouchableArea.PointToPointInArea(touch.screenPosition);
            localPoint -= (Vector2) this.m_SurfaceTargetPos;

            this.CalculateHandleTarget(localPoint);
            this.m_InputSimulate?.SendValueToControl(this.m_HandleTargetPos);
        }

        protected internal override void InteractBeforeEnd(Touch touch)
        {
            if (!this.IsUsable || this.FingerCount > 1) return;
            if (this.m_IsCooldownOnPressed || this.IsCooldown) return;
            if (!this.m_Cancellation.IsWithinArea(touch.screenPosition))
            {
                this.m_ButtonSimulate?.SendValueToControl(1f);
                this.EventActivateSkill?.Invoke();
                this.m_ButtonSimulate?.SendValueToControl(0f);

                this.m_Cooldown.Start();
                this.m_CanBypassReset = true;
            }
        }

        protected internal override void InteractAfterEnd(Touch touch)
        {
            if (!this.m_CanBypassReset)
            {
                if (!this.IsUsable) return;
                if (this.IsCooldown || this.m_IsCooldownOnPressed) return;
                if (this.FingerCount > 0) return;
            }
            
            this.Reset();
            this.m_Cancellation.SendCancelRelease(touch);
        }

        // COOLDOWN: ------------------------------------------------------------------------------

        public void StartCooldown(bool force = false)
        {
            this.m_IsCooldownOnPressed = true;
            this.m_Cooldown.Start(force);

            this.Reset();
            this.m_Control.ForceEndInteractAll();
        }

        public void ResetCooldown() => this.m_Cooldown.Reset();

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void UpdateHandle()
        {
            Vector3 handleTargetPos = this.m_HandleTargetPos;
            handleTargetPos *= this.StickSize * 0.5f;

            if (this.m_Handle.localPosition != handleTargetPos)
            {
                this.m_Handle.localPosition = Vector3.SmoothDamp(
                    this.m_Handle.localPosition, handleTargetPos,
                    ref this.m_HandleVelocity, this.m_HDamping, 
                    Mathf.Infinity, Time.unscaledDeltaTime
                );
            }
        }

        private void CalculateHandleTarget(Vector2 position)
        {
            if (this.m_HAxis == HandleAxis.YOnly) position.x = 0f;
            else if (this.m_HAxis == HandleAxis.XOnly) position.y = 0f;

            this.m_HandleTargetRawPos = position / this.StickSize * 2f;
            this.m_HandleTargetPos = Vector2.ClampMagnitude(this.m_HandleTargetRawPos, 1f);
        }

        private void UpdateArrow()
        {
            if (!this.m_ArrowExist) return;

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
                if (isArrowActive) arrow.SetActive(false);
                return;
            }

            float targetAngle = Vector2.SignedAngle(ArrowOrigin, this.m_HandleTargetPos);

            float newAngle = isArrowActive 
                ? Mathf.SmoothDampAngle(
                    this.m_Arrow.localEulerAngles.z, targetAngle,
                    ref this.m_ArrowVelocity, this.m_ADamping, 
                    Mathf.Infinity, Time.unscaledDeltaTime)
                : targetAngle;

            this.m_Arrow.localEulerAngles = Vector3.forward * newAngle;
            if (!isArrowActive) arrow.SetActive(true);
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
            this.m_Surface.gameObject.SetActive(false);

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

                this.m_ArrowExist = true;
            }
        }

        private void Reset()
        {
            this.m_Surface.gameObject.SetActive(false);
            this.m_SurfaceTargetPos = this.m_SurfaceOrigin;
            this.m_Surface.localPosition = this.m_SurfaceTargetPos;

            this.m_HandleTargetPos = Vector3.zero;
            this.m_HandleTargetRawPos = Vector3.zero;

            this.m_InputSimulate?.SendResetValueToControl();
        }

        // GIZMOS: --------------------------------------------------------------------------------
        
        #if UNITY_EDITOR

        protected internal override void DrawGizmos(Transform transform)
        {
            this.m_Cancellation.DrawGizmos(transform);

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