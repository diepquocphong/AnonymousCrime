using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Touchable Area")]
    
    [Parameter(
        "Raycast", 
        "Determines if a raycast will perform at the touched point to detect if another " +
        "graphic element obstructs the interaction. Use a Layer Mask to filter objects " +
        "affected by the raycast"
    )]

    [Parameter(
        "Auto Press", 
        "If enabled, automatically perform press to a finger that enters the defined area. " +
        "You can also choose whether fingers already used by another Tactile Control are " +
        "allowed to trigger this behavior or not"
    )]
    [Parameter(
        "Auto Release", 
        "If enabled, automatically perform release to a finger that leaves the defined area"
    )]

    [Parameter(
        "Multi Touch", 
        "Allows multiple fingers to interact with the control at the same time"
    )]

    [Parameter(
        "Interaction Config", 
        "Section for overriding the default interaction configuration"
    )]
    [Parameter(
        "↳ Tap Time", 
        "<indent=1.2em>The time (in seconds) within which a press and release has to occur for " +
        "it to be registered as a tap. When toggled, uses a specified custom tap time; " +
        "otherwise, uses the default"
    )]
    [Parameter(
        "↳ Slow Tap Time", 
        "<indent=1.2em>The minimum duration required of a press-and-release interaction to " +
        "evaluate to a slow-tap-interaction. When toggled, uses a specified custom slow tap " +
        "time; otherwise, uses the default"
    )]
    [Parameter(
        "↳ Multi Tap Time", 
        "<indent=1.2em>The maximum duration that may pass between taps in order to evaluate " +
        "to a multi-tap-interaction. When toggled, uses a specified custom multi tap delay time; " +
        "otherwise, uses the default"
    )]
    [Parameter(
        "↳ Hold Time", 
        "<indent=1.2em>The minimum duration required of a press-and-release interaction to " +
        "evaluate to a hold-interaction. When toggled, uses a specified custom hold time; " +
        "otherwise, uses the default"
    )]
    [Parameter(
        "↳ Tap Radius", 
        "<indent=1.2em>The maximum radius that a touch contact may be moved from its origin to " +
        "evaluate to a tap-interaction. When toggled, uses a specified custom hold time; " +
        "otherwise, uses the default"
    )]
    [Parameter(
        "↳ Hold Radius", 
        "<indent=1.2em>The maximum radius that a touch contact may be moved from its origin to " +
        "evaluate to a hold-interaction."
    )]

    [Serializable]
    public abstract class TTouchableArea
    {
        private enum AutoPressOption : byte
        {
            DisallowSharedFinger,
            AllowSharedFinger
        }

        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private bool m_Raycast = true;
        [SerializeField] private LayerMask m_LayerMask = 0x20;

        [SerializeField] private bool m_AutoPress;
        [SerializeField] private AutoPressOption m_AutoPressOption = 0;

        [SerializeField] private bool m_AutoRelease;
        [SerializeField] private bool m_MultiTouch = true;

        [SerializeField] private TouchInteraction m_Interaction = new TouchInteraction();

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private bool m_IsClearing;
        [NonSerialized] private bool m_IsPressInArea;
        [NonSerialized] private bool m_IsReleaseInArea;

        [NonSerialized] private Vector3[] m_Corners = new Vector3[4];

        [NonSerialized] private Canvas m_UICanvas;
        [NonSerialized] private RectTransform m_UITransform;

        [NonSerialized] protected TactileControl m_Control;

        private List<Finger> m_FingerList;
        private Dictionary<int, Vector2> m_StartPositions;

        private PointerEventData m_PointerEventData;
        private List<RaycastResult> m_RaycastResults;

        protected static readonly Comparison<RaycastResult> s_RaycastComparer = RaycastComparer;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public bool CanRaycast 
        {
            get => this.m_Raycast;
            set => this.m_Raycast = value;
        }

        public LayerMask RaycastLayerMask
        {
            get => this.m_LayerMask;
            set => this.m_LayerMask = value;
        }

        #pragma warning disable IDE1006

        public int fingerCount => this.m_FingerList.Count;

        public bool isLastPressIsInArea => this.m_IsPressInArea;
        public bool isLastReleaseIsInArea => this.m_IsReleaseInArea;

        public RectTransform uiTransform => this.m_UITransform;

        public Canvas uiCanvas => this.m_UICanvas;

        public Camera uiCamera
        {
            get
            {
                if (this.m_UICanvas != null && this.m_UICanvas.renderMode == RenderMode.WorldSpace)
                {
                    return this.m_UICanvas.worldCamera != null
                        ? this.m_UICanvas.worldCamera : ShortcutMainCamera.Get<Camera>();
                }

                return null;
            }
        }

        public Vector3[] worldCorners
        {
            get
            {
                this.uiTransform.GetWorldCorners(this.m_Corners);
                return this.m_Corners;
            }
        }

        public Vector2 fingersCentroid
        {
            get
            {
                var sumPos = Vector2.zero;
                if (this.fingerCount == 0) return sumPos;

                for (int i = 0; i < this.fingerCount; i++)
                {
                    sumPos += this.m_FingerList[i].screenPosition;
                }

                return sumPos / this.fingerCount;
            }
        }

        public TouchInteraction interaction => this.m_Interaction;

        #pragma warning restore IDE1006

        // INITIALIZATION: ------------------------------------------------------------------------

        internal void Initialize(TactileControl control) 
        { 
            this.m_Control = control;
            this.m_FingerList = new List<Finger>(this.m_MultiTouch ? 10 : 1);
            this.m_StartPositions = new (this.m_MultiTouch ? 10 : 1);

            this.m_UITransform = control.transform as RectTransform;
            this.m_UICanvas = control.GetComponentInParent<Canvas>(true);

            if (this.m_UICanvas != null && !this.m_UICanvas.isRootCanvas) 
            {
                this.m_UICanvas = this.m_UICanvas.rootCanvas;
            }

            if (this.m_Raycast)
            {
                this.m_RaycastResults = new List<RaycastResult>();
                this.m_PointerEventData = new PointerEventData(EventSystem.current);
            }

            this.Setup();
        }
        
        protected internal virtual void Setup()
        { }
        
        internal void Update()
        { 
            if (this.m_Interaction.CanHold)
            {
                float holdRadius = this.m_Interaction.HoldRadius;
                float scaleFactor = GeneralRepository.Get.ScaleFactor;

                for (int i = 0; i < this.fingerCount; i++)
                {
                    Touch touch = this.m_FingerList[i].currentTouch;
                    if (!touch.valid) 
                        continue;

                    if (!this.m_StartPositions.TryGetValue(touch.touchId, out Vector2 startPos))
                        continue;

                    Vector2 deltaPosition = (startPos - touch.screenPosition) * scaleFactor;
                    if (deltaPosition.sqrMagnitude < holdRadius * holdRadius)
                        continue;

                    this.m_Interaction.CanHold = false;
                }
            }

            this.m_Interaction.ExecuteHoldEvent(this.m_Control);
        }

        // INTERACTION: ---------------------------------------------------------------------------

        internal void InteractBeforeBegin(Touch touch)
        {
            if (!this.m_MultiTouch && this.fingerCount > 0) 
            {
                this.m_IsPressInArea = false;
                return;
            }

            Vector2 touchPoint = touch.screenPosition;
            this.m_IsPressInArea = this.ContainsPoint(touchPoint) && this.IsRaycastHit(touchPoint);
        }

        internal void InteractAfterBegin(Touch touch, bool force = false)
        {
            if (!force && !this.m_IsPressInArea) return;

            this.RegisterFinger(touch.finger);

            this.m_StartPositions.Clear();
            for (int i = 0; i < this.fingerCount; i++)
            {
                Touch touch1 = this.m_FingerList[i].currentTouch;
                this.m_StartPositions[touch1.touchId] = touch1.screenPosition;
            }

            this.m_Interaction.ExecutePressEvent(this.m_Control);
        }

        internal void InteractBeforeDrag(Touch touch)
        {
            if (!this.m_AutoPress) return;
            if (touch.phase != TouchPhase.Moved) return;
            if (this.ContainsFinger(touch.finger)) return;
            if (!this.ContainsPoint(touch.screenPosition)) return;

            if (this.m_AutoPressOption == AutoPressOption.DisallowSharedFinger)
            {
                foreach (var tactile in TactileControl.s_Tactiles.Values)
                {
                    if (tactile == this.m_Control) continue;
                    if (!tactile.isActiveAndEnabled) continue;
                    if (tactile.TouchableArea.ContainsFinger(touch.finger))
                        return;
                }
            }

            this.m_Control.OnInteractBegin(touch);
        }

        internal void InteractAfterDrag(Touch touch)
        {
            if (!this.m_AutoRelease) return;
            if (touch.phase != TouchPhase.Moved) return;
            if (this.ContainsPoint(touch.screenPosition)) return;

            this.m_Control.EndInteract(touch);
        }

        internal void InteractBeforeEnd(Touch touch)
        {
            this.m_IsReleaseInArea = touch.valid && this.ContainsPoint(touch.screenPosition);

            if (!this.m_StartPositions.TryGetValue(touch.touchId, out Vector2 startPos))
                return;
            
            float tapRadius = this.m_Interaction.TapRadius;
            float scaleFactor = GeneralRepository.Get.ScaleFactor;
            Vector2 deltaPosition = (startPos - touch.screenPosition) * scaleFactor;
            if (deltaPosition.sqrMagnitude >= tapRadius * tapRadius) 
                return;

            this.m_Interaction.ExecuteTapEvent(this.m_Control);
        }

        internal void InteractAfterEnd(Touch touch)
        {
            this.m_Interaction.ExecuteBerforeReleaseEvent(this.fingerCount, this.m_Control);

            if (this.m_IsClearing)
            {
                this.m_FingerList.Clear();
                this.m_StartPositions.Clear();
                this.m_IsClearing = false;
            }
            else
            {
                this.UnRegisterFinger(touch.finger);
                this.m_StartPositions.Remove(touch.touchId);
            }

            this.m_Interaction.ExecuteArfterReleaseEvent(this.fingerCount, this.m_Control);
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public virtual bool ContainsPoint(Vector2 screenPoint) 
        {
            return true;
        }

        public Vector2 PointToPointInArea(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.uiTransform, screenPoint, this.uiCamera, out Vector2 pos
            );

            return pos;
        }

        public Finger GetFinger(int index)
        {
            return index < this.fingerCount ? this.m_FingerList[index] : null;
        }

        public bool ContainsFinger(Finger finger)
        {
            return this.m_FingerList.Contains(finger);
        }

        public void RegisterFinger(Finger finger)
        {
            if (this.ContainsFinger(finger)) return;
            this.m_FingerList.Add(finger);
        }

        public void UnRegisterFinger(Finger finger)
        {
            this.m_FingerList.Remove(finger);
        }

        public void RequestClearFingers()
        {
            this.m_IsClearing = true;
        }

        public void ForceInteract(Touch touch)
        {
            this.m_IsPressInArea = this.ContainsPoint(touch.screenPosition);
            this.InteractAfterBegin(touch, true);
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        protected bool IsRaycastHit(Vector2 screenPoint) 
        {
            if (!this.m_Raycast) return true;

            #if UNITY_EDITOR
            this.m_RaycastResults ??= new List<RaycastResult>();
            this.m_PointerEventData ??= new PointerEventData(EventSystem.current);
            #endif

            this.m_RaycastResults.Clear();
            this.m_PointerEventData.position = screenPoint;
            
            var raycasters = RaycasterManager.GetRaycasters();
            for (int i = 0; i < raycasters.Count; i++)
            {
                var raycaster = raycasters[i] as UnityEngine.UI.GraphicRaycaster;
                if (raycaster == null || !raycaster.IsActive()) continue;

                raycaster.Raycast(this.m_PointerEventData, this.m_RaycastResults);
            }
            
            if (this.m_RaycastResults.Count > 0)
            {
                this.m_RaycastResults.Sort(s_RaycastComparer);

                // for (int i = 1; i < this.m_RaycastResults.Count; i++)
                // {
                //     int j = i - 1;
                //     RaycastResult key = m_RaycastResults[i];
                //     while (j >= 0 && s_RaycastComparer(m_RaycastResults[j], key) > 0)
                //     {
                //         m_RaycastResults[j + 1] = m_RaycastResults[j];
                //         j--;
                //     }
                //     m_RaycastResults[j + 1] = key;
                // }

                for (int i = this.m_RaycastResults.Count - 1; i >= 0; i--)
                {
                    RaycastResult raycastResult = this.m_RaycastResults[i];
                    int raycastLayer = 1 << raycastResult.gameObject.layer;

                    if ((raycastLayer & this.m_LayerMask) == 0)
                    {
                        this.m_RaycastResults.RemoveAt(i);
                    }
                }
            }

            if (this.m_RaycastResults.Count == 0) return true;

            Transform target = this.m_RaycastResults[0].gameObject.transform;
            return target == this.uiTransform || target.IsChildOf(this.uiTransform);
        }

        private static int RaycastComparer(RaycastResult lhs, RaycastResult rhs)
        {
            if (lhs.module != rhs.module)
            {
                Camera eventCamera = lhs.module.eventCamera;
                Camera eventCamera2 = rhs.module.eventCamera;

                if (eventCamera != null && eventCamera2 != null && 
                    eventCamera.depth != eventCamera2.depth)
                {
                    if (eventCamera.depth < eventCamera2.depth) return 1;
                    if (eventCamera.depth == eventCamera2.depth) return 0;
                    return -1;
                }

                int sortOrderPriority = lhs.module.sortOrderPriority;
                int sortOrderPriority2 = rhs.module.sortOrderPriority;

                if (sortOrderPriority != sortOrderPriority2)
                {
                    return sortOrderPriority2.CompareTo(sortOrderPriority);
                }

                int renderOrderPriority = lhs.module.renderOrderPriority;
                int renderOrderPriority2 = rhs.module.renderOrderPriority;

                if (renderOrderPriority != renderOrderPriority2)
                {
                    return renderOrderPriority2.CompareTo(renderOrderPriority);
                }
            }

            if (lhs.sortingLayer != rhs.sortingLayer)
            {
                int layerValue = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
                int layerValue2 = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
                return layerValue.CompareTo(layerValue2);
            }

            if (lhs.sortingOrder != rhs.sortingOrder)
            {
                return rhs.sortingOrder.CompareTo(lhs.sortingOrder);
            }

            if (lhs.depth != rhs.depth && 
                lhs.module.rootRaycaster == rhs.module.rootRaycaster)
            {
                return rhs.depth.CompareTo(lhs.depth);
            }

            if (lhs.distance != rhs.distance)
            {
                return lhs.distance.CompareTo(rhs.distance);
            }

            return lhs.index.CompareTo(rhs.index);
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public virtual void DrawGizmos(TactileControl control) 
        { }

        [Obsolete("Please use TTouchableArea.DrawBrokenLine() instead.")]
        protected void DrawBorkenLine(Vector3 start, Vector3 end, ref bool drawSegment, float length)
        {
            this.DrawBrokenLine(start, end, length);
        }

        protected void DrawBrokenLine(Vector3 start, Vector3 end, float length = 7f)
        {
            #if UNITY_EDITOR

            Vector3 direction = (end - start).normalized;
            float totalLength = Vector3.Distance(start, end);
            int segments = Mathf.CeilToInt(totalLength / length);
            if (segments % 4 != 0) segments += 4 - segments % 4;

            for (int i = 0; i < segments; i += 4)
            {
                float segmentStart = i * length;
                float segmentEnd = Mathf.Min((i + 1) * length, totalLength);
                if (segmentStart < totalLength)
                {
                    UnityEditor.Handles.DrawAAPolyLine(
                        3f,
                        start + direction * segmentStart,
                        start + direction * segmentEnd
                    );
                }

                segmentStart = (i + 1) * length;
                segmentEnd = Mathf.Min((i + 2) * length, totalLength);
                if (segmentStart < totalLength)
                {
                    UnityEditor.Handles.DrawAAPolyLine(
                        3f,
                        start + direction * segmentStart,
                        start + direction * segmentEnd
                    );
                }
            }
            
            #endif
        }

        protected void DrawBrokenArc(Vector3 center, float radius, float startAngle, float sweepAngle)
        {
            #if UNITY_EDITOR

            float angle = Mathf.Abs(sweepAngle) / 360f;
            int segments = Mathf.Max(2, Mathf.CeilToInt(radius * angle));
            if (segments % 4 != 0) segments += 4 - segments % 4;

            float startRad = startAngle * Mathf.Deg2Rad;
            float sweepRad = sweepAngle * Mathf.Deg2Rad;
            float angleStep = sweepRad / segments;

            for (int i = 0; i < segments; i += 4)
            {
                for (int j = 0; j < 2; j++)
                {
                    int segIndex = i + j;
                    if (segIndex >= segments) break;

                    float angle1 = startRad + segIndex * angleStep;
                    float angle2 = startRad + (segIndex + 1) * angleStep;

                    float x1 = Mathf.Cos(angle1) * radius;
                    float y1 = Mathf.Sin(angle1) * radius;
                    Vector3 pos1 = center + new Vector3(x1, y1, 0f);
                    
                    float x2 = Mathf.Cos(angle2) * radius;
                    float y2 = Mathf.Sin(angle2) * radius;
                    Vector3 pos2 = center + new Vector3(x2, y2, 0f);

                    UnityEditor.Handles.DrawAAPolyLine(3f, pos1, pos2);
                }
            }

            #endif
        }

        protected Color GetGizmosColor(TactileControl control)
        {
            var color = new Color(1f, 1f, 1f, 0.5f);

            if (Application.isPlaying)
            {
                if (Touchscreen.current == null) color = new Color(1f, 0f, 0f, 0.5f);
                else if (!control.Interactable) color = new Color(1f, 0.5f, 0f, 0.8f);
            }

            return color;
        }

    }
}