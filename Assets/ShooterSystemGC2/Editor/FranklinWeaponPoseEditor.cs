using System;
using System.Collections.Generic;
using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Shooter.Editor
{
    /// <summary>
    /// Edits only the rendered model transform of every Franklin shooter prop. GC2 gameplay root,
    /// muzzle, magazine data and projectile spawn remain independent from these visual changes.
    /// </summary>
    internal sealed class FranklinWeaponPoseEditor : EditorWindow
    {
        private const string CATALOG_PATH =
            "Assets/ShooterSystemGC2/Resources/FranklinShooter/Franklin Shooter Catalog.asset";
        private const string SELECTED_INDEX_KEY =
            "FranklinGame.Shooter.WeaponPoseEditor.SelectedIndex";

        private enum HandleMode
        {
            Position,
            Rotation,
            Scale
        }

        private readonly List<GameObject> m_LiveProps = new();

        private FranklinShooterCatalog m_Catalog;
        private SerializedObject m_SerializedCatalog;
        private int m_SelectedIndex;
        private bool m_LiveUpdate = true;
        private bool m_ShowFineTune = true;
        private HandleMode m_HandleMode = HandleMode.Position;
        private Animator m_PreviewAnimator;
        private GameObject m_PreviewObject;
        private Vector2 m_Scroll;

        private Vector3 m_SessionPosition;
        private Vector3 m_SessionRotation;
        private Vector3 m_SessionScale;
        private bool m_HasSessionPose;
        private double m_NextLiveRefresh;
        private double m_SaveDeadline = -1d;

        [MenuItem("Tools/Shooter System GC2/Weapon Pose Editor", false, 1512)]
        private static void Open()
        {
            FranklinWeaponPoseEditor window = GetWindow<FranklinWeaponPoseEditor>();
            window.titleContent = new GUIContent("Weapon Pose");
            window.minSize = new Vector2(410f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            this.m_SelectedIndex = EditorPrefs.GetInt(SELECTED_INDEX_KEY, 0);
            this.LoadCatalog();
            EditorApplication.update += this.OnEditorUpdate;
            EditorApplication.playModeStateChanged += this.OnPlayModeStateChanged;
            SceneView.duringSceneGui += this.OnSceneGUI;
            Undo.undoRedoPerformed += this.OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= this.OnEditorUpdate;
            EditorApplication.playModeStateChanged -= this.OnPlayModeStateChanged;
            SceneView.duringSceneGui -= this.OnSceneGUI;
            Undo.undoRedoPerformed -= this.OnUndoRedo;
            this.DestroyPreview();
            this.SaveCatalog();
        }

        private void OnGUI()
        {
            this.DrawHeader();
            if (this.m_Catalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Không tìm thấy Franklin Shooter Catalog. Hãy chạy Install or Repair trước.",
                    MessageType.Warning
                );
                if (GUILayout.Button("Load Catalog")) this.LoadCatalog();
                return;
            }

            this.m_SerializedCatalog.UpdateIfRequiredOrScript();
            SerializedProperty weapons = this.m_SerializedCatalog.FindProperty("m_Weapons");
            if (weapons == null || weapons.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Catalog chưa có khẩu súng nào.", MessageType.Warning);
                return;
            }

            this.m_SelectedIndex = Mathf.Clamp(this.m_SelectedIndex, 0, weapons.arraySize - 1);
            string[] names = this.BuildWeaponNames(weapons);
            int nextIndex = EditorGUILayout.Popup("Khẩu súng", this.m_SelectedIndex, names);
            if (nextIndex != this.m_SelectedIndex) this.ChangeSelection(nextIndex);

            FranklinShooterCatalog.Entry entry = this.SelectedEntry;
            if (entry == null)
            {
                EditorGUILayout.HelpBox("Dữ liệu khẩu súng không hợp lệ.", MessageType.Error);
                return;
            }

            this.m_Scroll = EditorGUILayout.BeginScrollView(this.m_Scroll);
            this.DrawTargetSection(entry);
            EditorGUILayout.Space(8f);
            this.DrawPoseSection(entry);
            EditorGUILayout.Space(8f);
            this.DrawActions(entry);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Franklin Weapon Pose Editor", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Catalog", EditorStyles.toolbarButton, GUILayout.Width(60f)) &&
                    this.m_Catalog != null)
                {
                    EditorGUIUtility.PingObject(this.m_Catalog);
                    Selection.activeObject = this.m_Catalog;
                }
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    this.LoadCatalog();
            }

            EditorGUILayout.HelpBox(
                "Chỉ chỉnh Visual Model của khẩu súng. Gameplay Root, dữ liệu magazine và " +
                "muzzle/projectile spawn của GC2 không bị di chuyển. Trong Play Mode khẩu đang " +
                "cầm cập nhật ngay; Edit Mode có thể tạo preview trên Animator humanoid.",
                MessageType.Info
            );
        }

        private void DrawTargetSection(FranklinShooterCatalog.Entry entry)
        {
            EditorGUILayout.LabelField("Xem realtime", EditorStyles.boldLabel);
            this.m_LiveUpdate = EditorGUILayout.ToggleLeft(
                "Tự cập nhật khẩu súng đang cầm trong Play Mode",
                this.m_LiveUpdate
            );

            if (EditorApplication.isPlaying)
            {
                this.FindLiveProps(entry, this.m_LiveProps);
                if (this.m_LiveProps.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Đang điều khiển {this.m_LiveProps.Count} instance của {entry.DisplayName}.",
                        MessageType.None
                    );
                    if (GUILayout.Button("Chọn prop đang cầm trong Hierarchy"))
                        Selection.activeGameObject = this.m_LiveProps[0];
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Chưa có character nào đang cầm {entry.DisplayName}.",
                        MessageType.None
                    );
                }
            }
            else
            {
                Animator nextAnimator = (Animator) EditorGUILayout.ObjectField(
                    "Animator preview",
                    this.m_PreviewAnimator,
                    typeof(Animator),
                    true
                );
                if (nextAnimator != this.m_PreviewAnimator)
                {
                    this.DestroyPreview();
                    this.m_PreviewAnimator = nextAnimator;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dùng character đang chọn"))
                        this.UseSelectedAnimator();
                    using (new EditorGUI.DisabledScope(this.m_PreviewAnimator == null))
                    {
                        if (GUILayout.Button(this.m_PreviewObject == null ? "Tạo preview" : "Tạo lại"))
                            this.CreatePreview(entry);
                    }
                    using (new EditorGUI.DisabledScope(this.m_PreviewObject == null))
                    {
                        if (GUILayout.Button("Xóa preview", GUILayout.Width(90f)))
                            this.DestroyPreview();
                    }
                }

                if (this.m_PreviewAnimator != null && !this.IsValidSceneAnimator(this.m_PreviewAnimator))
                {
                    EditorGUILayout.HelpBox(
                        "Animator preview phải là một object trong Scene, không phải prefab asset.",
                        MessageType.Warning
                    );
                }
                else if (this.m_PreviewObject != null)
                {
                    EditorGUILayout.HelpBox(
                        "Preview chỉ tồn tại tạm thời và không được lưu vào Scene.",
                        MessageType.None
                    );
                }
            }
        }

        private void DrawPoseSection(FranklinShooterCatalog.Entry entry)
        {
            if (!this.ReadPose(out Vector3 position, out Vector3 rotation, out Vector3 scale)) return;

            EditorGUILayout.LabelField("Transform chỉ dành cho Visual Model", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            position = EditorGUILayout.Vector3Field("Position", position);
            rotation = EditorGUILayout.Vector3Field("Rotation", rotation);
            scale = EditorGUILayout.Vector3Field("Scale", scale);
            if (EditorGUI.EndChangeCheck())
                this.WritePose(position, rotation, ClampScale(scale), "Edit Weapon Pose");

            this.m_ShowFineTune = EditorGUILayout.Foldout(
                this.m_ShowFineTune,
                "Fine tune bằng slider",
                true
            );
            if (this.m_ShowFineTune)
            {
                EditorGUI.indentLevel++;
                if (this.ReadPose(out position, out rotation, out scale))
                {
                    EditorGUI.BeginChangeCheck();
                    position = DrawVectorSliders("Position", position, -0.5f, 0.5f);
                    rotation = DrawVectorSliders("Rotation", rotation, -180f, 180f);
                    scale = DrawVectorSliders("Scale", scale, 0.01f, 3f);
                    if (EditorGUI.EndChangeCheck())
                        this.WritePose(position, rotation, ClampScale(scale), "Fine Tune Weapon Pose");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Scene handle", EditorStyles.miniBoldLabel);
            this.m_HandleMode = (HandleMode) GUILayout.Toolbar(
                (int) this.m_HandleMode,
                new[] { "Move", "Rotate", "Scale" }
            );
            EditorGUILayout.LabelField(
                "Handle chỉ tác động child Visual Model, không tác động Gameplay Root/muzzle.",
                EditorStyles.wordWrappedMiniLabel
            );
        }

        private void DrawActions(FranklinShooterCatalog.Entry entry)
        {
            EditorGUILayout.LabelField("Lưu và khôi phục", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lưu catalog")) this.SaveCatalog();
                using (new EditorGUI.DisabledScope(!this.m_HasSessionPose))
                {
                    if (GUILayout.Button("Khôi phục lúc mở/chọn"))
                    {
                        this.WritePose(
                            this.m_SessionPosition,
                            this.m_SessionRotation,
                            this.m_SessionScale,
                            "Revert Weapon Pose"
                        );
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Undo")) Undo.PerformUndo();
                if (GUILayout.Button("Reset mặc định hệ thống"))
                {
                    this.WritePose(
                        Vector3.zero,
                        Vector3.zero,
                        Vector3.one,
                        "Reset Weapon Pose"
                    );
                }
            }

            EditorGUILayout.HelpBox(
                "Các giá trị được lưu riêng cho từng khẩu và sẽ được giữ nguyên khi chạy " +
                "Shooter System GC2 > Install or Repair.",
                MessageType.None
            );
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            FranklinShooterCatalog.Entry entry = this.SelectedEntry;
            Transform target = this.GetHandleTarget(entry);
            if (entry == null || target == null) return;

            Handles.color = new Color(0.15f, 0.8f, 1f, 1f);
            Handles.Label(
                target.position + Vector3.up * HandleUtility.GetHandleSize(target.position) * 0.2f,
                $"{entry.DisplayName} - {this.m_HandleMode}"
            );

            EditorGUI.BeginChangeCheck();
            Vector3 worldPosition = target.position;
            Quaternion worldRotation = target.rotation;
            Vector3 actualLocalScale = target.localScale;
            float handleSize = HandleUtility.GetHandleSize(worldPosition);
            Transform parent = target.parent;
            if (parent == null) return;

            switch (this.m_HandleMode)
            {
                case HandleMode.Position:
                    worldPosition = Handles.PositionHandle(worldPosition, parent.rotation);
                    break;
                case HandleMode.Rotation:
                    worldRotation = Handles.RotationHandle(worldRotation, worldPosition);
                    break;
                case HandleMode.Scale:
                    actualLocalScale = Handles.ScaleHandle(
                        actualLocalScale,
                        worldPosition,
                        worldRotation,
                        handleSize
                    );
                    break;
            }

            if (!EditorGUI.EndChangeCheck()) return;

            Vector3 position = parent.InverseTransformPoint(worldPosition);
            Vector3 rotation = NormalizeEuler(
                (Quaternion.Inverse(parent.rotation) * worldRotation).eulerAngles
            );
            this.WritePose(
                position,
                rotation,
                ClampScale(actualLocalScale),
                "Edit Weapon Model Pose Handle",
                true
            );
        }

        private void OnEditorUpdate()
        {
            double time = EditorApplication.timeSinceStartup;
            if (this.m_SaveDeadline >= 0d && time >= this.m_SaveDeadline)
                this.SaveCatalog();

            if (!this.m_LiveUpdate || !EditorApplication.isPlaying || time < this.m_NextLiveRefresh)
                return;

            this.m_NextLiveRefresh = time + 0.1d;
            FranklinShooterCatalog.Entry entry = this.SelectedEntry;
            if (entry == null) return;
            this.ApplyPoseToLiveProps(entry);
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                this.DestroyPreview();
            }
            this.Repaint();
        }

        private void OnUndoRedo()
        {
            if (this.m_Catalog == null) return;
            this.m_SerializedCatalog = new SerializedObject(this.m_Catalog);
            FranklinShooterCatalog.Entry entry = this.SelectedEntry;
            if (entry != null) this.ApplyPoseToTargets(entry, true);
            this.QueueSave();
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void LoadCatalog()
        {
            this.DestroyPreview();
            this.m_Catalog = AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH);
            this.m_SerializedCatalog = this.m_Catalog != null
                ? new SerializedObject(this.m_Catalog)
                : null;
            this.m_SelectedIndex = Mathf.Clamp(
                this.m_SelectedIndex,
                0,
                Mathf.Max(0, this.m_Catalog != null ? this.m_Catalog.Count - 1 : 0)
            );
            this.CaptureSessionPose();
            this.Repaint();
        }

        private void ChangeSelection(int index)
        {
            this.DestroyPreview();
            this.m_SelectedIndex = index;
            EditorPrefs.SetInt(SELECTED_INDEX_KEY, index);
            this.CaptureSessionPose();
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void CaptureSessionPose()
        {
            this.m_HasSessionPose = this.ReadPose(
                out this.m_SessionPosition,
                out this.m_SessionRotation,
                out this.m_SessionScale
            );
        }

        private bool ReadPose(out Vector3 position, out Vector3 rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            scale = Vector3.one;
            SerializedProperty element = this.SelectedSerializedEntry;
            if (element == null) return false;

            SerializedProperty positionProperty =
                element.FindPropertyRelative("m_ModelLocalPosition");
            SerializedProperty rotationProperty =
                element.FindPropertyRelative("m_ModelLocalRotation");
            SerializedProperty scaleProperty =
                element.FindPropertyRelative("m_ModelLocalScale");
            if (positionProperty == null || rotationProperty == null || scaleProperty == null)
                return false;

            position = positionProperty.vector3Value;
            rotation = rotationProperty.vector3Value;
            scale = scaleProperty.vector3Value;
            return true;
        }

        private void WritePose(
            Vector3 position,
            Vector3 rotation,
            Vector3 scale,
            string undoName,
            bool forceLiveUpdate = false)
        {
            if (this.m_Catalog == null || this.m_SerializedCatalog == null) return;

            Undo.RegisterCompleteObjectUndo(this.m_Catalog, undoName);
            this.m_SerializedCatalog.Update();
            SerializedProperty element = this.SelectedSerializedEntry;
            if (element == null) return;
            element.FindPropertyRelative("m_ModelLocalPosition").vector3Value = position;
            element.FindPropertyRelative("m_ModelLocalRotation").vector3Value =
                NormalizeEuler(rotation);
            element.FindPropertyRelative("m_ModelLocalScale").vector3Value = ClampScale(scale);
            this.m_SerializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(this.m_Catalog);
            this.QueueSave();

            FranklinShooterCatalog.Entry entry = this.SelectedEntry;
            if (entry != null && (this.m_LiveUpdate || forceLiveUpdate))
                this.ApplyPoseToTargets(entry, true);
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void QueueSave()
        {
            this.m_SaveDeadline = EditorApplication.timeSinceStartup + 0.35d;
        }

        private void SaveCatalog()
        {
            if (this.m_Catalog == null) return;
            this.m_SaveDeadline = -1d;
            EditorUtility.SetDirty(this.m_Catalog);
            AssetDatabase.SaveAssetIfDirty(this.m_Catalog);
        }

        private void ApplyPoseToTargets(FranklinShooterCatalog.Entry entry, bool includePreview)
        {
            if (includePreview && this.m_PreviewObject != null)
                ApplyPose(this.m_PreviewObject, entry);
            if (EditorApplication.isPlaying) this.ApplyPoseToLiveProps(entry);
        }

        private void ApplyPoseToLiveProps(FranklinShooterCatalog.Entry entry)
        {
            this.FindLiveProps(entry, this.m_LiveProps);
            foreach (GameObject prop in this.m_LiveProps)
                if (prop != null) ApplyPose(prop, entry);
        }

        private void FindLiveProps(
            FranklinShooterCatalog.Entry entry,
            List<GameObject> results)
        {
            results.Clear();
            if (!EditorApplication.isPlaying || entry?.Weapon == null) return;

            Character[] characters = Resources.FindObjectsOfTypeAll<Character>();
            foreach (Character character in characters)
            {
                if (character == null || !character.gameObject.scene.IsValid()) continue;
                GameObject prop = character.Combat?.GetProp(entry.Weapon);
                if (prop != null && !results.Contains(prop)) results.Add(prop);
            }
        }

        private static void ApplyPose(GameObject prop, FranklinShooterCatalog.Entry entry)
        {
            if (prop == null || entry == null) return;
            FranklinWeaponModelPose modelPose = prop.GetComponent<FranklinWeaponModelPose>();
            if (modelPose == null) return;
            modelPose.Apply(
                entry.ModelLocalPosition,
                entry.ModelLocalRotation,
                entry.ModelLocalScale
            );
        }

        private Transform GetHandleTarget(FranklinShooterCatalog.Entry entry)
        {
            if (this.m_PreviewObject != null)
            {
                FranklinWeaponModelPose previewPose =
                    this.m_PreviewObject.GetComponent<FranklinWeaponModelPose>();
                return previewPose != null ? previewPose.ModelTransform : null;
            }
            if (!EditorApplication.isPlaying || entry == null) return null;
            this.FindLiveProps(entry, this.m_LiveProps);
            if (this.m_LiveProps.Count <= 0 || this.m_LiveProps[0] == null) return null;
            FranklinWeaponModelPose livePose =
                this.m_LiveProps[0].GetComponent<FranklinWeaponModelPose>();
            return livePose != null ? livePose.ModelTransform : null;
        }

        private void UseSelectedAnimator()
        {
            this.DestroyPreview();
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                this.m_PreviewAnimator = null;
                return;
            }

            this.m_PreviewAnimator = selected.GetComponent<Animator>();
            if (this.m_PreviewAnimator == null)
                this.m_PreviewAnimator = selected.GetComponentInChildren<Animator>(true);
            if (this.m_PreviewAnimator == null)
                this.m_PreviewAnimator = selected.GetComponentInParent<Animator>();
        }

        private void CreatePreview(FranklinShooterCatalog.Entry entry)
        {
            this.DestroyPreview();
            if (entry?.PropPrefab == null || !this.IsValidSceneAnimator(this.m_PreviewAnimator))
                return;

            Transform hand = FindRightHand(this.m_PreviewAnimator);
            if (hand == null)
            {
                Debug.LogWarning(
                    "Weapon Pose Editor: không tìm thấy RightHand trên Animator preview.",
                    this.m_PreviewAnimator
                );
                return;
            }

            this.m_PreviewObject = new GameObject(
                $"[Weapon Gameplay Root Preview] {entry.DisplayName}"
            )
            {
                layer = entry.PropPrefab.layer
            };
            this.m_PreviewObject.transform.SetParent(hand, false);
            this.m_PreviewObject.transform.localPosition = entry.LocalPosition;
            this.m_PreviewObject.transform.localRotation = entry.LocalRotation;
            this.m_PreviewObject.transform.localScale = Vector3.Scale(
                entry.PropPrefab.transform.localScale,
                entry.LocalScale
            );
            FranklinWeaponModelPose modelPose =
                this.m_PreviewObject.AddComponent<FranklinWeaponModelPose>();
            modelPose.Initialize(
                entry.PropPrefab,
                entry.ModelLocalPosition,
                entry.ModelLocalRotation,
                entry.ModelLocalScale
            );
            SetPreviewHideFlags(this.m_PreviewObject.transform);
            Selection.activeGameObject = modelPose.Model;
            SceneView.RepaintAll();
        }

        private void DestroyPreview()
        {
            if (this.m_PreviewObject == null) return;
            DestroyImmediate(this.m_PreviewObject);
            this.m_PreviewObject = null;
            SceneView.RepaintAll();
        }

        private bool IsValidSceneAnimator(Animator animator)
        {
            return animator != null &&
                   !EditorUtility.IsPersistent(animator) &&
                   animator.gameObject.scene.IsValid();
        }

        private static Transform FindRightHand(Animator animator)
        {
            if (animator == null) return null;
            if (animator.isHuman)
            {
                Transform humanHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (humanHand != null) return humanHand;
            }

            string[] names = { "RightHand", "Right Hand", "Hand_R", "hand_r", "Bip001 R Hand" };
            foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
            {
                foreach (string candidate in names)
                    if (string.Equals(child.name, candidate, StringComparison.OrdinalIgnoreCase))
                        return child;
            }
            return null;
        }

        private static void SetPreviewHideFlags(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        private FranklinShooterCatalog.Entry SelectedEntry =>
            this.m_Catalog != null ? this.m_Catalog.Get(this.m_SelectedIndex) : null;

        private SerializedProperty SelectedSerializedEntry
        {
            get
            {
                if (this.m_SerializedCatalog == null) return null;
                SerializedProperty weapons = this.m_SerializedCatalog.FindProperty("m_Weapons");
                if (weapons == null || this.m_SelectedIndex < 0 ||
                    this.m_SelectedIndex >= weapons.arraySize) return null;
                return weapons.GetArrayElementAtIndex(this.m_SelectedIndex);
            }
        }

        private string[] BuildWeaponNames(SerializedProperty weapons)
        {
            string[] names = new string[weapons.arraySize];
            for (int i = 0; i < weapons.arraySize; ++i)
            {
                FranklinShooterCatalog.Entry entry = this.m_Catalog.Get(i);
                names[i] = entry != null
                    ? $"{i + 1}. {entry.DisplayName} ({entry.Category})"
                    : $"{i + 1}. Missing";
            }
            return names;
        }

        private static Vector3 DrawVectorSliders(
            string label,
            Vector3 value,
            float minimum,
            float maximum)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            value.x = EditorGUILayout.Slider("X", value.x, minimum, maximum);
            value.y = EditorGUILayout.Slider("Y", value.y, minimum, maximum);
            value.z = EditorGUILayout.Slider("Z", value.z, minimum, maximum);
            return value;
        }

        private static Vector3 NormalizeEuler(Vector3 value)
        {
            value.x = NormalizeAngle(value.x);
            value.y = NormalizeAngle(value.y);
            value.z = NormalizeAngle(value.z);
            return value;
        }

        private static float NormalizeAngle(float value)
        {
            value %= 360f;
            if (value > 180f) value -= 360f;
            if (value < -180f) value += 360f;
            return value;
        }

        private static Vector3 ClampScale(Vector3 value)
        {
            value.x = Mathf.Max(0.001f, value.x);
            value.y = Mathf.Max(0.001f, value.y);
            value.z = Mathf.Max(0.001f, value.z);
            return value;
        }

    }
}
