using FranklinGame.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles.Editor
{
    /// <summary>
    /// Scene View authoring tool for the only RapidTemplate car converted to
    /// Sim-Cade. It edits ordinary child transforms, so Undo, prefab overrides
    /// and scene saving continue to work through Unity's standard workflow.
    /// </summary>
    public sealed class SimcadeCarEntrySceneTool : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";
        private const string CarPrefabPath =
            "Assets/Ash Assets/Vehicle Integration/Prefabs/Vehicles/Car.prefab";

        private enum EditAnchor
        {
            Standing,
            EntryStep,
            Seat,
            DoorHandle,
            VictimLanding
        }

        private enum PreviewAnimation
        {
            Enter,
            Exit
        }

        [SerializeField] private CarEntry m_Target;
        [SerializeField] private EditAnchor m_EditAnchor;
        [SerializeField] private bool m_FollowSelection = true;
        [SerializeField] private bool m_ShowEntryPath = true;
        [SerializeField, Range(0f, 1f)] private float m_EntryPathPreview;
        [SerializeField] private bool m_DoorPreview;
        [SerializeField, Range(0f, 1f)] private float m_DoorPreviewAmount;
        [SerializeField] private GameObject m_PlayerPrefab;
        [SerializeField] private bool m_PlayerPreview;
        [SerializeField] private PreviewAnimation m_PreviewAnimation;
        [SerializeField, Range(0f, 1f)] private float m_PlayerPreviewTime;
        [SerializeField] private bool m_PlayPlayerPreview;
        [SerializeField] private bool m_LoopPlayerPreview = true;
        [SerializeField] private bool m_SyncDoorToPlayer = true;
        [SerializeField] private bool m_PreviewDoorHandleIK = true;
        [SerializeField] private Vector2 m_ScrollPosition;

        private bool m_OwnsAnimationMode;
        private Quaternion m_DoorClosedRotation;
        private AnimationClip m_DoorPreviewClip;
        private GameObject m_PlayerPreviewInstance;
        private Animator m_PlayerPreviewAnimator;
        private Vector3 m_PlayerAnimatorBaseLocalPosition;
        private Quaternion m_PlayerAnimatorBaseLocalRotation;
        private Vector3 m_PlayerAnimatorBaseLocalScale;
        private bool m_PlayerOwnsDoorPreview;
        private float m_LastSyncedDoorAmount = -1f;
        private double m_LastPlayerPreviewEditorTime;

        [MenuItem("Tools/Franklin Game/Car Entry Live Setup", priority = 102)]
        public static void Open()
        {
            SimcadeCarEntrySceneTool window = GetWindow<SimcadeCarEntrySceneTool>();
            window.titleContent = new GUIContent("Car Entry Live");
            window.minSize = new Vector2(360f, 430f);
            window.TryUseSelectionOrSceneCar();
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += this.DuringSceneGUI;
            Selection.selectionChanged += this.OnSelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload += this.StopAllPreviews;
            EditorApplication.playModeStateChanged += this.OnPlayModeStateChanged;
            EditorApplication.update += this.UpdatePlayerPreviewPlayback;
            if (this.m_PlayerPrefab == null)
                this.m_PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            this.m_PlayerPreview = false;
            this.m_DoorPreview = false;
            this.TryUseSelectionOrSceneCar();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= this.DuringSceneGUI;
            Selection.selectionChanged -= this.OnSelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= this.StopAllPreviews;
            EditorApplication.playModeStateChanged -= this.OnPlayModeStateChanged;
            EditorApplication.update -= this.UpdatePlayerPreviewPlayback;
            this.StopAllPreviews();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) this.StopAllPreviews();
        }

        private void OnSelectionChanged()
        {
            if (!this.m_FollowSelection) return;
            CarEntry selected = ResolveCarEntry(Selection.activeGameObject);
            if (IsSimcadeCar(selected)) this.SetTarget(selected);
        }

        private void OnGUI()
        {
            using (EditorGUILayout.ScrollViewScope scroll =
                new EditorGUILayout.ScrollViewScope(this.m_ScrollPosition))
            {
                this.m_ScrollPosition = scroll.scrollPosition;
                this.DrawWindowContents();
            }
        }

        private void DrawWindowContents()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Sim-Cade Car Entry — Live Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Kéo trực tiếp các handle màu trong Scene View. Cyan: điểm đứng, " +
                "magenta: bước qua cửa, green: ghế, orange: tay nắm, red: vị trí NPC ngã. " +
                "Có Undo (Cmd/Ctrl+Z).",
                MessageType.Info
            );

            EditorGUI.BeginChangeCheck();
            CarEntry target = (CarEntry)EditorGUILayout.ObjectField(
                "Car Entry",
                this.m_Target,
                typeof(CarEntry),
                true
            );
            if (EditorGUI.EndChangeCheck()) this.SetTarget(target);

            this.m_FollowSelection = EditorGUILayout.ToggleLeft(
                "Tự theo object đang chọn trong Hierarchy",
                this.m_FollowSelection
            );

            if (GUILayout.Button("Dùng Sim-Cade Car đang chọn"))
            {
                this.TryUseSelectionOrSceneCar();
            }

            if (!IsSimcadeCar(this.m_Target))
            {
                EditorGUILayout.HelpBox(
                    "Hãy chọn đúng GameObject Car có SimcadeCarDriver. Tool không chỉnh các vehicle khác.",
                    MessageType.Warning
                );
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUILayout.Space(8f);
                if (!HasAllAnchors(this.m_Target))
                {
                    EditorGUILayout.HelpBox(
                        "Xe đang thiếu Entry Standing, Entry Step hoặc Door Handle Target.",
                        MessageType.Warning
                    );
                    if (GUILayout.Button("Tạo / sửa các điểm còn thiếu", GUILayout.Height(28f)))
                    {
                        EnsureAnchors(this.m_Target);
                    }
                }

                string[] tabs =
                {
                    "Điểm đứng", "Bước vào", "Ghế ngồi", "Tay nắm", "NPC ngã"
                };
                this.m_EditAnchor = (EditAnchor)GUILayout.Toolbar(
                    (int)this.m_EditAnchor,
                    tabs,
                    GUILayout.Height(26f)
                );

                EditorGUILayout.Space(6f);
                this.DrawActiveAnchorFields();

                this.m_ShowEntryPath = EditorGUILayout.ToggleLeft(
                    "Preview đường vào ghế",
                    this.m_ShowEntryPath
                );
                if (this.m_ShowEntryPath)
                {
                    this.m_EntryPathPreview = EditorGUILayout.Slider(
                        "Tiến trình vào xe",
                        this.m_EntryPathPreview,
                        0f,
                        1f
                    );
                }

                EditorGUILayout.Space(5f);
                this.DrawDoorPreviewControls();

                EditorGUILayout.Space(7f);
                this.DrawPlayerPreviewControls();

                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Focus handle đang chỉnh")) this.FrameActiveAnchor();

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 1f, 0.55f);
                if (GUILayout.Button(
                    "APPLY LIVE SETUP TO CAR PREFAB",
                    GUILayout.Height(32f)))
                {
                    this.ApplyLiveSetupToPrefab();
                }

                GUI.backgroundColor = new Color(1f, 0.55f, 0.4f);
                if (GUILayout.Button("RESET TOÀN BỘ VỀ PREFAB", GUILayout.Height(30f)))
                    this.ResetAllToPrefab();
                GUI.backgroundColor = previousBackground;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Nút APPLY chỉ ghi các điểm live setup và thông số vào đúng Car.prefab. " +
                "Vị trí xe trong Scene cùng các override không liên quan sẽ được giữ nguyên.",
                MessageType.None
            );
        }

        private void DrawPlayerPreviewControls()
        {
            EditorGUILayout.LabelField("Player Animation Preview", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            GameObject playerPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Player Prefab",
                this.m_PlayerPrefab,
                typeof(GameObject),
                false
            );
            if (EditorGUI.EndChangeCheck())
            {
                this.StopPlayerPreview();
                this.m_PlayerPrefab = playerPrefab;
            }

            bool preview = EditorGUILayout.ToggleLeft(
                "Preview Player animation thật",
                this.m_PlayerPreview
            );
            if (preview != this.m_PlayerPreview)
            {
                if (preview) this.StartPlayerPreview();
                else this.StopPlayerPreview();
            }

            using (new EditorGUI.DisabledScope(!this.m_PlayerPreview))
            {
                string[] animationNames = { "Enter Car", "Exit Car" };
                EditorGUI.BeginChangeCheck();
                this.m_PreviewAnimation = (PreviewAnimation)GUILayout.Toolbar(
                    (int)this.m_PreviewAnimation,
                    animationNames,
                    GUILayout.Height(24f)
                );
                this.m_PlayerPreviewTime = EditorGUILayout.Slider(
                    "Animation Time",
                    this.m_PlayerPreviewTime,
                    0f,
                    1f
                );

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                        this.m_PlayPlayerPreview ? "Pause" : "Play",
                        GUILayout.Height(24f)))
                    {
                        this.m_PlayPlayerPreview = !this.m_PlayPlayerPreview;
                        this.m_LastPlayerPreviewEditorTime = EditorApplication.timeSinceStartup;
                    }

                    if (GUILayout.Button("Reset Preview", GUILayout.Height(24f)))
                    {
                        this.ResetPlayerPreviewState();
                    }

                    this.m_LoopPlayerPreview = GUILayout.Toggle(
                        this.m_LoopPlayerPreview,
                        "Loop",
                        "Button",
                        GUILayout.Width(62f),
                        GUILayout.Height(24f)
                    );
                }

                this.m_SyncDoorToPlayer = EditorGUILayout.ToggleLeft(
                    "Đồng bộ cửa theo thời gian animation",
                    this.m_SyncDoorToPlayer
                );
                this.m_PreviewDoorHandleIK = EditorGUILayout.ToggleLeft(
                    "Preview IK tay bám tay nắm cửa",
                    this.m_PreviewDoorHandleIK
                );

                if (EditorGUI.EndChangeCheck()) this.RefreshPlayerPreview();

                AnimationClip clip = this.GetPreviewClip();
                if (clip != null)
                {
                    EditorGUILayout.LabelField(
                        $"Clip: {clip.name}  |  {clip.length:0.00}s  |  " +
                        $"Frame: {Mathf.RoundToInt(this.m_PlayerPreviewTime * clip.length * clip.frameRate)}"
                    );
                }
            }
        }

        private void DrawActiveAnchorFields()
        {
            Transform active = this.GetActiveAnchor();
            EditorGUILayout.ObjectField("Transform", active, typeof(Transform), true);
            if (active == null) return;

            EditorGUI.BeginChangeCheck();
            Vector3 localPosition = EditorGUILayout.Vector3Field("Local Position", active.localPosition);
            Vector3 localEuler = EditorGUILayout.Vector3Field("Local Rotation", active.localEulerAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(active, "Edit Car Entry Anchor");
                active.localPosition = localPosition;
                active.localEulerAngles = localEuler;
                RecordTransformChange(active);
            }

            if (GUILayout.Button("Reset điểm đang chọn về Prefab"))
            {
                this.ResetAnchorToPrefab(active);
            }

            if (this.m_EditAnchor == EditAnchor.DoorHandle)
            {
                AvatarIKGoal hand = this.m_Target.doorHandleHand == AvatarIKGoal.LeftHand
                    ? AvatarIKGoal.LeftHand
                    : AvatarIKGoal.RightHand;
                string[] handNames = { "Tay phải", "Tay trái" };
                int handIndex = hand == AvatarIKGoal.RightHand ? 0 : 1;
                EditorGUI.BeginChangeCheck();
                handIndex = EditorGUILayout.Popup("Bàn tay mở cửa", handIndex, handNames);
                float weight = EditorGUILayout.Slider(
                    "IK Weight",
                    this.m_Target.doorHandleIKWeight,
                    0f,
                    1f
                );
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this.m_Target, "Edit Door Handle IK");
                    this.m_Target.doorHandleHand = handIndex == 0
                        ? AvatarIKGoal.RightHand
                        : AvatarIKGoal.LeftHand;
                    this.m_Target.doorHandleIKWeight = weight;
                    RecordComponentChange(this.m_Target);
                }
            }
            else if (this.m_EditAnchor == EditAnchor.EntryStep)
            {
                EditorGUI.BeginChangeCheck();
                float stepTime = EditorGUILayout.Slider(
                    "Thời điểm qua ngưỡng cửa",
                    this.m_Target.entryStepNormalizedTime,
                    0.1f,
                    0.9f
                );
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this.m_Target, "Edit Entry Step Time");
                    this.m_Target.entryStepNormalizedTime = stepTime;
                    RecordComponentChange(this.m_Target);
                    if (this.m_PlayerPreview) this.RefreshPlayerPreview();
                }
            }
        }

        private void DrawDoorPreviewControls()
        {
            bool preview = EditorGUILayout.ToggleLeft(
                "Preview mở cửa trực tiếp trong Scene",
                this.m_DoorPreview
            );
            if (preview != this.m_DoorPreview)
            {
                if (preview) this.StartDoorPreview();
                else this.StopDoorPreview();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 openEuler = EditorGUILayout.Vector3Field(
                "Door Open Rotation",
                this.m_Target.doorOpenRotation
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this.m_Target, "Edit Door Open Rotation");
                this.m_Target.doorOpenRotation = openEuler;
                RecordComponentChange(this.m_Target);
                if (this.m_DoorPreview) this.RefreshDoorPreview();
            }

            if (GUILayout.Button("Reset góc mở cửa về Prefab"))
            {
                this.ResetCarEntryPropertyToPrefab("doorOpenRotation");
                if (this.m_DoorPreview) this.RefreshDoorPreview();
            }

            using (new EditorGUI.DisabledScope(!this.m_DoorPreview))
            {
                EditorGUI.BeginChangeCheck();
                this.m_DoorPreviewAmount = EditorGUILayout.Slider(
                    "Door Preview",
                    this.m_DoorPreviewAmount,
                    0f,
                    1f
                );
                if (EditorGUI.EndChangeCheck()) this.RefreshDoorPreview();
            }
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (!IsSimcadeCar(this.m_Target) || EditorApplication.isPlaying) return;

            if (this.m_PlayerPreview) this.RefreshPlayerPreview();

            Handles.zTest = CompareFunction.LessEqual;
            this.DrawAnchorMarker(
                this.m_Target.entryStandingPoint,
                new Color(0.1f, 0.9f, 1f, 1f),
                "ENTRY STAND",
                EditAnchor.Standing
            );
            this.DrawAnchorMarker(
                this.m_Target.entryStepPoint,
                new Color(1f, 0.15f, 0.85f, 1f),
                "ENTRY STEP",
                EditAnchor.EntryStep
            );
            this.DrawAnchorMarker(
                this.m_Target.entryParent,
                new Color(0.2f, 1f, 0.35f, 1f),
                "DRIVER SEAT",
                EditAnchor.Seat
            );
            this.DrawAnchorMarker(
                this.m_Target.doorHandleTarget,
                new Color(1f, 0.55f, 0.08f, 1f),
                "DOOR HANDLE",
                EditAnchor.DoorHandle
            );
            SimcadeCarjacking carjacking = this.m_Target.GetComponent<SimcadeCarjacking>();
            if (carjacking != null)
            {
                this.DrawAnchorMarker(
                    carjacking.VictimLandingPoint,
                    new Color(1f, 0.12f, 0.12f, 1f),
                    "NPC LANDING",
                    EditAnchor.VictimLanding
                );
            }

            Transform active = this.GetActiveAnchor();
            if (active != null) this.DrawTransformHandle(active);
            if (this.m_ShowEntryPath) this.DrawEntryPathPreview();

            sceneView.Repaint();
        }

        private void DrawAnchorMarker(
            Transform anchor,
            Color color,
            string label,
            EditAnchor anchorType)
        {
            if (anchor == null) return;

            float size = HandleUtility.GetHandleSize(anchor.position);
            Color previous = Handles.color;
            Handles.color = color;
            Handles.Label(anchor.position + Vector3.up * size * 0.18f, label, EditorStyles.boldLabel);

            if (anchorType != this.m_EditAnchor && Handles.Button(
                anchor.position,
                anchor.rotation,
                size * 0.08f,
                size * 0.1f,
                Handles.SphereHandleCap))
            {
                this.m_EditAnchor = anchorType;
                this.Repaint();
            }

            Handles.DrawLine(
                anchor.position,
                anchor.position + anchor.forward * size * 0.45f,
                3f
            );
            Handles.ArrowHandleCap(
                0,
                anchor.position + anchor.forward * size * 0.45f,
                anchor.rotation,
                size * 0.18f,
                EventType.Repaint
            );
            Handles.color = previous;
        }

        private void DrawTransformHandle(Transform anchor)
        {
            Quaternion orientation = Tools.pivotRotation == PivotRotation.Local
                ? anchor.rotation
                : Quaternion.identity;

            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(anchor.position, orientation);
            Quaternion rotation = Handles.RotationHandle(anchor.rotation, anchor.position);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(anchor, "Move Car Entry Anchor");
            anchor.SetPositionAndRotation(position, rotation);
            RecordTransformChange(anchor);
            this.Repaint();
        }

        private void DrawEntryPathPreview()
        {
            Transform standing = this.m_Target.entryStandingPoint;
            Transform seat = this.m_Target.entryParent;
            if (standing == null || seat == null) return;

            Color previous = Handles.color;
            Handles.color = new Color(0.95f, 0.85f, 0.1f, 0.9f);
            const int segments = 24;
            Vector3 previousPoint = standing.position;
            for (int index = 1; index <= segments; index++)
            {
                this.m_Target.EvaluateAuthoredEntryPath(
                    index / (float)segments,
                    out Vector3 pathPoint,
                    out _
                );
                Handles.DrawDottedLine(previousPoint, pathPoint, 4f);
                previousPoint = pathPoint;
            }

            this.m_Target.EvaluateAuthoredEntryPath(
                this.m_EntryPathPreview,
                out Vector3 position,
                out Quaternion rotation
            );
            this.DrawCharacterGuide(position, rotation);

            if (this.m_Target.doorHandleTarget != null)
            {
                Vector3 shoulder = position + rotation * new Vector3(0.25f, 1.35f, 0f);
                Handles.DrawDottedLine(
                    shoulder,
                    this.m_Target.doorHandleTarget.position,
                    3f
                );
            }

            Handles.color = previous;
        }

        private void DrawCharacterGuide(Vector3 root, Quaternion rotation)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 hips = root + up * 0.9f;
            Vector3 head = root + up * 1.7f;
            Handles.DrawWireDisc(root, up, 0.28f);
            Handles.DrawLine(root, head, 2f);
            Handles.DrawWireDisc(head, forward, 0.13f);
            Handles.ArrowHandleCap(
                0,
                hips,
                rotation,
                HandleUtility.GetHandleSize(hips) * 0.35f,
                EventType.Repaint
            );
        }

        private void StartPlayerPreview()
        {
            this.StopPlayerPreview();
            if (!IsSimcadeCar(this.m_Target)) return;

            if (this.m_PlayerPrefab == null)
                this.m_PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (this.m_PlayerPrefab == null)
            {
                Debug.LogError($"Player preview prefab is missing: {PlayerPrefabPath}");
                return;
            }

            this.m_PlayerPreviewInstance = Instantiate(this.m_PlayerPrefab);
            this.m_PlayerPreviewInstance.name = "Player Animation Preview (Editor Only)";
            SetPreviewHideFlags(this.m_PlayerPreviewInstance);

            this.m_PlayerPreviewAnimator =
                this.m_PlayerPreviewInstance.GetComponentInChildren<Animator>(true);
            if (this.m_PlayerPreviewAnimator == null)
            {
                Debug.LogError("Player.prefab has no Animator for car-entry preview");
                DestroyImmediate(this.m_PlayerPreviewInstance);
                this.m_PlayerPreviewInstance = null;
                return;
            }

            foreach (Behaviour behaviour in
                this.m_PlayerPreviewInstance.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour != this.m_PlayerPreviewAnimator) behaviour.enabled = false;
            }

            this.m_PlayerPreviewAnimator.enabled = true;
            this.m_PlayerPreviewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            this.m_PlayerAnimatorBaseLocalPosition =
                this.m_PlayerPreviewAnimator.transform.localPosition;
            this.m_PlayerAnimatorBaseLocalRotation =
                this.m_PlayerPreviewAnimator.transform.localRotation;
            this.m_PlayerAnimatorBaseLocalScale =
                this.m_PlayerPreviewAnimator.transform.localScale;
            this.m_PlayerPreview = true;
            this.m_PlayPlayerPreview = false;
            this.m_PlayerOwnsDoorPreview = false;
            this.m_LastSyncedDoorAmount = -1f;
            this.m_LastPlayerPreviewEditorTime = EditorApplication.timeSinceStartup;
            this.RefreshPlayerPreview();
        }

        private void UpdatePlayerPreviewPlayback()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = now - this.m_LastPlayerPreviewEditorTime;
            this.m_LastPlayerPreviewEditorTime = now;

            if (!this.m_PlayerPreview || !this.m_PlayPlayerPreview ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AnimationClip clip = this.GetPreviewClip();
            if (clip == null || clip.length <= 0f) return;

            float speed = this.GetPreviewAnimationSpeed();
            this.m_PlayerPreviewTime +=
                (float)Mathf.Min((float)delta, 0.1f) * speed / clip.length;
            if (this.m_PlayerPreviewTime >= 1f)
            {
                if (this.m_LoopPlayerPreview) this.m_PlayerPreviewTime %= 1f;
                else
                {
                    this.m_PlayerPreviewTime = 1f;
                    this.m_PlayPlayerPreview = false;
                }
            }

            this.RefreshPlayerPreview();
            this.Repaint();
        }

        private void RefreshPlayerPreview()
        {
            if (!this.m_PlayerPreview || this.m_PlayerPreviewInstance == null ||
                this.m_PlayerPreviewAnimator == null || this.m_Target == null)
            {
                return;
            }

            AnimationClip clip = this.GetPreviewClip();
            if (clip == null) return;

            Transform standing = this.m_Target.entryStandingPoint;
            Transform seat = this.m_Target.entryParent;
            if (standing == null || seat == null) return;

            float normalized = Mathf.Clamp01(this.m_PlayerPreviewTime);
            float sampleTime = Mathf.Min(clip.length, normalized * clip.length);

            Transform previewTransform = this.m_PlayerPreviewInstance.transform;
            previewTransform.SetPositionAndRotation(
                this.m_PreviewAnimation == PreviewAnimation.Enter
                    ? standing.position
                    : seat.position,
                this.m_PreviewAnimation == PreviewAnimation.Enter
                    ? standing.rotation
                    : seat.rotation
            );

            clip.SampleAnimation(this.m_PlayerPreviewAnimator.gameObject, 0f);
            Transform animatorTransform = this.m_PlayerPreviewAnimator.transform;
            Vector3 clipStartPosition = animatorTransform.localPosition;
            Quaternion clipStartRotation = animatorTransform.localRotation;

            clip.SampleAnimation(this.m_PlayerPreviewAnimator.gameObject, sampleTime);
            Vector3 sampledRootPosition = animatorTransform.localPosition;
            Quaternion sampledRootRotation = animatorTransform.localRotation;

            // SampleAnimation writes the clip's extracted root-motion curves back
            // onto the Animator transform. The real Character motor consumes those
            // curves at runtime. Transfer that delta to the preview root, then
            // restore the Animator child so the movement is applied exactly once.
            animatorTransform.localPosition = this.m_PlayerAnimatorBaseLocalPosition;
            animatorTransform.localRotation = this.m_PlayerAnimatorBaseLocalRotation;
            animatorTransform.localScale = this.m_PlayerAnimatorBaseLocalScale;

            Vector3 rootMotionDelta = sampledRootPosition - clipStartPosition;
            Quaternion rootMotionRotation = sampledRootRotation *
                Quaternion.Inverse(clipStartRotation);

            if (this.m_PreviewAnimation == PreviewAnimation.Enter)
            {
                this.m_Target.EvaluateAuthoredEntryPath(
                    normalized,
                    out Vector3 authoredPosition,
                    out Quaternion authoredRotation
                );
                previewTransform.SetPositionAndRotation(
                    authoredPosition,
                    authoredRotation
                );
            }
            else
            {
                previewTransform.SetPositionAndRotation(
                    seat.position + seat.rotation * rootMotionDelta,
                    seat.rotation * rootMotionRotation
                );
            }

            this.SyncDoorToPlayerAnimation(clip, sampleTime);
            if (this.m_PreviewDoorHandleIK) this.ApplyPreviewDoorHandleIK(normalized);
            SceneView.RepaintAll();
        }

        private AnimationClip GetPreviewClip()
        {
            if (this.m_Target == null) return null;
            return this.m_PreviewAnimation == PreviewAnimation.Enter
                ? this.m_Target.entryAnimation
                : this.m_Target.exitAnimation;
        }

        private float GetPreviewAnimationSpeed()
        {
            if (this.m_Target == null) return 1f;
            return Mathf.Max(
                0.01f,
                this.m_PreviewAnimation == PreviewAnimation.Enter
                    ? this.m_Target.entryAnimationSpeed
                    : this.m_Target.exitAnimationSpeed
            );
        }

        private void SyncDoorToPlayerAnimation(AnimationClip clip, float sampleTime)
        {
            if (!this.m_SyncDoorToPlayer)
            {
                if (this.m_PlayerOwnsDoorPreview) this.StopDoorPreview();
                this.m_PlayerOwnsDoorPreview = false;
                return;
            }

            float runtimeElapsed = sampleTime / this.GetPreviewAnimationSpeed();
            float duration = Mathf.Max(0.01f, this.m_Target.doorRotationDuration);
            float openStart = Mathf.Max(0f, this.m_Target.doorRotationStartDelay);
            float openedAt = openStart + duration;
            float closeStart = openedAt + Mathf.Max(0f, this.m_Target.doorResetDelay);
            float closedAt = closeStart + duration;

            float amount;
            if (runtimeElapsed <= openStart) amount = 0f;
            else if (runtimeElapsed < openedAt)
                amount = Mathf.InverseLerp(openStart, openedAt, runtimeElapsed);
            else if (runtimeElapsed <= closeStart) amount = 1f;
            else if (runtimeElapsed < closedAt)
                amount = 1f - Mathf.InverseLerp(closeStart, closedAt, runtimeElapsed);
            else amount = 0f;

            if (!this.m_DoorPreview)
            {
                this.m_PlayerOwnsDoorPreview = true;
                this.m_DoorPreviewAmount = amount;
                this.StartDoorPreview();
                this.m_LastSyncedDoorAmount = amount;
                return;
            }

            if (Mathf.Abs(amount - this.m_LastSyncedDoorAmount) < 0.0001f) return;
            this.m_DoorPreviewAmount = amount;
            this.m_LastSyncedDoorAmount = amount;
            this.RefreshDoorPreview();
        }

        private void ApplyPreviewDoorHandleIK(float normalized)
        {
            Transform target = this.m_Target.doorHandleTarget;
            if (target == null || this.m_PlayerPreviewAnimator == null ||
                !this.m_PlayerPreviewAnimator.isHuman)
            {
                return;
            }

            AnimationCurve curve = this.m_PreviewAnimation == PreviewAnimation.Enter
                ? this.m_Target.entryDoorHandleIKCurve
                : this.m_Target.exitDoorHandleIKCurve;
            float weight = curve != null ? curve.Evaluate(normalized) : 1f;
            if (this.m_PreviewAnimation == PreviewAnimation.Enter &&
                this.m_Target.entryStepPoint != null)
            {
                float releaseAt = Mathf.Clamp01(this.m_Target.entryStepNormalizedTime);
                float releaseFrom = Mathf.Max(0f, releaseAt - 0.15f);
                weight *= 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(releaseFrom, releaseAt, normalized)
                );
            }
            weight = Mathf.Clamp01(weight * this.m_Target.doorHandleIKWeight);
            if (weight <= 0.001f) return;

            bool leftHand = this.m_Target.doorHandleHand == AvatarIKGoal.LeftHand;
            Transform upperArm = this.m_PlayerPreviewAnimator.GetBoneTransform(
                leftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm
            );
            Transform lowerArm = this.m_PlayerPreviewAnimator.GetBoneTransform(
                leftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm
            );
            Transform hand = this.m_PlayerPreviewAnimator.GetBoneTransform(
                leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
            );
            if (upperArm == null || lowerArm == null || hand == null) return;

            const int iterations = 5;
            float iterationWeight = 1f - Mathf.Pow(1f - weight, 1f / iterations);
            for (int index = 0; index < iterations; index++)
            {
                RotateBoneTowards(lowerArm, hand, target.position, iterationWeight);
                RotateBoneTowards(upperArm, hand, target.position, iterationWeight);
            }

            hand.position = Vector3.Lerp(hand.position, target.position, weight);
            hand.rotation = Quaternion.Slerp(hand.rotation, target.rotation, weight);
        }

        private static void RotateBoneTowards(
            Transform bone,
            Transform hand,
            Vector3 target,
            float weight)
        {
            Vector3 currentDirection = hand.position - bone.position;
            Vector3 targetDirection = target - bone.position;
            if (currentDirection.sqrMagnitude < 0.000001f ||
                targetDirection.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(currentDirection, targetDirection);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * bone.rotation;
        }

        private void StopPlayerPreview()
        {
            if (this.m_PlayerPreviewInstance != null)
                DestroyImmediate(this.m_PlayerPreviewInstance);

            this.m_PlayerPreviewInstance = null;
            this.m_PlayerPreviewAnimator = null;
            this.m_PlayerPreview = false;
            this.m_PlayPlayerPreview = false;
            this.m_LastSyncedDoorAmount = -1f;

            if (this.m_PlayerOwnsDoorPreview) this.StopDoorPreview();
            this.m_PlayerOwnsDoorPreview = false;
            SceneView.RepaintAll();
        }

        private void StopAllPreviews()
        {
            this.StopPlayerPreview();
            this.StopDoorPreview();
        }

        private void ApplyLiveSetupToPrefab()
        {
            if (!IsSimcadeCar(this.m_Target)) return;

            CarEntry target = this.m_Target;
            this.StopAllPreviews();

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                target.gameObject
            );
            if (string.IsNullOrEmpty(prefabPath) && EditorUtility.IsPersistent(target))
                prefabPath = AssetDatabase.GetAssetPath(target);

            if (prefabPath != CarPrefabPath)
            {
                const string message =
                    "Apply bị chặn: object đang chọn không thuộc đúng Car.prefab của RapidTemplate.";
                this.ShowNotification(new GUIContent(message));
                Debug.LogError(message, target);
                return;
            }

            int appliedCount = 0;
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                appliedCount += ApplyAnchorTransform(target.entryStandingPoint, prefabPath);
                appliedCount += ApplyAnchorTransform(target.entryStepPoint, prefabPath);
                appliedCount += ApplyAnchorTransform(target.entryParent, prefabPath);
                appliedCount += ApplyAnchorTransform(target.doorHandleTarget, prefabPath);
                SimcadeCarjacking carjacking = target.GetComponent<SimcadeCarjacking>();
                if (carjacking != null)
                    appliedCount += ApplyAnchorTransform(
                        carjacking.VictimLandingPoint,
                        prefabPath
                    );

                string[] propertyNames =
                {
                    "entryStandingPoint",
                    "entryStepPoint",
                    "entryParent",
                    "doorHandleTarget",
                    "doorOpenRotation",
                    "doorHandleHand",
                    "doorHandleIKWeight",
                    "entryStepNormalizedTime",
                    "useAuthoredEntryPath",
                    "entrySeatAlignmentStart",
                    "entrySeatAlignmentSharpness"
                };
                foreach (string propertyName in propertyNames)
                    appliedCount += ApplyCarEntryProperty(target, propertyName, prefabPath);
            }
            else
            {
                // When the prefab asset itself is selected, there are no instance
                // overrides to apply; marking only these objects dirty is enough.
                MarkLiveSetupDirty(target);
                appliedCount = 1;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SimcadeCarInstaller.ValidateInstallation();

            string result = appliedCount > 0
                ? $"Đã Apply {appliedCount} phần live setup vào Car.prefab"
                : "Car.prefab đã trùng với live setup hiện tại";
            this.ShowNotification(new GUIContent(result));
            Debug.Log(result, target);
            this.Repaint();
            SceneView.RepaintAll();
        }

        private static int ApplyAnchorTransform(Transform anchor, string prefabPath)
        {
            if (anchor == null) return 0;

            if (PrefabUtility.IsAddedGameObjectOverride(anchor.gameObject))
            {
                PrefabUtility.ApplyAddedGameObject(
                    anchor.gameObject,
                    prefabPath,
                    InteractionMode.UserAction
                );
                return 1;
            }

            int appliedCount = 0;
            string[] transformProperties =
            {
                "m_LocalPosition",
                "m_LocalRotation",
                "m_LocalScale",
                "m_LocalEulerAnglesHint"
            };
            foreach (string propertyName in transformProperties)
            {
                SerializedObject serializedTransform = new SerializedObject(anchor);
                SerializedProperty property = serializedTransform.FindProperty(propertyName);
                if (property == null || !property.prefabOverride) continue;

                PrefabUtility.ApplyPropertyOverride(
                    property,
                    prefabPath,
                    InteractionMode.UserAction
                );
                appliedCount++;
            }
            return appliedCount;
        }

        private static int ApplyCarEntryProperty(
            CarEntry target,
            string propertyName,
            string prefabPath)
        {
            // Use a fresh SerializedObject after every Apply. Unity can rebuild the
            // prefab connection and invalidate SerializedProperty instances.
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty property = serializedTarget.FindProperty(propertyName);
            if (property == null || !property.prefabOverride) return 0;

            PrefabUtility.ApplyPropertyOverride(
                property,
                prefabPath,
                InteractionMode.UserAction
            );
            return 1;
        }

        private static void MarkLiveSetupDirty(CarEntry target)
        {
            EditorUtility.SetDirty(target);
            if (target.entryStandingPoint != null)
                EditorUtility.SetDirty(target.entryStandingPoint);
            if (target.entryStepPoint != null)
                EditorUtility.SetDirty(target.entryStepPoint);
            if (target.entryParent != null)
                EditorUtility.SetDirty(target.entryParent);
            if (target.doorHandleTarget != null)
                EditorUtility.SetDirty(target.doorHandleTarget);
        }

        private void ResetPlayerPreviewState()
        {
            this.m_PlayPlayerPreview = false;
            this.m_PreviewAnimation = PreviewAnimation.Enter;
            this.m_PlayerPreviewTime = 0f;
            this.m_EntryPathPreview = 0f;
            this.m_DoorPreviewAmount = 0f;
            this.m_LastSyncedDoorAmount = -1f;
            this.RefreshPlayerPreview();
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void ResetAnchorToPrefab(Transform anchor)
        {
            if (anchor == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(anchor))
            {
                PrefabUtility.RevertObjectOverride(anchor, InteractionMode.UserAction);
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
                if (prefab == null || this.m_Target == null) return;

                string path = AnimationUtility.CalculateTransformPath(
                    anchor,
                    this.m_Target.transform
                );
                Transform source = string.IsNullOrEmpty(path)
                    ? prefab.transform
                    : prefab.transform.Find(path);
                if (source == null) return;

                Undo.RecordObject(anchor, "Reset Car Entry Anchor");
                anchor.localPosition = source.localPosition;
                anchor.localRotation = source.localRotation;
                anchor.localScale = source.localScale;
                RecordTransformChange(anchor);
            }

            if (this.m_PlayerPreview) this.RefreshPlayerPreview();
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void ResetCarEntryPropertyToPrefab(string propertyName)
        {
            if (this.m_Target == null) return;

            SerializedObject serializedTarget = new SerializedObject(this.m_Target);
            SerializedProperty property = serializedTarget.FindProperty(propertyName);
            if (property == null) return;

            if (property.prefabOverride)
            {
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
            }
            else
            {
                CarEntry prefabEntry = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath)
                    ?.GetComponent<CarEntry>();
                if (prefabEntry == null) return;

                Undo.RecordObject(this.m_Target, "Reset Car Entry Setting");
                switch (propertyName)
                {
                    case "doorOpenRotation":
                        this.m_Target.doorOpenRotation = prefabEntry.doorOpenRotation;
                        break;
                    case "doorHandleHand":
                        this.m_Target.doorHandleHand = prefabEntry.doorHandleHand;
                        break;
                    case "doorHandleIKWeight":
                        this.m_Target.doorHandleIKWeight = prefabEntry.doorHandleIKWeight;
                        break;
                    case "entrySeatAlignmentStart":
                        this.m_Target.entrySeatAlignmentStart =
                            prefabEntry.entrySeatAlignmentStart;
                        break;
                    case "entrySeatAlignmentSharpness":
                        this.m_Target.entrySeatAlignmentSharpness =
                            prefabEntry.entrySeatAlignmentSharpness;
                        break;
                    case "entryStepNormalizedTime":
                        this.m_Target.entryStepNormalizedTime =
                            prefabEntry.entryStepNormalizedTime;
                        break;
                    case "useAuthoredEntryPath":
                        this.m_Target.useAuthoredEntryPath =
                            prefabEntry.useAuthoredEntryPath;
                        break;
                }
                RecordComponentChange(this.m_Target);
            }

            serializedTarget.Update();
            if (this.m_PlayerPreview) this.RefreshPlayerPreview();
            this.Repaint();
        }

        private void ResetAllToPrefab()
        {
            if (this.m_Target == null) return;

            this.StopAllPreviews();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reset Car Entry Live Setup");

            this.ResetAnchorToPrefab(this.m_Target.entryStandingPoint);
            this.ResetAnchorToPrefab(this.m_Target.entryStepPoint);
            this.ResetAnchorToPrefab(this.m_Target.entryParent);
            this.ResetAnchorToPrefab(this.m_Target.doorHandleTarget);
            SimcadeCarjacking carjacking = this.m_Target.GetComponent<SimcadeCarjacking>();
            if (carjacking != null)
                this.ResetAnchorToPrefab(carjacking.VictimLandingPoint);
            this.ResetCarEntryPropertyToPrefab("doorOpenRotation");
            this.ResetCarEntryPropertyToPrefab("doorHandleHand");
            this.ResetCarEntryPropertyToPrefab("doorHandleIKWeight");
            this.ResetCarEntryPropertyToPrefab("entrySeatAlignmentStart");
            this.ResetCarEntryPropertyToPrefab("entrySeatAlignmentSharpness");
            this.ResetCarEntryPropertyToPrefab("entryStepNormalizedTime");
            this.ResetCarEntryPropertyToPrefab("useAuthoredEntryPath");
            this.ResetPlayerPreviewState();

            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        private static void SetPreviewHideFlags(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component != null) component.hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }

        private void StartDoorPreview()
        {
            if (this.m_Target == null || this.m_Target.doorTransform == null) return;
            this.StopDoorPreview();

            this.m_DoorPreview = true;
            this.m_DoorClosedRotation = this.m_Target.doorTransform.localRotation;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                this.m_OwnsAnimationMode = true;
            }

            this.RefreshDoorPreview();
        }

        private void RefreshDoorPreview()
        {
            if (!this.m_DoorPreview || this.m_Target == null ||
                this.m_Target.doorTransform == null)
            {
                return;
            }

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                this.m_OwnsAnimationMode = true;
            }

            if (this.m_DoorPreviewClip != null) DestroyImmediate(this.m_DoorPreviewClip);
            this.m_DoorPreviewClip = this.BuildDoorPreviewClip();
            if (this.m_DoorPreviewClip == null) return;

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(
                this.m_Target.gameObject,
                this.m_DoorPreviewClip,
                this.m_DoorPreviewAmount
            );
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private AnimationClip BuildDoorPreviewClip()
        {
            Transform door = this.m_Target.doorTransform;
            string path = AnimationUtility.CalculateTransformPath(door, this.m_Target.transform);
            Quaternion opened = Quaternion.Euler(this.m_Target.doorOpenRotation);
            if (Quaternion.Dot(this.m_DoorClosedRotation, opened) < 0f)
                opened = new Quaternion(-opened.x, -opened.y, -opened.z, -opened.w);

            AnimationClip clip = new AnimationClip
            {
                name = "Sim-Cade Door Live Preview",
                hideFlags = HideFlags.HideAndDontSave,
                frameRate = 60f
            };

            SetQuaternionCurve(clip, path, "x", this.m_DoorClosedRotation.x, opened.x);
            SetQuaternionCurve(clip, path, "y", this.m_DoorClosedRotation.y, opened.y);
            SetQuaternionCurve(clip, path, "z", this.m_DoorClosedRotation.z, opened.z);
            SetQuaternionCurve(clip, path, "w", this.m_DoorClosedRotation.w, opened.w);
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void SetQuaternionCurve(
            AnimationClip clip,
            string path,
            string component,
            float closed,
            float opened)
        {
            AnimationCurve curve = AnimationCurve.Linear(0f, closed, 1f, opened);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + component, curve);
        }

        private void StopDoorPreview()
        {
            if (this.m_OwnsAnimationMode && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            this.m_OwnsAnimationMode = false;
            this.m_DoorPreview = false;
            if (this.m_DoorPreviewClip != null)
            {
                DestroyImmediate(this.m_DoorPreviewClip);
                this.m_DoorPreviewClip = null;
            }
            SceneView.RepaintAll();
        }

        private void SetTarget(CarEntry target)
        {
            if (target == this.m_Target) return;
            this.StopAllPreviews();
            this.m_Target = IsSimcadeCar(target) ? target : null;
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void TryUseSelectionOrSceneCar()
        {
            CarEntry selected = ResolveCarEntry(Selection.activeGameObject);
            if (IsSimcadeCar(selected))
            {
                this.SetTarget(selected);
                return;
            }

            foreach (CarEntry entry in FindObjectsByType<CarEntry>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (!IsSimcadeCar(entry)) continue;
                this.SetTarget(entry);
                return;
            }
        }

        private void FrameActiveAnchor()
        {
            Transform active = this.GetActiveAnchor();
            if (active == null) return;
            Selection.activeTransform = active;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private Transform GetActiveAnchor()
        {
            if (this.m_Target == null) return null;
            return this.m_EditAnchor switch
            {
                EditAnchor.Standing => this.m_Target.entryStandingPoint,
                EditAnchor.EntryStep => this.m_Target.entryStepPoint,
                EditAnchor.Seat => this.m_Target.entryParent,
                EditAnchor.DoorHandle => this.m_Target.doorHandleTarget,
                EditAnchor.VictimLanding => this.m_Target
                    .GetComponent<SimcadeCarjacking>()?.VictimLandingPoint,
                _ => null
            };
        }

        private static CarEntry ResolveCarEntry(GameObject gameObject)
        {
            return gameObject != null ? gameObject.GetComponentInParent<CarEntry>() : null;
        }

        private static bool IsSimcadeCar(CarEntry entry)
        {
            return entry != null && entry.GetComponent<SimcadeCarDriver>() != null;
        }

        private static bool HasAllAnchors(CarEntry entry)
        {
            return entry != null && entry.entryStandingPoint != null &&
                entry.entryStepPoint != null && entry.entryParent != null &&
                entry.doorHandleTarget != null;
        }

        private static void EnsureAnchors(CarEntry entry)
        {
            if (!IsSimcadeCar(entry)) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Car Entry Live Anchors");

            if (entry.entryStandingPoint == null)
            {
                Transform point = FindChild(entry.transform, "Entry Standing Point");
                if (point == null)
                {
                    GameObject pointObject = new GameObject("Entry Standing Point");
                    Undo.RegisterCreatedObjectUndo(pointObject, "Create Entry Standing Point");
                    Undo.SetTransformParent(
                        pointObject.transform,
                        entry.transform,
                        "Parent Entry Standing Point"
                    );
                    point = pointObject.transform;

                    Transform interaction = FindChild(entry.transform, "Triggers_Enter/Exit");
                    if (interaction != null)
                        point.SetPositionAndRotation(interaction.position, interaction.rotation);
                    else
                        point.localPosition = new Vector3(-1.75f, -0.4f, -0.45f);
                }

                Undo.RecordObject(entry, "Assign Entry Standing Point");
                entry.entryStandingPoint = point;
            }

            if (entry.entryStepPoint == null)
            {
                Transform step = FindChild(entry.transform, "Entry Step Point");
                if (step == null)
                {
                    GameObject stepObject = new GameObject("Entry Step Point");
                    Undo.RegisterCreatedObjectUndo(stepObject, "Create Entry Step Point");
                    Undo.SetTransformParent(
                        stepObject.transform,
                        entry.transform,
                        "Parent Entry Step Point"
                    );
                    step = stepObject.transform;

                    float stepTime = 0.52f;
                    if (entry.entryStandingPoint != null && entry.entryParent != null)
                    {
                        step.SetPositionAndRotation(
                            Vector3.Lerp(
                                entry.entryStandingPoint.position,
                                entry.entryParent.position,
                                stepTime
                            ),
                            Quaternion.Slerp(
                                entry.entryStandingPoint.rotation,
                                entry.entryParent.rotation,
                                stepTime
                            )
                        );
                    }
                }

                Undo.RecordObject(entry, "Assign Entry Step Point");
                entry.entryStepPoint = step;
                entry.entryStepNormalizedTime = 0.52f;
                entry.useAuthoredEntryPath = true;
            }

            if (entry.doorHandleTarget == null && entry.doorTransform != null)
            {
                Transform handle = FindChild(entry.doorTransform, "Door Handle Target");
                if (handle == null)
                {
                    GameObject handleObject = new GameObject("Door Handle Target");
                    Undo.RegisterCreatedObjectUndo(handleObject, "Create Door Handle Target");
                    Undo.SetTransformParent(
                        handleObject.transform,
                        entry.doorTransform,
                        "Parent Door Handle Target"
                    );
                    handle = handleObject.transform;
                    handle.SetPositionAndRotation(
                        GuessDoorHandlePosition(entry.transform, entry.doorTransform),
                        Quaternion.LookRotation(-entry.transform.right, entry.transform.up)
                    );
                }

                Undo.RecordObject(entry, "Assign Door Handle Target");
                entry.doorHandleTarget = handle;
                entry.doorHandleHand = AvatarIKGoal.RightHand;
            }

            RecordComponentChange(entry);
            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
        }

        private static Vector3 GuessDoorHandlePosition(Transform carRoot, Transform door)
        {
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;

            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(
                        bounds.extents,
                        new Vector3(x, y, z)
                    );
                    Vector3 local = carRoot.InverseTransformPoint(
                        renderer.transform.TransformPoint(corner)
                    );
                    if (!hasBounds)
                    {
                        minimum = local;
                        maximum = local;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum = Vector3.Min(minimum, local);
                        maximum = Vector3.Max(maximum, local);
                    }
                }
            }

            if (!hasBounds) return door.position + carRoot.up - carRoot.right * 0.05f;
            return carRoot.TransformPoint(new Vector3(
                minimum.x - 0.03f,
                Mathf.Lerp(minimum.y, maximum.y, 0.65f),
                Mathf.Lerp(minimum.z, maximum.z, 0.3f)
            ));
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child;
            }
            return null;
        }

        private static void RecordTransformChange(Transform transform)
        {
            EditorUtility.SetDirty(transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            if (transform.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(transform.gameObject.scene);
        }

        private static void RecordComponentChange(Component component)
        {
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (component.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }
}
