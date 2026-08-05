using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile
{
    public class PolygonPathTool : VisualElement
    {
        private const string PATH_USS = EditorPaths.PACKAGES + 
                                        "Tactile/Editor/StyleSheets/polygon-path-tool";

        private const float HANDLE_RADIUS = 0.05f;

        // MEMBERS: -------------------------------------------------------------------------------

        private int m_CurrentLineIndex = -1;
		private int m_CurrentPointIndex = -1;
        
        private Vector2 m_StartDragPosition;
        private Vector2 m_CurrentDragPosition;
        private Vector2 m_InitialTargetDragPosition;

        private bool m_IsDirty;
        private bool m_IsEditing;
        private bool m_IsDraging;
        private bool m_IsDeleting;

        private SerializedObject m_Object;
        private SerializedProperty m_Points;

        private Button m_EditButton;
        private ListView m_PointList;
        private Transform m_Transform;
        private TactileControl m_Control;

        private GUIStyle m_PointLabelStyle = new GUIStyle()
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12,
        };

        private GUIStyleState m_NormalLabelColor = new() { textColor = Theme.MainColor };
        private GUIStyleState m_DeleteLabelColor = new() { textColor = Color.red };
        private GUIStyleState m_SelectedLabelColor = new() { textColor = Color.yellow };

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public PolygonPathTool(SerializedProperty property)
        {
            this.m_Points = property.FindPropertyRelative("m_Points");
            if (this.m_Points == null) return;

            this.m_Object = property.serializedObject;
            this.m_Control = (TactileControl)this.m_Object.targetObject;
            this.m_Transform = this.m_Control.transform;

            var error = new ErrorMessage("You need at least three points to define an area");
            error.style.display = this.m_Points.arraySize < 3
                ? DisplayStyle.Flex : DisplayStyle.None;

            var info = new InfoMessage(
                "<b> Add</b><indent=4em>:   LMB on line segment</indent><br>" +
                "<b> Move</b><indent=4em>:   LMB Hold & Drag the point</indent><br>" +
                "<b> Undo</b><indent=4em>:   Ctrl + Z to cancel last action</indent><br>" +
                "<b> Delete</b><indent=4em>:   Hold Ctrl + LMB on line segment</indent>"

                #if UNITY_6000_0_OR_NEWER
                + "<br><b> Snap</b><indent=4em>:   Use scene view grid snapping</indent>"
                #endif
            );
            info.name = "Feature-Dialog";
            info.style.display = this.m_IsEditing ? DisplayStyle.Flex : DisplayStyle.None;

            this.m_PointList = new ListView
            {
                showFoldoutHeader = false,
                showAddRemoveFooter = false,
                showBoundCollectionSize = false,
                showBorder = true,

                bindItem = (element, index) =>
                {
                    if (element is not PropertyField field) return;
                    var property = this.m_Points.GetArrayElementAtIndex(index);
                    field.BindProperty(property);
                    field.label = "Point " + index;
                }
            };
            this.m_PointList.BindProperty(this.m_Points);
            this.m_PointList.style.display = this.m_IsEditing
                ? DisplayStyle.Flex : DisplayStyle.None;

            this.m_PointList.selectedIndicesChanged += (indices) =>
            {
                foreach (int index in indices)
                {
                    this.m_CurrentPointIndex = index;
                }

                SceneView.RepaintAll();
            };

            var editField = new VisualElement();
            editField.AddToClassList("points-edit-field");

            var editLabel = new Label("Points");
            editLabel.AddToClassList("points-edit-field__label");

            this.m_EditButton = new Button(() =>
            {
                this.m_IsEditing = !this.m_IsEditing;
                property.isExpanded = !this.m_IsEditing;

                if (this.m_IsEditing)
                {
                    error.style.display = DisplayStyle.None;
                    info.style.display = DisplayStyle.Flex;
                    this.m_PointList.style.display = DisplayStyle.Flex;
                }
                else
                {
                    error.style.display = this.m_Points.arraySize < 3
                        ? DisplayStyle.Flex : DisplayStyle.None;

                    info.style.display = DisplayStyle.None;
                    this.m_PointList.style.display = DisplayStyle.None;
                }

                this.RefreshEditModeButton();
            });
            this.m_EditButton.AddToClassList("points-edit-field__button");
            this.RefreshEditModeButton();

            editField.Add(editLabel);
            editField.Add(this.m_EditButton);
            AlignLabel.On(editField);

            this.RegisterCallback<DetachFromPanelEvent>(callback =>
            {
                Tools.hidden = false;
                TactileControlEditor.EventSceneGUI -= this.OnSceneGUI;

                if (this.m_IsEditing)
                {
                    TactileControl control = this.m_Control;
                    EditorApplication.delayCall += delegate()
                    {
                        if (control == null) return;
                        using var serializedObject = new SerializedObject(control);
                        serializedObject.FindProperty("m_TouchableArea").isExpanded = true;
                    };
                }
            });

            this.Add(error);
            this.Add(info);
            this.Add(editField);
            this.Add(this.m_PointList);

            StyleSheet[] styleSheets = StyleSheetUtils.Load(PATH_USS);
            foreach (StyleSheet sheet in styleSheets) this.styleSheets.Add(sheet);
        }

        private void RefreshEditModeButton()
        {
            if (this.m_EditButton == null) return;

            this.m_EditButton.text = this.m_IsEditing
                ? "Exit Edit Mode" 
                : "Enter Edit Mode";

            Color borderColor = this.m_IsEditing
                ? Theme.MainColor
                : ColorTheme.Get(ColorTheme.Type.Dark);
            
            this.m_EditButton.style.borderTopColor = borderColor;
            this.m_EditButton.style.borderBottomColor = borderColor;
            this.m_EditButton.style.borderLeftColor = borderColor;
            this.m_EditButton.style.borderRightColor = borderColor;

            this.m_EditButton.style.color = this.m_IsEditing
                ? Theme.MainColor
                : ColorTheme.Get(ColorTheme.Type.TextNormal);

            if (this.m_IsEditing)
                TactileControlEditor.EventSceneGUI += this.OnSceneGUI;
            else
                TactileControlEditor.EventSceneGUI -= this.OnSceneGUI;

            Tools.hidden = this.m_IsEditing;
            SceneView.RepaintAll();
        }

        // GUI: -----------------------------------------------------------------------------------

        private void OnSceneGUI(object target)
        {
            Event guiEvent = Event.current;

            this.m_IsDeleting = //this.m_Points.arraySize > 3 && 
                                guiEvent.modifiers == EventModifiers.Control;

            switch (guiEvent.type)
            {
                case EventType.Repaint:
                    this.DrawSceneHandles(guiEvent);
                    break;

                case EventType.Layout:
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                    break;

                default:
                    this.HandleInput(guiEvent);
                    if (this.m_IsDirty) HandleUtility.Repaint();
                    break;
            }
        }

        // DRAW: ----------------------------------------------------------------------------------

        private void DrawSceneHandles(Event guiEvent)
        {
            this.m_IsDirty = false;
            var handleColor = Handles.color;

            for (int i = 0; i < this.m_Points.arraySize; i++)
                this.DrawLineSegment(i);

            if (this.m_IsDeleting && !this.m_IsDraging) return;
            this.m_PointList.selectedIndex = this.m_CurrentPointIndex;

            if (this.m_CurrentPointIndex != -1)
            {
                this.m_PointList.Focus();
                
                Vector2 currentPointPosition = this.GetPointAtIndex(this.m_CurrentPointIndex);
                float dotThickness = this.GetDotThickness(currentPointPosition);

                Handles.color = Color.yellow;
                Handles.DrawSolidDisc(currentPointPosition, Vector3.forward, dotThickness * 1.5f);
                return;
            }

            Vector2 mousePosition = this.GetMousePosition(guiEvent);
            if (this.TryClampPointToNearestLine(mousePosition, out Vector2 clampedPosition))
            {
                float dotThickness = this.GetDotThickness(clampedPosition);
                Handles.color = Theme.MainColor;
                Handles.DrawSolidDisc(clampedPosition, Vector3.forward, dotThickness);
            }

            Handles.color = handleColor;
        }

        private void DrawLineSegment(int index)
        {
            this.m_PointLabelStyle.normal = this.m_NormalLabelColor;

            int pointCount = this.m_Points.arraySize;
            Vector3 currentPoint = this.GetPointAtIndex(index);
            Vector3 nextPoint = this.GetPointAtIndex((index + 1) % pointCount);
            Vector3 prevPoint = this.GetPointAtIndex(index > 0 ? index - 1 : pointCount - 1);

            if (index == this.m_CurrentPointIndex)
            {
                if (this.m_IsDraging)
                {
                    Handles.color = new Color(1f, 1f, 1f, 0.5f);
                    this.DrawBorkenLine(this.m_InitialTargetDragPosition, prevPoint);
                    this.DrawBorkenLine(this.m_InitialTargetDragPosition, nextPoint);
                }

                this.m_PointLabelStyle.normal = this.m_SelectedLabelColor;
            }

            Handles.color = index != this.m_CurrentLineIndex
                ? Theme.MainColor : Color.yellow;

            float lineThickness = index != this.m_CurrentLineIndex
                ? 1f : 3f;

            if (this.m_IsDeleting && !this.m_IsDraging)
            {
                bool isLineSelected = this.m_CurrentLineIndex != -1 &&
                    (index == this.m_CurrentLineIndex || 
                    index == (this.m_CurrentLineIndex - 1 + pointCount) % pointCount);

                bool isPointSelected = this.m_CurrentPointIndex != -1 &&
                    (index == this.m_CurrentPointIndex || 
                    index == (this.m_CurrentPointIndex - 1 + pointCount) % pointCount);

                if (isLineSelected || isPointSelected)
                {
                    if (index == this.m_CurrentPointIndex || index == this.m_CurrentLineIndex)
                    {
                        this.m_PointLabelStyle.normal = this.m_DeleteLabelColor;
                    }

                    Handles.color = Color.red;
                    lineThickness = 3f;
                }
            }

            Handles.DrawLine(currentPoint, nextPoint, lineThickness);

            Vector3 directionA = (currentPoint - prevPoint).normalized;
            Vector3 directionB = (nextPoint - currentPoint).normalized;
            Vector3 bisector = (directionA - directionB).normalized;

            Vector3 centroid = this.GetPointsCentroid();
            Vector3 toCentroid = (centroid - currentPoint).normalized;
            float angle = Vector3.Angle(directionA, directionB);

            if (angle == 0)
            {
                Vector3 fallbackAxis = Vector3.Cross(directionA, Vector3.up);

                if (fallbackAxis == Vector3.zero)
                    fallbackAxis = Vector3.Cross(directionA, Vector3.right);

                bisector = Vector3.Cross(fallbackAxis, directionA).normalized;
            }

            if (Vector3.Dot(bisector, toCentroid) < 0)
                bisector = -bisector;

            float size = HandleUtility.GetHandleSize(currentPoint);
            Vector3 labelPos = currentPoint + bisector * -(size * 0.15f);
            Handles.Label(labelPos, $"{index}", this.m_PointLabelStyle);

            if (pointCount == 1)
            {
                float dotThickness = this.GetDotThickness(currentPoint);
                Handles.DrawSolidDisc(currentPoint, Vector3.forward, dotThickness);
            }
        }

        private void DrawBorkenLine(Vector3 start, Vector3 end)
        {
            const float DASH_LENGTH = 7f;

            Vector3 direction = (end - start).normalized;
            float totalLength = Vector3.Distance(start, end);
            int segments = Mathf.CeilToInt(totalLength / DASH_LENGTH);
            if (segments % 4 != 0) segments += 4 - segments % 4;

            for (int j = 0; j < segments; j += 4)
            {
                float segmentStart = j * DASH_LENGTH;
                float segmentEnd = Mathf.Min((j + 1) * DASH_LENGTH, totalLength);
                if (segmentStart < totalLength)
                {
                    Handles.DrawAAPolyLine(
                        3f,
                        start + direction * segmentStart,
                        start + direction * segmentEnd
                    );
                }

                segmentStart = (j + 1) * DASH_LENGTH;
                segmentEnd = Mathf.Min((j + 2) * DASH_LENGTH, totalLength);
                if (segmentStart < totalLength)
                {
                    Handles.DrawAAPolyLine(
                        3f,
                        start + direction * segmentStart,
                        start + direction * segmentEnd
                    );
                }
            }
        }

        // INPUTS: --------------------------------------------------------------------------------

        private void HandleInput(Event guiEvent)
        {
            Vector2 mousePosition = this.GetMousePosition(guiEvent);

            if (guiEvent.button == 0)
            {
                switch (guiEvent.type)
                {
                    case EventType.MouseDown when this.m_IsDeleting:
                        this.HandleControlMouseDown(mousePosition);
                        break;
                    case EventType.MouseDown:
                        this.HandleMouseDown(mousePosition);
                        break;
                    case EventType.MouseUp:
                        this.HandleMouseUp(mousePosition);
                        break;
                    case EventType.MouseDrag:
                        this.HandleMouseDrag(mousePosition);
                        break;
                }
            }

            if (!this.m_IsDraging) this.HandleMouseHover(mousePosition);
        }

        private void HandleMouseDown(Vector2 mousePosition)
        {
            if (this.m_Points.arraySize < 2)
            {
                Undo.RecordObject(this.m_Control, "Touchable Area: Polygon Add point");
                this.InsertPointAtIndex(this.m_CurrentLineIndex + 1, mousePosition);
                this.ApplyProperties();

                this.m_CurrentPointIndex = this.m_CurrentLineIndex + 1;
                this.m_CurrentLineIndex = -1;
            }

            if (this.m_CurrentPointIndex == -1 && this.m_CurrentLineIndex == -1) return;

            if (this.m_CurrentLineIndex != -1 && 
                this.TryClampPointToNearestLine(mousePosition, out Vector2 clampedPoint))
            {
                Undo.RecordObject(this.m_Control, "Touchable Area: Polygon Add point");
                this.InsertPointAtIndex(this.m_CurrentLineIndex + 1, clampedPoint);
                this.ApplyProperties();

                this.m_CurrentPointIndex = this.m_CurrentLineIndex + 1;
                this.m_CurrentLineIndex = -1;
            }
            
            this.m_StartDragPosition = mousePosition;
            this.m_InitialTargetDragPosition = this.GetPointAtIndex(this.m_CurrentPointIndex);
            this.m_CurrentDragPosition = this.m_InitialTargetDragPosition;
            
            this.m_IsDraging = true;
            this.m_IsDirty = true;
        }

        private void HandleControlMouseDown(Vector2 mousePosition)
        {
            if (this.m_CurrentLineIndex == -1 && this.m_CurrentPointIndex == -1) return;

            int deletePointIndex = (this.m_CurrentPointIndex != -1)
                ? this.m_CurrentPointIndex : this.m_CurrentLineIndex;

            Undo.RecordObject(this.m_Control, "Touchable Area: Polygon Delete point");
            this.DeletePointAtIndex(deletePointIndex);
            this.ApplyProperties();

            this.m_CurrentLineIndex = -1;
            this.m_CurrentPointIndex = -1;
            this.m_IsDeleting = false;
            this.m_IsDirty = true;
        }

        private void HandleMouseUp(Vector2 mousePosition)
        {
            if (!this.m_IsDraging) return;

            this.SetPointAtIndex(this.m_CurrentPointIndex, this.m_InitialTargetDragPosition);
            this.ApplyProperties();
            
            Undo.RecordObject(this.m_Control, "Touchable Area: Polygon Move point");
            this.SetPointAtIndex(this.m_CurrentPointIndex, this.m_CurrentDragPosition);
            this.ApplyProperties();

            this.m_IsDraging = false;
            this.m_IsDirty = true;
        }

        private void HandleMouseDrag(Vector2 mousePosition)
        {
            if (!this.m_IsDraging) return;

            Vector2 offset = mousePosition - this.m_StartDragPosition;
            Vector2 newPointPosition = this.m_InitialTargetDragPosition + offset;
            
            #if UNITY_6000_0_OR_NEWER
            if (EditorSnapSettings.snapEnabled)
            {
                Vector2 snapValue = EditorSnapSettings.move;
                this.m_CurrentDragPosition = new Vector2 (
                    Mathf.Round(newPointPosition.x / snapValue.x) * snapValue.x,
                    Mathf.Round(newPointPosition.y / snapValue.y) * snapValue.y
                );
            }
            else
            #endif
            {
                this.m_CurrentDragPosition = newPointPosition;
            }

            this.m_IsDirty = true;

            this.SetPointAtIndex(this.m_CurrentPointIndex, this.m_CurrentDragPosition);
            this.ApplyProperties();
        }

        private void HandleMouseHover(Vector2 mousePosition)
        {
            int pointCount = this.m_Points.arraySize;
            int nearestPointIndex = -1, nearestLineIndex = -1;
            float nearestLineDistance = this.GetPointRange(mousePosition);

            float minDistance = float.MaxValue;
            for (int i = 0; i < pointCount; i++)
            {
                Vector3 currentPoint = this.GetPointAtIndex(i);
                Vector3 nextPoint = this.GetPointAtIndex((i + 1) % pointCount);

                float distancePoint = Vector3.Distance(mousePosition, currentPoint);
                if (distancePoint < this.GetPointRange(currentPoint) / 3f)
                {
                    nearestPointIndex = i;
                    nearestLineIndex = -1;
                    break;
                }

                float lineDistance = HandleUtility.DistancePointToLineSegment(
                    mousePosition, currentPoint, nextPoint
                );

                if (lineDistance < nearestLineDistance && lineDistance < minDistance)
                {
                    nearestLineIndex = i;
                    minDistance = lineDistance;
                }
            }

            this.m_CurrentPointIndex = nearestPointIndex;
            this.m_CurrentLineIndex = nearestLineIndex;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private bool TryClampPointToNearestLine(Vector2 position, out Vector2 clampedPosition)
        {
            int lineIndex = this.GetNearestLineIndex(position, this.GetPointRange(position));

            if (lineIndex < 0)
            {
                clampedPosition = Vector2.zero;
                return false;
            }

            Vector2 currentPoint = this.GetPointAtIndex(lineIndex);
            Vector2 nextPoint = this.GetPointAtIndex((lineIndex + 1) % this.m_Points.arraySize);
            Vector2 deltaPoint = nextPoint - currentPoint;
            Vector2 direction = deltaPoint.normalized;

            float dotProduct = Vector2.Dot(position - currentPoint, direction);
            float projection = Mathf.Clamp(dotProduct, 0, deltaPoint.magnitude);

            clampedPosition = currentPoint + direction * projection;
            return true;
        }

        private int GetNearestLineIndex(Vector2 position, float lineDistance)
        {
            int nearestIndex = -1;
            int pointCount = this.m_Points.arraySize;

            float minDistance = float.MaxValue;
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 currentPoint = this.GetPointAtIndex(i);
                Vector2 nextPoint = this.GetPointAtIndex((i + 1) % pointCount);

                float distance = HandleUtility.DistancePointToLineSegment(
                    position, 
                    currentPoint, 
                    nextPoint
                );

                if (distance < lineDistance && distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private Vector2 GetPointAtIndex(int index)
        {
            if (!this.m_IsDraging || index != this.m_CurrentPointIndex)
            {
                Vector2 value = this.m_Points.GetArrayElementAtIndex(index).vector2Value;
                return this.m_Transform.TransformPoint(value);
            }

            return this.m_CurrentDragPosition;
        }

        private void SetPointAtIndex(int index, Vector2 value)
        {
            this.m_Points.GetArrayElementAtIndex(index)
                .vector2Value = this.m_Transform.InverseTransformPoint(value);
        }

        private void InsertPointAtIndex(int index, Vector2 value)
        {
            this.m_Points.InsertArrayElementAtIndex(index);
            this.SetPointAtIndex(index, value);
        }

        private void DeletePointAtIndex(int index)
        {
            this.m_Points.DeleteArrayElementAtIndex(index);
        }

        private Vector3 GetPointsCentroid()
        {
            Vector3 centroid = Vector3.zero;
            int pointCount = this.m_Points.arraySize;

            for (int i = 0; i < pointCount; i++)
            {
                centroid += (Vector3)this.GetPointAtIndex(i);
            }

            return centroid / pointCount;
        }

        private float GetDotThickness(Vector2 point)
        {
            return HANDLE_RADIUS * HandleUtility.GetHandleSize(point);
        }

        private float GetPointRange(Vector2 point)
        {
            return this.GetDotThickness(point) * 10;
        }

        private Vector2 GetMousePosition(Event guiEvent)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiEvent.mousePosition);
            return ray.GetPoint(-ray.origin.z / ray.direction.z);
        }

        private void ApplyProperties()
        {
            this.m_Object.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}